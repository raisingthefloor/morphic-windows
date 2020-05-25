#!/usr/bin/env bash
set -e

# To be set by the environment. This is mostly for documentation purposes.
BUILD_NUM="${BUILD_NUM}"
BRANCH="${BRANCH}"
BRANCH_NAME="${BRANCH_NAME}"
COMMIT="${COMMIT}"

INFO_FILE="Morphic.Client/build-info.json"
FINAL_VERSION="v0.0.0-${BUILD_NUM}"

if [[ "${BRANCH}" == *"tags"* ]]; then
  FINAL_VERSION=${BRANCH_NAME}
fi

cat > $INFO_FILE << EOF
{
  "version": "${FINAL_VERSION}",
  "buildTime": "$(date -u)",
  "commit": "${COMMIT}"
}
EOF