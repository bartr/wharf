import contextlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock


SPEC = importlib.util.spec_from_file_location(
    "monitor_wharf_run", Path(__file__).resolve().parents[1] / "scripts/monitor-wharf-run.py")
MONITOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MONITOR)


class MonitorWharfRunTests(unittest.TestCase):
    def test_pressure_preserves_cumulative_totals_and_averages(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "pressure"
            path.write_text("some avg10=1.25 avg60=2.50 avg300=3.00 total=1234\n")
            self.assertEqual(MONITOR.pressure(path), {
                "some": {"avg10": 1.25, "avg60": 2.5, "avg300": 3.0, "total": 1234}})
            self.assertIsNone(MONITOR.pressure(Path(directory) / "unsupported"))

    def test_host_cpu_does_not_double_count_guest_time(self):
        values = {
            "/proc/stat": "cpu 10 2 3 40 5 6 7 8 9 1\nctxt 123\nprocs_running 4\n",
            "/proc/meminfo": "MemTotal: 100 kB\nMemAvailable: 50 kB\n",
            "/proc/vmstat": "pgfault 456\npswpin 0\n",
            "/proc/loadavg": "1.25 2.50 3.75 4/123 999\n",
        }
        with mock.patch.object(Path, "read_text", lambda path: values[str(path)]), \
                mock.patch.object(MONITOR, "pressure", return_value=None):
            sample = MONITOR.host_sample()
        self.assertEqual(sample["cpuTotalTicks"], 81)
        self.assertEqual(sample["cpuIdleTicks"], 45)
        self.assertEqual(sample["memory"]["MemAvailableBytes"], 51200)
        self.assertEqual(sample["scheduler"]["ctxt"], 123)

    def test_inspection_is_scoped_to_ownership_label_and_exact_run_identity(self):
        containers = [
            {"id": "a", "image": "agent-task__1234abcd:latest", "pid": 42},
            {"id": "b", "image": "verifier-task__1234abcd__attempt-2:latest", "pid": 43},
            {"id": "c", "image": "agent-task__1234abcde:latest", "pid": 44},
            {"id": "d", "image": "agent-task__cafebabe:latest", "pid": 45},
            {"id": "e", "image": "agent-task__1234abcd:latest", "pid": 0},
        ]
        results = [
            subprocess.CompletedProcess([], 0, stdout="a\nb\nc\nd\ne\n", stderr=""),
            subprocess.CompletedProcess([], 0,
                                        stdout="\n".join(map(json.dumps, containers)), stderr=""),
        ]
        with mock.patch.object(MONITOR.subprocess, "run", side_effect=results) as run:
            selected = MONITOR.owned_containers("1234abcd", mock.Mock())
        self.assertEqual([item["id"] for item in selected], ["a", "b"])
        self.assertIn("label=com.bartr.wharf.managed=true", run.call_args_list[0].args[0])

    def test_container_removal_races_are_recorded(self):
        for message in ("Error: No such object: gone", "error: no such object: gone"):
            with self.subTest(message=message):
                results = [
                    subprocess.CompletedProcess([], 0, stdout="gone\n", stderr=""),
                    subprocess.CompletedProcess([], 1, stdout="", stderr=message),
                ]
                event = mock.Mock()
                with mock.patch.object(MONITOR.subprocess, "run", side_effect=results):
                    self.assertEqual(MONITOR.owned_containers("1234abcd", event), [])
                event.assert_called_once_with("container-inspection-race", message)

    def test_other_docker_errors_do_not_become_empty_successful_samples(self):
        results = [
            subprocess.CompletedProcess([], 0, stdout="id\n", stderr=""),
            subprocess.CompletedProcess([], 1, stdout="", stderr="permission denied"),
        ]
        with mock.patch.object(MONITOR.subprocess, "run", side_effect=results):
            with self.assertRaisesRegex(RuntimeError, "permission denied"):
                MONITOR.owned_containers("1234abcd", mock.Mock())

    def test_cgroup_counters_keep_units_and_missing_peaks_explicit(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            group = root / "container"
            group.mkdir()
            for name, value in {
                "cpu.stat": "usage_usec 123\nuser_usec 100\nsystem_usec 23\n",
                "memory.current": "4096\n", "memory.peak": "8192\n",
                "pids.current": "12\n", "cgroup.procs": "42\n43\n",
            }.items():
                (group / name).write_text(value)
            original = Path.read_text

            def read(path, *args, **kwargs):
                if str(path) == "/proc/42/cgroup":
                    return "0::/container\n"
                return original(path, *args, **kwargs)

            with mock.patch.object(MONITOR, "CGROUP_ROOT", root), \
                    mock.patch.object(Path, "read_text", read):
                sample = MONITOR.container_sample({"pid": 42, "id": "a"})
            self.assertEqual(sample["cpu"]["usage_usec"], 123)
            self.assertEqual(sample["memoryPeakBytes"], 8192)
            self.assertEqual(sample["pidsIncludingThreads"], 12)
            self.assertEqual(sample["processCount"], 2)
            self.assertIsNone(sample["pidsPeakIncludingThreads"])
            self.assertEqual(sample["unavailableMetrics"], ["pids.peak"])

    def test_sampler_stops_when_benchmark_process_exits_and_records_late_start(self):
        self.run_monitor(fail_host=False)

    def test_host_failure_is_retained_and_raised(self):
        self.run_monitor(fail_host=True)

    def test_container_sampling_failure_is_counted_once(self):
        self.run_monitor(fail_host=False, fail_containers=True)

    def run_monitor(self, fail_host, fail_containers=False):
        with tempfile.TemporaryDirectory() as directory:
            run = Path(directory)
            (run / "run-plan.json").write_text(json.dumps({
                "configuration": {"k": 4, "n": 32, "p": 12, "containerCpus": 8},
                "tasks": [{"taskId": "example"}]}))
            arguments = [
                "monitor", "--run-directory", directory, "--run-id", "1234abcd", "--pid", "42"]
            host = {"cpuTotalTicks": 100, "cpuIdleTicks": 50}
            with mock.patch("sys.argv", arguments), \
                    mock.patch.object(MONITOR, "process_identity", side_effect=["start", None]), \
                    mock.patch.object(MONITOR, "owned_containers",
                                      side_effect=RuntimeError("cannot inspect containers")
                                      if fail_containers else None, return_value=[]), \
                    mock.patch.object(MONITOR, "host_sample",
                                      side_effect=OSError("cannot read host") if fail_host else None,
                                      return_value=host), \
                    mock.patch.object(MONITOR.signal, "signal"), \
                    contextlib.redirect_stdout(io.StringIO()):
                if fail_host:
                    with self.assertRaisesRegex(OSError, "cannot read host"):
                        MONITOR.main()
                else:
                    MONITOR.main()
            summary = json.loads((run / "performance/summary.json").read_text())
            self.assertTrue(summary["lateStart"])
            self.assertEqual(summary["sampleErrors"], 1 if fail_containers else 0)
            self.assertEqual(summary["status"],
                             "monitor-error" if fail_host else "benchmark-process-exited")
            self.assertEqual(summary["hostSamples"], 0 if fail_host else 1)


if __name__ == "__main__":
    unittest.main()
