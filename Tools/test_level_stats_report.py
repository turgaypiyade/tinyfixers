import unittest
from level_stats_report import reports


def player(uid, attempt=0, **changes):
    data = dict(level=126, measurementVersion=2, isDevelopment=False,
                trackingFromAttemptOne=True, firstObservedAtUnix=100, firstAppVersion="1.0",
                hasWon=attempt > 0, firstWinAttempt=attempt, highestAttempt=max(1, attempt),
                firstWinWithoutContinueOrMercy=True)
    data.update(changes)
    return {"path": f"users/{uid}/levels/L00126", "data": data}


class ReportsTest(unittest.TestCase):
    def test_denominators_include_not_yet_won(self):
        rows = reports([player("a", 1), player("b", 2), player("c", 2), player("d", highestAttempt=2)])
        r = rows[0]
        self.assertEqual(r["players_in_full_cohort"], 4)
        self.assertEqual(r["win_on_1_pct_of_entrants"], 25)
        self.assertEqual(r["win_on_2_pct_of_entrants"], 50)
        self.assertEqual(r["win_on_2_pct_of_winners"], 66.67)
        self.assertEqual(r["win_on_2_pct_of_reached_attempt"], 66.67)
        self.assertEqual(r["players_not_yet_won"], 1)

    def test_legacy_midlevel_and_dev_exclusion(self):
        r = reports([player("a", 1), player("b", 1, measurementVersion=1),
                     player("c", 2, trackingFromAttemptOne=False), player("d", 1, isDevelopment=True)])[0]
        self.assertEqual(r["players_in_full_cohort"], 1)
        self.assertEqual(r["players_excluded_incomplete_history"], 1)
        self.assertEqual(r["win_on_1_pct_of_entrants"], 100)

    def test_replays_do_not_inflate_reached_attempt_denominator(self):
        r = reports([player("a", 1, highestAttempt=20), player("b", 2)])[0]
        self.assertEqual(r["win_on_2_pct_of_reached_attempt"], 100)

    def test_continued_and_mercy_wins_are_separate(self):
        r = reports([player("a", 1, firstWinContinues=1, firstWinWithoutContinueOrMercy=False),
                     player("b", 1, firstWinAssistTier=1, firstWinWithoutContinueOrMercy=False),
                     player("c", 1)])[0]
        self.assertEqual(r["win_on_1_pct_of_entrants"], 100)
        self.assertEqual(r["first_attempt_no_continue_no_mercy_pct"], 33.33)
        self.assertEqual(r["first_wins_with_continue"], 1)
        self.assertEqual(r["first_wins_with_mercy"], 1)

    def test_cohort_date_version_and_unique_players(self):
        p = player("a", 1)
        r = reports([p, p, player("b", 1, firstObservedAtUnix=200),
                     player("c", 1, firstAppVersion="2.0")], entered_before=200, first_app_version="1.0")[0]
        self.assertEqual(r["players_in_full_cohort"], 1)

    def test_empty_denominator_and_late_buckets(self):
        r = reports([player("a", trackingFromAttemptOne=False)])[0]
        self.assertIsNone(r["win_on_1_pct_of_entrants"])
        r = reports([player("a", 8), player("b", 15)])[0]
        self.assertEqual(r["win_on_6_10_pct_of_entrants"], 50)
        self.assertEqual(r["win_on_11_plus_pct_of_entrants"], 50)

    def test_conflicting_duplicates_rejected(self):
        with self.assertRaises(ValueError):
            reports([player("a", 1), player("a", 2)])

    def test_development_remains_separate(self):
        rows = reports([player("a", 1), player("b", 2, isDevelopment=True)], include_development=True)
        self.assertEqual(len(rows), 2)
        self.assertEqual(rows[0]["win_on_1_pct_of_entrants"], 100)
        self.assertEqual(rows[1]["win_on_2_pct_of_entrants"], 100)


if __name__ == "__main__":
    unittest.main()
