#!/bin/bash
# Shared prerequisite checker — sourced by all deploy and local scripts.
# Must be sourced (not executed) so exported variables are visible to the caller.
#
# Flags:
#   --with-aws      Require aws + jq; verify AWS credentials for $REGION
#   --with-node     Require npm + node (hr-web build)
#   --with-docker   Require podman/docker (AWS deploy); exports DOCKER / DOCKER_PULL_OPTS / DOCKER_BUILD_OPTS
#   --with-runtime  Require podman/docker (local dev);  exports RUNTIME / COMPOSE
#
# Example:
#   source "${SCRIPT_DIR}/../helpers/check-prerequisites.sh" --with-aws --with-node --with-docker

_WITH_AWS=false
_WITH_NODE=false
_WITH_DOCKER=false
_WITH_RUNTIME=false

for _arg in "$@"; do
  case "${_arg}" in
    --with-aws)     _WITH_AWS=true ;;
    --with-node)    _WITH_NODE=true ;;
    --with-docker)  _WITH_DOCKER=true ;;
    --with-runtime) _WITH_RUNTIME=true ;;
  esac
done

# ── Required CLI tools ─────────────────────────────────────────────────────────
_REQUIRED=()
${_WITH_AWS}  && _REQUIRED+=(aws jq)
${_WITH_NODE} && _REQUIRED+=(npm node)

_MISSING=()
for _tool in "${_REQUIRED[@]}"; do
  command -v "${_tool}" &>/dev/null || _MISSING+=("${_tool}")
done

if [ ${#_MISSING[@]} -gt 0 ]; then
  echo "ERROR: missing required tools: ${_MISSING[*]}"
  echo ""
  echo "  aws:      https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html"
  echo "  jq:       brew install jq"
  echo "  npm/node: https://nodejs.org"
  exit 1
fi

# ── Node.js version ───────────────────────────────────────────────────────────
if command -v node &>/dev/null; then
  _NODE_VERSION=$(node -e "process.stdout.write(process.version.slice(1))")
  _NODE_MAJOR=$(echo "${_NODE_VERSION}" | cut -d. -f1)
  _NODE_MINOR=$(echo "${_NODE_VERSION}" | cut -d. -f2)

  _NODE_OK=false
  if [ "${_NODE_MAJOR}" -gt 22 ]; then
    _NODE_OK=true
  elif [ "${_NODE_MAJOR}" -eq 22 ] && [ "${_NODE_MINOR}" -ge 12 ]; then
    _NODE_OK=true
  elif [ "${_NODE_MAJOR}" -eq 20 ] && [ "${_NODE_MINOR}" -ge 19 ]; then
    _NODE_OK=true
  fi

  if ! ${_NODE_OK}; then
    echo "ERROR: Node.js ${_NODE_VERSION} is too old — Vite requires Node 20.19+ or 22.12+"
    echo ""
    echo "  nvm:  nvm install 22 && nvm use 22"
    echo "  brew: brew install node"
    echo "  url:  https://nodejs.org"
    exit 1
  fi
  unset _NODE_VERSION _NODE_MAJOR _NODE_MINOR _NODE_OK
fi

# ── AWS credentials ────────────────────────────────────────────────────────────
if ${_WITH_AWS}; then
  if ! aws sts get-caller-identity --region "${REGION}" > /dev/null 2>&1; then
    echo "ERROR: AWS credentials are not configured or are invalid for region ${REGION}"
    echo ""
    echo "  aws configure                        set up a default profile"
    echo "  export AWS_PROFILE=<profile>         switch to a named profile"
    echo "  export AWS_DEFAULT_REGION=${REGION}   set region explicitly"
    exit 1
  fi
fi

# ── Docker / Podman (AWS-style) ────────────────────────────────────────────────
# Exports: DOCKER, DOCKER_PULL_OPTS, DOCKER_BUILD_OPTS
if ${_WITH_DOCKER}; then
  if command -v podman &>/dev/null; then
    DOCKER=$(command -v podman)
    DOCKER_PULL_OPTS="--tls-verify=false"
    DOCKER_BUILD_OPTS="--tls-verify=false"
  elif command -v docker &>/dev/null && docker info &>/dev/null 2>&1; then
    DOCKER=$(command -v docker)
    DOCKER_PULL_OPTS=""
    DOCKER_BUILD_OPTS=""
  else
    echo "ERROR: neither podman nor docker is available"
    echo ""
    echo "  podman: https://podman.io/getting-started/installation"
    echo "  docker: https://docs.docker.com/get-docker/"
    exit 1
  fi
  export DOCKER DOCKER_PULL_OPTS DOCKER_BUILD_OPTS
fi

# ── Docker / Podman (local dev style) ─────────────────────────────────────────
# Exports: RUNTIME, COMPOSE
if ${_WITH_RUNTIME}; then
  if command -v podman &>/dev/null; then
    RUNTIME=podman
    COMPOSE="podman compose"
  elif command -v docker &>/dev/null; then
    RUNTIME=docker
    COMPOSE="docker compose"
  else
    echo "ERROR: neither podman nor docker found"
    echo ""
    echo "  podman: https://podman.io/getting-started/installation"
    echo "  docker: https://docs.docker.com/get-docker/"
    exit 1
  fi
  export RUNTIME COMPOSE
fi

unset _WITH_AWS _WITH_NODE _WITH_DOCKER _WITH_RUNTIME _REQUIRED _MISSING _tool _arg
