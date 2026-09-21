// Isolated production-method regression tests; no Unity or project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

var names = new[] { "HasLevelEndCondition", "RequestEvaluateLevelEndState", "EvaluateAndShowIfEnded", "EvaluateAfterBoardSettled", "IsBoardWorkingForLevelEnd", "DescribeBoardWorkForLevelEnd" };
var methods = string.Join("\n", CSharpSyntaxTree.ParseText(File.ReadAllText(
    "Assets/_Project/Scripts/UI/LevelEndSimplePopupController.cs")).GetRoot().DescendantNodes()
    .OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
var harness = @"
using System;
using System.Collections;
public static class Time { public static float unscaledDeltaTime=1; }
public static class Debug {
 public static int warnings,errors;
 public static void Log(string s){} public static void LogWarning(string s){warnings++;} public static void LogError(string s){errors++;}
}
public class Board {
 public int RemainingMoves=18,ActiveBackgroundJobs=1,BlockingBackgroundJobs,FlyingGoalOrbs,FlyingPatchBotDashes,DrainingBossStrikes=1;
 public bool IsBusy,IsActionSequencePlaying;public int drains,queued;
 public void RunAfterIdle(Action a){queued++;a();}
 public void ForceDrainAllJobs(){drains++;ActiveBackgroundJobs=0;BlockingBackgroundJobs=0;IsBusy=false;DrainingBossStrikes=0;}
}
public class Hud { public bool AreAllGoalsCompleted; }
public class End {
 public Board board=new Board();public Hud topHud=new Hud();
 public bool failPopupShown,successPopupShown,endCheckQueued,failSettleWaitRunning;
 public int successes,failures,deadlines;public IEnumerator waiting;
 public void StartLevelEndForceDeadlineIfOutOfMoves(){deadlines++;}
 public void TryQueueSuccessOrDeferForGold(){successes++;}
 public bool ResolvePendingBoardBeforeFail()=>false;
 public void BeginFailConfirmation(){failures++;}
 public void StartCoroutine(IEnumerator e){waiting=e;}
 public void Request()=>RequestEvaluateLevelEndState();
 public void Evaluate()=>EvaluateAndShowIfEnded();
 public IEnumerator Wait()=>EvaluateAfterBoardSettled();
 " + methods + @"
}
public static class Checks {
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 static void Finish(IEnumerator e){for(int n=0;n<40;n++){if(!e.MoveNext())return;}throw new Exception(""Wait did not terminate"");}
 public static int Run(){
  var playing=new End();playing.Request();playing.Evaluate();
  Check(playing.board.queued==0&&playing.waiting==null&&playing.deadlines==0,""18 moves and live boss: no level-end watchdog"");
  playing.failSettleWaitRunning=true;Finish(playing.Wait());
  Check(playing.board.drains==0&&!playing.failSettleWaitRunning,""Stale wait exits without draining live duel"");
  Check(playing.board.DrainingBossStrikes==1,""Gameplay hold stays intact"");
  var last=new End();last.board.RemainingMoves=0;last.Request();
  Check(last.waiting!=null&&last.failSettleWaitRunning,""Last move waits for pending boss strike"");
  for(int i=0;i<5;i++)Check(last.waiting.MoveNext(),""Pending strike is still awaited"");
  Check(last.failures==0&&last.board.drains==0,""Never fail before last strike completes"");
  last.board.ActiveBackgroundJobs=0;last.board.DrainingBossStrikes=0;Finish(last.waiting);
  Check(last.failures==1&&!last.failSettleWaitRunning,""Settled last move evaluates once"");
  var win=new End();win.topHud.AreAllGoalsCompleted=true;win.Request();
  Check(win.waiting!=null&&win.successes==0,""Winning hit must finish returning"");
  win.board.ActiveBackgroundJobs=0;Finish(win.waiting);
  Check(win.successes==1&&win.board.drains==0,""Win evaluates after hold releases"");
  var resume=new End();resume.board.RemainingMoves=0;resume.Request();resume.waiting.MoveNext();resume.board.RemainingMoves=5;Finish(resume.waiting);
  Check(resume.board.drains==0&&resume.failures==0&&!resume.failSettleWaitRunning,""Extra moves cancel old timeout"");
  var leak=new End();leak.board.RemainingMoves=0;leak.Request();Finish(leak.waiting);
  Check(leak.board.drains==1&&leak.failures==1,""Real terminal async leak still recovers"");
  var blocking=new End();blocking.board.RemainingMoves=0;blocking.board.IsBusy=true;blocking.Request();Finish(blocking.waiting);
  Check(blocking.board.drains==1&&blocking.failures==1,""Real terminal blocking leak still recovers"");
  var race=new End();race.board.RemainingMoves=0;race.Request();race.waiting.MoveNext();race.topHud.AreAllGoalsCompleted=true;race.board.ActiveBackgroundJobs=0;Finish(race.waiting);
  Check(race.successes==1&&race.failures==0,""Last strike completing goals wins"");
  return count;
 }
}
Checks.Run()";
var result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " isolated level-end watchdog checks (ongoing play, last move, win, extra moves, real leaks)");
