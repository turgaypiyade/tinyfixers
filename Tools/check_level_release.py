"""Real publisher functions with temporary Unity-YAML fixtures; cloud calls are doubled."""
import contextlib
import io
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest
import urllib.error
from unittest.mock import patch

sys.dont_write_bytecode = True
import level_release as release


def encoded(values):
    return struct.pack(f"<{len(values)}i", *values).hex()


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.write("ProjectSettings/ProjectSettings.asset", "  bundleVersion: 1.0\n")
        self.write("Assets/_Project/Scripts/Runtime.cs", "// shipped runtime\n")
        self.write("Assets/Library.asset", "library art and behavior\n")
        self.write("Assets/Library.asset.meta", "guid: " + "b" * 32 + "\n")
        self.write("ContentDelivery/wrangler.jsonc", (release.ROOT / "ContentDelivery/wrangler.jsonc").read_text())
        self.write("ContentDelivery/public/_headers", "/stable/*\n  Cache-Control: public, max-age=300\n")
        self.numbers = []
        self.add_level(1)

    def tearDown(self):
        self.temp.cleanup()

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def asset(self, number):
        return f"Assets/_Project/Settings/ProductionLevels/Fixture{number}.asset"

    def add_level(self, number, registered=True, library="b" * 32):
        self.write(self.asset(number), f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_Name: ArbitraryAssetFilename
  m_Script: {{fileID: 11500000, guid: {'a' * 32}, type: 3}}
  width: 2
  height: 2
  moves: 25
  cells: {encoded([1, 1, 1, 1])}
  obstacles: {encoded([0, 0, 0, 0])}
  obstacleOrigins: {encoded([-1, -1, -1, -1])}
  randomPool: {encoded([0, 1, 2, 3])}
  goals:
  - targetType: 0
    tileType: 0
    amount: 10
    iconOverride: {{fileID: 0}}
  obstacleLibrary: {{fileID: 11400000, guid: {library}, type: 2}}
  waterChannels:
  - pathCellIndices: {encoded([0, 1])}
  stackedObstacles:
  - originCellIndex: 2
    obstacleId: 27
    stackOrder: 1
  customFutureField:
    keepMe: 123
""")
        self.write(self.asset(number) + ".meta", f"guid: {number:032x}\n")
        if registered:
            self.numbers.append(number)
        self.catalog()

    def catalog(self):
        text = "MonoBehaviour:\n  entries:\n"
        for number in self.numbers:
            text += f"  - chapter: 1\n    level: {number}\n    levelKey: LevelCL_{number:03d}\n    levelData: {{fileID: 11400000, guid: {number:032x}, type: 2}}\n"
        self.write(release.CATALOG, text)
        release.save(self.root / release.NAMES, {"entries": [{"level": n, "name": f"Türkçe İsim {n}"} for n in self.numbers]})

    def baseline(self):
        return release.mark_release(self.root, "1.0-b1")

    @contextlib.contextmanager
    def cloud(self, fail=False):
        original_run = subprocess.run
        def run(command, **kwargs):
            if command[0] != "npx":
                return original_run(command, **kwargs)
            return subprocess.CompletedProcess(command, int(fail), "", "secret-token" if fail else "")
        def response(request, **kwargs):
            pending = release.read(self.root / release.STATE / "pending-publication.json")
            body = release.canonical(pending["plan"]["manifest"])
            return contextlib.closing(io.BytesIO(body))
        with patch.object(release, "credentials", return_value={"CLOUDFLARE_API_TOKEN": "secret-token", "CLOUDFLARE_ACCOUNT_ID": "test-account"}), \
             patch.object(release.subprocess, "run", side_effect=run), \
             patch.object(release.urllib.request, "urlopen", side_effect=response):
            yield

    def publish(self):
        plan = release.prepare(self.root)
        with self.cloud():
            result = release.publish(self.root, True)
        self.assertTrue(result["verified"])
        return plan

    def test_inventory_preserves_nested_data_and_hex(self):
        item = release.inventory(self.root)["1"]["document"]
        self.assertEqual(item["name"], "Türkçe İsim 1")
        self.assertEqual(item["data"]["cells"], [1] * 4)
        self.assertEqual(item["data"]["obstacleOrigins"], [-1] * 4)
        self.assertEqual(item["data"]["waterChannels"][0]["pathCellIndices"], [0, 1])
        self.assertEqual(item["data"]["customFutureField"], {"keepMe": 123})
        self.assertEqual(item["data"]["obstacleLibrary"], {"$asset": "b" * 32 + ":11400000"})

    def test_baseline_is_explicit_and_immutable(self):
        with self.assertRaises(release.ReleaseError):
            release.prepare(self.root)
        self.assertFalse((self.root / release.STATE / "active.json").exists())
        self.assertFalse(self.baseline()["buildExecuted"])
        with self.assertRaises(release.ReleaseError):
            self.baseline()

    def test_new_and_modified_level_detection_uses_catalog(self):
        self.baseline()
        self.add_level(2)
        first = self.root / self.asset(1)
        first.write_text(first.read_text().replace("moves: 25", "moves: 30"))
        plan = release.prepare(self.root)
        self.assertEqual(plan["newLevels"], [2])
        self.assertEqual(plan["changedLevels"], [1])
        self.assertEqual(plan["remotePayloadCount"], 2)
        self.assertEqual(plan["revision"], release.prepare(self.root)["revision"])

    def test_runtime_code_and_art_changes_are_blocked(self):
        self.baseline()
        for path in ["Assets/_Project/Scripts/Runtime.cs", "Assets/Library.asset"]:
            old = (self.root / path).read_text()
            self.write(path, old + "changed")
            with self.assertRaises(release.ReleaseError):
                release.prepare(self.root)
            self.write(path, old)

    def test_editor_only_tool_changes_do_not_require_game_release(self):
        self.baseline()
        self.write("Assets/_Project/Scripts/Editor/Tool.cs", "// editor tool")
        self.assertFalse(release.prepare(self.root)["hasChanges"])

    def test_new_asset_reference_is_blocked_even_if_asset_preexists(self):
        self.write("Assets/Unused.asset", "not shipped by level references")
        self.baseline()
        self.add_level(2, library="c" * 32)
        with self.assertRaises(release.ReleaseError):
            release.prepare(self.root)

    def test_new_unregistered_asset_is_not_silently_skipped(self):
        self.baseline()
        self.add_level(2, registered=False)
        with self.assertRaises(release.ReleaseError):
            release.prepare(self.root)

    def test_removal_and_number_gaps_are_blocked(self):
        self.add_level(2)
        self.baseline()
        self.numbers.remove(2)
        self.catalog()
        with self.assertRaises(release.ReleaseError):
            release.prepare(self.root)
        self.add_level(3)
        with self.assertRaises(release.ReleaseError):
            release.inventory(self.root)

    def test_prepare_and_dry_run_never_authenticate_or_deploy(self):
        self.baseline()
        self.add_level(2)
        release.prepare(self.root)
        with patch.object(release, "credentials", side_effect=AssertionError("must not read token")):
            self.assertFalse(release.publish(self.root)["deployExecuted"])

    def test_changes_after_prepare_and_manifest_tampering_are_blocked(self):
        self.baseline()
        self.add_level(2)
        plan = release.prepare(self.root)
        plan_path = self.root / release.OUTPUT / plan["release"] / plan["revision"] / "plan.json"
        plan["manifest"]["appVersion"] = "tampered"
        release.save(plan_path, plan)
        with self.assertRaises(release.ReleaseError):
            release.publish(self.root)
        release.prepare(self.root)
        asset = self.root / self.asset(2)
        asset.write_text(asset.read_text().replace("moves: 25", "moves: 26"))
        with self.assertRaises(release.ReleaseError):
            release.publish(self.root)

    def test_success_is_recorded_only_after_remote_manifest_verification(self):
        self.baseline()
        self.add_level(2)
        plan = self.publish()
        published = release.read(self.root / release.STATE / "published/1.0-b1.json")
        self.assertEqual(published["revision"], plan["revision"])
        self.assertFalse((self.root / release.STATE / "pending-publication.json").exists())
        self.assertFalse(release.prepare(self.root)["hasChanges"])

    def test_failed_deploy_redacts_token_and_keeps_previous_public_tree(self):
        self.baseline()
        self.add_level(2)
        release.prepare(self.root)
        with self.cloud(fail=True):
            with self.assertRaises(release.ReleaseError) as error:
                release.publish(self.root, True)
        self.assertNotIn("secret-token", str(error.exception))
        self.assertFalse((self.root / "ContentDelivery/public/stable/1.0-b1/manifest.json").exists())
        self.assertFalse((self.root / release.STATE / "published/1.0-b1.json").exists())
        with self.assertRaises(release.ReleaseError):
            release.publish(self.root)

    def test_corrupted_payload_cannot_be_uploaded(self):
        self.baseline()
        self.add_level(2)
        plan = release.prepare(self.root)
        entry = next(v for v in plan["manifest"]["levels"] if not v["bundled"])
        payload = self.root / release.OUTPUT / plan["release"] / plan["revision"] / "public" / entry["path"]
        payload.write_text("corrupt")
        with self.cloud():
            with self.assertRaises(release.ReleaseError):
                release.deploy(self.root, plan)

    def test_verified_history_rollback_keeps_old_and_new_blobs(self):
        self.baseline()
        self.add_level(2)
        first = self.publish()
        self.add_level(3)
        self.publish()
        with self.cloud():
            result = release.rollback(self.root, first["revision"], apply=True)
        self.assertTrue(result["rollback"])
        self.assertEqual(release.read(self.root / "ContentDelivery/public/stable/1.0-b1/manifest.json")["revision"], first["revision"])
        self.assertEqual(len(list((self.root / "ContentDelivery/public/levels").rglob("*.json"))), 2)

    def test_unknown_rollback_and_unsafe_release_names_are_rejected(self):
        self.baseline()
        with self.assertRaises(release.ReleaseError):
            release.rollback(self.root, "f" * 64)
        for name in ["../secret", "a/b", "a;rm", "", "x..y"]:
            with self.assertRaises(release.ReleaseError):
                release.identifier(name)


if __name__ == "__main__":
    unittest.main()
