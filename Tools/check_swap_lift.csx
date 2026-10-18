// csi Tools/check_swap_lift.csx — production movement with Unity doubles; no Unity build.
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
var source = File.ReadAllText("Assets/_Project/Scripts/Grid/TileView.cs");
var names = new[]{"MoveToGrid", "ApplySwapLift", "ClearSwapLift", "SnapToGrid", "ResetVisualScale", "IsCurrentLifetime"};
var methods = string.Join("\n", CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes()
    .OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var swapAction = CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Actions/SwapAction.cs"))
    .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single().ToFullString();
var code = @"
using System;
using System.Collections;
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero=>new Vector2();
 public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 public static float Distance(Vector2 a,Vector2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));
 public static Vector2 LerpUnclamped(Vector2 a,Vector2 b,float t)=>a+(b-a)*t;
}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 one=>new Vector3(1,1,1);}
public class RectTransform {
 public Vector2 anchoredPosition,pivot=new Vector2(0,1),anchorMin,anchorMax;
 public RectTransform parent;public Rect rect=new Rect();public Vector3 localScale=Vector3.one;
 public static implicit operator bool(RectTransform r)=>r!=null;public static bool operator!(RectTransform r)=>r==null;
}
public class Rect {public float width=100,height=100;}
public class Image {public RectTransform rectTransform=new RectTransform();}
public class GameObject {public bool activeInHierarchy=true;}
public class AnimationCurve {public float Evaluate(float x)=>x*x*(3-2*x);}
public static class Time {public static float deltaTime;}
public static class Mathf {
 public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Clamp01(float x)=>Math.Max(0,Math.Min(x,1));
}
public enum TileSpecial {None,LineH}
public enum TileRuntimeState {Idle,Swapping,Falling}
public class Model {public TileSpecial special;}
public class Board {public float SwapLiftAmount=.2f;public int FallGeneration;}
public class TileView {
 public RectTransform rt=new RectTransform(),parentRt;
 public Image iconImage=new Image();public Model model=new Model();public Board board=new Board();
 public GameObject gameObject=new GameObject();public int X,Y,LifetimeVersion,activeMoveToken,lastFallGeneration;
 public TileRuntimeState RuntimeState; public void SetRuntimeState(TileRuntimeState s){RuntimeState=s;}
 public bool isMovableObstacleTile,useFullCellIcon,swapLiftApplied;public Vector2 swapLiftOffset;
 public Vector2 iconSize=new Vector2(100,100),IconReferenceSize=new Vector2(100,100);
 public int arrivals,earlyArrivals;
 public static implicit operator bool(TileView t)=>t!=null;
 public T GetComponent<T>() where T:new()=>new T();
 void CancelActiveSettle(){} void ClearLandingPose(){} void ApplyTileSize(int s){}
 void RaiseFallArrived(){arrivals++;}
 void MaybeRaiseEarlyFallArrived(Vector2 e,int size){earlyArrivals++;}
 public void TakeOver(){ClearSwapLift();activeMoveToken++;}
 public void Reuse(){ClearSwapLift();LifetimeVersion++;activeMoveToken++;}
 public Vector2 Centre=>rt.anchoredPosition+new Vector2(50*rt.localScale.x,-50*rt.localScale.y);
 " + methods + @"
}
public abstract class BoardAction {public abstract IEnumerator ExecuteVisuals(ActionSequencer s);}
public class ActionSequencer {public Animator Animator=new Animator();}
public class Animator {public IEnumerator SwapTilesAnimated(TileView a,TileView b,float d){yield return null;}}
" + swapAction + @"
int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
foreach(int fps in new[]{30,60,120}) foreach(var target in new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1)}) foreach(bool lifted in new[]{true,false}) {
 Time.deltaTime=1f/fps;var t=new TileView{X=(int)target.x,Y=(int)target.y};
 var e=t.MoveToGrid(100,.28f,new AnimationCurve(),liftDuringSwap:lifted);int frames=0;float peak=1;
 while(e.MoveNext()) {
  frames++;peak=Math.Max(peak,t.rt.localScale.x);
  float k=Math.Min(frames*Time.deltaTime/.28f,1);float eased=k*k*(3-2*k);
  Check(Near(t.Centre.x,50+target.x*100*eased)&&Near(t.Centre.y,-50-target.y*100*eased),""centred lift must not distort travel"");
  if(frames==1)Check(Vector2.Distance(t.rt.anchoredPosition,new Vector2(target.x*100,-target.y*100))>1,""first frame must animate, including horizontal swaps"");
  Check(t.rt.localScale.x<=1.201f,""no oversized lift"");
  Check(frames<100,""movement terminates"");
 }
 Check(Near(peak,lifted?1.2f:1),""selected tile only reaches 20 percent lift"");
 Check(Near(t.rt.localScale.x,1)&&!t.swapLiftApplied,""scale cleared on arrival"");
 Check(Near(t.rt.anchoredPosition.x,target.x*100)&&Near(t.rt.anchoredPosition.y,-target.y*100),""exact grid arrival"");
 Check(frames*Time.deltaTime>=.28f&&frames*Time.deltaTime<.28f+Time.deltaTime+.001f,""duration unchanged"");
 Check(t.arrivals==1,""arrival event still fires once"");
 // Invalid swap returns along the same path, without retaining lift.
 t.X=0;t.Y=0;e=t.MoveToGrid(100,.28f,liftDuringSwap:true);while(e.MoveNext()){}
 Check(Near(t.rt.anchoredPosition.x,0)&&Near(t.rt.anchoredPosition.y,0)&&Near(t.rt.localScale.x,1),""reverse cleanup"");
}
Time.deltaTime=.03f;
{
 var t=new TileView{X=1};var e=t.MoveToGrid(100,.28f,liftDuringSwap:true);e.MoveNext();
 ((IDisposable)e).Dispose();Check(!t.swapLiftApplied&&Near(t.rt.localScale.x,1),""cancel restores scale"");
}
foreach(bool reuse in new[]{false,true}) {
 var t=new TileView{X=1};var e=t.MoveToGrid(100,.03f,liftDuringSwap:true);e.MoveNext();
 if(reuse)t.Reuse();else t.TakeOver();t.rt.anchoredPosition=new Vector2(999,888);t.rt.localScale=new Vector3(2,2,1);
 Check(!e.MoveNext(),""stale last-frame coroutine exits"");
 Check(Near(t.rt.anchoredPosition.x,999)&&Near(t.rt.localScale.x,2),""stale completion cannot snap or resize new owner"");
}
{
 var t=new TileView();var e=t.MoveToGrid(100,.28f,liftDuringSwap:true);
 Check(!e.MoveNext()&&t.arrivals==1&&!t.swapLiftApplied,""zero distance does not animate"");
}
{
 var a=new TileView();var b=new TileView();var e=new SwapAction(a,b,.28f).ExecuteVisuals(new ActionSequencer());e.MoveNext();
 a.Reuse();a.SetRuntimeState(TileRuntimeState.Swapping);b.SetRuntimeState(TileRuntimeState.Falling);((IDisposable)e).Dispose();
 Check(a.RuntimeState==TileRuntimeState.Swapping,""old swap must not idle a reused tile"");
 Check(b.RuntimeState==TileRuntimeState.Falling,""old swap must not idle gravity ownership"");
 var c=new TileView();e=new SwapAction(c,new TileView(),.28f).ExecuteVisuals(new ActionSequencer());e.MoveNext();((IDisposable)e).Dispose();
 Check(c.RuntimeState==TileRuntimeState.Idle,""ordinary cancellation returns swap to idle"");
}
Console.WriteLine(""Swap lift: ""+checks+"" PASS (30/60/120 fps, four directions, reverse, cancel, reuse). No Unity build."");
";
await CSharpScript.RunAsync(code, ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
