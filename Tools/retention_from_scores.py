#!/usr/bin/env python3
"""Daily install cohorts and rolling retention from the public Firestore leaderboard.

    python3 Tools/retention_from_scores.py                 # region_TR, last 10 days
    python3 Tools/retention_from_scores.py --board global --days 14 --csv out.csv

Read-only. leaderboards/*/scores is publicly readable (firestore.rules), so the query uses
the game's own Firebase API key from Assets/StreamingAssets/google-services.json and no login.

How a document maps to a player:
- createTime  = first score write of this install (anonymous uid) → cohort day.
- updatedAt   = written on every app launch (PublishOwnScoreIfChanged) → last seen day.
- score       = Σ(100 + 15 × moves left) per level won ≈ progress (early levels 150-550 each).
Retention is ROLLING ("seen on day N or later"), because only the last launch is stored.
A reinstall creates a new uid (a new cohort entry). Days are Europe/Istanbul (UTC+3).
A column shows "–" until the cohort is old enough to be measured.
"""
import argparse
import collections
import csv
import datetime as dt
import json
import os
import sys
import urllib.parse
import urllib.request

PROJECT = "tinyfixers-bc1cd"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SERVICES = os.path.join(ROOT, "Assets", "StreamingAssets", "google-services.json")
TZ = dt.timezone(dt.timedelta(hours=3))
# Known testers / own accounts (lower-case). Extend with --exclude.
TEST_NAMES = {"beyz", "gulcin", "berrin", "fbkral_31", "v10", "turgayp", "yeni başladı", "yeni2", "albea1.3mult",
              "oyuncu 18778"}
# Name prefixes excluded from cohorts. "erdal*" is one person reinstalling every build
# (erdal → erdal107 → erdal1.0.1 → …): each reinstall would otherwise count as a new player.
TEST_PREFIXES = ("erdal",)
RETENTION_DAYS = (1, 3, 7)
PROGRESS_SCORE = 1500   # ≈ level 5-7


def api_key():
    with open(SERVICES, encoding="utf-8") as f:
        return json.load(f)["client"][0]["api_key"][0]["current_key"]


def fetch(board):
    base = f"https://firestore.googleapis.com/v1/projects/{PROJECT}/databases/(default)/documents/leaderboards/{board}/scores"
    key, token, docs = api_key(), None, []
    while True:
        params = {"pageSize": 300, "key": key}
        if token:
            params["pageToken"] = token
        with urllib.request.urlopen(base + "?" + urllib.parse.urlencode(params), timeout=30) as r:
            page = json.load(r)
        docs += page.get("documents", [])
        token = page.get("nextPageToken")
        if not token:
            return docs


def parse_time(value):
    return dt.datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(TZ)


def players(docs, excluded):
    out = []
    for d in docs:
        f = d.get("fields", {})
        name = f.get("name", {}).get("stringValue", "")
        lowered = name.strip().lower()
        if lowered in excluded or lowered.startswith(TEST_PREFIXES):
            continue
        seen = f.get("updatedAt", {}).get("timestampValue") or d["updateTime"]
        out.append({"name": name, "score": int(f.get("score", {}).get("integerValue", 0)),
                    "first": parse_time(d["createTime"]).date(), "last": parse_time(seen).date()})
    return out


def cohorts(rows, today, days):
    by_day = collections.defaultdict(list)
    for p in rows:
        by_day[p["first"]].append(p)
    table = []
    for day in sorted(d for d in by_day if (today - d).days < days):
        group = by_day[day]
        n = len(group)
        row = {"day": day.isoformat(), "new": n,
               "played": sum(p["score"] > 0 for p in group),
               f"score_{PROGRESS_SCORE}+": sum(p["score"] >= PROGRESS_SCORE for p in group)}
        for k in RETENTION_DAYS:
            mature = (today - day).days >= k
            back = sum((p["last"] - day).days >= k for p in group)
            row[f"D{k}+"] = f"{back} (%{round(100 * back / n)})" if mature else "–"
        table.append(row)
    return table


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--board", default="region_TR", help="leaderboards/<board>/scores (region_TR, global)")
    ap.add_argument("--days", type=int, default=10, help="cohorts from the last N days")
    ap.add_argument("--exclude", default="", help="extra tester names, comma separated")
    ap.add_argument("--top", type=int, default=0, help="also list today's top N new players")
    ap.add_argument("--csv", help="write the cohort table to this CSV file")
    args = ap.parse_args()

    excluded = TEST_NAMES | {n.strip().lower() for n in args.exclude.split(",") if n.strip()}
    rows = players(fetch(args.board), excluded)
    if not rows:
        sys.exit("No scores returned.")
    now = dt.datetime.now(TZ)
    table = cohorts(rows, now.date(), args.days)

    cols = list(table[0].keys())
    widths = {c: max(len(c), *(len(str(r[c])) for r in table)) for c in cols}
    print(f"leaderboards/{args.board}  {len(rows)} players (testers excluded)  "
          f"last seen {max(p['last'] for p in rows)}  run {now:%Y-%m-%d %H:%M}")
    print("  ".join(c.ljust(widths[c]) for c in cols))
    for r in table:
        print("  ".join(str(r[c]).ljust(widths[c]) for c in cols))
    print("Retention is rolling (seen on day N or later); today's cohort is still filling.")

    if args.top:
        new_today = sorted((p for p in rows if p["first"] == now.date()), key=lambda p: -p["score"])
        print(f"\nToday's top {args.top}: " + ", ".join(f"{p['name']} {p['score']}" for p in new_today[:args.top]))

    if args.csv:
        with open(args.csv, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=cols)
            w.writeheader()
            w.writerows(table)
        print("CSV:", args.csv)


if __name__ == "__main__":
    main()
