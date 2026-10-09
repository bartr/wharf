# Large-host kernel limits research

## Scope

Evaluate whether `setup/base.sh` should raise host limits for Wharf machines
running 32 or more cores and N=64 or higher. This phase is read-only with
respect to provisioning behavior.

## Existing configuration

- `setup/base.sh:9-10` creates a configurable 16 GiB swap file by default.
- `setup/base.sh:77-79` configures Docker containers with a 65,536 soft
  `nofile` limit and a 1,048,576 hard limit.
- `setup/base.sh:113-114` explicitly sets only
  `fs.inotify.max_user_instances=1024`.
- `src/wharf/Host/HostChecks.cs:21-30` requires at least 1,024 inotify
  instances, 65,536 inotify watches, 65,536 soft and hard `nofile` limits,
  and `kernel.pid_max=32768`.
- `src/wharf/Agent/AgentExecution.cs:780-790` applies a separate per-container
  PID cap of 1,024. That is a workload isolation policy, not a host sysctl.
- `docs/wharf.md:408-416` requires preflight detection of insufficient file
  descriptors, inotify capacity, PIDs, and network capacity.

## Host measurements

Measurements were taken while the DigitalOcean run was draining from N=80.

| Limit or usage | z01 | DigitalOcean | Assessment |
|---|---:|---:|---|
| `max_user_instances` | 1,024 | 1,024 | Adequate at measured load; shared UID ceiling |
| Live inotify instances | 45 | 139 | 13.6% of the configured ceiling on the droplet |
| `max_user_watches` | 65,536 | 1,048,576 | Inconsistent across provisioned hosts |
| Live inotify watches | 1,340 | 4 | Far below both ceilings at measurement time |
| `fs.file-max` | `LONG_MAX` | `LONG_MAX` | No change needed |
| Allocated file handles | 10,812 | 7,872 | No global pressure |
| `kernel.pid_max` | 4,194,304 | 4,194,304 | No change needed |
| `kernel.threads-max` | 456,771 | 1,287,395 | No host-global pressure |
| Live host threads | 1,578 | 6,142 | Less than 0.5% of droplet capacity |
| Docker `TasksMax` | infinity | infinity | No systemd task ceiling |
| Docker `LimitNPROC` | infinity | infinity | No service process ceiling |
| `nf_conntrack_max` | 262,144 | 262,144 | No change needed |
| Live conntrack entries | 72 | 142 | No network table pressure |
| `vm.max_map_count` | 1,048,576 | 1,048,576 | No change needed |
| Swap after provisioning | 8 GiB | 16 GiB | Droplet swap enabled; usage was zero |

The droplet's 139 inotify instances with 33 containers projects to roughly 540
instances at 128 containers if usage scales linearly. The current 1,024 limit
is therefore adequate for the observed workload, but it provides less margin
for watcher-heavy tasks and host tools because all UID 1000 containers share
the host UID limit.

The `fd` agent's thread-spawn failure does not justify raising global process
limits: host PID, thread, systemd, and service limits were all far from
exhaustion. The remaining candidate is the per-container 1,024 PID cap, which
should be changed only with direct `pids.events` evidence or a reproducible
failure.

## Recommendation

Change only the inotify provisioning values:

1. Set `fs.inotify.max_user_instances=4096`.
2. Explicitly set `fs.inotify.max_user_watches=1048576` so z01 and new
   droplets receive the same capacity.
3. Keep `fs.inotify.max_queued_events=16384`; it is per instance and no queue
   overflow was observed.
4. Keep the current Docker `nofile`, host PID/thread, `vm.max_map_count`,
   conntrack, and systemd limits.
5. Keep the 16 GiB swap default and measure actual use before increasing it.
6. Do not change the per-container 1,024 PID cap in this host-setup change.
   Add PID-pressure evidence capture separately before reconsidering it.

Raising inotify ceilings does not preallocate the corresponding kernel memory;
memory is consumed when applications create instances and watches. The proposed
values add concurrency headroom without reserving significant resources or
weakening container isolation.

## Not recommended

- Raising global PID or thread limits that are already orders of magnitude
  above observed use.
- Increasing conntrack or ephemeral-port ranges without evidence of network
  allocation failures.
- Increasing `nofile` beyond the existing 65,536 soft and 1,048,576 hard
  container defaults.
- Raising the per-container PID cap based on one ambiguous `EAGAIN` failure.
