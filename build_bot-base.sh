#!/usr/bin/bash

git submodule foreach git pull

VERSION=$(cd $FF_REPO && git describe --tags | awk -F'-' '{print $1 "-" $2}')
if [[ -z "$VERSION" ]]; then
    echo "Error: Invaild tags, Have you clone all submodules?"
    exit 1
fi

IMAGE_NAME="registry.cn-beijing.aliyuncs.com/nidb-cr/streaming-capture-bot-base"
IMAGE_TAG="${IMAGE_NAME}:${VERSION}"

docker build ./StreamingCaptureBot.Base -t "$IMAGE_TAG"
