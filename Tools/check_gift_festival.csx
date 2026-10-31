// Run: csi Tools/check_gift_festival.csx. Isolated production rule/state tests; no Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#r "System.Web.Extensions"
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

string root = "Assets/_Project/Scripts/Events/GiftFestival/";
foreach (var file in Directory.GetFiles(root, "*.cs").Concat(new[]{"Assets/_Project/Scripts/Editor/GiftFestivalDebugMenu.cs"}))
{
    var errors = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview))
        .GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
    if (errors.Length > 0) throw new Exception(file + ": " + string.Join("\n", errors.Select(x => x.ToString())));
}

string production = string.Join("\n", new[]{"GiftFestivalRules.cs", "GiftFestivalRewards.cs", "GiftFestivalState.cs"}
    .Select(x => Regex.Replace(File.ReadAllText(root + x), @"(?m)^using [^\r\n]+;\r?$", "")))
    + "\n" + Regex.Replace(File.ReadAllText("Assets/_Project/Scripts/Core/PlayerStats.cs"), @"(?m)^using [^\r\n]+;\r?$", "");
string configYaml = File.ReadAllText("Assets/_Project/Resources/Events/GiftFestivalConfig.asset");
string actualGreen = Regex.Match(configYaml, @"(?m)^  greenWidth: ([\d.]+)").Groups[1].Value;
// The installed Mono interactive compiler predates target-typed coalescing assignment.
production = production.Replace("data.delivery ??= Array.Empty<DailySlotReward>();", "if (data.delivery == null) data.delivery = Array.Empty<DailySlotReward>();");
string harness = @"
using System;
using System.Collections.Generic;
 public enum RuntimeInitializeLoadType { BeforeSceneLoad }
 public class RuntimeInitializeOnLoadMethodAttribute : Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType x) {}}
 public static class Mathf {
  public static int Max(int a,int b)=>Math.Max(a,b);
  public static int Clamp(int x,int a,int b)=>Math.Max(a,Math.Min(b,x));
 }
 public static class PlayerPrefs {
  public static Dictionary<string,string> Text=new Dictionary<string,string>();
  public static string GetString(string k,string fallback="""")=>Text.TryGetValue(k,out var s)?s:fallback;
  public static void SetString(string k,string v){Text[k]=v;}
  public static int GetInt(string k,int fallback=0)=>int.TryParse(GetString(k),out var value)?value:fallback;
  public static void SetInt(string k,int value){Text[k]=value.ToString();}
  public static void DeleteKey(string k){Text.Remove(k);} public static void Save(){}
 }
 public static class JsonUtility {
  static readonly System.Web.Script.Serialization.JavaScriptSerializer Serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
  public static string ToJson(object x)=>Serializer.Serialize(x);
  public static T FromJson<T>(string json)=>Serializer.Deserialize<T>(json);
 }
public static class RuntimeSimulationSession {public static bool IsActive;}
public static class FirebaseCloudSaveService {public static void MarkDirty(){}}
public static class CurrentLevel {public static int Global=1;}
public static class LevelAttemptStats {public static void RecordWin(int level){}public static void RecordGiveUp(int level){}}
public class Sprite {}
public class GiftFestivalArt {public Sprite ball;}
public readonly struct LevelLossItem {
 public readonly int Amount;public readonly bool Achieved;
 public LevelLossItem(Sprite icon,string label,int amount,bool achieved){Amount=amount;Achieved=achieved;}
}
public static class LevelLossRegistry {
 public static Dictionary<string,Func<IEnumerable<LevelLossItem>>> Providers=new Dictionary<string,Func<IEnumerable<LevelLossItem>>>();
 public static void Register(string key,Func<IEnumerable<LevelLossItem>> provider){Providers[key]=provider;}
}
public static class GameLocalization {public static string GetOr(string key,string fallback)=>fallback;}
public enum DailySlotRewardType {Coins=0,Lives=1,Stars=2,Joker_PulseCore=11,Joker_SystemOverride=12,Joker_Line=13,Booster_Hammer=20,Booster_Shuffle=23,Booster_PaintBucket=24,Empty=99}
public class DailySlotReward {public DailySlotRewardType type;public int amount,durationMinutes;public bool IsTimed=>durationMinutes>0;}
public static class LivesManager {public static void AddLivesWithCheckpoint(int amount,string key,string json){PlayerPrefs.SetString(key,json);PlayerPrefs.Save();}}
public static class PlayerWallet {
 public static int Coins,Stars;public static bool CrashAfterSave,CrashAfterStarSave;
 public static void AddCoinsWithCheckpoint(int amount,string key,string json){
  Coins+=amount;PlayerPrefs.SetString(key,json);PlayerPrefs.Save();
  if(CrashAfterSave){CrashAfterSave=false;throw new Exception(""simulated crash after coin checkpoint"");}
 }
 public static void AddStarsWithCheckpoint(int amount,string key,string json){
  Stars+=amount;PlayerPrefs.SetString(key,json);PlayerPrefs.Save();
  if(CrashAfterStarSave){CrashAfterStarSave=false;throw new Exception(""simulated crash after star checkpoint"");}
 }
}
public static class DailySlotRewardService {
 public static int Items;public static bool CrashAfterSave;
 public static void Grant(DailySlotReward reward){
  if(reward==null)return;Items+=reward.amount;PlayerPrefs.Save();
  if(CrashAfterSave){CrashAfterSave=false;throw new Exception(""simulated crash after item checkpoint"");}
 }
}
public class GiftFestivalRound {
 public int size=2,boxCoins=25,completionCoins=100;
 public DailySlotReward[] boxRewards=new DailySlotReward[0];
 public DailySlotReward[] completionRewards=new DailySlotReward[0];public int Size=>Math.Max(2,Math.Min(4,size));
}
public class GiftFestivalConfig {
 public static GiftFestivalConfig Shared;
 public int unlockAfterLevel=100,activeHours=48,restHours=120;
 public GiftFestivalArt Art=new GiftFestivalArt();
 public float redWidth=.15f,greenWidth=.30f;
 public float emptyBoxFraction;
 public GiftFestivalRound[] rounds={new GiftFestivalRound(),new GiftFestivalRound{size=3,boxCoins=40,completionCoins=250},new GiftFestivalRound{size=4,boxCoins=60,completionCoins=500}};
 public int RoundCount=>rounds.Length;
 public GiftFestivalRound GetRound(int i)=>i>=0&&i<rounds.Length?rounds[i]:null;
 public float SafeRedWidth=>GiftFestivalRules.RedWidth(redWidth);
 public float SafeGreenWidth=>GiftFestivalRules.GreenWidth(redWidth,greenWidth);
}
" + production + @"
public static class Checks {
 static int count;
 static readonly DateTime Now=new DateTime(2026,10,30,10,0,0,DateTimeKind.Utc);
 static void Check(bool ok,string text){count++;if(!ok)throw new Exception(text);}
 static void Forget(){
  var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
  typeof(GiftFestivalState).GetField(""data"",flags).SetValue(null,null);
  typeof(GiftFestivalState).GetField(""lastJson"",flags).SetValue(null,null);
 }
 static GiftFestivalConfig Reset(){
  PlayerPrefs.Text.Clear();Forget();RuntimeSimulationSession.IsActive=false;PlayerWallet.Coins=0;PlayerWallet.Stars=0;PlayerWallet.CrashAfterStarSave=false;
  PlayerWallet.CrashAfterSave=false;DailySlotRewardService.Items=0;DailySlotRewardService.CrashAfterSave=false;
  CurrentLevel.Global=101;GiftFestivalConfig.Shared=new GiftFestivalConfig();return GiftFestivalConfig.Shared;
 }
 static void Balls(int n){GiftFestivalState.Data.balls=n;PlayerPrefs.SetString(GiftFestivalState.SaveKey,JsonUtility.ToJson(GiftFestivalState.Data));}
 public static int Run(){
  var cfg=Reset();
  Check(GiftFestivalRules.GetWindow(Now,Now,48,120).Active,""First instant active"");
  Check(GiftFestivalRules.GetWindow(Now,Now.AddHours(48).AddTicks(-1),48,120).Active,""Last active tick included"");
  Check(!GiftFestivalRules.GetWindow(Now,Now.AddHours(48),48,120).Active,""Exact 48-hour deadline expired"");
  Check(!GiftFestivalRules.GetWindow(Now,Now.AddHours(168).AddTicks(-1),48,120).Active,""Full five-day rest"");
  var w=GiftFestivalRules.GetWindow(Now,Now.AddHours(168),48,120);
  Check(w.Active&&w.Cycle==1&&w.Start==Now.AddDays(7),""Next 48-hour festival starts after five rest days"");
  Check(!GiftFestivalRules.GetWindow(Now,Now.AddTicks(-1),48,120).Active,""Clock before anchor is unavailable"");
  for(int cycle=0;cycle<200;cycle++){
   var at=Now.AddDays(cycle*7);var a=GiftFestivalRules.GetWindow(Now,at,48,120);
   Check(a.Active&&a.Cycle==cycle&&a.End==at.AddHours(48)&&a.NextStart==at.AddDays(7),""Long-term schedule drift"");
  }
  Check(GiftFestivalRules.Evaluate(.15f,.15f,.3f)==GiftFestivalRules.Hit.Down,""Red boundary belongs to yellow"");
  Check(GiftFestivalRules.Evaluate(.35f,.15f,.3f)==GiftFestivalRules.Hit.Perfect,""Green boundary included"");
  Check(GiftFestivalRules.Evaluate(float.NaN,.15f,.3f)==GiftFestivalRules.Hit.Miss,""NaN cannot award an exact hit"");
  foreach(int size in new[]{2,3,4})for(int selected=0;selected<size*size;selected++)for(int n=0;n<=1000;n++){
   var hit=GiftFestivalRules.Evaluate(n/1000f,.15f,.3f);int target=GiftFestivalRules.TargetCell(selected,size,hit);
   if(hit==GiftFestivalRules.Hit.Miss)Check(target==-1,""Red always misses"");
   else if(hit==GiftFestivalRules.Hit.Perfect)Check(target==selected,""Green always hits selected box"");
   else Check(target>=0&&target<size*size&&target!=selected&&target%size==selected%size&&Math.Abs(target-selected)==size,""Yellow hits exactly one vertical neighbour including edge reflection"");
  }
  CurrentLevel.Global=100;GiftFestivalState.Sync(cfg,Now);
  Check(!GiftFestivalState.IsUnlocked(cfg)&&GiftFestivalState.Data.anchorTicks==0,""No unlock before completing 100"");
  CurrentLevel.Global=101;GiftFestivalState.Sync(cfg,Now);
  Check(GiftFestivalState.IsLive(cfg,Now)&&GiftFestivalState.Balls==0&&!GiftFestivalState.HasJoined,""Unlock makes invitation available without joining or granting balls"");
  GiftFestivalState.Shot shot;
  Check(!GiftFestivalState.TryHit(cfg,0,.5f,Now,out shot),""Unjoined players cannot take a shot"");
  Check(GiftFestivalState.TryJoin(cfg,Now)&&GiftFestivalState.HasJoined&&GiftFestivalState.Balls==0,""Explicit join alone never grants a ball"");
  Check(GiftFestivalState.TryJoin(cfg,Now)&&GiftFestivalState.Balls==0,""Repeated joins do not grant balls"");
  Balls(3); // Seed earned balls for shot/reward persistence scenarios.
  GiftFestivalState.Sync(cfg,Now.AddMinutes(10));Forget();GiftFestivalState.Sync(cfg,Now.AddMinutes(11));
  Check(GiftFestivalState.Balls==3,""Sync and reload never repeat starter balls"");
  Check(!GiftFestivalState.TryHit(cfg,-1,.5f,Now,out shot)&&GiftFestivalState.Balls==3,""Invalid selection cannot spend"");
  Check(GiftFestivalState.TryHit(cfg,0,0,Now,out shot)&&!shot.Opened&&GiftFestivalState.Balls==2&&PlayerWallet.Coins==0,""Miss spends one and grants nothing"");
  Check(GiftFestivalState.TryHit(cfg,0,.5f,Now,out shot)&&shot.Opened&&shot.Target==0&&GiftFestivalState.Balls==1&&PlayerWallet.Coins==25,""Perfect hit opens selected box once"");
  Check(!GiftFestivalState.TryHit(cfg,0,.5f,Now,out shot)&&GiftFestivalState.Balls==1&&PlayerWallet.Coins==25,""Opened selection cannot be replayed"");
  Check(GiftFestivalState.TryHit(cfg,1,.2f,Now,out shot)&&shot.Target==3&&shot.Opened&&GiftFestivalState.Balls==0&&PlayerWallet.Coins==50,""Yellow spends one and awards actual neighbour"");
  Check(!GiftFestivalState.TryHit(cfg,2,.5f,Now,out shot)&&GiftFestivalState.OpenMask==9,""No balls cannot mutate board"");
  Balls(10);
  Check(GiftFestivalState.TryHit(cfg,1,.2f,Now,out shot)&&!shot.Opened&&GiftFestivalState.Balls==9&&PlayerWallet.Coins==50,""Yellow on an already opened neighbour grants no duplicate"");
  GiftFestivalState.TryHit(cfg,1,.5f,Now,out shot);GiftFestivalState.TryHit(cfg,2,.5f,Now,out shot);
  Check(shot.Completed&&GiftFestivalState.Round==1&&GiftFestivalState.OpenMask==0&&PlayerWallet.Coins==200,""2x2 board grants four box rewards and one bonus"");
  Forget();GiftFestivalState.Sync(cfg,Now.AddMinutes(20));
  Check(GiftFestivalState.Round==1&&GiftFestivalState.Balls==7&&PlayerWallet.Coins==200,""Board and balance survive closing/reloading"");
  GiftFestivalState.Sync(cfg,Now.AddHours(48));
  Check(!GiftFestivalState.IsLive(cfg,Now.AddHours(48))&&!GiftFestivalState.TryHit(cfg,0,.5f,Now.AddHours(48),out shot),""Expiry rejects new shots"");
  GiftFestivalState.Sync(cfg,Now.AddDays(7));
  Check(GiftFestivalState.Round==0&&GiftFestivalState.OpenMask==0&&GiftFestivalState.Balls==0&&!GiftFestivalState.HasJoined&&PlayerWallet.Coins==200,""New season resets participation, board and balls while retaining rewards"");
  Check(GiftFestivalState.TryJoin(cfg,Now.AddDays(7))&&GiftFestivalState.Balls==0,""New cycle rejoin never grants starters"");

  cfg=Reset();GiftFestivalState.TryJoin(cfg,Now);Balls(100);
  for(int round=0;round<3;round++){
   int side=cfg.rounds[round].size;
   for(int cell=0;cell<side*side;cell++)Check(GiftFestivalState.TryHit(cfg,cell,.5f,Now,out shot),""All board sizes are completable"");
  }
  Check(GiftFestivalState.IsFinished(cfg)&&GiftFestivalState.Balls==71&&PlayerWallet.Coins==2270,""29 gifts and three completion bonuses paid once"");
  Check(!GiftFestivalState.TryHit(cfg,0,.5f,Now,out shot)&&PlayerWallet.Coins==2270,""Final completion cannot duplicate prizes"");

  cfg=Reset();GiftFestivalState.TryJoin(cfg,Now);Balls(3);PlayerWallet.CrashAfterSave=true;
  try{GiftFestivalState.TryHit(cfg,0,.5f,Now,out shot);}catch(Exception){}
  Forget();GiftFestivalState.Sync(cfg,Now);
  Check(PlayerWallet.Coins==25&&GiftFestivalState.Balls==2&&GiftFestivalState.OpenMask==1,""Crash after wallet checkpoint resumes without duplicate coins or lost shot"");
  cfg=Reset();cfg.rounds[0].completionRewards=new[]{new DailySlotReward{type=DailySlotRewardType.Booster_Hammer,amount=2}};
  GiftFestivalState.TryJoin(cfg,Now);Balls(8);
  for(int cell=0;cell<3;cell++)GiftFestivalState.TryHit(cfg,cell,.5f,Now,out shot);
  DailySlotRewardService.CrashAfterSave=true;
  try{GiftFestivalState.TryHit(cfg,3,.5f,Now,out shot);}catch(Exception){}
  Forget();GiftFestivalState.Sync(cfg,Now);
  Check(DailySlotRewardService.Items==2&&PlayerWallet.Coins==200&&GiftFestivalState.Round==1,""Interrupted item delivery resumes without a second item grant"");

  var mixed=new[]{
   new DailySlotReward{type=DailySlotRewardType.Coins,amount=35},new DailySlotReward{type=DailySlotRewardType.Joker_Line,amount=1},
   new DailySlotReward{type=DailySlotRewardType.Booster_Hammer,amount=1},new DailySlotReward{type=DailySlotRewardType.Stars,amount=1}};
  foreach(int side in new[]{2,3,4})for(int seed=1;seed<=128;seed++){
   var def=new GiftFestivalRound{size=side,boxRewards=mixed};
   var a=GiftFestivalRewards.Create(def,.25f,seed,side-2,0);var b=GiftFestivalRewards.Create(def,.25f,seed,side-2,0);
   int empty=0;
   for(int i=0;i<a.Length;i++){
    if(GiftFestivalRewards.IsEmpty(a[i]))empty++;
    Check(a[i].type==b[i].type&&a[i].amount==b[i].amount,""Prize layout is stable for seed/board"");
   }
   Check(empty==(side==2?1:side==3?2:4),""Fresh boards contain exactly 1/4, 2/9, 4/16 empty boxes"");
  }
  cfg=Reset();cfg.emptyBoxFraction=.25f;foreach(var def in cfg.rounds)def.boxRewards=mixed;
  GiftFestivalState.TryJoin(cfg,Now);Balls(100);
  int emptyCell=Array.FindIndex(GiftFestivalState.Data.boxPrizes,r=>GiftFestivalRewards.IsEmpty(r));
  string snapshot=JsonUtility.ToJson(GiftFestivalState.Data.boxPrizes);
  Check(GiftFestivalState.TryHit(cfg,emptyCell,.5f,Now,out shot)&&shot.Opened&&shot.Empty&&GiftFestivalState.Balls==99&&PlayerWallet.Coins==0&&PlayerWallet.Stars==0&&DailySlotRewardService.Items==0,""An empty box opens, spends one ball and awards nothing"");
  Check(!GiftFestivalState.TryHit(cfg,emptyCell,.5f,Now,out shot)&&GiftFestivalState.Balls==99,""An empty box cannot be opened twice"");
  Forget();GiftFestivalState.Sync(cfg,Now);
  Check(JsonUtility.ToJson(GiftFestivalState.Data.boxPrizes)==snapshot&&GiftFestivalRules.IsOpen(GiftFestivalState.OpenMask,emptyCell),""Reload preserves both hidden prizes and opened empty box"");
  int expectedCoins=0,expectedStars=0,expectedItems=0;
  for(int round=0;round<3;round++){
   var prizes=GiftFestivalState.Data.boxPrizes;var def=cfg.rounds[round];
   foreach(var reward in prizes)if(!GiftFestivalRewards.IsEmpty(reward)){
    if(reward.type==DailySlotRewardType.Coins)expectedCoins+=reward.amount;
    else if(reward.type==DailySlotRewardType.Stars)expectedStars+=reward.amount;
    else expectedItems+=reward.amount;
   }
   expectedCoins+=def.completionCoins;
   for(int cell=0;cell<def.Size*def.Size;cell++){
    if(round==0&&cell==emptyCell)continue;
    Check(GiftFestivalState.TryHit(cfg,cell,.5f,Now,out shot),""Mixed/empty boards remain completable"");
   }
  }
  Check(GiftFestivalState.IsFinished(cfg)&&GiftFestivalState.Balls==71&&PlayerWallet.Coins==expectedCoins&&PlayerWallet.Stars==expectedStars&&DailySlotRewardService.Items==expectedItems,""All actual mixed prizes and bonuses are delivered exactly once"");
  cfg=Reset();cfg.rounds[0].boxRewards=new[]{new DailySlotReward{type=DailySlotRewardType.Stars,amount=1}};
  GiftFestivalState.TryJoin(cfg,Now);Balls(3);PlayerWallet.CrashAfterStarSave=true;
  try{GiftFestivalState.TryHit(cfg,0,.5f,Now,out shot);}catch(Exception){}
  Forget();GiftFestivalState.Sync(cfg,Now);
  Check(PlayerWallet.Stars==1&&GiftFestivalState.Balls==2&&GiftFestivalState.OpenMask==1,""Star delivery checkpoint survives interruption without duplicate stars"");

  cfg=Reset();CurrentLevel.Global=100;GiftFestivalState.Sync(cfg,Now);
  Check(!GiftFestivalState.CanShowInvitation(cfg,Now)&&!GiftFestivalState.MarkInvitationShown(cfg,Now),""Locked players cannot receive or consume the invitation"");
  CurrentLevel.Global=101;GiftFestivalState.Sync(cfg,Now);
  Check(GiftFestivalState.CanShowInvitation(cfg,Now),""First eligible cycle has a pending invitation"");
  Check(GiftFestivalState.MarkInvitationShown(cfg,Now)&&!GiftFestivalState.CanShowInvitation(cfg,Now),""Showing an invitation consumes its automatic reminder"");
  Check(!GiftFestivalState.MarkInvitationShown(cfg,Now)&&!GiftFestivalState.HasJoined&&GiftFestivalState.Balls==0,""Viewing/declining never enrolls or grants starter balls"");
  Forget();GiftFestivalState.Sync(cfg,Now.AddHours(1));
  Check(!GiftFestivalState.CanShowInvitation(cfg,Now.AddHours(1))&&GiftFestivalState.Data.invitationShown,""Reminder receipt survives menu/app reloads"");
  GiftFestivalState.Sync(cfg,Now.AddHours(48));
  Check(!GiftFestivalState.CanShowInvitation(cfg,Now.AddHours(48))&&!GiftFestivalState.MarkInvitationShown(cfg,Now.AddHours(48)),""No reminder during the five-day rest"");
  GiftFestivalState.Sync(cfg,Now.AddDays(7));
  Check(GiftFestivalState.CanShowInvitation(cfg,Now.AddDays(7))&&!GiftFestivalState.Data.invitationShown,""A new cycle resets the reminder receipt"");
  RuntimeSimulationSession.IsActive=true;
  Check(!GiftFestivalState.CanShowInvitation(cfg,Now.AddDays(7))&&!GiftFestivalState.MarkInvitationShown(cfg,Now.AddDays(7))&&!GiftFestivalState.Data.invitationShown,""Simulation neither shows nor consumes reminders"");
  RuntimeSimulationSession.IsActive=false;
  Check(GiftFestivalState.TryJoin(cfg,Now.AddDays(7))&&!GiftFestivalState.CanShowInvitation(cfg,Now.AddDays(7)),""Already joined players are not reminded"");

  typeof(GiftFestivalState).GetMethod(""RegisterHooks"",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
  typeof(GiftFestivalState).GetMethod(""RegisterHooks"",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
  cfg=Reset();CurrentLevel.Global=99;PlayerStats.Win();Check(GiftFestivalState.Balls==0,""Level 99 cannot earn balls"");
  CurrentLevel.Global=100;PlayerStats.Win();Check(GiftFestivalState.Balls==0&&!GiftFestivalState.HasJoined&&GiftFestivalState.Data.anchorTicks>0,""Winning level 100 starts the cycle without earning before join"");
  CurrentLevel.Global=101;PlayerStats.Win();Check(GiftFestivalState.Balls==0,""Unjoined level wins award nothing"");
  Check(LevelLossRegistry.Providers[""giftfestival""]()==null,""No ball loss warning before joining"");
  CurrentLevel.Global=102;GiftFestivalState.TryJoin(cfg,DateTime.UtcNow);
  Check(GiftFestivalState.Balls==0,""Joining after multiple wins grants no retroactive win balls"");
  CurrentLevel.Global=101;PlayerStats.Win();Check(GiftFestivalState.Balls==0,""A pre-join win cannot be replayed after joining"");
  CurrentLevel.Global=102;PlayerStats.Win();Check(GiftFestivalState.Balls==1,""A new joined level win earns exactly one ball"");
  PlayerStats.Win();Check(GiftFestivalState.Balls==1,""Duplicate joined win signal is idempotent"");
  CurrentLevel.Global=103;PlayerStats.Win();Check(GiftFestivalState.Balls==2,""Next joined new level earns one ball"");
  RuntimeSimulationSession.IsActive=true;CurrentLevel.Global=104;PlayerStats.Win();Check(GiftFestivalState.Balls==2,""Simulation never mutates event economy"");
  RuntimeSimulationSession.IsActive=false;
  var losses=new List<LevelLossItem>(LevelLossRegistry.Providers[""giftfestival""]());
  Check(losses.Count==1&&losses[0].Amount==1&&!losses[0].Achieved&&GiftFestivalState.Balls==2,""Fail warning lists only one potential ball and preserves the banked balance"");
  // Current production config, using the REAL PlayerStats success/failure methods.
  cfg=Reset();cfg.greenWidth=/* ACTUAL GREEN */f;
  Check(Math.Abs(cfg.greenWidth-.2f)<.00001f,""Production config has no join bonus and a 20 percent green zone"");
  GiftFestivalState.TryJoin(cfg,DateTime.UtcNow);
  Check(GiftFestivalState.Balls==0,""Joining without completing a level grants no balls"");
  for(int attempt=0;attempt<5;attempt++){
   GiftFestivalState.Sync(cfg,DateTime.UtcNow);PlayerStats.MarkCurrentLevelFailed();
   Forget();GiftFestivalState.Sync(cfg,DateTime.UtcNow);
   Check(GiftFestivalState.Balls==0,""Real failure, retry and state reload do not grant balls"");
  }
  PlayerStats.RecordLevelCleared();
  Check(GiftFestivalState.Balls==1,""A genuine success after multiple failures awards exactly one ball"");
  PlayerStats.RecordLevelCleared();Check(GiftFestivalState.Balls==1,""Repeated success notification for the same level cannot award twice"");
  CurrentLevel.Global++;PlayerStats.MarkCurrentLevelFailed();
  Check(GiftFestivalState.Balls==1,""Losing the next level preserves the bank without adding a ball"");
  PlayerStats.RecordLevelCleared();Check(GiftFestivalState.Balls==2,""Next genuinely completed level awards the second ball"");
  Forget();GiftFestivalState.Sync(cfg,DateTime.UtcNow);GiftFestivalState.TryJoin(cfg,DateTime.UtcNow);
  Check(GiftFestivalState.Balls==2,""Reopening/rejoining never removes banked balls or adds another grant"");
  int perfect=0,misses=0;
  for(int sample=0;sample<1000;sample++){
   var hit=GiftFestivalRules.Evaluate((sample+.5f)/1000f,cfg.redWidth,cfg.greenWidth);
   if(hit==GiftFestivalRules.Hit.Perfect)perfect++;if(hit==GiftFestivalRules.Hit.Miss)misses++;
  }
  Check(perfect==200&&misses==300,""Exactly 20 percent of meter samples hit the chosen cell, with red still 30 percent total"");
  Check(GiftFestivalRules.Evaluate(.39f,cfg.redWidth,cfg.greenWidth)==GiftFestivalRules.Hit.Down
    &&GiftFestivalRules.Evaluate(.61f,cfg.redWidth,cfg.greenWidth)==GiftFestivalRules.Hit.Up,""Former green edges now deflect into the yellow zones"");

  cfg=Reset();
  Check(!GiftFestivalState.TryBeginTutorial(cfg,Now)&&GiftFestivalState.Balls==0,""An invitation alone cannot start the tutorial or grant a ball"");
  GiftFestivalState.TryJoin(cfg,Now);
  Check(GiftFestivalState.TryBeginTutorial(cfg,Now)&&GiftFestivalState.Balls==1&&!GiftFestivalState.TutorialCompleted,""First on-screen tutorial grants exactly one free ball"");
  for(int reopen=0;reopen<5;reopen++){
   Forget();GiftFestivalState.Sync(cfg,Now);GiftFestivalState.TryJoin(cfg,Now);
   Check(GiftFestivalState.TryBeginTutorial(cfg,Now)&&GiftFestivalState.Balls==1,""Closing before the shot resumes without another ball"");
  }
  Check(!GiftFestivalState.TryHit(cfg,-1,.5f,Now,out shot)&&!GiftFestivalState.TutorialCompleted&&GiftFestivalState.Balls==1,""Invalid tutorial shot cannot consume the receipt or ball"");
  Check(GiftFestivalState.TryHit(cfg,0,.5f,Now,out shot)&&GiftFestivalState.Balls==0&&GiftFestivalState.TutorialCompleted,""One tutorial shot spends its ball and durably completes introduction"");
  Forget();GiftFestivalState.Sync(cfg,Now);
  Check(!GiftFestivalState.TryBeginTutorial(cfg,Now)&&GiftFestivalState.Balls==0,""Completed tutorial never returns after reload"");
  GiftFestivalState.TryJoin(cfg,Now.AddDays(7));
  Check(!GiftFestivalState.TryBeginTutorial(cfg,Now.AddDays(7))&&GiftFestivalState.Balls==0&&GiftFestivalState.TutorialCompleted,""Next festival has no tutorial or new starter grant"");

  cfg=Reset();GiftFestivalState.TryJoin(cfg,Now);GiftFestivalState.TryBeginTutorial(cfg,Now);Balls(5);
  GiftFestivalState.Sync(cfg,Now.AddHours(48));
  Check(!GiftFestivalState.TryBeginTutorial(cfg,Now.AddHours(48)),""Tutorial cannot start in an expired festival"");
  GiftFestivalState.Sync(cfg,Now.AddDays(7));
  Check(GiftFestivalState.Balls==1&&!GiftFestivalState.HasJoined,""Unspent reserved tutorial shot survives expiry; ordinary bank follows normal reset"");
  GiftFestivalState.TryJoin(cfg,Now.AddDays(7));Forget();
  Check(GiftFestivalState.TryBeginTutorial(cfg,Now.AddDays(7))&&GiftFestivalState.Balls==1,""Reserved introduction resumes next cycle without a fresh grant"");
  GiftFestivalState.TryHit(cfg,0,.5f,Now.AddDays(7),out shot);
  Check(GiftFestivalState.TutorialCompleted&&GiftFestivalState.Balls==0,""Resumed introduction spends exactly its one reserved ball"");

  cfg=Reset();GiftFestivalState.TryJoin(cfg,Now);Balls(4);
  Check(GiftFestivalState.TryBeginTutorial(cfg,Now)&&GiftFestivalState.Balls==5,""One introduction ball preserves existing earned balances"");
  PlayerWallet.CrashAfterSave=true;
  try{GiftFestivalState.TryHit(cfg,0,.5f,Now,out shot);}catch(Exception){}
  Forget();GiftFestivalState.Sync(cfg,Now);
  Check(GiftFestivalState.TutorialCompleted&&GiftFestivalState.Balls==4&&PlayerWallet.Coins==25
   &&!GiftFestivalState.TryBeginTutorial(cfg,Now),""Interrupted reward delivery preserves tutorial completion, spent ball and reward exactly once"");
  cfg=Reset();GiftFestivalState.TryJoin(cfg,Now);RuntimeSimulationSession.IsActive=true;
  Check(!GiftFestivalState.TryBeginTutorial(cfg,Now)&&GiftFestivalState.Balls==0&&!GiftFestivalState.Data.tutorialBallGranted,""Simulation cannot consume the lifetime introduction grant"");
  RuntimeSimulationSession.IsActive=false;
  return count;
 }
}
Checks.Run()".Replace("PlayerStats.Win()", "PlayerStats.RecordLevelCleared()")
    .Replace("/* ACTUAL GREEN */", actualGreen);
var references = ScriptOptions.Default.AddReferences(typeof(System.Web.Script.Serialization.JavaScriptSerializer).Assembly);
int checks = CSharpScript.EvaluateAsync<int>(harness, references).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + checks + " Gift Festival rule, schedule, persistence, reward and win-hook checks; all new C# files parse.");
