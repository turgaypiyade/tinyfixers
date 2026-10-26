"""Job orchestration tests with fake workers; never starts Unity or builds the project."""
import contextlib
import csv
import importlib.util
import io
import json
import os
from pathlib import Path
import plistlib
import signal
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

spec = importlib.util.spec_from_file_location("run_sim", Path(__file__).with_name("run_sim.py"))
sim = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sim)


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.out = self.root / "output with spaces"
        self.commands = []
        self.workers = []
        self.outcomes = ["Won", "Lost"]
        self.report_completed = True
        self.write_report = True
        self.exitcode = 0

    def worker(self, cmd, **kwargs):
        self.commands.append(cmd)
        cfg_path = Path(cmd[cmd.index("-tfsim") + 1])
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
        games = [
            {"level": f"L{level:03}", "levelPath": f"L{level:03}", "sample": sample,
             "outcome": self.outcomes[sample % len(self.outcomes)], "movesLeft": 2,
             "remainingGoalUnits": 3, "goalsStart": "Stone=10", "goalsLeft": "Stone=3"}
            for level in cfg["levelNumbers"] for sample in range(cfg["gamesPerLevel"])
        ]
        if self.write_report:
            Path(cfg["outputPath"]).write_text(json.dumps({"config": cfg, "completed": self.report_completed,
                                                         "games": games}), encoding="utf-8-sig")
        proc = Mock(returncode=self.exitcode)
        proc.poll.return_value = self.exitcode
        self.workers.append(proc)
        return proc

    def run_jobs(self, *args, factory=None):
        with patch.object(sim, "find_binary", return_value="fake sim executable"), \
             patch.object(sim.subprocess, "Popen", side_effect=factory or self.worker), \
             patch.object(sim.signal, "signal"), contextlib.redirect_stdout(io.StringIO()), \
             contextlib.redirect_stderr(io.StringIO()):
            return sim.main(["--levels", "1-4", "--games", "2", "--jobs", "2", "--out", str(self.out), *args])

    def test_ranges_and_duplicates(self):
        self.assertEqual(sim.parse_levels(" 1-3,2,5,3 "), [1, 2, 3, 5])
        for value in ["", ",", "4-1", "0", "-1", "2-x", "x", "2147483648"]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                sim.parse_levels(value)

    def test_successful_parallel_jobs(self):
        self.assertEqual(self.run_jobs(), 0)
        configs = [json.loads((self.out / f"job{i:02}" / "config.json").read_text()) for i in range(2)]
        self.assertEqual([c["levelNumbers"] for c in configs], [[1, 3], [2, 4]])
        self.assertTrue(all(Path(c["outputPath"]).is_absolute() for c in configs))
        self.assertTrue(all("-batchmode" in cmd and "-nographics" in cmd for cmd in self.commands))
        with (self.out / "summary.csv").open(encoding="utf-8-sig", newline="") as f:
            rows = list(csv.DictReader(f))
        self.assertEqual(len(rows), 4)
        self.assertTrue(all(row["Win%"] == "50.0" and row["Games"] == "2" for row in rows))

    def test_invalid_numeric_arguments_do_not_launch(self):
        for args in [("--games", "0"), ("--jobs", "0"), ("--speed", "nan"), ("--speed", "0"),
                     ("--timeout", "inf"), ("--timeout", "-1"), ("--seed", "2147483648")]:
            with self.subTest(args=args), self.assertRaises(SystemExit) as raised:
                self.run_jobs(*args)
            self.assertEqual(raised.exception.code, 2)
        self.assertEqual(self.commands, [])

    def test_mac_windows_use_identical_simulation_config(self):
        configs = []
        for platform in ("darwin", "win32"):
            self.out = self.root / platform
            with patch.object(sim.sys, "platform", platform):
                self.assertEqual(self.run_jobs("--speed", "1", "--seed", "42"), 0)
            workers = []
            for i in range(2):
                cfg = json.loads((self.out / f"job{i:02}" / "config.json").read_text())
                cfg.pop("outputPath")
                workers.append(cfg)
            configs.append(workers)
        self.assertEqual(configs[0], configs[1])

    def test_nonempty_output_is_preserved(self):
        self.out.mkdir()
        sentinel = self.out / "STOP"
        sentinel.write_text("old run")
        with self.assertRaises(SystemExit):
            self.run_jobs()
        self.assertEqual(sentinel.read_text(), "old run")
        self.assertEqual(self.commands, [])

    def test_seed_wraps_to_int32(self):
        self.assertEqual(self.run_jobs("--seed", "2147483647"), 0)
        cfg = json.loads((self.out / "job01/config.json").read_text())
        self.assertTrue(-(2**31) <= cfg["seed"] < 2**31)

    def test_worker_failure_is_nonzero_even_with_valid_report(self):
        self.exitcode = 7
        self.assertEqual(self.run_jobs(), 1)

    def test_missing_report_is_failure(self):
        self.write_report = False
        self.assertEqual(self.run_jobs(), 1)

    def test_partial_report_is_failure(self):
        self.report_completed = False
        self.assertEqual(self.run_jobs(), 1)

    def test_unresolved_games_are_not_silently_successful(self):
        self.outcomes = ["Won", "Timeout"]
        self.assertEqual(self.run_jobs(), 1)
        with (self.out / "summary.csv").open(encoding="utf-8-sig", newline="") as f:
            rows = list(csv.DictReader(f))
        self.assertTrue(all(r["Games"] == "2" and r["Errors/Timeouts"] == "1" and r["Win%"] == "100.0" for r in rows))

    def test_launch_failure_stops_already_started_workers(self):
        def launch(cmd, **kwargs):
            if self.workers:
                raise OSError("second worker failed")
            proc = self.worker(cmd, **kwargs)
            proc.poll.return_value = None
            return proc
        with patch.object(sim, "stop_workers") as stop:
            self.assertEqual(self.run_jobs(factory=launch), 1)
        self.assertEqual(len(stop.call_args.args[0]), 1)

    def test_stop_targets_running_workers_only(self):
        self.out.mkdir()
        active, done = Mock(), Mock()
        active.poll.return_value, done.poll.return_value = None, 0
        sim.request_stop([(str(self.out), active), (str(self.root), done)])
        self.assertTrue((self.out / "STOP").exists())
        self.assertFalse((self.root / "STOP").exists())

    def test_unresponsive_worker_is_killed_after_grace(self):
        self.out.mkdir()
        proc = Mock()
        proc.poll.return_value = None
        proc.wait.side_effect = [subprocess.TimeoutExpired("sim", 5), 0]
        sim.stop_workers([(str(self.out), proc)], grace=0)
        self.assertTrue((self.out / "STOP").exists())
        proc.terminate.assert_called_once()
        proc.kill.assert_called_once()

    def test_stop_file_failure_still_terminates_worker(self):
        proc = Mock()
        proc.poll.return_value = None
        with patch("builtins.open", side_effect=PermissionError("read only")), contextlib.redirect_stderr(io.StringIO()):
            sim.stop_workers([(str(self.out), proc)], grace=0)
        proc.terminate.assert_called_once()

    @unittest.skipIf(os.name == "nt", "POSIX fake executable; Windows paths covered separately")
    def test_real_subprocess_orchestration_with_fake_binary(self):
        binary = self.root / "fake worker"
        binary.write_text("#!" + sys.executable + "\n" + '''
import json, pathlib, sys
cfg = json.loads(pathlib.Path(sys.argv[sys.argv.index('-tfsim') + 1]).read_text())
games = [dict(level=f'L{n:03}', levelPath=f'L{n:03}', sample=s, outcome='Won', movesLeft=1)
         for n in cfg['levelNumbers'] for s in range(cfg['gamesPerLevel'])]
pathlib.Path(cfg['outputPath']).write_text(json.dumps(dict(config=cfg, completed=True, games=games)))
''', encoding="utf-8")
        binary.chmod(0o755)
        with patch.object(sim, "find_binary", return_value=str(binary)), \
             contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            status = sim.main(["--levels", "1-2", "--games", "2", "--jobs", "2", "--out", str(self.out)])
        self.assertEqual(status, 0)
        self.assertTrue((self.out / "job00/report.json").exists())
        self.assertTrue((self.out / "job01/report.json").exists())

    def test_mac_executable_comes_from_plist(self):
        app = self.root / "Fake.app"
        mac = app / "Contents/MacOS"
        mac.mkdir(parents=True)
        (mac / "decoy").touch()
        (mac / "RealExecutable").touch()
        (app / "Contents/Info.plist").write_bytes(plistlib.dumps({"CFBundleExecutable": "RealExecutable"}))
        with patch.object(sim.sys, "platform", "darwin"), patch.object(sim, "APP", str(app)):
            self.assertEqual(sim.find_binary(), str(mac / "RealExecutable"))

    def test_windows_executable_path(self):
        exe = self.root / "Win/TinyFixersSim.exe"
        exe.parent.mkdir()
        exe.touch()
        with patch.object(sim.sys, "platform", "win32"), patch.object(sim, "WIN_EXE", str(exe)):
            self.assertEqual(sim.find_binary(), str(exe))


if __name__ == "__main__":
    unittest.main()
