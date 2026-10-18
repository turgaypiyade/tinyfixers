// Production hint ID collection with board doubles; no Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Tutorial/ObstacleHintManager.cs")).GetRoot();
var methods=string.Join("\n",root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"CollectObstacleIds","IsColoredToolThreat"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString())).Replace("private List<ObstacleId> CollectObstacleIds", "public List<ObstacleId> CollectObstacleIds");
var enums=string.Join("\n",CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Core/LevelData.cs")).GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().Where(e=>e.Identifier.Text=="ObstacleId"||e.Identifier.Text=="LevelKind").Select(e=>e.ToFullString()));
var stubs=@"
using System;using System.Collections.Generic;
public class StackEntry {public ObstacleId obstacleId;}
public class LevelData {
 public int[] obstacles;public object[] ancientSeals,safes,tubes,magnets;
 public StackEntry[] stackedObstacles;public LevelKind levelKind;
 public bool bossToolThreatEnabled;public ObstacleId[] bossToolThreatTypes;
}
public class State {
 public List<ObstacleId> beneath=new List<ObstacleId>();public List<int> lilies=new List<int>(),flowers=new List<int>();
 public void CollectStampedBeneathIds(List<ObstacleId> into){into.AddRange(beneath);}
 public void CollectWaterLilyCells(List<int> into){into.AddRange(lilies);}
 public void CollectGrassFlowerCells(List<int> into){into.AddRange(flowers);}
}
public class BoardController {
 public LevelData ActiveLevelData;public State ObstacleStateService=new State();
 public static bool IsGelLauncher(ObstacleId id)=>id>=ObstacleId.GelLauncherUp&&id<=ObstacleId.GelLauncherRight;
}
public static class BossDuelObstaclePressure {public static ObstacleId[] GetPool(LevelData l)=>Array.Empty<ObstacleId>();}
public static class BossDuelToolThreat {public static bool IsToolThreat(ObstacleId id)=>true;}
public class Probe {public BoardController board;/* METHODS */}
".Replace("/* METHODS */",methods);
var tests=@"
int checks=0;Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
var b=new BoardController{ActiveLevelData=new LevelData{obstacles=new[]{42,42,68,70,67,66,31,34,63,70}}};
b.ObstacleStateService.lilies.AddRange(new[]{0,1});b.ObstacleStateService.flowers.Add(2);
var p=new Probe{board=b};var ids=p.CollectObstacleIds();
foreach(var id in new[]{ObstacleId.GrassWaterLily,ObstacleId.GrassFlower,ObstacleId.TarpStatue,ObstacleId.RhodesStatue,ObstacleId.BilliardPocket,ObstacleId.BilliardBall,ObstacleId.HatLauncher,ObstacleId.Cargo,ObstacleId.PaintCanBox})
 check(ids.Contains(id),""missing recent obstacle hint ID: ""+id);
check(ids.Count==new HashSet<ObstacleId>(ids).Count,""shared/multiple cells introduce an obstacle once"");
b.ActiveLevelData.stackedObstacles=new[]{new StackEntry{obstacleId=ObstacleId.RhodesStatue}};
b.ObstacleStateService.beneath.Add(ObstacleId.GrassWaterLily);
check(p.CollectObstacleIds().Count==ids.Count,""stacked and converted variants do not duplicate hints"");
b=new BoardController{ActiveLevelData=new LevelData{obstacles=new[]{59,60,61,62}}};p.board=b;
check(p.CollectObstacleIds().Count==1,""gel directions keep their single rule hint"");
p.board=null;check(p.CollectObstacleIds().Count==0,""missing board safely yields no pending hints"");
Console.WriteLine(""PASS: ""+checks+"" production hint collection checks, including runtime Grass conversion and recent obstacles."");
";
await CSharpScript.RunAsync(stubs+enums+tests,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
