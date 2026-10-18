// Production goal presentation, sequential counting and reward delivery; no Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
string Read(string p)=>File.ReadAllText("Assets/_Project/Scripts/"+p);
var config=CSharpSyntaxTree.ParseText(Read("Core/Events/Progress/ProgressEventConfig.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.Text=="ProgressGoalDefinition").ToFullString();
var service=CSharpSyntaxTree.ParseText(Read("Core/Events/Progress/ProgressEventService.cs")).GetRoot();
var members=string.Join("\n",service.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"Matches","SimulateStagedGain","ApplySignal","GrantProgressReward","EnsureSessionGainSlots","AllGoalsClaimed"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var signals=string.Join("\n",service.DescendantNodes().OfType<EnumDeclarationSyntax>().Where(e=>e.Identifier.Text=="SignalKind").Select(e=>e.ToFullString()))+service.DescendantNodes().OfType<StructDeclarationSyntax>().Single(e=>e.Identifier.Text=="Signal").ToFullString();
var timed=CSharpSyntaxTree.ParseText(Read("Core/Events/Progress/TimedRewardService.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single().ToFullString();
var rewardEnum=CSharpSyntaxTree.ParseText(Read("Core/Events/DailySlotReward.cs")).GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().Single().ToFullString();
var runtime=CSharpSyntaxTree.ParseText(Read("Core/Events/Progress/ProgressGoalRuntime.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single().ToFullString();
var stubs=@"
using System;using System.Collections.Generic;
public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
public class MinAttribute:Attribute {public MinAttribute(int n){}}
public class Sprite {}
public enum TileSpecial {None,LineH,LineV,PatchBot,PulseCore,SystemOverride}
public static class Mathf {
 public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
 public static int Clamp(int a,int b,int c)=>Math.Max(b,Math.Min(a,c));public static float Clamp01(float f)=>Math.Max(0,Math.Min(f,1));
}
public static class PlayerPrefs {public static Dictionary<string,string> values=new Dictionary<string,string>();public static void SetString(string k,string v){values[k]=v;}public static string GetString(string k,string d)=>values.TryGetValue(k,out var v)?v:d;public static void Save(){}}
public static class GameLocalization {public static string GetFormat(string k,params object[] a)=>k+""|""+a[0];}
public class TileIconLibrary {
 public static TileIconLibrary Shared=new TileIconLibrary();
 public Sprite gear=new Sprite(),core=new Sprite(),bolt=new Sprite(),plate=new Sprite(),lineH=new Sprite(),lineV=new Sprite(),boosterHammer=new Sprite(),pulseCore=new Sprite(),star=new Sprite();
}
public class DailySlotReward {public DailySlotRewardType type;public int amount=1;public Sprite icon;}
public static class DailySlotRewardService {public static DailySlotReward last;public static int grants;public static void Grant(DailySlotReward r){last=r;grants++;}}
public class SessionGainRecord {public int GoalIndex,GainedCount;public bool RewardGranted;public DailySlotReward Reward;}
";
var tests=@"
 private readonly List<ProgressGoalRuntime> goals=new List<ProgressGoalRuntime>();
 private readonly List<SessionGainRecord> sessionGains=new List<SessionGainRecord>();
 private readonly List<DailySlotReward> pendingRewardReveals=new List<DailySlotReward>(),pendingMenuRewardFx=new List<DailySlotReward>();
 private int[] stagedSpacePerGoal;private int saves;private Action<int> OnProgressGained;private Action<TileType,int> OnProgressGainedTyped;
 private void SaveGoals(){saves++;}
 private static int checks;private static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 public static void Run(){
  var wrong=new Sprite();var d=new ProgressGoalDefinition{goalType=ProgressGoalType.TileCleared_Bolt,targetCount=250,goalIcon=wrong,reward=new DailySlotReward{type=DailySlotRewardType.Stars,icon=wrong}};
  Check(d.ResolveGoalIcon()==TileIconLibrary.Shared.bolt,""blue target ignores stale star/reward icons"");
  foreach(ProgressGoalType type in Enum.GetValues(typeof(ProgressGoalType))){d.goalType=type;Check(d.ResolveGoalIcon()!=null&&d.GetDescription().Contains(""|250""),""every target has canonical icon and count description"");}
  var lib=TileIconLibrary.Shared;TileIconLibrary.Shared=null;Check(d.ResolveGoalIcon()==wrong,""authored target fallback works without library"");d.goalIcon=null;Check(d.ResolveGoalIcon()==null,""never substitutes reward icon for missing target"");TileIconLibrary.Shared=lib;
  d.fallbackDescription=""Authored seasonal task"";Check(d.GetDescription()==d.fallbackDescription,""server custom descriptions preserved"");
  foreach(DailySlotRewardType type in Enum.GetValues(typeof(DailySlotRewardType))){
   d.reward=new DailySlotReward{type=type,amount=1};d.rewardDurationMinutes=15;
   bool supported=type==DailySlotRewardType.Lives||type==DailySlotRewardType.Joker_LineH||type==DailySlotRewardType.Joker_Line||type==DailySlotRewardType.Joker_PulseCore||type==DailySlotRewardType.Joker_SystemOverride;
   Check(d.EffectiveRewardDurationMinutes==(supported?15:0),""timed support matches usable inventory: ""+type);
  }
  d.reward=new DailySlotReward{type=DailySlotRewardType.Booster_Hammer,amount=1};d.rewardDurationMinutes=15;
  GrantProgressReward(d);Check(DailySlotRewardService.last==d.reward&&DailySlotRewardService.grants==1&&PlayerPrefs.values.Count==0,""legacy timed hammer grants a real hammer"");
  d.reward=new DailySlotReward{type=DailySlotRewardType.Lives};GrantProgressReward(d);Check(PlayerPrefs.values.ContainsKey(""timed_reward_1"")&&DailySlotRewardService.grants==1,""timed lives use expiry, not a single life"");
  TimedRewardService.Grant(DailySlotRewardType.Stars,15);Check(!PlayerPrefs.values.ContainsKey(""timed_reward_2""),""unsupported expiry is never recorded"");
  var p=new Probe();
  p.goals.Add(new ProgressGoalRuntime(new ProgressGoalDefinition{goalType=ProgressGoalType.TileCleared_Bolt,targetCount=3,reward=new DailySlotReward{type=DailySlotRewardType.Coins,amount=150}}));
  p.goals.Add(new ProgressGoalRuntime(new ProgressGoalDefinition{goalType=ProgressGoalType.TileCleared_Core,targetCount=2,reward=new DailySlotReward{type=DailySlotRewardType.Booster_Hammer}}));
  p.stagedSpacePerGoal=new[]{3,2};
  var red=new Signal{kind=SignalKind.Tile,tile=TileType.Core,count=10};var blue=new Signal{kind=SignalKind.Tile,tile=TileType.Bolt,count=3};
  Check(p.SimulateStagedGain(red)==0,""future color goal cannot advance before active goal"");
  Check(p.SimulateStagedGain(blue)==3&&p.SimulateStagedGain(red)==2,""staging advances sequentially as goals fill"");
  Check(p.goals[0].CurrentCount==0&&!p.goals[0].IsRewardClaimed,""staged losses do not grant currency"");
  int granted=DailySlotRewardService.grants;p.ApplySignal(blue,false);p.ApplySignal(red,false);
  Check(p.AllGoalsClaimed()&&DailySlotRewardService.grants==granted+2,""winning commit grants each target exactly once"");
  p.ApplySignal(blue,false);p.ApplySignal(red,false);Check(DailySlotRewardService.grants==granted+2,""claimed goals cannot grant twice"");
  Check(p.pendingMenuRewardFx.Count==2&&p.pendingRewardReveals.Count==1,""menu feedback tracks each grant, final chest only once"");
  Console.WriteLine(""PASS: ""+checks+"" production progress goal/icon/timed-reward/staging/claim checks."");
 }
";
var source=stubs+Read("Grid/TileType.cs")+Read("Core/Events/Progress/ProgressGoalType.cs")+rewardEnum+config+runtime+timed+"\npublic class Probe {"+signals+members+tests+"}\nProbe.Run();";
await CSharpScript.RunAsync(source,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
