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
var stateRoot=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs")).GetRoot();
var stateMethods=string.Join("\n",stateRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"GetHintObstacleId","CollectStampedBeneathIds"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
foreach(var path in new[]{"Assets/_Project/Scripts/Tutorial/ObstacleHintManager.cs","Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs","Assets/_Project/Scripts/Grid/GridSpawner.cs"}){
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var enums=string.Join("\n",CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Core/LevelData.cs")).GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().Where(e=>e.Identifier.Text=="ObstacleId"||e.Identifier.Text=="LevelKind").Select(e=>e.ToFullString()));
var stubs=@"
using System;using System.Collections.Generic;
public class StackEntry {public ObstacleId obstacleId;public int originCellIndex;}
public class LevelData {
 public int[] obstacles;public object[] ancientSeals,safes,tubes,magnets;
 public StackEntry[] stackedObstacles;public LevelKind levelKind;
 public bool bossToolThreatEnabled;public ObstacleId[] bossToolThreatTypes;
}
public class State {
 public class Layer {public ObstacleId Id;}
 public Dictionary<int,List<Layer>> _stampedBeneathByCell=new Dictionary<int,List<Layer>>();
 public Dictionary<int,(ObstacleId Id,int Remaining)> _underTileBeneathMovable=new Dictionary<int,(ObstacleId Id,int Remaining)>();
 public HashSet<int> waterLilyCells=new HashSet<int>();public HashSet<int> lilies=>waterLilyCells;public List<int> flowers=new List<int>();
 public void CollectWaterLilyCells(List<int> into){into.AddRange(lilies);}
 public void CollectGrassFlowerCells(List<int> into){into.AddRange(flowers);}
 /* STATE METHODS */
}
public class BoardController {
 public LevelData ActiveLevelData;public State ObstacleStateService=new State();
 public static bool IsGelLauncher(ObstacleId id)=>id>=ObstacleId.GelLauncherUp&&id<=ObstacleId.GelLauncherRight;
}
public static class BossDuelObstaclePressure {public static ObstacleId[] GetPool(LevelData l)=>Array.Empty<ObstacleId>();}
public static class BossDuelToolThreat {public static bool IsToolThreat(ObstacleId id)=>true;}
public class Probe {public BoardController board;/* METHODS */}
".Replace("/* METHODS */",methods).Replace("/* STATE METHODS */",stateMethods);
var tests=@"
int checks=0;Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
var b=new BoardController{ActiveLevelData=new LevelData{obstacles=new[]{42,42,68,70,67,66,31,34,63,70}}};
b.ObstacleStateService.lilies.UnionWith(new[]{0,1});b.ObstacleStateService.flowers.Add(2);
var p=new Probe{board=b};var ids=p.CollectObstacleIds();
foreach(var id in new[]{ObstacleId.GrassWaterLily,ObstacleId.GrassFlower,ObstacleId.TarpStatue,ObstacleId.RhodesStatue,ObstacleId.BilliardPocket,ObstacleId.BilliardBall,ObstacleId.HatLauncher,ObstacleId.Cargo,ObstacleId.PaintCanBox})
 check(ids.Contains(id),""missing recent obstacle hint ID: ""+id);
check(ids.Count==new HashSet<ObstacleId>(ids).Count,""shared/multiple cells introduce an obstacle once"");
b.ActiveLevelData.stackedObstacles=new[]{new StackEntry{obstacleId=ObstacleId.RhodesStatue}};
b.ObstacleStateService._stampedBeneathByCell[0]=new List<State.Layer>{new State.Layer{Id=ObstacleId.GrassWaterLily}};
check(p.CollectObstacleIds().Count==ids.Count,""stacked and converted variants do not duplicate hints"");
b=new BoardController{ActiveLevelData=new LevelData{obstacles=new[]{59,60,61,62}}};p.board=b;
check(p.CollectObstacleIds().Count==1,""gel directions keep their single rule hint"");
p.board=null;check(p.CollectObstacleIds().Count==0,""missing board safely yields no pending hints"");
// Only converted Water Lily cells: never invent an extra Grass introduction.
b=new BoardController{ActiveLevelData=new LevelData{obstacles=new[]{42,42,76}}};b.ObstacleStateService.lilies.UnionWith(new[]{0,1});p.board=b;
ids=p.CollectObstacleIds();check(ids.Contains(ObstacleId.GrassWaterLily)&&!ids.Contains(ObstacleId.Grass),""lily-only grass backing introduces only Water Lily"");
// Genuine standalone Grass remains a separate obstacle in the same scene.
b.ActiveLevelData.obstacles=new[]{42,42,42};ids=p.CollectObstacleIds();check(ids.Contains(ObstacleId.Grass)&&ids.Contains(ObstacleId.GrassWaterLily),""mixed scene retains genuine Grass introduction"");
// Lily grass can sit in a stamped stack, below a movable, or in converted authored entries.
b=new BoardController{ActiveLevelData=new LevelData{obstacles=new[]{76,75},stackedObstacles=new[]{new StackEntry{obstacleId=ObstacleId.Grass,originCellIndex=0}}}};p.board=b;b.ObstacleStateService.lilies.UnionWith(new[]{0,1});
b.ObstacleStateService._stampedBeneathByCell[0]=new List<State.Layer>{new State.Layer{Id=ObstacleId.Grass}};
b.ObstacleStateService._underTileBeneathMovable[1]=(ObstacleId.Grass,1);ids=p.CollectObstacleIds();check(ids.Contains(ObstacleId.GrassWaterLily)&&!ids.Contains(ObstacleId.Grass),""all converted layer routes use lily identity"");
var raw=new List<ObstacleId>();b.ObstacleStateService.CollectStampedBeneathIds(raw);check(raw.TrueForAll(id=>id==ObstacleId.Grass),""render/gameplay callers keep raw Grass identity by default"");
var hints=new List<ObstacleId>();b.ObstacleStateService.CollectStampedBeneathIds(hints,true);check(hints.TrueForAll(id=>id==ObstacleId.GrassWaterLily),""hint-only beneath collection resolves lily identity"");
b.ObstacleStateService._stampedBeneathByCell[2]=new List<State.Layer>{new State.Layer{Id=ObstacleId.Grass}};check(p.CollectObstacleIds().Contains(ObstacleId.Grass),""standalone buried Grass is not globally suppressed"");
check(b.ObstacleStateService.GetHintObstacleId(0,ObstacleId.Wall)==ObstacleId.Wall,""non-Grass obstacle identity unaffected"");
b.ObstacleStateService.lilies.Clear();check(b.ObstacleStateService.GetHintObstacleId(0,ObstacleId.Grass)==ObstacleId.Grass,""cleared/reset registration restores plain Grass identity"");
b.ObstacleStateService.CollectStampedBeneathIds(null,true);check(true,""null output collection accepted"");
Console.WriteLine(""PASS: ""+checks+"" production hint collection checks, including runtime Grass conversion and recent obstacles."");
";
await CSharpScript.RunAsync(stubs+enums+tests,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
