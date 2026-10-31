// csi Tools/check_gift_festival_delivery.csx -- real reward/wallet/life/inventory APIs, no Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#r "System.Web.Extensions"
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

string[] paths = {
 "Events/GiftFestival/GiftFestivalRules.cs", "Events/GiftFestival/GiftFestivalRewards.cs", "Events/GiftFestival/GiftFestivalState.cs",
 "Core/Events/DailySlotReward.cs", "Core/Events/DailySlotRewardService.cs", "Core/PlayerWallet.cs", "Core/LivesManager.cs",
 "Core/BoosterInventory.cs", "GamePlay/PreLevelSpecialInventory.cs"
};
string source = string.Join("\n", paths.Select(path => {
 var tree = CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/" + path), new CSharpParseOptions(LanguageVersion.Preview));
 var errors = tree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
 if (errors.Length > 0) throw new Exception(path + ": " + string.Join("\n", errors.Select(x => x.ToString())));
 return string.Join("\n", ((CompilationUnitSyntax)tree.GetRoot()).Members.Select(x => x.ToFullString()));
})).Replace("data.delivery ??= Array.Empty<DailySlotReward>();", "if (data.delivery == null) data.delivery = Array.Empty<DailySlotReward>();")
 .Replace("UnityEngine.Random.Range", "RandomStub.Range");

string harness = @"
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
public class TooltipAttribute:Attribute {public TooltipAttribute(string value){}}
public class MinAttribute:Attribute {public MinAttribute(int value){}}
public enum RuntimeInitializeLoadType {BeforeSceneLoad}
public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value){}}
public static class Mathf {
 public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
 public static int Clamp(int a,int b,int c)=>Math.Max(b,Math.Min(c,a));
}
public static class RandomStub {public static int Next;public static int Range(int a,int b)=>Next;}
public class Sprite {}
public static class TileIconLibrary {public static Library Shared=new Library();public class Library {public Sprite GetRewardIcon(DailySlotRewardType type)=>null;}}
public static class CurrentLevel {public static int Global=101;}
public static class RuntimeSimulationSession {public static bool IsActive;}
public static class FirebaseCloudSaveService {public static void MarkDirty(){}}
public static class CurrencyLedger {public static void RecordCoins(int value){}public static void RecordStars(int value){}}
public static class GameLocalization {
 public static string GetOr(string key,string fallback)=>fallback;
 public static string GetFormat(string key,int value)=>value.ToString();
}
public static class PlayerStats {public static event Action OnLevelCleared;}
public static class LevelAttemptStats {public static void RecordLifeSpent(int level){}public static void RefundLifeSpent(int level){}}
public enum TileSpecial {LineH,LineV,PulseCore,SystemOverride}
public class BoardController {public enum BoosterMode {None,Single,Row,Column,Shuffle,PaintBucket}}
public static class TimedRewardService {
 public static bool SupportsTimed(DailySlotRewardType t)=>t==DailySlotRewardType.Lives;
 public static bool IsLivesFree()=>false;public static bool IsSpecialFree(TileSpecial value)=>false;
 public static void Grant(DailySlotRewardType type,int minutes){}
}
public readonly struct LevelLossItem {public LevelLossItem(Sprite icon,string label,int amount,bool achieved){}}
public static class LevelLossRegistry {public static void Register(string key,Func<IEnumerable<LevelLossItem>> provider){}}
public class GiftFestivalArt {public Sprite ball;}
public class GiftFestivalRound {
 public int size=2,boxCoins=25,completionCoins;public int Size=>size;
 public DailySlotReward[] boxRewards=Array.Empty<DailySlotReward>(),completionRewards=Array.Empty<DailySlotReward>();
}
public class GiftFestivalConfig {
 public static GiftFestivalConfig Shared;public int unlockAfterLevel=100,activeHours=48,restHours=120;
 public float emptyBoxFraction;public GiftFestivalArt Art=new GiftFestivalArt();
 public GiftFestivalRound[] rounds={new GiftFestivalRound()};public int RoundCount=>rounds.Length;
 public GiftFestivalRound GetRound(int i)=>i>=0&&i<rounds.Length?rounds[i]:null;
 public float SafeRedWidth=>.15f;public float SafeGreenWidth=>.2f;
}
public static class JsonUtility {
 static readonly System.Web.Script.Serialization.JavaScriptSerializer serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
 public static string ToJson(object value)=>serializer.Serialize(value);
 public static T FromJson<T>(string value)=>serializer.Deserialize<T>(value);
}
public static class PlayerPrefs {
 public static Dictionary<string,string> Memory=new Dictionary<string,string>(),Disk=new Dictionary<string,string>();
 public static Action OnSaved;
 public static int GetInt(string k,int fallback=0)=>int.TryParse(GetString(k),out var value)?value:fallback;
 public static string GetString(string k,string fallback="""")=>Memory.TryGetValue(k,out var value)?value:fallback;
 public static void SetInt(string k,int value){Memory[k]=value.ToString();}public static void SetString(string k,string value){Memory[k]=value;}
 public static void Save(){Disk=new Dictionary<string,string>(Memory);OnSaved?.Invoke();}
 public static void Restart(){Memory=new Dictionary<string,string>(Disk);OnSaved=null;}
 public static void Clear(){Memory.Clear();Disk.Clear();OnSaved=null;}
}
" + source + @"
public static class Checks {
 static int count;static DateTime now=DateTime.UtcNow;
 static void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
 static void Forget(){
  const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
  typeof(GiftFestivalState).GetField(""data"",flags).SetValue(null,null);
  typeof(GiftFestivalState).GetField(""lastJson"",flags).SetValue(null,null);
  typeof(LivesManager).GetField(""_initialized"",flags).SetValue(null,false);
 }
 static GiftFestivalConfig Reset(DailySlotRewardType type,int lives=3){
  PlayerPrefs.Clear();Forget();RuntimeSimulationSession.IsActive=false;RandomStub.Next=0;
  PlayerPrefs.SetInt(""lives_current"",lives);PlayerPrefs.SetString(""lives_next_ticks"",DateTime.UtcNow.AddHours(1).Ticks.ToString());
  var cfg=new GiftFestivalConfig();cfg.rounds[0].boxRewards=new[]{new DailySlotReward{type=type,amount=2}};
  GiftFestivalConfig.Shared=cfg;GiftFestivalState.TryJoin(cfg,now);GiftFestivalState.TryBeginTutorial(cfg,now);
  return cfg;
 }
 static GiftFestivalState.Shot Fire(GiftFestivalConfig cfg){
  GiftFestivalState.Shot shot;Check(GiftFestivalState.TryHit(cfg,0,.5f,now,out shot),""Paid shot succeeds"");return shot;
 }
 public static int Run(){
  for(int start=0;start<=10;start++){
   var cfg=Reset(DailySlotRewardType.Lives,start);int notifications=0;Action listener=()=>notifications++;
   LivesManager.OnLivesChanged+=listener;
   var shot=Fire(cfg);
   LivesManager.OnLivesChanged-=listener;
   Check(shot.BoxReward.type==DailySlotRewardType.Lives&&shot.BoxReward.amount==2,""Saved two-heart prize reaches real life API"");
   Check(LivesManager.Current==Math.Min(10,start+2),""Two lives actually credit central balance within cap"");
   Check(notifications==(start<10?1:0),""Visible HUD gets the life-change notification"");
   PlayerPrefs.Restart();Forget();GiftFestivalState.Sync(cfg,now);
   Check(LivesManager.Current==Math.Min(10,start+2)&&GiftFestivalState.Balls==0,""Life balance and spent ball survive app restart"");
   Check(!GiftFestivalState.TryBeginTutorial(cfg,now)&&GiftFestivalState.Balls==0,""Reopening after winning hearts cannot regrant an attempt"");
  }
  var lifeCfg=Reset(DailySlotRewardType.Lives,5);
  Action crash=()=>{throw new Exception(""interrupted UI callback after persisted lives"");};
  LivesManager.OnLivesChanged+=crash;
  try{Fire(lifeCfg);}catch(Exception){}
  LivesManager.OnLivesChanged-=crash;
  PlayerPrefs.Restart();Forget();GiftFestivalState.Sync(lifeCfg,now);
  Check(LivesManager.Current==7&&GiftFestivalState.Balls==0&&GiftFestivalState.TutorialCompleted,""Interrupted life delivery reloads as exactly seven lives and a spent shot"");
  GiftFestivalState.Sync(lifeCfg,now);Check(LivesManager.Current==7,""Life receipt is not paid twice"");

  // Initialize/offline regen may save; it must not publish the event receipt before adding the reward.
  lifeCfg=Reset(DailySlotRewardType.Lives,0);
  PlayerPrefs.SetString(""lives_next_ticks"",DateTime.UtcNow.AddMinutes(-1).Ticks.ToString());PlayerPrefs.Save();
  int beforePaidSaves=0;bool sawPaid=false;
  PlayerPrefs.OnSaved=()=>{
   var paid=JsonUtility.FromJson<GiftFestivalState.SaveData>(PlayerPrefs.Disk[GiftFestivalState.SaveKey]);
   if(paid.delivery.Length>0&&paid.delivered==1){sawPaid=true;Check(PlayerPrefs.GetInt(""lives_current"")==3,""Paid receipt never precedes actual life grant"");}
   else if(!sawPaid)beforePaidSaves++;
  };
  Fire(lifeCfg);PlayerPrefs.OnSaved=null;
  Check(sawPaid&&beforePaidSaves>0&&LivesManager.Current==3,""Offline regeneration and two-heart grant both survive initialization saves"");

  foreach(var type in new[]{DailySlotRewardType.Coins,DailySlotRewardType.Stars,
   DailySlotRewardType.Joker_LineH,DailySlotRewardType.Joker_Line,DailySlotRewardType.Joker_PulseCore,DailySlotRewardType.Joker_SystemOverride,
   DailySlotRewardType.Booster_Hammer,DailySlotRewardType.Booster_Row,DailySlotRewardType.Booster_Column,DailySlotRewardType.Booster_Shuffle,DailySlotRewardType.Booster_PaintBucket}){
   for(int direction=0;direction<2;direction++){
    var cfg=Reset(type);RandomStub.Next=direction;Fire(cfg);PlayerPrefs.Restart();Forget();GiftFestivalState.Sync(cfg,now);
    if(type==DailySlotRewardType.Coins)Check(PlayerWallet.Coins==2,""Actual coin wallet was credited once"");
    else if(type==DailySlotRewardType.Stars)Check(PlayerWallet.TotalStars==2,""Actual star wallet was credited once"");
    else if(type==DailySlotRewardType.Joker_LineH||type==DailySlotRewardType.Joker_Line){
     Check(PreLevelSpecialInventory.GetCount(TileSpecial.LineH)==12&&PreLevelSpecialInventory.GetCount(TileSpecial.LineV)==12,""Both line outcomes credit the visible shared line inventory"");
     Check(PlayerPrefs.GetInt(""prelevel_special_unknown_count"")==0,""A vertical line never vanishes into unknown inventory"");
    }
    else if(type==DailySlotRewardType.Joker_PulseCore)Check(PreLevelSpecialInventory.GetCount(TileSpecial.PulseCore)==12,""Actual PulseCore inventory credited"");
    else if(type==DailySlotRewardType.Joker_SystemOverride)Check(PreLevelSpecialInventory.GetCount(TileSpecial.SystemOverride)==12,""Actual Override inventory credited"");
    else {
     var mode=type==DailySlotRewardType.Booster_Hammer?BoardController.BoosterMode.Single
      :type==DailySlotRewardType.Booster_Row?BoardController.BoosterMode.Row:type==DailySlotRewardType.Booster_Column?BoardController.BoosterMode.Column
      :type==DailySlotRewardType.Booster_Shuffle?BoardController.BoosterMode.Shuffle:BoardController.BoosterMode.PaintBucket;
     Check(BoosterInventory.GetCount(mode)==2,""Actual booster inventory credited"");
    }
    Check(GiftFestivalState.Balls==0&&GiftFestivalState.OpenMask==1,""Reload retains paid prize and cannot refund the shot"");
   }
  }
  return count;
 }
}
Checks.Run()";
int checks = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default.AddReferences(typeof(System.Web.Script.Serialization.JavaScriptSerializer).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + checks + " real Gift Festival reward delivery / life / wallet / inventory checks.");
