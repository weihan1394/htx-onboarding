#!/bin/bash
set -e

# Runs Flyway migrations for hr-db and onboarding-db as one-shot Fargate tasks.
# Flyway is idempotent — already-applied migrations are skipped automatically.
# Safe to run on every deploy.

REGION="ap-southeast-1"
LOG_GROUP="/ecs/htx-onboarding/migrations"

ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com"

stack_output() {
  aws cloudformation describe-stacks \
    --stack-name "$1" --region "${REGION}" \
    --query "Stacks[0].Outputs[?OutputKey==\`$2\`].OutputValue" \
    --output text
}

get_secret_field() {
  aws secretsmanager get-secret-value \
    --secret-id "$1" --region "${REGION}" \
    --query SecretString --output text \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['$2'])"
}

echo "==> Fetching stack values..."
DB_ENDPOINT=$(stack_output htx-onboarding-storage DBEndpoint)
ECS_CLUSTER=$(stack_output htx-onboarding-compute-infra ECSClusterName)
SUBNET=$(stack_output htx-onboarding-network AppSubnet1Id)
ECS_SG=$(stack_output htx-onboarding-network ECSSecurityGroupId)
EXEC_ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/htx-onboarding-task-execution"

echo "  DB:      ${DB_ENDPOINT}"
echo "  Cluster: ${ECS_CLUSTER}"

echo ""
echo "==> Reading admin credentials..."
ADMIN_USER=$(get_secret_field "htx-onboarding/prod/db/admin" "username")
ADMIN_PWD=$(get_secret_field "htx-onboarding/prod/db/admin" "password")

echo ""
echo "==> Ensuring log group exists..."
aws logs create-log-group --log-group-name "${LOG_GROUP}" --region "${REGION}" 2>/dev/null || true
aws logs put-retention-policy --log-group-name "${LOG_GROUP}" --retention-in-days 14 --region "${REGION}"

FLYWAY_URL="jdbc:postgresql://${DB_ENDPOINT}:5432/htx"

run_migration() {
  local name="$1"
  local image="$2"

  echo ""
  echo "==> Running migration: ${name}..."

  CONTAINER_DEFS=$(jq -n \
    --arg image "${image}" \
    --arg log_group "${LOG_GROUP}" \
    --arg region "${REGION}" \
    --arg name "${name}" \
    '[{
      name: $name,
      image: $image,
      essential: true,
      logConfiguration: {
        logDriver: "awslogs",
        options: {
          "awslogs-group": $log_group,
          "awslogs-region": $region,
          "awslogs-stream-prefix": $name
        }
      }
    }]')

  TASK_DEF_ARN=$(aws ecs register-task-definition \
    --region "${REGION}" \
    --family "htx-onboarding-${name}" \
    --network-mode awsvpc \
    --requires-compatibilities FARGATE \
    --cpu 256 --memory 512 \
    --execution-role-arn "${EXEC_ROLE_ARN}" \
    --container-definitions "${CONTAINER_DEFS}" \
    --query 'taskDefinition.taskDefinitionArn' \
    --output text)

  OVERRIDES=$(jq -n \
    --arg name "${name}" \
    --arg url "${FLYWAY_URL}" \
    --arg user "${ADMIN_USER}" \
    --arg pwd "${ADMIN_PWD}" \
    '{
      containerOverrides: [{
        name: $name,
        command: ["migrate"],
        environment: [
          {name: "FLYWAY_URL",      value: $url},
          {name: "FLYWAY_USER",     value: $user},
          {name: "FLYWAY_PASSWORD", value: $pwd}
        ]
      }]
    }')

  TASK_ARN=$(aws ecs run-task \
    --cluster "${ECS_CLUSTER}" \
    --task-definition "${TASK_DEF_ARN}" \
    --launch-type FARGATE \
    --region "${REGION}" \
    --network-configuration "awsvpcConfiguration={subnets=[${SUBNET}],securityGroups=[${ECS_SG}],assignPublicIp=DISABLED}" \
    --overrides "${OVERRIDES}" \
    --query 'tasks[0].taskArn' \
    --output text)

  echo "  Task: ${TASK_ARN}"
  echo "  Waiting for task to complete..."

  aws ecs wait tasks-stopped \
    --cluster "${ECS_CLUSTER}" \
    --tasks "${TASK_ARN}" \
    --region "${REGION}"

  EXIT_CODE=$(aws ecs describe-tasks \
    --cluster "${ECS_CLUSTER}" \
    --tasks "${TASK_ARN}" \
    --region "${REGION}" \
    --query 'tasks[0].containers[0].exitCode' \
    --output text)

  if [ "${EXIT_CODE}" = "0" ]; then
    echo "  ${name}: migrations applied successfully."
  else
    echo "ERROR: ${name} migration failed (exit ${EXIT_CODE})"
    echo "       Logs: aws logs tail ${LOG_GROUP} --region ${REGION} --follow"
    exit 1
  fi
}

run_migration "hr-db-migrate"         "${ECR}/htx-onboarding/hr-db-migrate:latest"
run_migration "onboarding-db-migrate" "${ECR}/htx-onboarding/onboarding-db-migrate:latest"

echo ""
echo "==> All migrations complete."
