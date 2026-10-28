#!/bin/bash
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq >/dev/null
apt-get install -y -qq --no-install-recommends \
    build-essential cmake git curl ca-certificates pkg-config patchelf \
    libmariadb-dev libmariadb-dev-compat libssl-dev zlib1g-dev \
    libasound2-dev libudev-dev libwayland-dev libxkbcommon-dev clang >/dev/null

build_server() {
    rm -rf /work/server && mkdir -p /work/server
    tar -C /src/server --exclude=.git -cf - . | tar -C /work/server -xf -
    cmake -S /work/server -B /cache/server -DCMAKE_BUILD_TYPE=Release -DBUILD_EXTRACTORS=1 -DBUILD_FOR_HOST_CPU=OFF \
        -DCMAKE_INSTALL_PREFIX=/out/server >/dev/null
    if ! cmake --build /cache/server -j"$(nproc)" -- -k > /cache/server-build.log 2>&1; then
        grep -E ': error:|undefined reference' /cache/server-build.log | sort -u
        echo 'Linux server build failed' >&2
        exit 1
    fi
    rm -rf /out/server
    cmake --install /cache/server >/dev/null
    cp -L "$(ldconfig -p | awk '/libmariadb\.so\.3 /{print $NF; exit}')" /out/server/bin/
    for binary in /out/server/bin/mangosd /out/server/bin/realmd; do patchelf --set-rpath '$ORIGIN' "$binary"; done
    strip /out/server/bin/mangosd /out/server/bin/realmd /out/server/bin/Extractors/*Extractor /out/server/bin/Extractors/VMapAssembler /out/server/bin/Extractors/MoveMapGenerator
}

build_client() {
    if ! command -v cargo >/dev/null; then
        curl -sSf https://sh.rustup.rs | sh -s -- -y -q --default-toolchain none >/dev/null
    fi
    . "$HOME/.cargo/env"
    cd /src/client
    rustup toolchain install --profile minimal >/dev/null
    if ! CARGO_TARGET_DIR=/cache/client cargo build --profile ship --locked -p hoa --no-default-features > /cache/client-build.log 2>&1; then
        grep -E -A5 '^error' /cache/client-build.log
        echo 'Linux client build failed' >&2
        exit 1
    fi
    grep -E 'Finished' /cache/client-build.log
    mkdir -p /out/client
    cp /cache/client/ship/HordesOfAzeroth /out/client/
    strip /out/client/HordesOfAzeroth
}

case "${1:-all}" in
    server) build_server ;;
    client) build_client ;;
    all) build_server; build_client ;;
esac

echo "glibc floor: $(ldd --version | head -1 | awk '{print $NF}')"
find /out -maxdepth 3 -type f \( -name HordesOfAzeroth -o -name mangosd -o -name realmd -o -name 'libmariadb*' \) -printf '%P %s\n'
