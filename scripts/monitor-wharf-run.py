#!/usr/bin/env python3
"""Read-only, local cgroup-v2 resource sampling for one existing Wharf run."""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import signal
import socket
import subprocess
import time
from datetime import datetime, timezone


CGROUP_ROOT = Path("/sys/fs/cgroup")


def timestamp():
    return datetime.now(timezone.utc).isoformat()


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value) + "\n")
    temporary.replace(path)


def append_json(path, value):
    with path.open("a") as stream:
        stream.write(json.dumps(value) + "\n")


def process_identity(pid):
    try:
        return Path(f"/proc/{pid}/stat").read_text().rsplit(")", 1)[1].split()[19]
    except FileNotFoundError:
        return None


def pressure(path):
    if not path.exists():
        return None
    result = {}
    for line in path.read_text().splitlines():
        kind, *fields = line.split()
        result[kind] = {
            key: float(value) if key != "total" else int(value)
            for key, value in (field.split("=", 1) for field in fields)
        }
    return result


def host_sample():
    stat = Path("/proc/stat").read_text().splitlines()
    cpu = [int(value) for value in stat[0].split()[1:]]
    counters = {}
    for line in stat[1:]:
        fields = line.split()
        if fields and fields[0] in ("ctxt", "processes", "procs_running", "procs_blocked"):
            counters[fields[0]] = int(fields[1])
    memory = {}
    for line in Path("/proc/meminfo").read_text().splitlines():
        key, value = line.split(":", 1)
        if key in ("MemTotal", "MemAvailable", "SwapTotal", "SwapFree", "Dirty"):
            memory[key + "Bytes"] = int(value.split()[0]) * 1024
    vm = {}
    for line in Path("/proc/vmstat").read_text().splitlines():
        key, value = line.split()
        if key in ("pgfault", "pgmajfault", "pswpin", "pswpout"):
            vm[key] = int(value)
    load = Path("/proc/loadavg").read_text().split()
    return {
        "cpuTicks": cpu,
        "cpuTotalTicks": sum(cpu[:8]),
        "cpuIdleTicks": cpu[3] + cpu[4],
        "loadAverage": [float(value) for value in load[:3]],
        "runnableAndTotalTasks": load[3],
        "memory": memory,
        "scheduler": counters,
        "vm": vm,
        "pressure": {kind: pressure(Path("/proc/pressure") / kind)
                     for kind in ("cpu", "memory", "io")},
    }


def owned_containers(run_id, event):
    listing = subprocess.run(
        ["docker", "ps", "-q", "--no-trunc", "--filter", "label=com.bartr.wharf.managed=true"],
        capture_output=True, text=True, timeout=15, check=True)
    ids = listing.stdout.split()
    if not ids:
        return []
    template = (
        '{"id":{{json .Id}},"name":{{json .Name}},"image":{{json .Config.Image}},'
        '"pid":{{.State.Pid}},"cpuSet":{{json .HostConfig.CpusetCpus}}}'
    )
    inspection = subprocess.run(
        ["docker", "inspect", "--format", template, *ids],
        capture_output=True, text=True, timeout=15)
    if inspection.returncode:
        message = inspection.stderr.lower()
        if "no such object:" not in message and "no such container:" not in message:
            raise RuntimeError(f"Docker inspection failed: {inspection.stderr.strip()}")
        event("container-inspection-race", inspection.stderr.strip())
    identity = re.compile(r"__" + re.escape(run_id) + r"(?=__|:|$)")
    return [item for item in map(json.loads, inspection.stdout.splitlines())
            if identity.search(item["image"]) and item["pid"] > 0]


def container_sample(container):
    membership = Path(f"/proc/{container['pid']}/cgroup").read_text().splitlines()
    unified = next((line[3:] for line in membership if line.startswith("0::")), None)
    if unified is None:
        raise RuntimeError("Resource sampling requires the local Docker host and cgroup v2")
    group = (CGROUP_ROOT / unified.lstrip("/")).resolve()
    group.relative_to(CGROUP_ROOT)
    cpu = dict(line.split() for line in (group / "cpu.stat").read_text().splitlines())
    missing = []

    def metric(name, required=False):
        try:
            return int((group / name).read_text().strip())
        except FileNotFoundError:
            if required:
                raise
            missing.append(name)
            return None

    return {
        **container,
        "cgroup": str(group),
        "cpu": {key: int(value) for key, value in cpu.items()},
        "memoryBytes": metric("memory.current", required=True),
        "memoryPeakBytes": metric("memory.peak"),
        "pidsIncludingThreads": metric("pids.current", required=True),
        "pidsPeakIncludingThreads": metric("pids.peak"),
        "processCount": len((group / "cgroup.procs").read_text().splitlines()),
        "pressure": {kind: pressure(group / f"{kind}.pressure")
                     for kind in ("cpu", "memory", "io")},
        "unavailableMetrics": missing,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-directory", type=Path, required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--pid", type=int, required=True)
    parser.add_argument("--interval", type=float, default=5)
    parser.add_argument("--max-seconds", type=float, default=21600)
    args = parser.parse_args()
    if (not math.isfinite(args.interval) or not math.isfinite(args.max_seconds)
            or args.interval <= 0 or args.max_seconds <= 0 or args.pid <= 0
            or not re.fullmatch(r"[0-9a-f]{8}", args.run_id)):
        parser.error("interval and max-seconds must be finite and positive; pid must be positive; "
                     "run-id must be eight hex digits")
    run = args.run_directory.resolve()
    plan_path = run / "run-plan.json"
    plan_bytes = plan_path.read_bytes()
    plan = json.loads(plan_bytes)
    identity = process_identity(args.pid)
    if identity is None:
        parser.error("benchmark process is no longer running")
    output = run / "performance"
    output.mkdir()
    metadata = {
        "schemaVersion": "1.0", "startedAt": timestamp(), "hostname": socket.gethostname(),
        "kernel": os.uname().release, "logicalCpus": os.cpu_count(),
        "clockTicksPerSecond": os.sysconf("SC_CLK_TCK"), "intervalSeconds": args.interval,
        "benchmarkPid": args.pid, "processStartTicks": identity, "runId": args.run_id,
        "runDirectory": str(run), "runPlanSha256": hashlib.sha256(plan_bytes).hexdigest(),
        "configuration": {key: plan["configuration"].get(key)
                          for key in ("k", "n", "p", "model", "effort", "cliVersion", "containerCpus")},
        "taskCount": len(plan["tasks"]), "lateStart": True,
        "notes": [
            "Samples begin after benchmark launch; missing earlier samples cannot be recovered.",
            "Host metrics include other workloads; container metrics match this run's managed image identities.",
            "PID counts include processes and threads; CPU counters are cumulative cgroup microseconds.",
            "Short-lived containers and peaks before first observation may be missed.",
        ],
    }
    write_json(output / "metadata.json", metadata)
    stop = False

    def request_stop(_signal, _frame):
        nonlocal stop
        stop = True

    signal.signal(signal.SIGTERM, request_stop)
    signal.signal(signal.SIGINT, request_stop)
    errors = 0
    samples = 0
    container_samples = 0
    observed = set()
    start = time.monotonic()
    previous = None
    reason = "monitor-stopped"

    def event(kind, message):
        append_json(output / "events.jsonl", {"timestamp": timestamp(), "kind": kind, "message": message})
        print(f"{timestamp()} {kind}: {message}", flush=True)

    event("started", f"Sampling {run} every {args.interval:g}s; no benchmark resources will be modified")
    try:
        while not stop:
            began = time.monotonic()
            now = timestamp()
            host = host_sample()
            host["cpuBusyPercent"] = None
            if previous is not None:
                total = host["cpuTotalTicks"] - previous["cpuTotalTicks"]
                idle = host["cpuIdleTicks"] - previous["cpuIdleTicks"]
                if total > 0:
                    host["cpuBusyPercent"] = 100 * (total - idle) / total
            previous = host
            append_json(output / "host.jsonl", {"timestamp": now, **host})
            samples += 1
            count = 0
            try:
                containers = owned_containers(args.run_id, event)
                for container in containers:
                    try:
                        sample = container_sample(container)
                    except FileNotFoundError:
                        event("container-exited-during-sample", container["id"])
                        continue
                    append_json(output / "containers.jsonl", {"timestamp": now, **sample})
                    observed.add(container["id"])
                    container_samples += 1
                    count += 1
            except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
                errors += 1
                event("sample-error", str(error))
            summary = {
                "schemaVersion": "1.0", "startedAt": metadata["startedAt"],
                "updatedAt": timestamp(), "status": "sampling", "hostSamples": samples,
                "containerSamples": container_samples, "uniqueContainers": len(observed),
                "sampleErrors": errors, "lateStart": True,
            }
            write_json(output / "summary.json", summary)
            print(f"{now} samples={samples} containers={count} total-container-samples={container_samples} "
                  f"host-cpu={host['cpuBusyPercent']}% errors={errors}", flush=True)
            if process_identity(args.pid) != identity:
                reason = "benchmark-process-exited"
                break
            if time.monotonic() - start >= args.max_seconds:
                reason = "monitor-time-limit"
                break
            time.sleep(max(0, args.interval - (time.monotonic() - began)))
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        reason = "monitor-error"
        event("monitor-error", str(error))
        raise
    finally:
        final = {
            "schemaVersion": "1.0", "startedAt": metadata["startedAt"], "endedAt": timestamp(),
            "status": reason, "hostSamples": samples, "containerSamples": container_samples,
            "uniqueContainers": len(observed), "sampleErrors": errors, "lateStart": True,
        }
        write_json(output / "summary.json", final)
        event("stopped", reason)


if __name__ == "__main__":
    main()
