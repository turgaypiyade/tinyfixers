#!/usr/bin/env python3
"""Record app releases and publish compatible level-data changes. Never builds Unity.

No baseline or cloud write is implicit. `mark-release` is the operator's declaration
that this exact workspace is the shipped app. `publish --apply` is an explicit deploy.
Python 3 + Ruby/Psych (already used by this project's asset checks) are required.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import struct
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
CATALOG = "Assets/_Project/Settings/LevelCatalogPro.asset"
NAMES = "Assets/_Project/Resources/LevelNames.json"
STATE = "Docs/Releases"
OUTPUT = "Exports/LevelReleases"
PROTOCOL = 1
HOST = "https://levels.wondigames.com"
IDENTIFIER = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,79}\Z")
INTEGER_ARRAYS = {"cells", "obstacles", "obstacleOrigins", "wallPieceIds", "hamsterSatietyByCell",
                  "pinnedTileTypes", "pinnedSpecialTypes", "randomPool", "bossThrownObstacles",
                  "bossToolThreatTypes", "pathCellIndices"}
UNITY_METADATA = {"m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
                  "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"}


class ReleaseError(Exception):
    pass


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def file_digest(path):
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest()


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    data = canonical(value)
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as stream:
        temporary = Path(stream.name)
        stream.write(data)
    os.replace(temporary, path)


def identifier(value):
    if not IDENTIFIER.fullmatch(value) or ".." in value:
        raise ReleaseError("Sürüm kimliği yalnız harf, sayı, nokta, tire ve alt çizgi içerebilir.")
    return value


def unity_documents(paths):
    """Use the existing local Ruby YAML parser, keeping Unity hex arrays as strings."""
    if not paths:
        return []
    ruby = shutil.which("ruby") or ("/usr/bin/ruby" if Path("/usr/bin/ruby").exists() else None)
    if ruby is None:
        raise ReleaseError("Unity YAML okuması için Ruby gerekiyor; hiçbir dosya yayımlanmadı.")
    texts = []
    for path in paths:
        text = path.read_text(encoding="utf-8")
        text = "\n".join(line for line in text.splitlines() if not line.startswith(("%", "--- !u!")))
        text = re.sub(r"(^\s*(?:-\s+)?\w+: )([0-9a-fA-F]{8,})(\s*)$", r'\1"\2"\3', text, flags=re.M)
        text = re.sub(r"guid: ([0-9a-fA-F]{32})(?=[, }])", r'guid: "\1"', text)
        texts.append(text)
    code = 'JSON.parse(STDIN.read).each { |s| puts JSON.generate(YAML.safe_load(s, aliases: false)) }'
    result = subprocess.run([ruby, "-rjson", "-ryaml", "-e", code], input=json.dumps(texts),
                            capture_output=True, text=True)
    if result.returncode:
        raise ReleaseError("Unity YAML çözülemedi; dosyaları değiştirmeden işlemi durdurdum.\n" + result.stderr[:1200])
    documents = [json.loads(line) for line in result.stdout.splitlines()]
    if len(documents) != len(paths):
        raise ReleaseError("Eksik YAML çözümleme sonucu.")
    return [document["MonoBehaviour"] for document in documents]


def normalize(value, key=""):
    if key in INTEGER_ARRAYS:
        if value is None:
            return []
        if isinstance(value, str):
            if not re.fullmatch(r"(?:[0-9a-fA-F]{8})*", value):
                raise ReleaseError(f"Bozuk Unity sayısal dizisi: {key}")
            return list(struct.unpack(f"<{len(value) // 8}i", bytes.fromhex(value)))
        if not isinstance(value, list):
            raise ReleaseError(f"Geçersiz sayısal dizi türü: {key}")
    if isinstance(value, dict):
        if "fileID" in value:
            if value["fileID"] == 0:
                return None
            guid = value.get("guid")
            if not isinstance(guid, str) or not re.fullmatch(r"[0-9a-fA-F]{32}", guid):
                raise ReleaseError("Level dosyasında taşınamayan yerel Unity nesne referansı var.")
            return {"$asset": f"{guid}:{value['fileID']}"}
        return {k: normalize(v, k) for k, v in value.items()}
    if isinstance(value, list):
        return [normalize(v) for v in value]
    return value


def reference_ids(value):
    if isinstance(value, dict):
        if "$asset" in value:
            return {value["$asset"]}
        return set().union(*(reference_ids(v) for v in value.values())) if value else set()
    if isinstance(value, list):
        return set().union(*(reference_ids(v) for v in value)) if value else set()
    return set()


def inventory(root):
    catalog = unity_documents([root / CATALOG])[0]
    names = {int(row["level"]): row["name"] for row in read(root / NAMES).get("entries", [])}
    assets = {}
    for meta in (root / "Assets").rglob("*.asset.meta"):
        match = re.search(r"^guid: (\w+)$", meta.read_text(encoding="utf-8"), re.M)
        if match:
            assets[match[1]] = Path(str(meta)[:-5])
    entries = catalog.get("entries") or []
    paths = []
    for entry in entries:
        guid = entry.get("levelData", {}).get("guid")
        if guid not in assets:
            raise ReleaseError(f"Katalogdaki level asset'i bulunamadı: {entry.get('level')}")
        paths.append(assets[guid])
    levels = {}
    for entry, path, raw in zip(entries, paths, unity_documents(paths)):
        number = int(entry["level"])
        if number < 1 or str(number) in levels:
            raise ReleaseError(f"Geçersiz veya çakışan global level numarası: {number}")
        data = normalize({k: v for k, v in raw.items() if k not in UNITY_METADATA})
        width, height = data.get("width", 0), data.get("height", 0)
        if not 1 <= width <= 10 or not 1 <= height <= 11 or not 1 <= data.get("moves", 0) <= 200:
            raise ReleaseError(f"Level {number}: boyut/hamle sınırı geçersiz.")
        for field in ("cells", "obstacles", "obstacleOrigins"):
            if not isinstance(data.get(field), list) or len(data[field]) != width * height:
                raise ReleaseError(f"Level {number}: {field} uzunluğu width × height ile eşleşmiyor.")
        for field in ("pinnedTileTypes", "pinnedSpecialTypes", "wallPieceIds", "hamsterSatietyByCell"):
            if data.get(field) and len(data[field]) != width * height:
                raise ReleaseError(f"Level {number}: {field} uzunluğu geçersiz.")
        for index in data["obstacleOrigins"]:
            if not isinstance(index, int) or index < -1 or index >= width * height:
                raise ReleaseError(f"Level {number}: obstacle origin board dışında.")
        for group in ("magnets", "waterChannels"):
            for item in data.get(group) or []:
                route = item.get("pathCellIndices") or []
                if len(route) < 2 or any(not 0 <= cell < width * height for cell in route):
                    raise ReleaseError(f"Level {number}: {group} yolu geçersiz.")
        if not data.get("goals") or any(goal.get("amount", 0) < 1 for goal in data["goals"]):
            raise ReleaseError(f"Level {number}: hedef tanımı eksik/geçersiz.")
        document = {"schemaVersion": PROTOCOL, "level": number, "chapter": entry["chapter"],
                    "levelKey": entry["levelKey"], "name": names.get(number, entry["levelKey"]), "data": data}
        content = canonical(document)
        levels[str(number)] = {"sha256": digest(content), "bytes": len(content),
                               "source": path.relative_to(root).as_posix(), "document": document}
    if not levels:
        raise ReleaseError("Katalog boş.")
    if sorted(map(int, levels)) != list(range(1, max(map(int, levels)) + 1)):
        raise ReleaseError("Global level numaralarında boşluk var; oyuncunun ilerleyişini yarıda bırakan katalog yayımlanamaz.")
    return levels


def runtime_files(root, levels):
    exclude = {CATALOG, CATALOG + ".meta", NAMES, NAMES + ".meta"}
    for item in levels.values():
        exclude.update((item["source"], item["source"] + ".meta"))
    result = {}
    for directory in ("Assets", "Packages", "ProjectSettings"):
        for path in (root / directory).rglob("*"):
            if not path.is_file():
                continue
            relative = path.relative_to(root).as_posix()
            if relative in exclude or "Editor" in path.relative_to(root).parts or "/ProductionLevels/" in relative:
                continue
            if path.name == ".DS_Store" or "__pycache__" in path.parts:
                continue
            result[relative] = file_digest(path)
    return result


def unregistered_levels(root, levels):
    registered = {v["source"] for v in levels.values()}
    return {p.relative_to(root).as_posix(): file_digest(p)
            for p in (root / "Assets/_Project/Settings/ProductionLevels").glob("*.asset")
            if p.relative_to(root).as_posix() not in registered}


def project_version(root):
    text = (root / "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8")
    version = re.search(r"^  bundleVersion: (.+)$", text, re.M)
    return version[1].strip() if version else "unknown"


def active_baseline(root, release=None):
    if release is None:
        active = root / STATE / "active.json"
        if not active.exists():
            raise ReleaseError('Henüz uygulama release kaydı yok. Gerçek release için önce mark-release kullan.')
        release = read(active)["release"]
    return read(root / STATE / "baselines" / (identifier(release) + ".json"))


def mark_release(root, release):
    identifier(release)
    path = root / STATE / "baselines" / (release + ".json")
    if path.exists():
        raise ReleaseError("Bu release zaten kayıtlı; yayımlanmış uygulama kaydını ezmiyorum.")
    levels = inventory(root)
    baseline = {"format": PROTOCOL, "release": release, "appVersion": project_version(root),
                "levels": {n: {k: v for k, v in item.items() if k != "document"} for n, item in levels.items()},
                "allowedReferences": sorted(set().union(*(reference_ids(v["document"]) for v in levels.values()))),
                "unregisteredLevels": unregistered_levels(root, levels),
                "runtimeFiles": runtime_files(root, levels)}
    save(path, baseline)
    save(root / STATE / "active.json", {"release": release})
    return {"release": release, "levelCount": len(levels), "recorded": True, "buildExecuted": False}


def compatible(root, baseline, levels):
    missing = [p for p, sha in unregistered_levels(root, levels).items()
               if baseline.get("unregisteredLevels", {}).get(p) != sha]
    if missing:
        raise ReleaseError("Yeni/değişen level asset'i kataloğa bağlı değil; sessizce atlamıyorum:\n" + "\n".join(missing[:20]))
    current = runtime_files(root, levels)
    old = baseline["runtimeFiles"]
    changed = sorted(p for p in set(current) | set(old) if current.get(p) != old.get(p))
    if changed:
        raise ReleaseError("Yalnız level yayını yapılamaz: uygulama kodu/gömülü içeriği değişmiş. Yeni uygulama release'i gerekir.\n" + "\n".join(changed[:20]))
    references = set(baseline["allowedReferences"])
    for number, item in levels.items():
        if not reference_ids(item["document"]) <= references:
            raise ReleaseError(f"Level {number}, release'te level referansı olarak kayıtlı olmayan bir asset kullanıyor; yeni uygulama release'i gerekir.")


def prepare(root, release=None):
    baseline = active_baseline(root, release)
    levels = inventory(root)
    compatible(root, baseline, levels)
    release = baseline["release"]
    published_path = root / STATE / "published" / (release + ".json")
    previous = read(published_path) if published_path.exists() else {"levels": baseline["levels"]}
    removed = (set(baseline["levels"]) | set(previous["levels"])) - set(levels)
    if removed:
        raise ReleaseError("Yayımlanan level numaraları silinemez; geri dönüş komutunu kullan: " + ", ".join(sorted(removed, key=int)))
    new = sorted((int(n) for n in levels if n not in previous["levels"]))
    changed = sorted(int(n) for n, v in levels.items() if n in previous["levels"] and v["sha256"] != previous["levels"][n]["sha256"])
    entries = []
    payloads = {}
    for number, item in sorted(levels.items(), key=lambda pair: int(pair[0])):
        bundled = baseline["levels"].get(number, {}).get("sha256") == item["sha256"]
        path = None if bundled else f"levels/{number}/{item['sha256']}.json"
        entries.append({"level": int(number), "sha256": item["sha256"], "bytes": item["bytes"], "bundled": bundled, "path": path})
        if path:
            payloads[path] = item["document"]
    manifest = {"schemaVersion": PROTOCOL, "clientRelease": release, "appVersion": baseline["appVersion"], "levels": entries}
    revision = digest(canonical(manifest))
    manifest["revision"] = revision
    directory = root / OUTPUT / release / revision
    for path, document in payloads.items():
        save(directory / "public" / path, document)
    save(directory / "public" / "releases" / release / (revision + ".json"), manifest)
    save(directory / "public" / "stable" / release / "manifest.json", manifest)
    plan = {"release": release, "revision": revision, "newLevels": new, "changedLevels": changed,
            "hasChanges": bool(new or changed), "levelCount": len(levels), "remotePayloadCount": len(payloads),
            "manifest": manifest, "manifestUrl": f"{HOST}/stable/{release}/manifest.json"}
    save(directory / "plan.json", plan)
    save(root / STATE / "prepared.json", {"release": release, "revision": revision})
    return plan


def credentials():
    values = {}
    path = Path.home() / ".wondi-cloudflare.env"
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            match = re.match(r"\s*(?:export\s+)?([A-Za-z_][A-Za-z_0-9]*)\s*=\s*(.*)", line)
            if match:
                parts = shlex.split(match[2], comments=True)
                if parts and match[1] in ("CLOUDFLARE_API_TOKEN", "CLOUDFLARE_ACCOUNT_ID"):
                    values[match[1]] = parts[0]
    for key in ("CLOUDFLARE_API_TOKEN", "CLOUDFLARE_ACCOUNT_ID"):
        if os.environ.get(key):
            values[key] = os.environ[key]
        if not values.get(key):
            raise ReleaseError(f"Cloudflare kimlik bilgisi eksik: {key}; değerleri çıktıya yazmıyorum.")
    return values


def publish(root, apply=False):
    if (root / STATE / "pending-publication.json").exists():
        raise ReleaseError("Önceki yayının uzak sonucu bekliyor. Önce verify-publication çalıştır; başka yayını onun üstüne yazmıyorum.")
    selected = read(root / STATE / "prepared.json")
    release, revision = identifier(selected["release"]), selected["revision"]
    old = read(root / OUTPUT / release / revision / "plan.json")
    fresh = prepare(root, release)
    if fresh["revision"] != revision or old["manifest"] != fresh["manifest"]:
        raise ReleaseError("Hazırlamadan sonra level içeriği değişti; yeni planı inceleyip yeniden gönder.")
    if not old["hasChanges"]:
        raise ReleaseError("Gönderilecek yeni/değişen level yok.")
    if not apply:
        return {"deployExecuted": False, "release": release, "revision": revision,
                "newLevels": old["newLevels"], "changedLevels": old["changedLevels"]}
    return deploy(root, old)


def deploy(root, plan):
    release, revision = plan["release"], plan["revision"]
    auth = credentials()
    public = root / "ContentDelivery/public"
    prepared = root / OUTPUT / release / revision / "public"
    stage = root / OUTPUT / release / revision / "deploy-stage"
    if stage.exists():
        shutil.rmtree(stage)
    shutil.copytree(public, stage)
    for entry in plan["manifest"]["levels"]:
        if entry["bundled"]:
            continue
        expected = f"levels/{entry['level']}/{entry['sha256']}.json"
        if entry["path"] != expected or not re.fullmatch(r"[0-9a-f]{64}", entry["sha256"]):
            raise ReleaseError("Hazırlanan içerikte geçersiz dosya yolu var.")
        source = prepared / expected if (prepared / expected).exists() else public / expected
        if file_digest(source) != entry["sha256"] or source.stat().st_size != entry["bytes"]:
            raise ReleaseError("Hazır paket değişmiş/bozulmuş; yüklemeden önce yeniden hazırla.")
        destination = stage / expected
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
    save(stage / "releases" / release / (revision + ".json"), plan["manifest"])
    save(stage / "stable" / release / "manifest.json", plan["manifest"])
    for path in stage.rglob("*"):
        if path.is_file() and path.name != "_headers":
            relative = path.relative_to(stage).as_posix()
            if not re.fullmatch(r"(?:levels/\d+/[0-9a-f]{64}|releases/[A-Za-z0-9._-]+/[0-9a-f]{64}|stable/[A-Za-z0-9._-]+/manifest)\.json", relative):
                raise ReleaseError("Yayın klasöründe beklenmeyen dosya var; yüklemeyi durdurdum: " + relative)
    config = root / "ContentDelivery/wrangler.jsonc"
    config_data = json.loads("\n".join(line for line in config.read_text().splitlines() if not line.lstrip().startswith("//")))
    config_data["assets"]["directory"] = str(stage)
    config_path = stage.parent / "deploy-config.json"
    save(config_path, config_data)
    env = os.environ.copy()
    env.update(auth)
    # A failed/ambiguous network acknowledgement cannot silently advance the local release.
    save(root / STATE / "pending-publication.json", {"plan": plan, "stage": stage.relative_to(root).as_posix()})
    command = ["npx", "--yes", "wrangler@4", "deploy", "--config", str(config_path)]
    process = subprocess.run(command, cwd=root / "ContentDelivery", env=env, capture_output=True, text=True)
    if process.returncode:
        output = process.stdout + process.stderr
        for value in auth.values():
            output = output.replace(value, "[REDACTED]")
        raise ReleaseError("Cloudflare yayını doğrulanamadı; yerel yayın kaydı ilerletilmedi. verify-publication ile sonucu kontrol et.\n" + output[-3000:])
    return verify_publication(root)


def verify_publication(root, allow_retry=False):
    pending_path = root / STATE / "pending-publication.json"
    if not pending_path.exists():
        raise ReleaseError("Doğrulanmayı bekleyen yayın yok.")
    pending = read(pending_path)
    plan = pending["plan"]
    release, revision = identifier(plan["release"]), plan["revision"]
    request = urllib.request.Request(plan["manifestUrl"] + "?verify=" + revision, headers={"Cache-Control": "no-cache"})
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            deployed = json.loads(response.read(2_000_000))
    except urllib.error.HTTPError as error:
        if error.code == 404:
            if allow_retry:
                pending_path.unlink()
            return {"verified": False, "deployExecuted": False, "release": release,
                    "retryAllowed": allow_retry, "pendingRetained": not allow_retry}
        raise
    if deployed != plan["manifest"]:
        previous = root / STATE / "published" / (release + ".json")
        if previous.exists() and deployed.get("revision") == read(previous)["revision"]:
            if allow_retry:
                pending_path.unlink()
            return {"verified": False, "deployExecuted": False, "release": release,
                    "retryAllowed": allow_retry, "pendingRetained": not allow_retry}
        raise ReleaseError("Sunucu manifesti hazırlanan planla eşleşmedi; yerel yayın kaydı ilerletilmedi.")
    stage = (root / pending["stage"]).resolve()
    if not stage.is_relative_to((root / OUTPUT).resolve()):
        raise ReleaseError("Geçersiz yayın çalışma klasörü.")
    shutil.copytree(stage, root / "ContentDelivery/public", dirs_exist_ok=True)
    save(root / STATE / "published" / (release + ".json"), {
        "release": release, "revision": revision, "levels": {str(v["level"]): v for v in deployed["levels"]}})
    save(root / STATE / "history" / release / (revision + ".json"), plan)
    pending_path.unlink()
    return {"deployExecuted": True, "verified": True, "release": release, "revision": revision, "manifestUrl": plan["manifestUrl"]}


def rollback(root, revision, release=None, apply=False):
    if (root / STATE / "pending-publication.json").exists():
        raise ReleaseError("Önce bekleyen yayının sonucunu verify-publication ile doğrula.")
    if not re.fullmatch(r"[0-9a-f]{64}", revision):
        raise ReleaseError("Geçersiz içerik revizyonu.")
    baseline = active_baseline(root, release)
    history = root / STATE / "history" / baseline["release"] / (revision + ".json")
    if not history.exists():
        raise ReleaseError("Bu revizyon için doğrulanmış yayın kaydı yok; hazırlanmış dosya tek başına yayın sayılmaz.")
    plan = read(history)
    if plan["release"] != baseline["release"]:
        raise ReleaseError("Farklı uygulama release'leri arasında geri dönüş yapılamaz.")
    if not apply:
        return {"deployExecuted": False, "rollback": True, "release": plan["release"], "revision": revision}
    result = deploy(root, plan)
    result["rollback"] = True
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--result-file", type=Path, help="Editor status JSON; never contains credentials")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("status")
    verify = commands.add_parser("verify-publication")
    verify.add_argument("--allow-retry", action="store_true")
    mark = commands.add_parser("mark-release")
    mark.add_argument("--release", required=True)
    prep = commands.add_parser("prepare")
    prep.add_argument("--release")
    pub = commands.add_parser("publish")
    pub.add_argument("--apply", action="store_true")
    back = commands.add_parser("rollback")
    back.add_argument("--revision", required=True)
    back.add_argument("--release")
    back.add_argument("--apply", action="store_true")
    args = parser.parse_args(argv)
    root = args.root.resolve()
    try:
        if args.command == "mark-release":
            result = mark_release(root, args.release)
        elif args.command == "prepare":
            result = prepare(root, args.release)
        elif args.command == "publish":
            result = publish(root, args.apply)
        elif args.command == "verify-publication":
            result = verify_publication(root, args.allow_retry)
        elif args.command == "rollback":
            result = rollback(root, args.revision, args.release, args.apply)
        else:
            levels = inventory(root)
            active = root / STATE / "active.json"
            result = {"levelCount": len(levels), "highestLevel": max(map(int, levels)), "appVersion": project_version(root),
                      "unregisteredLevelAssets": sorted(unregistered_levels(root, levels)),
                      "activeRelease": read(active)["release"] if active.exists() else None, "deployExecuted": False}
        # Manifests are on disk; keep editor/terminal status compact.
        summary = {"ok": True, **{k: v for k, v in result.items() if k != "manifest"}}
        if args.result_file:
            save(args.result_file, summary)
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return 0
    except (ReleaseError, OSError, ValueError, KeyError) as error:
        if args.result_file:
            save(args.result_file, {"ok": False, "error": str(error)})
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
