#!/bin/bash
set -e

export AWS_PAGER=""

REGION="ap-southeast-1"
IMAGE_TAG="${1:-latest}"
GITHUB_ORG="${2:-weihan1394}"
GITHUB_REPO="${3:-htx-onboarding}"
TEMPORAL_VERSION="1.25.2"
TEMPORAL_UI_VERSION="2.34.0"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATES="${SCRIPT_DIR}/templates"
ROOT="${SCRIPT_DIR}/../.."

# ── Prerequisites ─────────────────────────────────────────────────────────────
# shellcheck source=helpers/check-prerequisites.sh
source "${SCRIPT_DIR}/../helpers/check-prerequisites.sh" --with-aws --with-node --with-docker

ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com"

echo "==> Deploying HTX Onboarding to ${REGION} (image: ${IMAGE_TAG})"
echo "    Account: ${ACCOUNT_ID}"
echo "    Runtime: ${DOCKER}"

deploy() {
  local stack="$1"
  local template="$2"
  shift 2
  echo ""
  echo "==> [$((++STEP))] Deploying ${stack}..."

  # cloudformation deploy cannot update a stack stuck in a terminal failed state.
  # delete it first so the subsequent deploy creates it fresh.
  local current_status
  current_status=$(aws cloudformation describe-stacks \
    --stack-name "${stack}" --region "${REGION}" \
    --query 'Stacks[0].StackStatus' --output text 2>/dev/null || echo "DOES_NOT_EXIST")

  case "${current_status}" in
    ROLLBACK_COMPLETE|CREATE_FAILED|DELETE_FAILED)
      echo "    Stack is in ${current_status} — deleting before re-deploy..."
      aws cloudformation delete-stack --stack-name "${stack}" --region "${REGION}"
      aws cloudformation wait stack-delete-complete --stack-name "${stack}" --region "${REGION}" 2>/dev/null || true
      local post_delete
      post_delete=$(aws cloudformation describe-stacks \
        --stack-name "${stack}" --region "${REGION}" \
        --query 'Stacks[0].StackStatus' --output text 2>/dev/null || echo "DOES_NOT_EXIST")
      if [ "${post_delete}" != "DOES_NOT_EXIST" ]; then
        echo "    ERROR: could not delete stuck stack ${stack} (status: ${post_delete})"
        exit 1
      fi
      echo "    Cleared. Deploying fresh..."
      ;;
  esac

  aws cloudformation deploy \
    --template-file "${TEMPLATES}/${template}" \
    --stack-name "${stack}" \
    --region "${REGION}" \
    --capabilities CAPABILITY_IAM CAPABILITY_NAMED_IAM \
    --no-fail-on-empty-changeset \
    "$@"
  echo "    Done."
}

STEP=0

# returns 0 (true) if the given tag already exists in ECR, 1 (false) otherwise
# usage: ecr_image_exists <full-repo-uri> <tag>
ecr_image_exists() {
  local uri="$1"
  local tag="$2"
  local repo_name="${uri#*.amazonaws.com/}"  # strip "account.dkr.ecr.region.amazonaws.com/"
  aws ecr describe-images \
    --repository-name "${repo_name}" \
    --image-ids "imageTag=${tag}" \
    --region "${REGION}" \
    --output text 2>/dev/null | grep -q "IMAGE"
}

# ── 0. GitHub Actions OIDC role ───────────────────────────────────────────────
# Provisions the OIDC provider and deploy role once. Safe to re-run — CloudFormation
# is idempotent. Pass --parameter-overrides CreateOIDCProvider=false if the
# token.actions.githubusercontent.com OIDC provider already exists in this account.
echo "    GitHub:  ${GITHUB_ORG}/${GITHUB_REPO}"
deploy htx-onboarding-github-oidc 0-github-oidc.yaml \
  --parameter-overrides \
    GitHubOrg="${GITHUB_ORG}" \
    GitHubRepo="${GITHUB_REPO}"

echo ""
ROLE_ARN=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-github-oidc --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`GitHubDeployRoleArn`].OutputValue' \
  --output text)
echo "    ✅  GitHub deploy role: ${ROLE_ARN}"
echo "    Add this as AWS_ROLE_ARN in GitHub → Settings → Secrets → Actions"
echo ""

 # ── 1. Network ────────────────────────────────────────────────────────────────
deploy htx-onboarding-network 1-network.yaml

# ── 2. ECR ────────────────────────────────────────────────────────────────────
deploy htx-onboarding-ecr 2-ecr.yaml

# ── 3. Build and push all images ──────────────────────────────────────────────
echo ""
echo "==> [$((++STEP))] Building and pushing images to ECR..."
aws ecr get-login-password --region "${REGION}" | \
  "${DOCKER}" login --username AWS --password-stdin "${ECR}"

ecr_output() {
  aws cloudformation describe-stacks \
    --stack-name htx-onboarding-ecr --region "${REGION}" \
    --query "Stacks[0].Outputs[?OutputKey==\`$1\`].OutputValue" \
    --output text
}

HR_SVC_REPO=$(ecr_output HRSvcRepoUri)
ONBOARDING_SVC_REPO=$(ecr_output OnboardingSvcRepoUri)
WORKFLOW_SVC_REPO=$(ecr_output WorkflowSvcRepoUri)
TEMPORAL_REPO=$(ecr_output TemporalRepoUri)
TEMPORAL_UI_REPO=$(ecr_output TemporalUIRepoUri)
DB_INIT_REPO=$(ecr_output DBInitRepoUri)
HR_DB_MIGRATE_REPO=$(ecr_output HRDBMigrateRepoUri)
ONBOARDING_DB_MIGRATE_REPO=$(ecr_output OnboardingDBMigrateRepoUri)

# third-party images: only pull/push if the specific version tag is not already in ECR
# (these are mirrors of Docker Hub images — no need to re-push on every deploy)
if ecr_image_exists "${TEMPORAL_REPO}" "${TEMPORAL_VERSION}"; then
  echo "    temporal:${TEMPORAL_VERSION} — already in ECR, skipping"
else
  echo "    temporal (mirror from Docker Hub)..."
  "${DOCKER}" pull ${DOCKER_PULL_OPTS} --platform linux/amd64 temporalio/auto-setup:${TEMPORAL_VERSION}
  "${DOCKER}" tag temporalio/auto-setup:${TEMPORAL_VERSION} "${TEMPORAL_REPO}:${TEMPORAL_VERSION}"
  "${DOCKER}" tag temporalio/auto-setup:${TEMPORAL_VERSION} "${TEMPORAL_REPO}:latest"
  "${DOCKER}" push "${TEMPORAL_REPO}:${TEMPORAL_VERSION}"
  "${DOCKER}" push "${TEMPORAL_REPO}:latest"
fi

if ecr_image_exists "${TEMPORAL_UI_REPO}" "${TEMPORAL_UI_VERSION}"; then
  echo "    temporal-ui:${TEMPORAL_UI_VERSION} — already in ECR, skipping"
else
  echo "    temporal-ui (mirror from Docker Hub)..."
  "${DOCKER}" pull ${DOCKER_PULL_OPTS} --platform linux/amd64 temporalio/ui:${TEMPORAL_UI_VERSION}
  "${DOCKER}" tag temporalio/ui:${TEMPORAL_UI_VERSION} "${TEMPORAL_UI_REPO}:${TEMPORAL_UI_VERSION}"
  "${DOCKER}" tag temporalio/ui:${TEMPORAL_UI_VERSION} "${TEMPORAL_UI_REPO}:latest"
  "${DOCKER}" push "${TEMPORAL_UI_REPO}:${TEMPORAL_UI_VERSION}"
  "${DOCKER}" push "${TEMPORAL_UI_REPO}:latest"
fi

# app images: skip build+push if the exact IMAGE_TAG already exists in ECR
# (useful when re-running a deploy for the same commit SHA)
if ecr_image_exists "${HR_SVC_REPO}" "${IMAGE_TAG}"; then
  echo "    hr-svc:${IMAGE_TAG} — already in ECR, skipping"
else
  echo "    hr-svc..."
  "${DOCKER}" build ${DOCKER_BUILD_OPTS} --platform linux/amd64 -t "${HR_SVC_REPO}:${IMAGE_TAG}" "${ROOT}/hr-svc/"
  "${DOCKER}" push "${HR_SVC_REPO}:${IMAGE_TAG}"
fi

if ecr_image_exists "${ONBOARDING_SVC_REPO}" "${IMAGE_TAG}"; then
  echo "    onboarding-svc:${IMAGE_TAG} — already in ECR, skipping"
else
  echo "    onboarding-svc..."
  "${DOCKER}" build ${DOCKER_BUILD_OPTS} --platform linux/amd64 -t "${ONBOARDING_SVC_REPO}:${IMAGE_TAG}" "${ROOT}/onboarding-svc/"
  "${DOCKER}" push "${ONBOARDING_SVC_REPO}:${IMAGE_TAG}"
fi

if ecr_image_exists "${WORKFLOW_SVC_REPO}" "${IMAGE_TAG}"; then
  echo "    workflow-svc:${IMAGE_TAG} — already in ECR, skipping"
else
  echo "    workflow-svc..."
  "${DOCKER}" build ${DOCKER_BUILD_OPTS} --platform linux/amd64 -t "${WORKFLOW_SVC_REPO}:${IMAGE_TAG}" "${ROOT}/workflow-svc/"
  "${DOCKER}" push "${WORKFLOW_SVC_REPO}:${IMAGE_TAG}"
fi

if ecr_image_exists "${DB_INIT_REPO}" "latest"; then
  echo "    db-init:latest — already in ECR, skipping"
else
  echo "    db-init..."
  "${DOCKER}" build ${DOCKER_BUILD_OPTS} --platform linux/amd64 -t "${DB_INIT_REPO}:latest" "${ROOT}/ops/aws/helpers/db-init/"
  "${DOCKER}" push "${DB_INIT_REPO}:latest"
fi

if ecr_image_exists "${HR_DB_MIGRATE_REPO}" "latest"; then
  echo "    hr-db-migrate:latest — already in ECR, skipping"
else
  echo "    hr-db-migrate..."
  "${DOCKER}" build ${DOCKER_BUILD_OPTS} --platform linux/amd64 -t "${HR_DB_MIGRATE_REPO}:latest" "${ROOT}/hr-db/"
  "${DOCKER}" push "${HR_DB_MIGRATE_REPO}:latest"
fi

if ecr_image_exists "${ONBOARDING_DB_MIGRATE_REPO}" "latest"; then
  echo "    onboarding-db-migrate:latest — already in ECR, skipping"
else
  echo "    onboarding-db-migrate..."
  "${DOCKER}" build ${DOCKER_BUILD_OPTS} --platform linux/amd64 -t "${ONBOARDING_DB_MIGRATE_REPO}:latest" "${ROOT}/onboarding-db/"
  "${DOCKER}" push "${ONBOARDING_DB_MIGRATE_REPO}:latest"
fi

echo "    Done."

# ── 4. Storage (RDS + ElastiCache + Secrets) ──────────────────────────────────
deploy htx-onboarding-storage 3-storage.yaml

# ── 5. Populate connection strings ────────────────────────────────────────────
# Must happen before compute so ECS tasks start with real DB credentials.
echo ""
echo "==> [$((++STEP))] Updating connection strings in Secrets Manager..."

DB_ENDPOINT=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-storage --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`DBEndpoint`].OutputValue' \
  --output text)

get_secret_field() {
  aws secretsmanager get-secret-value \
    --secret-id "$1" --region "${REGION}" \
    --query SecretString --output text \
  | jq -r ".$2"
}

HR_SVC_PWD=$(get_secret_field "htx-onboarding/prod/db/hr-svc" "password")
ONBOARDING_SVC_PWD=$(get_secret_field "htx-onboarding/prod/db/onboarding-svc" "password")

aws secretsmanager put-secret-value \
  --secret-id "htx-onboarding/prod/connection-string/hr-svc" \
  --region "${REGION}" \
  --secret-string "{\"ConnectionStrings__DefaultConnection\":\"Host=${DB_ENDPOINT};Port=5432;Database=htx;Username=hr_svc;Password=${HR_SVC_PWD};Maximum Pool Size=5;Minimum Pool Size=1\"}"

aws secretsmanager put-secret-value \
  --secret-id "htx-onboarding/prod/connection-string/onboarding-svc" \
  --region "${REGION}" \
  --secret-string "{\"ConnectionStrings__DefaultConnection\":\"Host=${DB_ENDPOINT};Port=5432;Database=htx;Username=onboarding_svc;Password=${ONBOARDING_SVC_PWD};Maximum Pool Size=5;Minimum Pool Size=1\"}"

echo "    Done."

# ── 6. Compute infra (cluster, ALB, task defs — no services yet) ──────────────
deploy htx-onboarding-compute-infra 4a-compute-infra.yaml \
  --parameter-overrides \
    ImageTag="${IMAGE_TAG}" \
    TemporalVersion="${TEMPORAL_VERSION}" \
    TemporalUIVersion="${TEMPORAL_UI_VERSION}"

# ── 7. Init database ──────────────────────────────────────────────────────────
# Creates the htx database and service accounts. Idempotent — safe on every run.
echo ""
echo "==> [$((++STEP))] Initialising database..."
"${SCRIPT_DIR}/helpers/init-db.sh"

# ── 8. Run Flyway migrations ──────────────────────────────────────────────────
# Applies any new SQL migrations. Idempotent — already-applied ones are skipped.
echo ""
echo "==> [$((++STEP))] Running migrations..."
"${SCRIPT_DIR}/helpers/migrate.sh"

# ── 9. Start Temporal only — must be stable before namespace bootstrap ────────
deploy htx-onboarding-temporal 4b-temporal.yaml

echo ""
echo "==> [$((++STEP))] Waiting for Temporal to be stable..."
CLUSTER=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-compute-infra --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`ECSClusterName`].OutputValue' --output text)
aws ecs wait services-stable \
  --cluster "${CLUSTER}" \
  --services temporal \
  --region "${REGION}"
echo "    Done."

# ── 9b. Bootstrap Temporal namespace + search attributes ─────────────────────
# Idempotent — safe to re-run. Runs after Temporal is confirmed stable so the
# namespace exists before workflow-svc starts.
echo ""
echo "==> [$((++STEP))] Bootstrapping Temporal namespace..."

TEMPORAL_SUBNET=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-network --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`AppSubnet1Id`].OutputValue' --output text)
TEMPORAL_SG=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-network --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`ECSSecurityGroupId`].OutputValue' --output text)
TEMPORAL_EXEC_ROLE="arn:aws:iam::${ACCOUNT_ID}:role/htx-onboarding-task-execution"
TEMPORAL_IMAGE="${ECR}/htx-onboarding/temporal:${TEMPORAL_VERSION}"

bootstrap_cmd="temporal operator namespace create --address temporal.htx-network:7233 --retention 7d htx-onboarding 2>&1 | grep -v 'already registered' || true && temporal operator search-attribute create --address temporal.htx-network:7233 --namespace htx-onboarding --name EmployeeName --type Text 2>/dev/null || true && temporal operator search-attribute create --address temporal.htx-network:7233 --namespace htx-onboarding --name EmployeeNumber --type Keyword 2>/dev/null || true && temporal operator search-attribute create --address temporal.htx-network:7233 --namespace htx-onboarding --name Department --type Keyword 2>/dev/null || true && echo DONE"

CONTAINER_DEFS=$(jq -n \
  --arg image "${TEMPORAL_IMAGE}" \
  --arg cmd   "${bootstrap_cmd}" \
  --arg lg    "/ecs/htx-onboarding/temporal" \
  --arg region "${REGION}" \
  '[{
    name: "ns-init",
    image: $image,
    essential: true,
    entryPoint: ["/bin/sh", "-c"],
    command: [$cmd],
    logConfiguration: {
      logDriver: "awslogs",
      options: {
        "awslogs-group": $lg,
        "awslogs-region": $region,
        "awslogs-stream-prefix": "ns-init"
      }
    }
  }]')

aws ecs register-task-definition \
  --region "${REGION}" \
  --family htx-onboarding-temporal-ns-init \
  --network-mode awsvpc \
  --requires-compatibilities FARGATE \
  --cpu 256 --memory 512 \
  --execution-role-arn "${TEMPORAL_EXEC_ROLE}" \
  --container-definitions "${CONTAINER_DEFS}" > /dev/null

BOOTSTRAP_TASK=$(aws ecs run-task \
  --cluster "${CLUSTER}" \
  --task-definition htx-onboarding-temporal-ns-init \
  --launch-type FARGATE \
  --region "${REGION}" \
  --network-configuration "awsvpcConfiguration={subnets=[${TEMPORAL_SUBNET}],securityGroups=[${TEMPORAL_SG}],assignPublicIp=DISABLED}" \
  --query 'tasks[0].taskArn' --output text)
BOOTSTRAP_TASK_ID=$(echo "${BOOTSTRAP_TASK}" | rev | cut -d'/' -f1 | rev)
echo "    Task: ${BOOTSTRAP_TASK_ID}"
aws ecs wait tasks-stopped --cluster "${CLUSTER}" --tasks "${BOOTSTRAP_TASK_ID}" --region "${REGION}"
echo "    Done."

# ── 9c. Remaining app services (namespace exists — workflow-svc starts cleanly) ─
deploy htx-onboarding-compute-services 4c-compute-services.yaml

# ── 10. CDN (CloudFront + S3) ─────────────────────────────────────────────────
export TEMPORAL_UI_BASIC_AUTH=$(printf '%s' 'admin:P@ssw0rd' | base64)
deploy htx-onboarding-cdn 5-cdn.yaml \
  --parameter-overrides TemporalUIBasicAuth="${TEMPORAL_UI_BASIC_AUTH}"

# ── 10b. Allow CloudFront VPC Origin SG into ALB SG ──────────────────────────
# CloudFront creates a managed SG for its VPC Origin ENIs. CIDR-based rules alone
# are not sufficient — the ALB SG must explicitly reference the CloudFront SG.
echo ""
echo "==> Updating ALB security group to allow CloudFront VPC Origin..."
ALB_SG=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-network --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`ALBSecurityGroupId`].OutputValue' \
  --output text)
CF_VPC_ORIGIN_SG=$(aws ec2 describe-security-groups \
  --region "${REGION}" \
  --filters "Name=group-name,Values=CloudFront-VPCOrigins-Service-SG" \
            "Name=vpc-id,Values=$(aws cloudformation describe-stacks \
              --stack-name htx-onboarding-network --region "${REGION}" \
              --query 'Stacks[0].Outputs[?OutputKey==`VPCId`].OutputValue' --output text)" \
  --query 'SecurityGroups[0].GroupId' --output text)
if [ -n "${CF_VPC_ORIGIN_SG}" ] && [ "${CF_VPC_ORIGIN_SG}" != "None" ]; then
  aws ec2 authorize-security-group-ingress \
    --group-id "${ALB_SG}" \
    --protocol tcp --port 80 \
    --source-group "${CF_VPC_ORIGIN_SG}" \
    --region "${REGION}" 2>/dev/null || true
  echo "    CloudFront VPC Origin SG ${CF_VPC_ORIGIN_SG} added to ALB SG."
fi

# ── 11. Build and deploy hr-web (Vite SPA → S3 + CloudFront invalidation) ──────
echo ""
echo "==> [$((++STEP))] Building and deploying hr-web..."

CLOUDFRONT_DOMAIN=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-cdn --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`CloudFrontDomain`].OutputValue' \
  --output text)

S3_BUCKET=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-cdn --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`HRWebBucketName`].OutputValue' \
  --output text)

DISTRIBUTION_ID=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-cdn --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`CloudFrontDistributionId`].OutputValue' \
  --output text)

echo "    Domain:       ${CLOUDFRONT_DOMAIN}"
echo "    Bucket:       ${S3_BUCKET}"
echo "    Distribution: ${DISTRIBUTION_ID}"

cd "${ROOT}/hr-web"
echo "    Installing dependencies..."
npm install
echo "    Building..."
VITE_API_BASE_URL="https://${CLOUDFRONT_DOMAIN}" \
npm run build

aws s3 sync dist/ "s3://${S3_BUCKET}/" --delete --region "${REGION}"

aws cloudfront create-invalidation \
  --distribution-id "${DISTRIBUTION_ID}" \
  --paths "/*" \
  --region us-east-1 > /dev/null

cd "${SCRIPT_DIR}"
echo "    Done. https://${CLOUDFRONT_DOMAIN}"

# ── 12. Scheduler (auto stop/start ECS services) ─────────────────────────────
deploy htx-onboarding-scheduler 6-scheduler.yaml

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo "==> All done. CloudFront domain:"
echo "    https://${CLOUDFRONT_DOMAIN}"
echo ""
echo "    ECS services auto-stop  at 20:00 SGT daily."
echo "    ECS services auto-start at 08:00 SGT daily."
echo "    To override manually: ops/aws/stop-services.sh / ops/aws/start-services.sh"
