// Run: csi Tools/check_mercy_refill.csx (no Unity/project build).
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#load "../Assets/_Project/Scripts/Grid/Board/RefillAssistScoring.cs"
#load "../Assets/_Project/Scripts/Core/LevelAssist.cs"
#load "../Assets/_Project/Scripts/GamePlay/PreLevelAutoSpecials.cs"
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Only external services are stubbed; scoring, tier selection and automatic reward lists
// above are loaded directly from production sources.
public static class RuntimeSimulationSession { public static bool IsActive; }
public static class CurrentLevel { public static int Global = 126; }
public static class LevelAttemptStats {
    public static int Fails, ActiveAssistTier;
    public static bool HasActiveAttempt;
    public static int StrugglesOn(int level) => Fails;
}
public enum TileSpecial { LineH, PulseCore, SystemOverride }
public enum DailySlotRewardType { Joker_Line, Joker_LineH, Joker_PulseCore, Joker_SystemOverride }
public static class TimedRewardService {
    public static HashSet<DailySlotRewardType> Active = new HashSet<DailySlotRewardType>();
    public static bool IsActive(DailySlotRewardType type) => Active.Contains(type);
}

int checkedCases = 0;
void Check(bool value, string name) {
    if (!value) throw new Exception("FAIL: " + name);
    checkedCases++;
}
int Score(string[] rows, int x, int y, int color) {
    var snapshot = string.Join("/", rows);
    Func<int, int, int?> at = (cx, cy) => cx < 0 || cy < 0 || cy >= rows.Length || cx >= rows[cy].Length
        || rows[cy][cx] == '.' ? (int?)null : rows[cy][cx] - '0';
    int result = RefillAssistScoring.Score(color, x, y, at);
    Check(snapshot == string.Join("/", rows), "scoring leaves source board unchanged");
    return result;
}

int three = Score(new[]{"11."}, 2, 0, 1);
int four = Score(new[]{"111."}, 3, 0, 1);
int five = Score(new[]{"1111."}, 4, 0, 1);
Check(three > 0 && four > three && five > four, "4/5 matches are preferred, not rejected");
Check(Score(new[]{"11.11"}, 2, 0, 1) == five, "count matching stones on both sides of the gap");
Check(Score(new[]{"11", "1."}, 1, 1, 1) > three, "2x2 PatchBot opportunity");
Check(Score(new[]{"..1..", "..11.", "..1.."}, 1, 1, 1) > four, "finish off-center T arm");
Check(Score(new[]{"1..", "1..", "11."}, 2, 2, 1) > four, "finish off-center L arm");
Check(Score(new[]{"....", "1112"}, 3, 0, 1) > three, "normal incoming stone enables a special-producing swap");
Check(Score(new[]{"111."}, 3, 0, 2) < four, "unhelpful color loses to special-producing color");
Check(Score(new[]{"1.1."}, 3, 0, 1) < three, "unmatchable cell splits the run");
Check(Score(new[]{"."}, 0, 0, 1) == 0, "isolated tile yields no invented match");

LevelAttemptStats.Fails = 2;
Check(LevelAssist.RefillBias == 0f, "no help below threshold");
LevelAttemptStats.Fails = 3;
Check(LevelAssist.RefillBias == 0.15f, "tier 1 remains 15 percent");
LevelAttemptStats.Fails = 5;
Check(LevelAssist.RefillBias == 0.30f, "tier 2 increases refill help");
LevelAttemptStats.Fails = 10;
Check(LevelAssist.RefillBias == 0.45f, "long struggle gets stronger help");
LevelAttemptStats.Fails = 100;
Check(LevelAssist.RefillBias == 0.45f, "help is capped");
Check(PreLevelAutoSpecials.Collect().Count == 0, "mercy never grants a starting special");
TimedRewardService.Active.Add(DailySlotRewardType.Joker_Line);
TimedRewardService.Active.Add(DailySlotRewardType.Joker_SystemOverride);
Check(PreLevelAutoSpecials.Collect().SequenceEqual(new[]{TileSpecial.LineH, TileSpecial.SystemOverride}),
    "earned timed specials remain intact");
LevelAttemptStats.HasActiveAttempt = true;
LevelAttemptStats.ActiveAssistTier = 1;
LevelAttemptStats.Fails = 8;
Check(LevelAssist.RefillBias == 0.15f, "tier stays fixed during current attempt");
RuntimeSimulationSession.IsActive = true;
LevelAttemptStats.ActiveAssistTier = 2;
Check(LevelAssist.RefillBias == 0f && LevelAssist.TierFor(126) == 0, "simulation overrides cached player assistance");

var path = "Assets/_Project/Scripts/Grid/Board/CascadeLogic.cs";
var cascade = File.ReadAllText(path);
var tree = CSharpSyntaxTree.ParseText(cascade);
Check(!tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error), "cascade syntax");
var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == "TryPickHelpfulRefillColor");
Check(method.ToString().Contains("foreach (var candidate in board.RandomPool)"), "only authored refill colors are candidates");
Check(!method.ToString().Contains("WouldExtendRunTooFar") && !method.ToString().Contains("SetSpecial"),
    "helpful selection permits specials through matches but never converts a tile");
Check(cascade.Contains("view.SetType(vTile.SpawnType);\n                        view.SetSpecial(TileSpecial.None);"),
    "new refill stones still spawn as normal tiles");
foreach (string sourcePath in new[]{
    "Assets/_Project/Scripts/Core/LevelAssist.cs", "Assets/_Project/Scripts/Grid/Board/RefillAssistScoring.cs",
    "Assets/_Project/Scripts/GamePlay/PreLevelAutoSpecials.cs", "Assets/_Project/Scripts/Editor/LevelAssistDebugMenu.cs"})
    Check(!CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath)).GetDiagnostics()
        .Any(d => d.Severity == DiagnosticSeverity.Error), "syntax: " + sourcePath);
Console.WriteLine("PASS: " + checkedCases + " mercy checks (actual scoring/tier/reward sources; no Unity build).");
