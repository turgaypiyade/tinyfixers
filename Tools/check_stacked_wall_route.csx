// Production stamp/origin, editor goal count, beneath-store and wall-distance methods.
// Unity doubles only; no Unity build/Play or board/gravity/cascade simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
Func<string,CompilationUnitSyntax> read=p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),path:p).GetCompilationUnitRoot();
var data=read("Assets/_Project/Scripts/Core/LevelData.cs");
var before=Environment.GetCommandLineArgs().Contains("--before");
var spawn=read(before?"Docs/LevelDesign/Batches/226-230-Lantern-Wind-Crystal/Revisions/v2-wall-route/Before/Assets/_Project/Scripts/Grid/GridSpawner.cs":"Assets/_Project/Scripts/Grid/GridSpawner.cs");
var editor=read("Assets/_Project/Scripts/Editor/LevelDataEditor.cs");
var state=read("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs");
var wall=read("Assets/_Project/Scripts/Grid/Wall/WallPieceView.cs");
foreach(var root in new[]{data,spawn,editor,state,wall}){
 var errors=root.GetDiagnostics().Where(x=>x.Severity==DiagnosticSeverity.Error);
 if(errors.Any())throw new Exception(string.Join("\n",errors));
}
Func<CompilationUnitSyntax,string,string> method=(r,n)=>r.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text==n).ToFullString();
var entry=data.DescendantNodes().OfType<StructDeclarationSyntax>().Single(s=>s.Identifier.Text=="StackedObstacleEntry");
var entrySource=entry.RemoveNodes(entry.DescendantNodes().OfType<AttributeListSyntax>(),SyntaxRemoveOptions.KeepNoTrivia).ToFullString();
var beneath=state.DescendantNodes().OfType<StructDeclarationSyntax>().Single(s=>s.Identifier.Text=="StampedBeneath").ToFullString();
var source=@"
using System;using System.Collections.Generic;using System.Linq;
public enum ObstacleId{None,Wall,MetalWall,Grass,GrassFlower,GrassWaterLily,Flour,Lantern,KeyGenerator,Hamster,BilliardBall,SpreadingGel,Safe,AncientSeal,BatteryBox,ColorCrateYellow,ColorCrateRed,ColorCrateBlue,ColorCrateGreen}
public class ObstacleDef{public int hits=2;public bool IsMovableObstacle;public Size size=new Size(1,1);}
public struct Size{public int x,y;public Size(int x,int y){this.x=x;this.y=y;}}
public class Library{public ObstacleDef Get(ObstacleId id)=>new ObstacleDef{IsMovableObstacle=id==ObstacleId.Lantern};}
public static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);public static int Clamp(int n,int a,int b)=>Math.Max(a,Math.Min(n,b));}
public static class Time{public static float unscaledTime=1;}
/* ENTRY */
public class LevelData{
 public int width=9,height=11;public int[] cells=new int[99],obstacles=new int[99],obstacleOrigins=Enumerable.Repeat(-1,99).ToArray();
 public Library obstacleLibrary=new Library();public StackedObstacleEntry[] stackedObstacles;
 public object[] safes,ancientSeals;
 public Size GetObstacleFootprint(ObstacleId id,int origin,ObstacleDef def)=>def.size;
}
public class Spawner{
 public readonly List<(int cell,ObstacleId beneathId,int beneathOrigin,int overOrigin)> pendingStampedBeneath=new List<(int,ObstacleId,int,int)>();
 public int coveringOverlays;
 bool IsMovableContent(LevelData l,int cell)=>l.obstacles[cell]==(int)ObstacleId.Lantern;
 bool HasPendingCoveringOverlay(int cell)=>false;
 public readonly List<(int cell,ObstacleId id)> pendingCoveringOverlays=new List<(int,ObstacleId)>();
 void MaterializePendingCoveringOverlay(LevelData l,int cell){}
 /* STAMP */
 public void Stamp(LevelData l,StackedObstacleEntry e)=>StampStackedEntry(l,e);
}
public class State{
 public LevelData level;public Library library=new Library();const float RevealGraceSeconds=.1f;
 public readonly Dictionary<int,List<StampedBeneath>> _stampedBeneathByCell=new Dictionary<int,List<StampedBeneath>>();
 readonly Dictionary<int,float> _revealGraceUntil=new Dictionary<int,float>();
 int ResolveInitialColorCrateHits(ObstacleId id,int origin,int hits)=>hits;
 /* BENEATH */
 /* STORE */
 public StampedBeneath Pop(int cell){if(!TryPopStampedBeneathLayer(cell,out var b))throw new Exception(""missing beneath"");return b;}
}
public static class Editor{
 /* COUNT */
 public static int Count(LevelData l,ObstacleId id)=>CountGoalObstacles(l,id);
}
public class Wave{
 public int width=9;public Dictionary<int,object> cells=new Dictionary<int,object>();
 /* DISTANCE */
 public Dictionary<int,int> From(int start)=>RingDistances(start);
}
public static class Checks{
 static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 public static void Run(bool before){
  var route=new List<int>();for(int y=3;y<11;y++)for(int x=4;x<6;x++)route.Add(y*9+x);
  for(int y=9;y<11;y++)for(int x=0;x<4;x++)route.Add(y*9+x);
  route.Sort();int origin=route[0];
  foreach(bool reverse in new[]{false,true}){
   var l=new LevelData();foreach(int i in route){l.cells[i]=1;l.obstacles[i]=(int)ObstacleId.Lantern;l.obstacleOrigins[i]=i;}
   l.stackedObstacles=route.Select(i=>new StackedObstacleEntry{originCellIndex=i,obstacleId=ObstacleId.Wall,stackOrder=1,wallPieceOriginPlusOne=origin+1}).ToArray();
   var sp=new Spawner();foreach(var e in reverse?l.stackedObstacles.Reverse():l.stackedObstacles)sp.Stamp(l,e);
   if(before){Check(route.Select(i=>l.obstacleOrigins[i]).Distinct().Count()==24,""Old stamp must split the route into24 pieces"");Console.WriteLine(""REPRODUCED: old production stamp splits a layered24-cell wall route into24 independent origins."");return;}
   Check(route.All(i=>l.obstacles[i]==(int)ObstacleId.Wall&&l.obstacleOrigins[i]==origin),""All24 cells must form one runtime piece, independently of stamp order"");
   Check(Editor.Count(l,ObstacleId.Wall)==1,""Editor goal must count one wall, not24 covers"");
   var st=new State{level=l};foreach(var p in sp.pendingStampedBeneath){
    Check(p.overOrigin==origin&&p.beneathId==ObstacleId.Lantern&&p.beneathOrigin==p.cell,""Each lantern must retain its own origin under the shared cover"");
    st.RegisterStampedBeneath(p.cell,p.beneathId,p.beneathOrigin,p.overOrigin);
   }
   foreach(int i in route){var b=st.Pop(i);Check(b.Id==ObstacleId.Lantern&&b.Origin==i&&b.OverOrigin==origin&&b.Remaining==2,""Two-hit lantern and shared cover attribution survive real beneath storage"");}
   Check(st._stampedBeneathByCell.Count==0,""Beneath storage clears after reveal"");
   var wave=new Wave();foreach(int i in route)wave.cells[i]=new object();
   foreach(int start in route)Check(wave.From(start).Count==24,""Existing wall-wave distance must reach every route cell from every trigger"");
  }
  foreach(var id in Enum.GetValues(typeof(ObstacleId)).Cast<ObstacleId>()){
   var e=new StackedObstacleEntry{originCellIndex=31,obstacleId=id};
   Check(e.ResolveRuntimeOrigin(99)==31,""Absent metadata preserves all legacy origins"");
   e.wallPieceOriginPlusOne=4;
   Check(e.ResolveRuntimeOrigin(99)==((id==ObstacleId.Wall||id==ObstacleId.MetalWall)?3:31),""Only wall families accept shared origins"");
   foreach(int bad in new[]{-20,-1,100,10000}){e.wallPieceOriginPlusOne=bad;Check(e.ResolveRuntimeOrigin(99)==31,""Invalid shared origin falls back to placement"");}
  }
  var legacy=new LevelData();legacy.stackedObstacles=route.Select(i=>new StackedObstacleEntry{originCellIndex=i,obstacleId=ObstacleId.Wall}).ToArray();
  var oldSpawn=new Spawner();foreach(var e in legacy.stackedObstacles)oldSpawn.Stamp(legacy,e);
  Check(route.Select(i=>legacy.obstacleOrigins[i]).Distinct().Count()==24&&Editor.Count(legacy,ObstacleId.Wall)==24,""Existing ungrouped wall covers remain independent"");
  Console.WriteLine(""PASS: ""+checks+"" production layered-wall origin/stamp/beneath/editor/wave checks; no board simulation."");
 }
}
";
source=source.Replace("/* ENTRY */",entrySource).Replace("/* STAMP */",method(spawn,"StampStackedEntry"))
 .Replace("/* BENEATH */",beneath).Replace("/* STORE */",method(state,"RegisterStampedBeneath")+method(state,"TryPopStampedBeneathLayer"))
 .Replace("/* COUNT */",method(editor,"CountGoalObstacles")).Replace("/* DISTANCE */",method(wall,"RingDistances"));
await CSharpScript.RunAsync(source+"\nChecks.Run("+(before?"true":"false")+");",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
