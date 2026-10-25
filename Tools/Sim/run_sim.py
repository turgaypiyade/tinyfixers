#!/usr/bin/env python3
"""Arka plan level simülasyonu — gerçek oyunu penceresiz (headless) oynatır.

Önce sim build'i alınmış olmalı: Unity ▸ TinyFixers ▸ Sim ▸ Build Headless Sim (Mac)
(çıktı: Builds/Sim/TinyFixersSim.app).

Örnek:
  python3 Tools/Sim/run_sim.py --levels 90-110 --games 20 --speed 10 --jobs 4
  python3 Tools/Sim/run_sim.py --levels 1-186 --games 30 --jobs 6 --out ~/Desktop/Sim/gece1

Her iş kendi klasörüne report.json / report.csv / report.status.txt / log.txt yazar.
Durdurmak için: çıktı klasöründeki her iş klasörüne STOP dosyası koy (ya da Ctrl+C → betik koyar).
Sonuç: <out>/summary.csv (seviye başına kazanma oranı, ort. kalan hamle, hata/zaman aşımı).
"""
import argparse, csv, datetime, json, os, signal, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
APP = os.path.join(ROOT, "Builds", "Sim", "TinyFixersSim.app")
POLICIES = {"Random": 0, "GoalAware": 1, "Greedy": 2}   # RuntimeBotPolicy (JsonUtility enum = int)


def parse_levels(spec):
    out = []
    for part in spec.split(","):
        part = part.strip()
        if "-" in part:
            a, b = part.split("-")
            out.extend(range(int(a), int(b) + 1))
        elif part:
            out.append(int(part))
    return out


def find_binary():
    macos = os.path.join(APP, "Contents", "MacOS")
    if not os.path.isdir(macos):
        sys.exit(f"Sim build bulunamadı: {APP}\nUnity ▸ TinyFixers ▸ Sim ▸ Build Headless Sim (Mac) ile al.")
    names = [n for n in os.listdir(macos) if not n.startswith(".")]
    return os.path.join(macos, names[0])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--levels", required=True, help="ör. 90-110 ya da 1-50,95,97")
    ap.add_argument("--games", type=int, default=20, help="seviye başına oyun")
    ap.add_argument("--speed", type=float, default=10.0, help="Time.timeScale")
    ap.add_argument("--jobs", type=int, default=max(1, (os.cpu_count() or 4) // 2))
    ap.add_argument("--policy", default="GoalAware", choices=POLICIES.keys())
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--timeout", type=float, default=120.0, help="adım başına gerçek-zaman sınırı (sn)")
    ap.add_argument("--out", default=None)
    a = ap.parse_args()

    levels = parse_levels(a.levels)
    binary = find_binary()
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M")
    out = os.path.expanduser(a.out or os.path.join("~", "Desktop", "Sim", stamp))
    os.makedirs(out, exist_ok=True)

    jobs = max(1, min(a.jobs, len(levels)))
    chunks = [levels[i::jobs] for i in range(jobs)]   # sırayla dağıt: zor/kolay aralıklar işlere yayılır
    procs = []
    for i, chunk in enumerate(chunks):
        jd = os.path.join(out, f"job{i:02d}")
        os.makedirs(jd, exist_ok=True)
        cfg = {
            "levelPaths": [], "levelNumbers": chunk, "scenePath": "Assets/_Project/Scenes/01_Game.unity",
            "gamesPerLevel": a.games, "seed": a.seed + i * 100003, "policy": POLICIES[a.policy],
            "timeScale": a.speed, "stepTimeoutSeconds": a.timeout, "outputPath": os.path.join(jd, "report.json"),
            "recordDir": "", "ffmpegPath": "", "recordFps": 30, "thinkSeconds": 0, "tailSeconds": 0,
        }
        cfg_path = os.path.join(jd, "config.json")
        with open(cfg_path, "w") as f:
            json.dump(cfg, f, indent=1)
        cmd = [binary, "-batchmode", "-nographics", "-tfsim", cfg_path, "-logFile", os.path.join(jd, "log.txt")]
        procs.append((jd, subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)))
    print(f"{len(levels)} seviye × {a.games} oyun, {jobs} paralel iş, hız ×{a.speed} → {out}")

    def stop(*_):
        for jd, _p in procs:
            open(os.path.join(jd, "STOP"), "w").close()
        print("\nSTOP gönderildi; işler kısmi raporu yazıp kapanacak...")
    signal.signal(signal.SIGINT, stop)

    started = time.time()
    while any(p.poll() is None for _, p in procs):
        time.sleep(15)
        status = []
        for jd, p in procs:
            try:
                status.append(open(os.path.join(jd, "report.status.txt")).read().strip()[:60])
            except OSError:
                status.append("başlıyor")
        print(f"[{int(time.time() - started)//60} dk] " + " | ".join(status), flush=True)

    summarize(out, [jd for jd, _ in procs])


def summarize(out, job_dirs):
    rows = {}
    for jd in job_dirs:
        try:
            report = json.load(open(os.path.join(jd, "report.json")))
        except (OSError, ValueError):
            print(f"Rapor okunamadı: {jd} (log.txt'ye bak)")
            continue
        for g in report.get("games", []):
            key = g.get("levelPath") or g.get("level")
            r = rows.setdefault(key, {"name": g.get("level"), "won": 0, "lost": 0, "other": 0, "moves_left": [], "goals_left": []})
            o = g.get("outcome")
            if o == "Won":
                r["won"] += 1
                r["moves_left"].append(g.get("movesLeft", 0))
            elif o == "Lost":
                r["lost"] += 1
                r["goals_left"].append(g.get("remainingGoalUnits", 0))
            else:
                r["other"] += 1
    path = os.path.join(out, "summary.csv")
    with open(path, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["Level", "Name", "Games", "Win%", "AvgMovesLeftOnWin", "AvgGoalsLeftOnLoss", "Errors/Timeouts"])
        for key in sorted(rows, key=lambda k: (len(k), k)):
            r = rows[key]
            n = r["won"] + r["lost"]
            avg = lambda v: round(sum(v) / len(v), 1) if v else ""
            w.writerow([key, r["name"], n, round(100.0 * r["won"] / n, 1) if n else "", avg(r["moves_left"]),
                        avg(r["goals_left"]), r["other"]])
    print("Özet:", path)


if __name__ == "__main__":
    main()
