// Production origin lift/scale and cancellation; Unity doubles, no build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Actions/SystemOverrideFanoutPlacementAction.cs")).GetRoot();
var cls=root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
var names=new[]{"OriginLiftCells","OriginLiftRiseSeconds","OriginMarkScale","OriginBobPixels","board","origin","sourceOverrideTile","sourceOverrideLifetime","targets","deferredPulseExplosionCells","deferredPatchBotCells","launchPatchBots"};
var members=string.Join("\n",cls.Members.Where(m=>m is ConstructorDeclarationSyntax||m is FieldDeclarationSyntax f&&f.Declaration.Variables.Any(v=>names.Contains(v.Identifier.Text))||m is TypeDeclarationSyntax t&&new[]{"OriginVisualPose","OriginLiftHandle"}.Contains(t.Identifier.Text)||m is MethodDeclarationSyntax md&&new[]{"ResolveOriginTile","StartOriginLift","OriginLiftRoutine","IsCurrentOrigin","HasAnyLiveOriginVisual","ApplyOriginLift","StopOriginLift","Smooth01"}.Contains(md.Identifier.Text)).Select(m=>m.ToFullString())).Replace("SystemOverrideFanoutPlacementAction(","Probe(");
var stub=@"
using System;using System.Collections;using System.Collections.Generic;
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>new Vector3();public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);}
public class Transform {public Vector3 localPosition,localScale=Vector3.one,localEulerAngles;}
public class Coroutine {public IEnumerator e;public bool stopped;}
public static class Time {public static float deltaTime;}
public static class Mathf {public static int Min(int a,int b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Sin(float x)=>(float)Math.Sin(x);public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));public static float Lerp(float a,float b,float t)=>a+(b-a)*t;}
public class OverrideSpecialView {public int begun,stopped;public void BeginActivation(){begun++;}public void Stop(){stopped++;}}
public enum TileSpecial {None,SystemOverride,LineH,LineV,PulseCore,PatchBot}
public class TileView {public Transform[] visuals;public int sortingBegins,sortingEnds;public void BeginOverrideActivationSorting(){sortingBegins++;}public void EndOverrideActivationSorting(){sortingEnds++;}public int LifetimeVersion=1;public bool active=true;public TileSpecial special=TileSpecial.SystemOverride;public TileSpecial GetSpecial()=>special;public bool IsCurrentLifetime(int v)=>active&&LifetimeVersion==v;public OverrideSpecialView OverrideSpecialView=new OverrideSpecialView();}
public class BoardController {public int TileSize=100,Width=3,Height=3;public TileView[,] Tiles=new TileView[3,3];public Coroutine StartCoroutine(IEnumerator e)=>new Coroutine{e=e};public void StopCoroutine(Coroutine c){c.stopped=true;}}
public class Probe {private Transform[] GetImplantBreathVisuals(TileView t)=>t.visuals;
";
var tests=@"
public static void Run(){int checks=0;Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
foreach(int fps in new[]{30,60,120}){
 var visual=new Transform{localPosition=new Vector3(5,7,0),localScale=new Vector3(.8f,1.1f,1),localEulerAngles=new Vector3(0,0,37)};
 var tile=new TileView{visuals=new[]{visual,null}};var board=new BoardController();board.Tiles[1,1]=tile;
 var p=new Probe(board,new Vector2Int(1,1),new List<Vector2Int>(),false,sourceOverrideTile:tile);
 board.Tiles[1,1]=null; // production Line/Pulse consume the source cell before visual execution
 check(ReferenceEquals(p.ResolveOriginTile(),tile),""cleared grid cell retains original source visual"");
 var h=p.StartOriginLift(p.ResolveOriginTile());Time.deltaTime=1f/fps;
 check(tile.sortingBegins==1&&tile.sortingEnds==0,""source foreground scope starts with marking"");
 for(int i=0;i<fps;i++){h.Routine.e.MoveNext();check(visual.localScale.x>=.8f&&visual.localScale.x<=1.0001f,""scale eases without overshoot"");}
 check(Math.Abs(visual.localScale.x-1f)<.001f&&Math.Abs(visual.localScale.y-1.375f)<.001f,""marking holds exactly 25 percent larger at FPS ""+fps);
 check(visual.localEulerAngles.z==37&&tile.OverrideSpecialView.begun==1,""turning keeps its own rotation and never restarts"");
 p.StopOriginLift(h);p.StopOriginLift(h);
 check(tile.sortingEnds==1&&tile.OverrideSpecialView.stopped==1,""cancel/repeated cleanup releases foreground exactly once"");
 check(visual.localPosition.y==7&&Math.Abs(visual.localScale.x-.8f)<.001f&&Math.Abs(visual.localScale.y-1.1f)<.001f&&h.Routine.stopped,""cancel/repeated cleanup restore authored position and nonuniform scale"");
}
{
 var b=new BoardController();var source=new TileView{visuals=new[]{new Transform()}};b.Tiles[1,1]=source;
 var p=new Probe(b,new Vector2Int(1,1),new List<Vector2Int>(),false);b.Tiles[1,1]=null;
 check(ReferenceEquals(p.ResolveOriginTile(),source),""legacy caller captures source before cell removal too"");
 var replacement=new TileView();b.Tiles[1,1]=replacement;
 check(ReferenceEquals(p.ResolveOriginTile(),source),""refill does not steal source animation"");
 var h=p.StartOriginLift(p.ResolveOriginTile());h.Routine.e.MoveNext();
 source.LifetimeVersion++;source.visuals[0].localScale=new Vector3(2,3,1);source.visuals[0].localPosition=new Vector3(9,11,0);
 check(p.ResolveOriginTile()==null&&!h.Routine.e.MoveNext(),""pooled source lifetime stops old lift"");
 int stops=source.OverrideSpecialView.stopped,sortingEnds=source.sortingEnds;p.StopOriginLift(h);p.StopOriginLift(h);
 check(source.sortingEnds==sortingEnds,""stale cleanup cannot remove foreground sorting of a reused tile"");
 check(source.visuals[0].localScale.x==2&&source.visuals[0].localScale.y==3&&source.visuals[0].localPosition.y==11&&source.OverrideSpecialView.stopped==stops,""stale cleanup cannot reset a reused tile or stop its new turn"");
}
{
 var b=new BoardController();var source=new TileView{visuals=new[]{new Transform()}};
 var p=new Probe(b,new Vector2Int(1,1),new List<Vector2Int>(),false,sourceOverrideTile:source);
 source.active=false;check(p.ResolveOriginTile()==null,""scene teardown rejects inactive source"");
 source.active=true;source.special=TileSpecial.None;check(p.ResolveOriginTile()==null,""transformed source cannot animate as Override"");
 var empty=new Probe(null,new Vector2Int(-1,-1),null,false);check(empty.ResolveOriginTile()==null,""missing board/source is safe"");
}
Console.WriteLine(""PASS: ""+checks+"" production Override marking scale/rotation/cancellation checks."");}
}
Probe.Run();
";
await CSharpScript.RunAsync(stub+members+tests);
var fanoutRoot=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Resolver/SpecialFanoutService.cs")).GetRoot();
var creation=fanoutRoot.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single(n=>n.Type.ToString()=="SystemOverrideFanoutPlacementAction");
if(!creation.ArgumentList.Arguments.Any(a=>a.NameColon?.Name.Identifier.Text=="sourceOverrideTile"&&a.Expression.ToString()=="ctx.OverrideFanoutOrigin"))throw new Exception("Shared normal/Line/Pulse/PatchBot fanout lost its source view");
var execute=root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n=>n.Identifier.Text=="ExecuteVisuals");
if(!execute.ToString().Contains("originTile = ResolveOriginTile()")||execute.ToString().Contains("originTile = board.Tiles"))throw new Exception("Placement still relies on a cell that source consumption clears");
Console.WriteLine("PASS: shared fanout passes source view, and placement resolves it independently of grid occupancy.");
