#!/bin/bash
set -e

# disable AWS CLI pager so the script runs fully unattended
export AWS_PAGER=""

REGION="ap-southeast-1"
DB_INSTANCE="htx-onboarding-prod"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ── Prerequisites ─────────────────────────────────────────────────────────────
# shellcheck source=helpers/check-prerequisites.sh
source "${SCRIPT_DIR}/../helpers/check-prerequisites.sh" --with-aws

ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
HR_WEB_BUCKET="htx-onboarding-hr-web-${ACCOUNT_ID}"
HR_WEB_LOGS_BUCKET="htx-onboarding-hr-web-logs-${ACCOUNT_ID}"

ECR_REPOS=(
  htx-onboarding/hr-svc
  htx-onboarding/onboarding-svc
  htx-onboarding/workflow-svc
  htx-onboarding/hr-db-migrate
  htx-onboarding/onboarding-db-migrate
  htx-onboarding/db-init
  htx-onboarding/temporal
  htx-onboarding/temporal-ui
  htx-onboarding/xray-daemon
)

LOG_GROUPS=(
  /ecs/htx-onboarding/hr-svc
  /ecs/htx-onboarding/onboarding-svc
  /ecs/htx-onboarding/workflow-svc
  /ecs/htx-onboarding/temporal
  /ecs/htx-onboarding/temporal-ui
  /ecs/htx-onboarding/db-init
  /ecs/htx-onboarding/migrations
)

SECRETS=(
  htx-onboarding/prod/db/admin
  htx-onboarding/prod/db/hr-svc
  htx-onboarding/prod/db/onboarding-svc
  htx-onboarding/prod/connection-string/hr-svc
  htx-onboarding/prod/connection-string/onboarding-svc
)

# Stacks deleted in reverse dependency order.
# htx-onboarding-network is handled separately (VPC endpoint pre-cleanup needed).
STACKS=(
  htx-onboarding-scheduler
  htx-onboarding-cdn
  htx-onboarding-compute-services
  htx-onboarding-temporal
  htx-onboarding-compute-infra
  htx-onboarding-storage
  htx-onboarding-ecr
)

FINAL_STACKS=(
  htx-onboarding-network
  htx-onboarding-github-oidc
)

STEP=0

echo "==> Tearing down HTX Onboarding in ${REGION}"

# ── 1. Disable RDS deletion protection ───────────────────────────────────────
echo ""
echo "==> [$((++STEP))] Disabling RDS deletion protection..."
aws rds modify-db-instance \
  --db-instance-identifier "${DB_INSTANCE}" \
  --no-deletion-protection \
  --region "${REGION}" 2>/dev/null && \
  echo "    Waiting for RDS to apply change..." && \
  aws rds wait db-instance-available \
    --db-instance-identifier "${DB_INSTANCE}" \
    --region "${REGION}" || \
  echo "    (not found or already gone, skipping)"

# ── 2. Empty hr-web S3 bucket (all versions + delete markers) ────────────────
# Versioning is enabled on the bucket, so aws s3 rm only removes current versions.
# CloudFormation cannot delete a versioned bucket unless all versions are purged first.
echo ""
echo "==> [$((++STEP))] Emptying S3 bucket: ${HR_WEB_BUCKET}..."
if aws s3api head-bucket --bucket "${HR_WEB_BUCKET}" --region "${REGION}" 2>/dev/null; then
  aws s3api delete-objects \
    --bucket "${HR_WEB_BUCKET}" \
    --region "${REGION}" \
    --delete "$(aws s3api list-object-versions \
      --bucket "${HR_WEB_BUCKET}" \
      --region "${REGION}" \
      --query '{Objects: Versions[].{Key:Key,VersionId:VersionId}}' \
      --output json 2>/dev/null)" > /dev/null 2>&1 || true
  aws s3api delete-objects \
    --bucket "${HR_WEB_BUCKET}" \
    --region "${REGION}" \
    --delete "$(aws s3api list-object-versions \
      --bucket "${HR_WEB_BUCKET}" \
      --region "${REGION}" \
      --query '{Objects: DeleteMarkers[].{Key:Key,VersionId:VersionId}}' \
      --output json 2>/dev/null)" > /dev/null 2>&1 || true
  echo "    ${HR_WEB_BUCKET} — emptied (all versions)"
else
  echo "    (not found or already empty, skipping)"
fi

if aws s3api head-bucket --bucket "${HR_WEB_LOGS_BUCKET}" --region "${REGION}" 2>/dev/null; then
  aws s3api delete-objects \
    --bucket "${HR_WEB_LOGS_BUCKET}" \
    --region "${REGION}" \
    --delete "$(aws s3api list-object-versions \
      --bucket "${HR_WEB_LOGS_BUCKET}" \
      --region "${REGION}" \
      --query '{Objects: Versions[].{Key:Key,VersionId:VersionId}}' \
      --output json 2>/dev/null)" > /dev/null 2>&1 || true
  aws s3api delete-objects \
    --bucket "${HR_WEB_LOGS_BUCKET}" \
    --region "${REGION}" \
    --delete "$(aws s3api list-object-versions \
      --bucket "${HR_WEB_LOGS_BUCKET}" \
      --region "${REGION}" \
      --query '{Objects: DeleteMarkers[].{Key:Key,VersionId:VersionId}}' \
      --output json 2>/dev/null)" > /dev/null 2>&1 || true
  echo "    ${HR_WEB_LOGS_BUCKET} — emptied (all versions)"
else
  echo "    (not found or already empty, skipping)"
fi

# ── 3. Force-delete ECR repositories ─────────────────────────────────────────
# delete-repository --force empties the repo and deletes it atomically.
# This avoids the list-images + batch-delete-image pattern whose JMESPath
# projection returns null (not []) for empty repos, causing false "no images"
# skips and leaving repos non-empty when CloudFormation tries to delete them.
echo ""
echo "==> [$((++STEP))] Force-deleting ECR repositories..."
for REPO in "${ECR_REPOS[@]}"; do
  aws ecr delete-repository \
    --repository-name "${REPO}" \
    --force \
    --region "${REGION}" > /dev/null 2>&1 && \
    echo "    ${REPO} — deleted" || \
    echo "    ${REPO} — (not found, skipping)"
done

# ── 4. Delete CloudFormation stacks in reverse order ─────────────────────────
echo ""
echo "==> [$((++STEP))] Deleting CloudFormation stacks (cdn → ecr)..."
for STACK in "${STACKS[@]}"; do
  STATUS=$(aws cloudformation describe-stacks \
    --stack-name "${STACK}" --region "${REGION}" \
    --query 'Stacks[0].StackStatus' --output text 2>/dev/null || echo "DOES_NOT_EXIST")

  if [ "${STATUS}" = "DOES_NOT_EXIST" ]; then
    echo "    ${STACK} — (not found, skipping)"
    continue
  fi

  echo "    Deleting ${STACK} (${STATUS})..."
  aws cloudformation delete-stack --stack-name "${STACK}" --region "${REGION}"
  aws cloudformation wait stack-delete-complete --stack-name "${STACK}" --region "${REGION}"
  echo "    ${STACK} — deleted"
done

# ── 4b. Delete VPC Interface Endpoints before network stack ──────────────────
# CloudFormation deletes endpoints and the VPC concurrently. The VPC cannot
# be deleted while endpoint ENIs are still attached, causing the stack to
# hang. Deleting endpoints explicitly first ensures the VPC is free.
echo ""
echo "==> [$((++STEP))] Deleting VPC Interface Endpoints..."
VPC_ID=$(aws cloudformation describe-stacks \
  --stack-name htx-onboarding-network \
  --region "${REGION}" \
  --query 'Stacks[0].Outputs[?OutputKey==`VPCId`].OutputValue' \
  --output text 2>/dev/null || echo "")

if [ -n "${VPC_ID}" ] && [ "${VPC_ID}" != "None" ] && [ "${VPC_ID}" != "" ]; then
  ENDPOINT_IDS=$(aws ec2 describe-vpc-endpoints \
    --filters "Name=vpc-id,Values=${VPC_ID}" \
              "Name=vpc-endpoint-state,Values=available,pending,pending-acceptance" \
    --region "${REGION}" \
    --query 'VpcEndpoints[*].VpcEndpointId' \
    --output text 2>/dev/null || echo "")

  if [ -n "${ENDPOINT_IDS}" ] && [ "${ENDPOINT_IDS}" != "None" ]; then
    echo "    Deleting endpoints: ${ENDPOINT_IDS}"
    aws ec2 delete-vpc-endpoints \
      --vpc-endpoint-ids ${ENDPOINT_IDS} \
      --region "${REGION}" > /dev/null

    echo "    Waiting for all endpoints to finish deleting..."
    WAIT=0
    while true; do
      REMAINING=$(aws ec2 describe-vpc-endpoints \
        --filters "Name=vpc-id,Values=${VPC_ID}" \
                  "Name=vpc-endpoint-state,Values=deleting,available,pending,pending-acceptance" \
        --region "${REGION}" \
        --query 'length(VpcEndpoints)' \
        --output text 2>/dev/null || echo "0")
      [ "${REMAINING}" = "0" ] && break
      WAIT=$((WAIT + 1))
      if [ $WAIT -ge 30 ]; then
        echo "    WARNING: ${REMAINING} endpoint(s) still deleting after 5 min — proceeding anyway"
        break
      fi
      echo "    Still waiting (${REMAINING} endpoint(s) remaining)..."
      sleep 10
    done
    [ "${REMAINING}" = "0" ] && echo "    All endpoints deleted"
  else
    echo "    No active endpoints found"
  fi
else
  echo "    htx-onboarding-network not found, skipping"
fi

# ── 4c. Delete remaining ENIs before network stack ───────────────────────────
# Fargate task ENIs are not cleaned up instantly after the compute stacks are
# deleted. The VPC cannot be deleted while any subnet still has attached ENIs,
# so we wait for in-use ENIs to drain and then delete any that are available.
echo ""
echo "==> [$((++STEP))] Waiting for Fargate ENIs to drain..."
if [ -n "${VPC_ID}" ] && [ "${VPC_ID}" != "None" ] && [ "${VPC_ID}" != "" ]; then
  WAIT=0
  while true; do
    IN_USE=$(aws ec2 describe-network-interfaces \
      --filters "Name=vpc-id,Values=${VPC_ID}" \
                "Name=status,Values=in-use" \
      --region "${REGION}" \
      --query 'length(NetworkInterfaces)' \
      --output text 2>/dev/null || echo "0")
    [ "${IN_USE}" = "0" ] && break
    WAIT=$((WAIT + 1))
    if [ $WAIT -ge 18 ]; then
      echo "    WARNING: ${IN_USE} ENI(s) still in-use after 3 min — proceeding anyway"
      break
    fi
    echo "    ${IN_USE} ENI(s) still in-use, waiting..."
    sleep 10
  done

  ENI_IDS=$(aws ec2 describe-network-interfaces \
    --filters "Name=vpc-id,Values=${VPC_ID}" \
              "Name=status,Values=available" \
    --region "${REGION}" \
    --query 'NetworkInterfaces[*].NetworkInterfaceId' \
    --output text 2>/dev/null || echo "")

  if [ -n "${ENI_IDS}" ] && [ "${ENI_IDS}" != "None" ]; then
    for ENI_ID in ${ENI_IDS}; do
      aws ec2 delete-network-interface \
        --network-interface-id "${ENI_ID}" \
        --region "${REGION}" 2>/dev/null && \
        echo "    ${ENI_ID} — deleted" || \
        echo "    ${ENI_ID} — (skipped)"
    done
  else
    echo "    No available ENIs"
  fi
else
  echo "    VPC not found, skipping"
fi

# ── 4d. Delete network + github-oidc stacks ──────────────────────────────────
echo ""
echo "==> [$((++STEP))] Deleting network + github-oidc stacks..."

delete_stack_with_retry() {
  local STACK="$1"

  local STATUS
  STATUS=$(aws cloudformation describe-stacks \
    --stack-name "${STACK}" --region "${REGION}" \
    --query 'Stacks[0].StackStatus' --output text 2>/dev/null || echo "DOES_NOT_EXIST")

  if [ "${STATUS}" = "DOES_NOT_EXIST" ]; then
    echo "    ${STACK} — (not found, skipping)"
    return 0
  fi

  echo "    Deleting ${STACK} (${STATUS})..."
  aws cloudformation delete-stack --stack-name "${STACK}" --region "${REGION}"

  if aws cloudformation wait stack-delete-complete --stack-name "${STACK}" --region "${REGION}" 2>/dev/null; then
    echo "    ${STACK} — deleted"
    return 0
  fi

  # wait returned non-zero — check actual stack state
  local RETRY_STATUS
  RETRY_STATUS=$(aws cloudformation describe-stacks \
    --stack-name "${STACK}" --region "${REGION}" \
    --query 'Stacks[0].StackStatus' --output text 2>/dev/null || echo "DOES_NOT_EXIST")

  # stack gone by the time wait polled — treat as success
  if [ "${RETRY_STATUS}" = "DOES_NOT_EXIST" ]; then
    echo "    ${STACK} — deleted"
    return 0
  fi

  if [ "${RETRY_STATUS}" != "DELETE_FAILED" ]; then
    echo "    ERROR: ${STACK} ended in unexpected state ${RETRY_STATUS}"
    return 1
  fi

  echo "    DELETE_FAILED — checking failed resources..."
  aws cloudformation describe-stack-resources \
    --stack-name "${STACK}" --region "${REGION}" \
    --query 'StackResources[?ResourceStatus==`DELETE_FAILED`].[LogicalResourceId,ResourceType,ResourceStatusReason]' \
    --output table 2>/dev/null || true

  if [ -n "${VPC_ID}" ] && [ "${VPC_ID}" != "None" ]; then
    echo "    Cleaning up remaining ENIs in VPC..."
    for ENI_ID in $(aws ec2 describe-network-interfaces \
      --filters "Name=vpc-id,Values=${VPC_ID}" "Name=status,Values=available" \
      --region "${REGION}" \
      --query 'NetworkInterfaces[*].NetworkInterfaceId' --output text 2>/dev/null || echo ""); do
      aws ec2 delete-network-interface --network-interface-id "${ENI_ID}" --region "${REGION}" 2>/dev/null && \
        echo "    ${ENI_ID} — deleted" || true
    done

    echo "    Cleaning up non-default security groups in VPC..."
    for SG_ID in $(aws ec2 describe-security-groups \
      --filters "Name=vpc-id,Values=${VPC_ID}" \
      --region "${REGION}" \
      --query 'SecurityGroups[?GroupName!=`default`].GroupId' --output text 2>/dev/null || echo ""); do
      aws ec2 delete-security-group --group-id "${SG_ID}" --region "${REGION}" 2>/dev/null && \
        echo "    ${SG_ID} — deleted" || true
    done
  fi

  echo "    Retrying ${STACK} deletion..."
  aws cloudformation delete-stack --stack-name "${STACK}" --region "${REGION}"
  aws cloudformation wait stack-delete-complete --stack-name "${STACK}" --region "${REGION}"
  echo "    ${STACK} — deleted"
}

for STACK in "${FINAL_STACKS[@]}"; do
  delete_stack_with_retry "${STACK}"
done

# ── 5. Delete CloudWatch log groups ──────────────────────────────────────────
echo ""
echo "==> [$((++STEP))] Deleting CloudWatch log groups..."
for LG in "${LOG_GROUPS[@]}"; do
  aws logs delete-log-group \
    --log-group-name "${LG}" \
    --region "${REGION}" 2>/dev/null && \
    echo "    ${LG} — deleted" || \
    echo "    ${LG} — (not found, skipping)"
done

# ── 6. Deregister all ECS task definitions ───────────────────────────────────
# CloudFormation does not deregister task definitions on stack delete — all
# revisions remain as INACTIVE entries until explicitly deregistered.
echo ""
echo "==> [$((++STEP))] Deregistering all task definitions..."
for FAMILY in \
  "htx-onboarding-hr-svc" \
  "htx-onboarding-onboarding-svc" \
  "htx-onboarding-workflow-svc" \
  "htx-onboarding-temporal" \
  "htx-onboarding-temporal-ui" \
  "htx-onboarding-db-init" \
  "htx-onboarding-hr-db-migrate" \
  "htx-onboarding-onboarding-db-migrate" \
  "htx-onboarding-temporal-ns-init"; do
  TASK_DEF_ARNS=$(aws ecs list-task-definitions \
    --family-prefix "${FAMILY}" \
    --region "${REGION}" \
    --query 'taskDefinitionArns[*]' \
    --output text 2>/dev/null || true)

  if [ -n "${TASK_DEF_ARNS}" ]; then
    for ARN in ${TASK_DEF_ARNS}; do
      aws ecs deregister-task-definition \
        --task-definition "${ARN}" \
        --region "${REGION}" > /dev/null
      echo "    Deregistered ${ARN}"
    done
  else
    echo "    ${FAMILY} — (none found, skipping)"
  fi
done

# ── 7. Force-delete Secrets Manager secrets ───────────────────────────────────
# CloudFormation schedules secrets for 30-day recovery by default, which blocks
# redeployment with the same secret names. Force-delete removes them immediately.
echo ""
echo "==> [$((++STEP))] Force-deleting Secrets Manager secrets..."
for SECRET in "${SECRETS[@]}"; do
  aws secretsmanager delete-secret \
    --secret-id "${SECRET}" \
    --force-delete-without-recovery \
    --region "${REGION}" 2>/dev/null && \
    echo "    ${SECRET} — deleted" || \
    echo "    ${SECRET} — (not found or already deleted, skipping)"
done

# ── 8. Delete CloudFormation exports are clean — no S3 bucket needed anymore ──
echo ""
echo "    No CFN template bucket to clean (templates deployed directly)."

echo ""
echo "==> Teardown complete. No resources remain."
