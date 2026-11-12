#!/bin/zsh
set -eu

if (( EUID != 0 )); then
  echo "Error: run this script as root or with sudo: sudo $0" >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive

# create the swap file if needed
swap_file=${SWAP_FILE:-/swap.img}
swap_size_gib=${SWAP_SIZE_GIB:-16}

if ! swapon --noheadings --show=NAME | grep -Fxq "$swap_file"; then
  if [[ ! -e "$swap_file" ]]; then
    fallocate -l "${swap_size_gib}G" "$swap_file"
    chmod 0600 "$swap_file"
    mkswap "$swap_file"
  fi

  swapon "$swap_file"
fi

if ! awk -v swap_file="$swap_file" \
  '$1 == swap_file && $3 == "swap" { found=1 } END { exit !found }' /etc/fstab; then
  printf '%s none swap sw 0 0\n' "$swap_file" >> /etc/fstab
fi

# install packages
apt-get update
apt-get install -y git curl wget nano gpg apt-utils dialog ca-certificates
apt-get install -y ripgrep shellcheck fd-find tree bat ncdu shfmt jq tmux htop sysstat lsof
apt-get install -y libssl-dev libffi-dev build-essential gnupg pass software-properties-common
apt-get install -y zip unzip httpie dnsutils dotnet-sdk-10.0

# add Docker repo
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
cat > /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
rm -f /etc/apt/sources.list.d/docker.list

# add gh cli repo
mkdir -p -m 755 /etc/apt/keyrings
curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg -o /etc/apt/keyrings/githubcli-archive-keyring.gpg
chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" > /etc/apt/sources.list.d/github-cli.list

apt-get update

# install Docker and GitHub CLI
apt-get install -y gh docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

systemctl stop docker.service docker.socket containerd.service || true

# handle VMs with mounted data drives
if mountpoint -q /mnt/data; then
  docker_data_root=/mnt/data/docker
  containerd_data_root=/mnt/data/containerd
  use_data_disk=true
else
  docker_data_root=/var/lib/docker
  containerd_data_root=/var/lib/containerd
  use_data_disk=false
fi

install -d -m 0711 "$docker_data_root" "$containerd_data_root"

# configure dockerd
cat > /etc/docker/daemon.json <<EOF
{
  "data-root": "$docker_data_root",
  "default-address-pools": [
    { "base": "10.240.0.0/14", "size": 24 }
  ],
  "default-ulimits": {
    "nofile": { "Name": "nofile", "Soft": 65536, "Hard": 1048576 }
  }
}
EOF

# Docker uses containerd's image store for image layers and snapshots.
cat > /etc/containerd/config.toml <<EOF
version = 4
disabled_plugins = ["io.containerd.grpc.v1.cri"]
root = "$containerd_data_root"
state = "/run/containerd"
EOF

install -d -m 0755 \
  /etc/systemd/system/docker.service.d \
  /etc/systemd/system/containerd.service.d

# add systemd overrides for data disk if needed
if "$use_data_disk"; then
  cat > /etc/systemd/system/docker.service.d/data-root.conf <<'EOF'
[Unit]
RequiresMountsFor=/mnt/data
EOF

  cat > /etc/systemd/system/containerd.service.d/data-root.conf <<'EOF'
[Unit]
RequiresMountsFor=/mnt/data
EOF
else
  rm -f \
    /etc/systemd/system/docker.service.d/data-root.conf \
    /etc/systemd/system/containerd.service.d/data-root.conf
fi

# reload systemd to apply any changes made to service overrides
systemctl daemon-reload

# configure inotify
cat > /etc/sysctl.d/99-deep-swe.conf <<'EOF'
fs.inotify.max_user_instances = 1024
fs.inotify.max_user_watches = 1048576
EOF
sysctl --system
dockerd --validate --config-file=/etc/docker/daemon.json
containerd --config /etc/containerd/config.toml config dump > /dev/null

# [re]start containerd and docker
systemctl restart containerd.service docker.service
