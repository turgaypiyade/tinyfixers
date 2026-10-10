#!/usr/bin/env python3
"""Level difficulty report from the new per-player Firestore summaries.

Offline: python3 Tools/level_stats_report.py --input summaries.json --output report.csv
Live (read-only): python3 Tools/level_stats_report.py --project PROJECT_ID --output report.csv
Live mode requires firebase-admin and Application Default Credentials with read access.
Never bundle an Admin credential in the Unity app.

JSON input: [{"path":"users/UID/levels/L00126", "data":{...Firestore fields...}}, ...]
Percentages use UNIQUE players, not number of attempts. All ratios are observed-to-date:
recent entrants still have time to succeed. Use --entered-before YYYY-MM-DD for mature cohorts.
Development, legacy, and mid-level rollout records are excluded from rate denominators.
"""
import argparse
import csv
import datetime as dt
import json
import sys
from collections import defaultdict


def percentage(numerator, denominator):
    return round(100 * numerator / denominator, 2) if denominator else None


def reports(records, entered_before=None, first_app_version=None, include_development=False):
    players = {}
    for record in records:
        parts = record.get("path", "").split("/")
        if len(parts) != 4 or parts[0] != "users" or parts[2] != "levels":
            raise ValueError("Expected users/UID/levels/DOCUMENT path")
        data = record["data"]
        if data.get("measurementVersion") != 2:
            continue
        development = bool(data.get("isDevelopment"))
        if development and not include_development:
            continue
        if entered_before is not None and data.get("firstObservedAtUnix", 0) >= entered_before:
            continue
        if first_app_version and data.get("firstAppVersion") != first_app_version:
            continue
        key = (parts[1], int(data["level"]), development)
        if key in players and players[key] != data:
            raise ValueError("Conflicting duplicate player/level summary: " + record["path"])
        players[key] = data

    groups = defaultdict(list)
    for (_, level, development), data in players.items():
        groups[(level, development)].append(data)
    output = []
    for (level, development), all_players in sorted(groups.items()):
        cohort = [p for p in all_players if p.get("trackingFromAttemptOne") is True]
        winners = [p for p in cohort if p.get("hasWon") and p.get("firstWinAttempt", 0) > 0]
        total, won = len(cohort), len(winners)
        row = {
            "level": level,
            "environment": "development" if development else "production",
            "players_observed": len(all_players),
            "players_in_full_cohort": total,
            "players_excluded_incomplete_history": len(all_players) - total,
            "players_won": won,
            "players_not_yet_won": total - won,
            "win_pct_of_entrants": percentage(won, total),
            "giveups": sum(p.get("giveUps", 0) for p in cohort),
            "struggles": sum(p.get("struggles", 0) for p in cohort),
            "interrupted_attempts": sum(p.get("interruptedAttempts", 0) for p in cohort),
            "players_with_giveup_pct": percentage(sum(p.get("giveUps", 0) > 0 for p in cohort), total),
            "avg_first_win_attempt": round(sum(p["firstWinAttempt"] for p in winners) / won, 2) if won else None,
            "avg_first_win_seconds": round(sum(p.get("firstWinSeconds", 0) for p in winners) / won, 2) if won else None,
            "avg_giveups_before_first_win": round(sum(p.get("firstWinGiveUpsBefore", 0) for p in winners) / won, 2) if won else None,
            "avg_first_win_moves_left": round(sum(p.get("firstWinMovesLeft", 0) for p in winners) / won, 2) if won else None,
            "first_wins_with_continue": sum(p.get("firstWinContinues", 0) > 0 for p in winners),
            "first_wins_with_mercy": sum(p.get("firstWinAssistTier", 0) > 0 for p in winners),
            "continues": sum(p.get("continues", 0) for p in cohort),
            "ad_continues": sum(p.get("adContinues", 0) for p in cohort),
            "continue_coins_spent": sum(p.get("continueCoinsSpent", 0) for p in cohort),
        }
        for attempt in range(1, 6):
            count = sum(p["firstWinAttempt"] == attempt for p in winners)
            # Later replays cannot add players to the denominator for an earlier first-clear attempt.
            reached = sum((p.get("firstWinAttempt", 0) >= attempt) if p.get("hasWon")
                          else p.get("highestAttempt", 0) >= attempt for p in cohort)
            row[f"win_on_{attempt}_players"] = count
            row[f"win_on_{attempt}_pct_of_entrants"] = percentage(count, total)
            row[f"win_on_{attempt}_pct_of_winners"] = percentage(count, won)
            row[f"win_on_{attempt}_pct_of_reached_attempt"] = percentage(count, reached)
        for name, lo, hi in (("6_10", 6, 10), ("11_plus", 11, float("inf"))):
            count = sum(lo <= p["firstWinAttempt"] <= hi for p in winners)
            row[f"win_on_{name}_players"] = count
            row[f"win_on_{name}_pct_of_entrants"] = percentage(count, total)
            row[f"win_on_{name}_pct_of_winners"] = percentage(count, won)
        row["first_attempt_no_continue_no_mercy_pct"] = percentage(sum(
            p["firstWinAttempt"] == 1 and p.get("firstWinWithoutContinueOrMercy") is True
            for p in winners), total)
        output.append(row)
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--input", help="JSON export in the documented path/data format")
    source.add_argument("--project", help="Firebase project ID; read-only Admin SDK query")
    parser.add_argument("--output", help="CSV output; defaults to stdout")
    parser.add_argument("--entered-before", help="UTC date, exclusive, to exclude very recent entrants")
    parser.add_argument("--first-app-version", help="Filter by version when observation started")
    parser.add_argument("--include-development", action="store_true", help="Separate development rows; never mixes cohorts")
    args = parser.parse_args()
    cutoff = int(dt.datetime.strptime(args.entered_before, "%Y-%m-%d").replace(tzinfo=dt.timezone.utc).timestamp()) if args.entered_before else None
    if args.input:
        with open(args.input, encoding="utf-8") as source_file:
            records = json.load(source_file)
    else:
        try:
            import firebase_admin
            from firebase_admin import firestore
            from google.cloud.firestore_v1.base_query import FieldFilter
        except ImportError:
            parser.error("Live mode requires firebase-admin. Offline --input mode needs no external packages.")
        firebase_admin.initialize_app(options={"projectId": args.project})
        snapshots = firestore.client().collection_group("levels").where(
            filter=FieldFilter("measurementVersion", "==", 2)).stream()
        records = ({"path": snap.reference.path, "data": snap.to_dict()} for snap in snapshots)
    rows = reports(records, cutoff, args.first_app_version, args.include_development)
    target = open(args.output, "w", newline="", encoding="utf-8") if args.output else sys.stdout
    try:
        if rows:
            writer = csv.DictWriter(target, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)
        else:
            print("No matching measured level summaries.", file=sys.stderr)
    finally:
        if args.output:
            target.close()


if __name__ == "__main__":
    main()
