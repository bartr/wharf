#!/bin/zsh
set -eu

cd "$(dirname $0)"
export dir=$(pwd)

export DEBIAN_FRONTEND=noninteractive

update-alternatives --set iptables /usr/sbin/iptables-legacy

apt-get update

apt-get install -y ripgrep shellcheck fd-find tree bat ncdu shfmt jq tmux htop sysstat lsof gh
apt-get install -y gpg wget
apt-get install -y apt-utils dialog ca-certificates software-properties-common
apt-get install -y libssl-dev libffi-dev build-essential cifs-utils gnupg pass
apt-get install -y curl git nano zsh
apt-get install -y zip unzip httpie dnsutils dotnet-sdk-10.0

#apt-get upgrade -y -o Dpkg::Options::="--force-confold"

# add Docker repo
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list

# add gh cli repo
mkdir -p -m 755 /etc/apt/keyrings
curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg -o /etc/apt/keyrings/githubcli-archive-keyring.gpg
chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" > /etc/apt/sources.list.d/github-cli.list

apt-get update

apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

systemctl stop docker.service docker.socket containerd.service || true

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

# Docker 29 uses containerd's image store for image layers and snapshots.
cat > /etc/containerd/config.toml <<EOF
version = 4
disabled_plugins = ["io.containerd.grpc.v1.cri"]
root = "$containerd_data_root"
state = "/run/containerd"
EOF

install -d -m 0755 \
  /etc/systemd/system/docker.service.d \
  /etc/systemd/system/containerd.service.d

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

systemctl daemon-reload

# configure inotify
echo 'fs.inotify.max_user_instances = 1024' > /etc/sysctl.d/99-deep-swe.conf
sysctl --system
dockerd --validate --config-file=/etc/docker/daemon.json
containerd --config /etc/containerd/config.toml config dump > /dev/null

# [re]start containerd and docker
systemctl restart containerd.service docker.service
