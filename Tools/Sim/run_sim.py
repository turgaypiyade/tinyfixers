#!/usr/bin/env python3
r"""Arka plan level simülasyonu — gerçek oyunu penceresiz (headless) oynatır.

Önce sim build'i alınmış olmalı: Unity ▸ TinyFixers ▸ Sim ▸ Build Headless Sim (Mac / Windows)
(çıktı: Builds/Sim/TinyFixersSim.app  ya da  Builds/Sim/Win/TinyFixersSim.exe).
Windows: py Tools\Sim\run_sim.py --levels 1-200 --games 30 --jobs 12

Örnek:
  python3 Tools/Sim/run_sim.py --levels 90-110 --games 20 --speed 10 --jobs 4
  python3 Tools/Sim/run_sim.py --levels 1-186 --games 30 --jobs 6 --out ~/Desktop/Sim/gece1

Her iş kendi klasörüne report.json / report.csv / report.status.txt / log.txt yazar.
Durdurmak için: çıktı klasöründeki her iş klasörüne STOP dosyası koy (ya da Ctrl+C → betik koyar).
Sonuç: <out>/summary.csv (seviye başına kazanma oranı, ort. kalan hamle, hata/zaman aşımı).
"""
import argparse, csv, datetime, json, math, os, plistlib, signal, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
APP = os.path.join(ROOT, "Builds", "Sim", "TinyFixersSim.app")
WIN_EXE = os.path.join(ROOT, "Builds", "Sim", "Win", "TinyFixersSim.exe")
POLICIES = {"Random": 0, "GoalAware": 1, "Greedy": 2}   # RuntimeBotPolicy (JsonUtility enum = int)


def parse_levels(spec):
    out = []
    for part in spec.split(","):
        part = part.strip()
        if "-" in part:
            a, b = map(int, part.split("-"))
            if a < 1 or b < a or b >= 2**31:
                raise ValueError("Seviye aralığı pozitif ve artan olmalı.")
            out.extend(range(a, b + 1))
        elif part:
            value = int(part)
            if value < 1 or value >= 2**31:
                raise ValueError("Seviye numarası pozitif olmalı.")
            out.append(value)
    if not out:
        raise ValueError("En az bir seviye gerekli.")
    return list(dict.fromkeys(out))


def find_binary():
    if sys.platform.startswith("win"):
        if not os.path.isfile(WIN_EXE):
            sys.exit(f"Sim build bulunamadı: {WIN_EXE}\nUnity ▸ TinyFixers ▸ Sim ▸ Build Headless Sim (Windows) ile al.")
        return WIN_EXE
    if sys.platform != "darwin":
        sys.exit("Sim çalıştırıcısı yalnız macOS ve Windows destekler.")
    macos = os.path.join(APP, "Contents", "MacOS")
    if not os.path.isdir(macos):
        sys.exit(f"Sim build bulunamadı: {APP}\nUnity ▸ TinyFixers ▸ Sim ▸ Build Headless Sim (Mac) ile al.")
    with open(os.path.join(APP, "Contents", "Info.plist"), "rb") as f:
        name = plistlib.load(f).get("CFBundleExecutable", "")
    binary = os.path.join(macos, name)
    if not name or os.path.basename(name) != name or not os.path.isfile(binary):
        sys.exit(f"Sim build içinde çalıştırılabilir dosya bulunamadı: {APP}")
    return binary


def request_stop(procs):
    for jd, p in procs:
        if p.poll() is None:
            try:
                with open(os.path.join(jd, "STOP"), "w"):
                    pass
            except OSError as ex:
                print(f"STOP yazılamadı: {jd}: {ex}", file=sys.stderr)


def stop_workers(procs, grace=30.0):
    request_stop(procs)
    deadline = time.monotonic() + grace
    while any(p.poll() is None for _, p in procs) and time.monotonic() < deadline:
        time.sleep(0.25)
    for _, p in procs:
        if p.poll() is None:
            p.terminate()
    for _, p in procs:
        try:
            p.wait(timeout=5)
        except subprocess.TimeoutExpired:
            p.kill()
            p.wait(timeout=5)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--levels", required=True, help="ör. 90-110 ya da 1-50,95,97")
    ap.add_argument("--games", type=int, default=20, help="seviye başına oyun")
    ap.add_argument("--speed", type=float, default=10.0, help="Time.timeScale")
    ap.add_argument("--jobs", type=int, default=max(1, (os.cpu_count() or 4) // 2))
    ap.add_argument("--policy", default="GoalAware", choices=POLICIES.keys())
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--timeout", type=float, default=120.0, help="adım başına gerçek-zaman sınırı (sn)")
    ap.add_argument("--out", default=None)
    a = ap.parse_args(argv)

    try:
        levels = parse_levels(a.levels)
    except ValueError as ex:
        ap.error(str(ex))
    if a.games < 1 or a.jobs < 1:
        ap.error("--games ve --jobs en az 1 olmalı.")
    if not all(math.isfinite(v) and v > 0 for v in (a.speed, a.timeout)):
        ap.error("--speed ve --timeout pozitif, sonlu sayı olmalı.")
    if not -(2**31) <= a.seed < 2**31:
        ap.error("--seed 32-bit işaretli sayı aralığında olmalı.")
    binary = find_binary()
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S_%f")
    out = os.path.abspath(os.path.expanduser(a.out or os.path.join("~", "Desktop", "Sim", stamp)))
    if os.path.exists(out) and (not os.path.isdir(out) or os.listdir(out)):
        ap.error("Çıktı klasörü boş olmalı; eski STOP/raporlarla karışmaması için yeni --out seç.")
    os.makedirs(out, exist_ok=True)

    jobs = max(1, min(a.jobs, len(levels)))
    chunks = [levels[i::jobs] for i in range(jobs)]   # sırayla dağıt: zor/kolay aralıklar işlere yayılır
    procs = []
    stopping = False
    def stop(*_):
        nonlocal stopping
        stopping = True
    handlers = {sig: signal.signal(sig, stop) for sig in (signal.SIGINT, signal.SIGTERM)}
    failed = False
    try:
        for i, chunk in enumerate(chunks):
            if stopping:
                break
            jd = os.path.join(out, f"job{i:02d}")
            os.makedirs(jd)
            cfg = {
                "levelPaths": [], "levelNumbers": chunk, "scenePath": "Assets/_Project/Scenes/01_Game.unity",
                "gamesPerLevel": a.games, "seed": (a.seed + i * 100003 + 2**31) % 2**32 - 2**31,
                "policy": POLICIES[a.policy], "timeScale": a.speed, "stepTimeoutSeconds": a.timeout,
                "outputPath": os.path.join(jd, "report.json"),
                "recordDir": "", "ffmpegPath": "", "recordFps": 30, "thinkSeconds": 0, "tailSeconds": 0,
            }
            cfg_path = os.path.join(jd, "config.json")
            with open(cfg_path, "w", encoding="utf-8") as f:
                json.dump(cfg, f, indent=1)
            cmd = [binary, "-batchmode", "-nographics", "-tfsim", cfg_path, "-logFile", os.path.join(jd, "log.txt")]
            procs.append((jd, subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)))
        print(f"{len(levels)} seviye × {a.games} oyun, {jobs} paralel iş, hız ×{a.speed} → {out}", flush=True)
        started = last_status = time.monotonic()
        while any(p.poll() is None for _, p in procs):
            if stopping:
                print("\nSTOP gönderildi; işler kısmi raporu yazıp kapanacak...")
                stop_workers(procs)
                break
            time.sleep(0.25)
            now = time.monotonic()
            if now - last_status < 15:
                continue
            status = []
            for jd, p in procs:
                try:
                    with open(os.path.join(jd, "report.status.txt"), encoding="utf-8-sig") as f:
                        status.append(f.read().strip()[:60])
                except OSError:
                    status.append("başlıyor" if p.poll() is None else f"çıkış={p.returncode}")
            print(f"[{int(now - started)//60} dk] " + " | ".join(status), flush=True)
            last_status = now
    except OSError as ex:
        print(f"İş başlatılamadı/yazılamadı: {ex}", file=sys.stderr)
        failed = True
        stop_workers(procs)
    finally:
        for sig, handler in handlers.items():
            signal.signal(sig, handler)

    valid = summarize(out, [jd for jd, _ in procs])
    failed |= len(procs) != jobs
    for jd, p in procs:
        if p.returncode != 0:
            print(f"İş başarısız: {jd}, çıkış={p.returncode}; log.txt'ye bak.", file=sys.stderr)
            failed = True
    return 130 if stopping else 1 if failed or not valid else 0


def summarize(out, job_dirs):
    rows = {}
    valid = True
    for jd in job_dirs:
        try:
            with open(os.path.join(jd, "report.json"), encoding="utf-8-sig") as f:
                report = json.load(f)
            if not isinstance(report, dict) or not isinstance(report.get("games"), list):
                raise ValueError("Rapor games listesi içermeli.")
            cfg = report.get("config") or {}
            if not isinstance(cfg, dict):
                raise ValueError("Rapor config nesnesi içermeli.")
            requested = len(cfg.get("levelPaths") or cfg.get("levelNumbers") or []) * cfg.get("gamesPerLevel", 0)
            if report.get("completed") is not True or requested <= 0 or len(report["games"]) != requested:
                print(f"Eksik/kısmi rapor: {jd} ({len(report['games'])}/{requested})")
                valid = False
        except (OSError, ValueError, TypeError):
            print(f"Rapor okunamadı: {jd} (log.txt'ye bak)")
            valid = False
            continue
        for g in report.get("games", []):
            if not isinstance(g, dict):
                valid = False
                continue
            key = str(g.get("levelPath") or g.get("level") or "Unknown")
            r = rows.setdefault(key, {"name": g.get("level"), "won": 0, "lost": 0, "other": 0, "moves_left": [], "goals_left": [],
                                      "per_goal": {}, "start": g.get("goalsStart") or ""})
            o = g.get("outcome")
            if o == "Won":
                r["won"] += 1
                r["moves_left"].append(g.get("movesLeft", 0))
            elif o == "Lost":
                r["lost"] += 1
                r["goals_left"].append(g.get("remainingGoalUnits", 0))
                for name, left in parse_goals(g.get("goalsLeft")).items():
                    r["per_goal"].setdefault(name, []).append(left)
            else:
                r["other"] += 1
                valid = False
    path = os.path.join(out, "summary.csv")
    with open(path, "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        w.writerow(["Level", "Name", "Games", "Win%", "AvgMovesLeftOnWin", "AvgGoalsLeftOnLoss", "Errors/Timeouts",
                    "GoalsLeftOnLoss (ort. kalan / başlangıç)"])
        for key in sorted(rows, key=lambda k: (len(k), k)):
            r = rows[key]
            n = r["won"] + r["lost"]
            avg = lambda v: round(sum(v) / len(v), 1) if v else ""
            start = parse_goals(r["start"])
            per_goal = "; ".join(f"{name} {avg(v)}/{start.get(name, '?')}" for name, v in r["per_goal"].items())
            w.writerow([key, r["name"], n + r["other"], round(100.0 * r["won"] / n, 1) if n else "", avg(r["moves_left"]),
                        avg(r["goals_left"]), r["other"], per_goal])
    print("Özet:", path)
    return valid and bool(rows)


def parse_goals(text):
    out = {}
    for part in (text or "").split(";"):
        if "=" in part:
            name, value = part.split("=", 1)
            try:
                out[name.strip()] = int(value)
            except ValueError:
                pass
    return out


if __name__ == "__main__":
    sys.exit(main())
