// Run: csi Tools/check_harvest_rewards.csx. Isolated production state/schedule; no Unity/project build.
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
string Root="Assets/_Project/Scripts/Events/Harvest/";
var state=CSharpSyntaxTree.ParseText(File.ReadAllText(Root+"HarvestState.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.ValueText=="HarvestState");
var stateText="public static class HarvestState {"+string.Join("\n",state.Members.Where(m=>!(m is MethodDeclarationSyntax md)||!(new[]{"TryDig","DebugReset"}.Contains(md.Identifier.ValueText))).Select(m=>m.ToFullString()))+"}";
stateText=stateText.Replace("UnityEngine.Object.FindFirstObjectByType<BoardController>()","BoardController.Active");
var schedule=CSharpSyntaxTree.ParseText(File.ReadAllText(Root+"HarvestSchedule.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single().ToFullString();
string harness=@"
using System;
using System.Globalization;
using System.Collections.Generic;
public enum RuntimeInitializeLoadType {AfterSceneLoad}
public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
public static class PlayerPrefs {
 static Dictionary<string,int> ints=new Dictionary<string,int>();static Dictionary<string,string> strings=new Dictionary<string,string>();
 public static int GetInt(string k,int v=0)=>ints.TryGetValue(k,out var n)?n:v;public static void SetInt(string k,int v){ints[k]=v;}
 public static string GetString(string k,string v="""")=>strings.TryGetValue(k,out var s)?s:v;public static void SetString(string k,string v){strings[k]=v;}
 public static void DeleteKey(string k){ints.Remove(k);strings.Remove(k);}public static void Save(){}public static void Reset(){ints.Clear();strings.Clear();}
}
public static class CurrentLevel {public static int Global=50;}
public static class PlayerStats {public static event Action OnLevelCleared;public static bool LastClearWasFirstTry,IsCurrentLevelFirstTry;public static void Win()=>OnLevelCleared?.Invoke();}
public enum LevelKind {Normal,BossDuel}
public class LevelData {public LevelKind levelKind;}
public class BoardController {public static BoardController Active;public LevelData ActiveLevelData;}
public class HarvestConfig {
 public static HarvestConfig Shared;public int minLevelGate=50,cycleDays=7,activeDays=5,FloorCount=5;
 public int trowelsPerWin=1,firstTryBonus=1,bossWinTrowels=2,trowelGoldValue=10;
 public string epochUtc;public object trowelIcon;
}
public enum DailySlotRewardType {Coins}
public class DailySlotReward {public DailySlotRewardType type;public int amount;}
public static class DailySlotRewardService {public static int Gold;public static void Grant(DailySlotReward r){Gold+=r.amount;}}
public static class GameAnalytics {public static void LogEvent(string name,params (string,object)[] values){}}
public static class GameLocalization {public static string Get(string key)=>key;}
public class LevelLossItem {public LevelLossItem(object icon,string label,int amount,bool flag){}}
public static class LevelLossRegistry {public static void Register(string key,Func<LevelLossItem[]> factory){}}
"+stateText+schedule+@"
public static class Checks {
 static int count;static void Check(bool ok,string label){count++;if(!ok)throw new Exception(label);}
 static void Reset(){PlayerPrefs.Reset();DailySlotRewardService.Gold=0;CurrentLevel.Global=50;BoardController.Active=null;PlayerStats.LastClearWasFirstTry=false;HarvestConfig.Shared=new HarvestConfig{epochUtc=DateTime.UtcNow.Date.ToString(""yyyy-MM-dd"",CultureInfo.InvariantCulture)};}
 public static int Run(){
  typeof(HarvestState).GetMethod(""RegisterHooks"",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
  typeof(HarvestState).GetMethod(""RegisterHooks"",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
  Reset();PlayerStats.Win();Check(HarvestState.Trowels==1,""Active normal win grants one even without opening event UI; hook registration is idempotent"");
  PlayerStats.LastClearWasFirstTry=true;PlayerStats.Win();Check(HarvestState.Trowels==3,""First try grants two"");
  PlayerStats.LastClearWasFirstTry=false;BoardController.Active=new BoardController{ActiveLevelData=new LevelData{levelKind=LevelKind.BossDuel}};
  PlayerStats.Win();Check(HarvestState.Trowels==5,""Boss grants two after previous failure"");
  PlayerStats.LastClearWasFirstTry=true;PlayerStats.Win();Check(HarvestState.Trowels==7,""Boss first try grants two, no duplicate bonus"");
  HarvestState.SyncCycle(HarvestConfig.Shared,DateTime.UtcNow);Check(HarvestState.Trowels==7,""Returning to event in same cycle preserves earned shovels"");
  PlayerPrefs.SetInt(""harvest_floor"",5);PlayerStats.Win();Check(HarvestState.IsLive(HarvestConfig.Shared,DateTime.UtcNow)&&HarvestState.Trowels==7,""Calendar active but all five harvests finished deliberately stops earning"");
  PlayerPrefs.SetString(""harvest_cycle"",""old-season"");PlayerStats.Win();Check(HarvestState.Floor==0&&HarvestState.Trowels==2&&DailySlotRewardService.Gold==70,""First win of new season resets old completed floor and grants new shovels; old balance converts once"");
  Reset();CurrentLevel.Global=49;PlayerStats.Win();Check(HarvestState.Trowels==0,""Below level 50 earns none"");
  CurrentLevel.Global=50;PlayerStats.Win();Check(HarvestState.Trowels==1,""Level 50 is eligible"");
  Reset();var cfg=HarvestConfig.Shared;var start=DateTime.ParseExact(cfg.epochUtc,""yyyy-MM-dd"",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
  Check(HarvestSchedule.IsActiveNow(cfg,start)&&HarvestSchedule.IsActiveNow(cfg,start.AddDays(5).AddTicks(-1)),""Active window includes start and excludes only exact end"");
  Check(!HarvestSchedule.IsActiveNow(cfg,start.AddDays(5))&&!HarvestSchedule.IsActiveNow(cfg,start.AddDays(6)),""Two idle days earn no shovels"");
  Check(HarvestSchedule.GetNextStart(cfg,start.AddDays(6))==start.AddDays(7)&&HarvestSchedule.IsActiveNow(cfg,start.AddDays(7)),""Next weekly season starts correctly"");
  HarvestState.SyncCycle(cfg,start);HarvestState.AddTrowels(3);HarvestState.SyncCycle(cfg,start.AddDays(5));HarvestState.SyncCycle(cfg,start.AddDays(6));
  Check(HarvestState.Trowels==0&&DailySlotRewardService.Gold==30,""Season end converts unspent shovels once"");
  cfg.epochUtc=DateTime.UtcNow.Date.AddDays(-5).ToString(""yyyy-MM-dd"",CultureInfo.InvariantCulture);PlayerStats.Win();Check(HarvestState.Trowels==0,""Win after calendar expiry grants none even if level started earlier"");
  cfg.epochUtc=""invalid"";PlayerStats.Win();Check(HarvestState.Trowels==0&&!HarvestState.IsLive(cfg,DateTime.UtcNow),""Invalid calendar disables event and earning consistently"");
  for(int level=1;level<=100;level++)foreach(bool first in new[]{false,true})foreach(bool boss in new[]{false,true}){
   Reset();CurrentLevel.Global=level;PlayerStats.LastClearWasFirstTry=first;BoardController.Active=new BoardController{ActiveLevelData=new LevelData{levelKind=boss?LevelKind.BossDuel:LevelKind.Normal}};
   PlayerStats.Win();int expected=level<50?0:(boss||first?2:1);Check(HarvestState.Trowels==expected,""Unlock/first-try/boss award matrix"");
  }
  return count;
 }
}
Checks.Run()";
Console.WriteLine("PASS: "+CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult()+" Harvest reward/schedule checks");
