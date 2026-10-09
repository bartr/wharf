#!/bin/zsh
set -euo pipefail

mount_point=${DATA_MOUNT_POINT:-/mnt/data}
filesystem_label=${DATA_DISK_LABEL:-data}
minimum_size=$((1024 * 1024 * 1024))
assume_yes=false

usage() {
  cat <<EOF
Usage: sudo $0 [--yes]

Find one blank, unmounted, non-OS disk, format it as ext4, and mount it at
$mount_point with continuous discard disabled. Enable periodic TRIM through
fstrim.timer. Use --yes to skip the interactive device-name confirmation.
EOF
}

fail() {
  echo "Error: $*" >&2
  exit 1
}

persist_mount_options() {
  local data_device
  local temp_fstab
  local uuid

  data_device=$(readlink -f "$(findmnt -nro SOURCE --target "$mount_point")")
  [[ -b $data_device ]] ||
    fail "mount source is not a block device: $data_device"
  uuid=$(blkid -s UUID -o value "$data_device")
  [[ -n $uuid ]] || fail "could not read the filesystem UUID from $data_device"

  if [[ ! -e /etc/fstab.deep-swe.bak ]]; then
    cp --preserve=all /etc/fstab /etc/fstab.deep-swe.bak
  fi

  if awk -v target="$mount_point" \
    '$1 !~ /^#/ && $2 == target { found=1 } END { exit !found }' /etc/fstab; then
    temp_fstab=$(mktemp)
    if ! awk -v target="$mount_point" '
      function append_option(option) {
        options = options ? options "," option : option
      }
      $1 !~ /^#/ && $2 == target {
        options=""
        count=split($4, current, ",")
        for (i=1; i<=count; i++) {
          if (current[i] != "discard" && current[i] != "nodiscard" &&
              current[i] != "atime" && current[i] != "relatime" &&
              current[i] != "noatime") {
            append_option(current[i])
          }
        }
        append_option("noatime")
        append_option("nodiscard")
        $4=options
      }
      { print }
    ' /etc/fstab >"$temp_fstab"; then
      rm -f "$temp_fstab"
      fail "could not update mount options in /etc/fstab"
    fi
    install -m 0644 "$temp_fstab" /etc/fstab
    rm -f "$temp_fstab"
  else
    printf 'UUID=%s %s ext4 defaults,nofail,noatime,nodiscard 0 2\n' \
      "$uuid" "$mount_point" >>/etc/fstab
  fi

  systemctl daemon-reload
  mount -o remount,noatime,nodiscard "$mount_point" ||
    fail "could not remount $mount_point with continuous discard disabled"
}

configure_periodic_trim() {
  local mount_options

  mount_options=$(findmnt -nro OPTIONS --target "$mount_point") ||
    fail "could not read mount options for $mount_point"
  if grep -qw discard <<<"${mount_options//,/ }"; then
    fail "$mount_point uses continuous discard; replace 'discard' with 'nodiscard' in /etc/fstab and remount it"
  fi

  systemctl enable --now fstrim.timer ||
    fail "could not enable fstrim.timer"
  echo "Enabled periodic TRIM with fstrim.timer; continuous discard is disabled."
}

validate_container_storage() {
  local containerd_config=/etc/containerd/config.toml
  local containerd_root
  local docker_root
  local storage_mount

  if ! command -v docker >/dev/null; then
    echo "Docker is not installed; skipping container storage validation."
    return
  fi

  command -v containerd >/dev/null ||
    fail "Docker is installed, but containerd was not found"
  [[ -f $containerd_config ]] ||
    fail "Docker is installed, but $containerd_config does not exist"

  docker_root=$(docker info --format '{{.DockerRootDir}}') ||
    fail "could not query Docker's effective data root; ensure Docker is running"
  [[ -n $docker_root && -d $docker_root ]] ||
    fail "Docker reported an invalid data root: ${docker_root:-<empty>}"

  storage_mount=$(findmnt -nro TARGET --target "$docker_root") ||
    fail "could not determine the filesystem used by Docker at $docker_root"
  [[ $storage_mount == "$mount_point" ]] ||
    fail "Docker data root $docker_root is on $storage_mount, not $mount_point"

  containerd_root=$(
    containerd --config "$containerd_config" config dump |
      awk -F ' = ' '$1 == "root" {
        value=$2
        gsub(/["\047]/, "", value)
        print value
        exit
      }'
  )
  [[ -n $containerd_root && -d $containerd_root ]] ||
    fail "containerd reported an invalid root: ${containerd_root:-<empty>}"

  storage_mount=$(findmnt -nro TARGET --target "$containerd_root") ||
    fail "could not determine the filesystem used by containerd at $containerd_root"
  [[ $storage_mount == "$mount_point" ]] ||
    fail "containerd root $containerd_root is on $storage_mount, not $mount_point"

  echo "Validated Docker storage at $docker_root on $mount_point."
  echo "Validated containerd storage at $containerd_root on $mount_point."
}

case "${1:-}" in
  "")
    ;;
  --yes)
    assume_yes=true
    ;;
  -h | --help)
    usage
    exit 0
    ;;
  *)
    usage >&2
    exit 2
    ;;
esac

((EUID == 0)) || fail "run this script with sudo"
[[ $mount_point == /* && $mount_point != *[[:space:]]* ]] ||
  fail "DATA_MOUNT_POINT must be an absolute path without whitespace"

for command in findmnt lsblk readlink wipefs mkfs.ext4 blkid mount install awk grep systemctl cp mktemp rm; do
  command -v "$command" >/dev/null || fail "required command not found: $command"
done

if mountpoint -q "$mount_point"; then
  echo "$mount_point is already mounted from $(findmnt -nro SOURCE "$mount_point")."
  persist_mount_options
  configure_periodic_trim
  validate_container_storage
  exit 0
fi

if findmnt --fstab --target "$mount_point" >/dev/null 2>&1; then
  install -d -m 0755 "$mount_point"
  mount "$mount_point"
  mountpoint -q "$mount_point" ||
    fail "the existing fstab entry did not mount $mount_point"
  echo "Mounted $(findmnt -nro SOURCE "$mount_point") at $mount_point."
  persist_mount_options
  configure_periodic_trim
  validate_container_storage
  exit 0
fi

root_source=$(readlink -f "$(findmnt -nro SOURCE /)")
[[ -b $root_source ]] || fail "root filesystem source is not a block device: $root_source"

mapfile -t os_disks < <(
  lsblk -snp -o PATH,TYPE "$root_source" |
    awk '$2 == "disk" { print $1 }'
)
((${#os_disks[@]} > 0)) || fail "could not determine the OS disk"

is_os_disk() {
  local disk=$1
  local os_disk

  for os_disk in "${os_disks[@]}"; do
    [[ $disk == "$os_disk" ]] && return 0
  done
  return 1
}

candidates=()
while read -r disk type size read_only removable; do
  [[ $type == disk && $read_only == 0 && $removable == 0 ]] || continue
  ((size >= minimum_size)) || continue
  is_os_disk "$disk" && continue

  if lsblk -nrpo MOUNTPOINT "$disk" | grep -q '[^[:space:]]'; then
    continue
  fi

  candidates+=("$disk")
done < <(lsblk -dnbrpo NAME,TYPE,SIZE,RO,RM)

case ${#candidates[@]} in
  0)
    fail "no eligible unmounted data disk was found"
    ;;
  1)
    data_disk=${candidates[0]}
    ;;
  *)
    printf 'Eligible data disks:\n' >&2
    printf '  %s\n' "${candidates[@]}" >&2
    fail "multiple data disks found; refusing to choose one automatically"
    ;;
esac

if ((${#os_disks[@]} == 1)) && [[ $data_disk == "${os_disks[0]}" ]]; then
  fail "refusing to format the OS disk: $data_disk"
fi

if [[ $(lsblk -nrpo TYPE "$data_disk" | grep -vc '^disk$') -ne 0 ]]; then
  fail "$data_disk contains partitions; refusing to format it"
fi

signatures=$(wipefs -n "$data_disk")
[[ -z $signatures ]] ||
  fail "$data_disk contains filesystem or partition signatures; refusing to format it"

disk_size=$(lsblk -dnro SIZE "$data_disk")
disk_model=$(lsblk -dnro MODEL "$data_disk" | awk '{$1=$1};1')
echo "Blank data disk found: $data_disk ($disk_size${disk_model:+, $disk_model})"
echo "It will be formatted as ext4 and mounted at $mount_point."

if ! "$assume_yes"; then
  [[ -t 0 ]] ||
    fail "interactive confirmation requires a terminal; rerun with --yes if intended"
  read -r -p "Type $data_disk to confirm: " confirmation
  [[ $confirmation == "$data_disk" ]] || fail "confirmation did not match; no changes made"
fi

mkfs.ext4 -m 0 -L "$filesystem_label" "$data_disk"
uuid=$(blkid -s UUID -o value "$data_disk")
[[ -n $uuid ]] || fail "could not read the new filesystem UUID"

install -d -m 0755 "$mount_point"
printf 'UUID=%s %s ext4 defaults,nofail,noatime,nodiscard 0 2\n' "$uuid" "$mount_point" >>/etc/fstab

if ! mount "$mount_point"; then
  sed -i "\|^UUID=$uuid $mount_point ext4 |d" /etc/fstab
  fail "mount failed; removed the new fstab entry"
fi

mountpoint -q "$mount_point" || fail "$mount_point is not a mount point after mounting"
configure_periodic_trim
echo "Mounted $data_disk at $mount_point and added its UUID to /etc/fstab."
