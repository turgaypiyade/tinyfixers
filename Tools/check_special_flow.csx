// Run: csi Tools/check_special_flow.csx (isolated production-method tests; no Unity/project build).
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
string Source(string path) => File.ReadAllText("Assets/_Project/Scripts/" + path);
string Methods(string path, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(Source(path)).GetRoot()
    .DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
var scope = CSharpSyntaxTree.ParseText(Source("Grid/Board/BoardAnimator.cs")).GetRoot().DescendantNodes()
    .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "LineSweepGravityScope").ToFullString()
    .Replace("private sealed class", "public sealed class");
var safari = Source("Events/Safari/SafariState.cs").Replace("using System;", "").Replace("using UnityEngine;", "");
var lives = Source("Core/LivesManager.cs").Replace("using System;", "").Replace("using UnityEngine;", "");
var harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
 public struct Vector2Int { public int x,y; public Vector2Int(int x,int y){this.x=x;this.y=y;} }
 public static class Time { public static float time; }
 public static class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); public static int Max(int a,int b)=>Math.Max(a,b); public static int Min(int a,int b)=>Math.Min(a,b); public static int Clamp(int n,int a,int b)=>Math.Min(b,Math.Max(a,n)); }
 public static class Debug { public static void Log(string s){} }
 public static class PlayerPrefs {
  static Dictionary<string,int> ints=new Dictionary<string,int>(); static Dictionary<string,string> strings=new Dictionary<string,string>();
  public static int GetInt(string k,int d=0)=>ints.TryGetValue(k,out var n)?n:d;
  public static void SetInt(string k,int n){ints[k]=n;}
  public static string GetString(string k,string d="""")=>strings.TryGetValue(k,out var s)?s:d;
  public static void SetString(string k,string s){strings[k]=s;} public static void Save(){}
 }
 public enum RuntimeInitializeLoadType {AfterSceneLoad}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public static class Resources {public static T Load<T>(string p) where T:class=>null;}
public class SafariConfig { public object lossIcon; public int prizePoolGold=100; }
public static class SafariSchedule {public static string GetCycleKey(SafariConfig c,DateTime t)=>t.Date.Ticks.ToString();}
public static class GameLocalization {public static string Get(string s)=>s;}
public class LevelLossItem {public LevelLossItem(object a,string b,int c,bool d){}}
public static class LevelLossRegistry {public static void Register(string s,Func<LevelLossItem[]> f){}}
public static class PlayerWallet {public static int Coins; public static void AddCoins(int n){Coins+=n;}}
public static class TimedRewardService {public static bool Free; public static bool IsLivesFree()=>Free;}
" + safari + lives + @"
public class Rewards {
 public SafariConfig config=new SafariConfig(); public int LastRewardShare;
 public bool FinalRewardClaimed=>SafariState.RewardClaimed;
 public bool IsEventAvailable=true; public static DateTime UtcNow=DateTime.UtcNow;
" + Methods("Events/Safari/SafariEventController.cs", "ClaimFinalReward", "CanContinueNow") + @"
}
public enum LevelGoalTargetType {Tile,Obstacle}
public enum TileType {Key,Gear}
public enum ObstacleId {KeyGenerator,Grass}
public class LevelGoalDefinition {public LevelGoalTargetType targetType;public TileType tileType;public ObstacleId obstacleId;public int amount;}
public class LevelData {public LevelGoalDefinition[] goals;}
public class KeyQuota {
 public BoardController board;
" + Methods("Grid/Board/Obstacles/KeyGeneratorService.cs", "ResolveKeyGoalAmount").Replace("private int", "public int") + @"
}
public class BoardAction {}
public struct LightningLineStrike {public Vector2Int originCell;public bool isHorizontal;public LightningLineStrike(int x,int y,bool h){originCell=new Vector2Int(x,y);isHorizontal=h;}}
public class CascadeLogic {public int passes;public List<BoardAction> CalculateCascades(){passes++;return new List<BoardAction>{new BoardAction()};}}
public class BoardController {
 public LevelData ActiveLevelData;
 public int Width=3,Height=3,falls,resolves; public CascadeLogic CascadeLogic=new CascadeLogic();
 public Dictionary<Vector2Int,int> reservations=new Dictionary<Vector2Int,int>();
 public List<IEnumerator> routines=new List<IEnumerator>();
 public void ReserveLineSweepCell(Vector2Int c){reservations.TryGetValue(c,out int n);reservations[c]=n+1;}
 public void ReleaseLineSweepCell(Vector2Int c){if(!reservations.TryGetValue(c,out int n))return;if(n==1)reservations.Remove(c);else reservations[c]=n-1;}
 public void StartCoroutine(IEnumerator e){if(e.MoveNext())routines.Add(e);}
 public void StartImmediateAction(BoardAction a){falls++;} public void RefreshAllSortingOrders(){}
 public void RequestResolveAfterActionSequence(){resolves++;}
 public void Tick(){var copy=routines.ToArray();routines.Clear();foreach(var e in copy)if(e.MoveNext())routines.Add(e);}
}
" + scope + @"
public static class SweepWait {
" + Methods("Grid/Board/BoardAnimator.cs", "WaitForLightningSweep").Replace("private static", "public static") + @"
}
public static class Checks {
 static int count; static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
 public static int Run(){
  var now=DateTime.UtcNow; Rewards.UtcNow=now; SafariState.SyncCycle(new SafariConfig(),now);
  SafariState.BeginRun(now);SafariState.SetPitstop(7);SafariState.SetRunStatus(SafariRunStatus.Completed);SafariState.StartFallCooldown(now,30);
  var r=new Rewards();r.ClaimFinalReward(20,5);new Rewards().ClaimFinalReward(20,5);
  Check(PlayerWallet.Coins==20,""Reloading controller cannot claim the completed reward twice"");
  Check(!r.CanContinueNow(out var remaining)&&remaining==TimeSpan.FromMinutes(30),""Completed race shows 30 minute cooldown"");
  Rewards.UtcNow=now.AddMinutes(31);Check(!r.CanContinueNow(out remaining),""Expired completed race still requires rejoin"");
  SafariState.BeginRun(Rewards.UtcNow);Check(SafariState.CurrentPitstop==0&&!SafariState.RewardClaimed&&r.CanContinueNow(out remaining),""Rejoin resets progress and reward guard"");
  r.ClaimFinalReward(20,5);Check(PlayerWallet.Coins==20,""Unfinished run cannot claim"");
  var midnight=now.Date.AddDays(1);SafariState.StartFallCooldown(midnight.AddMinutes(-10),30);SafariState.SyncCycle(new SafariConfig(),midnight);
  Check(SafariState.FallCooldownRemaining(midnight)==TimeSpan.FromMinutes(20),""Cycle rollover preserves the remaining cooldown"");
  PlayerPrefs.SetInt(""lives_current"",1);LivesManager.Initialize();Check(LivesManager.SpendLife()&&!LivesManager.HasLives,""Last loss consumes life and blocks retry"");
  Check(!LivesManager.SpendLife()&&LivesManager.Current==0,""Zero lives cannot be spent"");
  TimedRewardService.Free=true;Check(LivesManager.HasLives&&LivesManager.SpendLife()&&LivesManager.Current==0,""Timed free lives work without manufacturing lives"");
  var b=new BoardController();var strikes=new[]{new LightningLineStrike(1,1,true)};
  var first=new LineSweepGravityScope(b,strikes);var second=new LineSweepGravityScope(b,strikes);
  var left=new Vector2Int(0,1);first.Reach(0,left);first.Reach(0,left);b.Tick();
  Check(b.falls==0&&b.CascadeLogic.passes==0&&b.reservations[left]==2,""Beam impacts cannot start competing falls or release partial gravity paths"");
  Check(b.reservations[new Vector2Int(2,1)]==2,""Unvisited cells remain anchored during early gravity"");
  second.Reach(0,left);Check(b.reservations[left]==2,""Passed cells stay anchored until their clear passes commit"");
  first.Dispose();Check(b.reservations[left]==1,""Finishing one pass preserves overlapping sweep ownership"");
  first.Dispose();Check(b.reservations[left]==1&&b.resolves==1,""Repeated disposal cannot release another sweep or request duplicate resolve"");
  second.Dispose();b.Tick();Check(b.reservations.Count==0&&b.falls==0&&b.resolves==2,""Cancellation releases every anchor and delegates gravity to normal resolve"");
  var cross=new LineSweepGravityScope(b,new[]{new LightningLineStrike(1,1,true),new LightningLineStrike(1,1,false)});
  var center=new Vector2Int(1,1);cross.Reach(0,center);Check(b.reservations.ContainsKey(center),""Cross intersection waits for both strike callbacks"");
  cross.Reach(1,center);Check(b.reservations.ContainsKey(center)&&cross.HasPendingHits,""Cross intersection stays anchored while other impacts are pending"");
  cross.Reach(0,new Vector2Int(0,1));cross.Reach(0,new Vector2Int(2,1));
  cross.Reach(1,new Vector2Int(1,0));cross.Reach(1,new Vector2Int(1,2));
  Check(!cross.HasPendingHits&&b.reservations.Count==5,""All cross impacts complete without exposing partial fall paths"");cross.Dispose();
  Check(b.reservations.Count==0,""Completed cross releases its full footprint"");
  var vertical=new LineSweepGravityScope(b,new[]{new LightningLineStrike(1,1,false)});
  for(int y=0;y<3;y++){vertical.Reach(0,new Vector2Int(1,y));b.Tick();Check(b.falls==0&&b.CascadeLogic.passes==0&&b.reservations.Count==3,""Successive LineV frames cannot replan a falling tile"");}
  Time.time=0.1f;var done=SweepWait.WaitForLightningSweep(vertical,0f,0.5f);
  Check(!done.MoveNext(),""Final impact releases the wait without waiting for the visual tail"");vertical.Dispose();
  var missing=new LineSweepGravityScope(b,strikes);Time.time=0.3f;
  var fallback=SweepWait.WaitForLightningSweep(missing,0f,0.5f);
  Check(fallback.MoveNext(),""Missing callbacks initially wait for real impacts"");
  Time.time=0.51f;Check(fallback.MoveNext(),""Estimated playback ending must not close unfinished hits"");
  Time.time=1.11f;Check(!fallback.MoveNext(),""Missing callbacks eventually time out without deadlocking"");missing.Dispose();
  Time.time=1f;Check(!SweepWait.WaitForLightningSweep(null,0f,0.5f).MoveNext(),""Already elapsed playback adds no extra wait"");
  Time.time=0f;var slow=new LineSweepGravityScope(b,strikes);
  var slowWait=SweepWait.WaitForLightningSweep(slow,0f,.405f);
  Time.time=.35f;slow.Reach(0,left);Check(slowWait.MoveNext(),""Slow sweep waits for remaining cells"");
  Time.time=.6f;Check(slowWait.MoveNext()&&b.reservations.Count==3,""Low FPS cannot release anchors at estimated duration"");
  Time.time=.95f;slow.Reach(0,new Vector2Int(2,1));
  Time.time=1.3f;Check(slowWait.MoveNext(),""New impacts renew the stall watchdog beyond total estimated time"");
  slow.Reach(0,left);Check(slow.LastProgressAt==.95f,""Duplicate callbacks cannot keep a stalled sweep alive"");
  Time.time=1.4f;slow.Reach(0,new Vector2Int(1,1));Check(!slowWait.MoveNext(),""Last real impact completes immediately even after a slow sweep"");slow.Dispose();
  var quota=new KeyQuota{board=new BoardController{ActiveLevelData=new LevelData{goals=new[]{
   new LevelGoalDefinition{targetType=LevelGoalTargetType.Tile,tileType=TileType.Key,amount=10},
   new LevelGoalDefinition{targetType=LevelGoalTargetType.Obstacle,obstacleId=ObstacleId.KeyGenerator,amount=15}}}}};
  Check(quota.ResolveKeyGoalAmount()==15,""A smaller key goal cannot close generators five keys early"");
  Array.Reverse(quota.board.ActiveLevelData.goals);Check(quota.ResolveKeyGoalAmount()==15,""Production quota is independent of goal ordering"");
  return count;
 }
}
Checks.Run()";
var result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " Safari, lives and overlapping Line gravity regression checks");
