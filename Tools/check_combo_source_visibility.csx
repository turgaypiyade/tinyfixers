// Production-method interleaving checks with UI doubles; no Unity build/Play or board simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System; using System.IO; using System.Linq;
using Microsoft.CodeAnalysis; using Microsoft.CodeAnalysis.CSharp; using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting; using Microsoft.CodeAnalysis.Scripting;
string grid="Assets/_Project/Scripts/Grid/";
string Methods(string file,params string[] names)=>string.Join("\n",CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
foreach(var path in new[]{grid+"TileView.cs",grid+"TileAnimator.cs",grid+"Board/BoardVfxService.cs",grid+"Board/Resolver/SpecialVisualService.cs"}) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var source=@"
using System;using System.Collections;using System.Collections.Generic;
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static bool operator==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y;public static bool operator!=(Vector2 a,Vector2 b)=>!(a==b);public override bool Equals(object o)=>o is Vector2 v&&this==v;public override int GetHashCode()=>0;}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new Vector3(1,1,1);public static Vector3 zero=>new Vector3();public static Vector3 operator*(Vector3 v,float f)=>new Vector3(v.x*f,v.y*f,v.z*f);public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>new Vector3(Mathf.Lerp(a.x,b.x,t),Mathf.Lerp(a.y,b.y,t),Mathf.Lerp(a.z,b.z,t));}
public struct Quaternion {public static Quaternion identity=>new Quaternion();}
public struct Color {public float a;public static Color white=>new Color{a=1};}
public class Transform {public Vector3 localScale=Vector3.one;public Quaternion localRotation;}
public class RectTransform:Transform {public Vector2 pivot;public static implicit operator bool(RectTransform r)=>r!=null;public static bool operator!(RectTransform r)=>r==null;}
public class CanvasGroup {public float alpha=1;public bool blocksRaycasts=true,interactable=true;}
public class GameObject {public bool activeInHierarchy=true;public CanvasGroup group;public int adds;public T AddComponent<T>() where T:new(){adds++;var v=new T();if(v is CanvasGroup c)group=c;return v;}}
public class Image {public Color color=Color.white;public RectTransform rectTransform=new RectTransform();public Transform transform=>rectTransform;}
public class StopView {public void Stop(){}}
public class MissingReferenceException:Exception {}
public class WaitForSeconds {public float seconds;public WaitForSeconds(float s){seconds=s;}}
public static class Time {public static float deltaTime=.02f;}
public static class Mathf {public static float Clamp(float n,float a,float b)=>Math.Max(a,Math.Min(n,b));public static float Clamp01(float n)=>Clamp(n,0,1);public static float Max(float a,float b)=>Math.Max(a,b);public static float Lerp(float a,float b,float k)=>a+(b-a)*Clamp01(k);}
public class TileView {
 public int LifetimeVersion=1;public GameObject gameObject=new GameObject();public RectTransform RectTransform=new RectTransform();public Transform transform=>RectTransform;
 public Image iconImage=new Image();public Image IconImage=>iconImage;public bool KeepHiddenForCombo;public string HiddenBy;
 StopView propellerView,overrideSpecialView,fuseSparkleView;int lastAppliedTileSize;
 public bool TryGetComponent<T>(out T component) where T:class {component=gameObject.group as T;return component!=null;}
 public T GetComponent<T>() where T:class=>gameObject.group as T;
 public static implicit operator bool(TileView t)=>t!=null;public static bool operator!(TileView t)=>t==null;
 public void NoteHidden(string s){HiddenBy=s;}public void ClearMovableObstaclePresentation(){}public void ClearSwapLift(){}public void CancelActiveSettle(){}public void ClearLandingPose(){}public void ApplyTileSize(int n){}
 public void Reuse(){LifetimeVersion++;ResetVisualState();}public void Disable(){LifetimeVersion++;gameObject.activeInHierarchy=false;ResetVisualState();}
 /* TILE */
}
public class BoardController {public int bursts;public void StartCoroutine(IEnumerator routine){bursts++;} /* BOARD */ }
public static class SpecialVisualService { /* HIDE */ }
public static class TileClearBurstVfx {public static IEnumerator CoPlayBurst(TileView t,BoardController b,float seconds){yield break;}}
public class TileAnimator {
 readonly BoardController board;public TileAnimator(BoardController b){board=b;}
 const float BURST_DURATION=.06f,TILE_SHRINK_END=0,TILE_SHRINK_MID=.55f,BURST_VFX_DURATION=.3f;
 static readonly Vector2 CenterPivot=new Vector2(.5f,.5f);static void SetPivotWithoutVisualJump(RectTransform rt,Vector2 p){rt.pivot=p;}
 public void Restore(TileView t){RestoreTileVisualState(t);}
 /* ANIM */
}
public class Routine {
 readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();public Routine(IEnumerator e){stack.Push(e);}public bool Done=>stack.Count==0;
 public void Step(){while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();(e as IDisposable)?.Dispose();continue;}if(e.Current is IEnumerator child){stack.Push(child);continue;}return;}}
 public void Finish(){for(int n=0;n<100&&!Done;n++)Step();if(!Done)throw new Exception(""stalled"");}
 public void Cancel(){while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
}
".Replace("/* TILE */",Methods(grid+"TileView.cs","ResetVisualState","SetIconAlpha","IsCurrentLifetime","RunForCurrentLifetime","RunForLifetime"))
 .Replace("/* BOARD */",Methods(grid+"Board/BoardController.cs","RestoreTilePresentation"))
 .Replace("/* HIDE */",Methods(grid+"Board/Resolver/SpecialVisualService.cs","HideTileVisualForCombo"))
 .Replace("/* ANIM */",Methods(grid+"TileAnimator.cs","EmptyAnimation","PlayPop","PlayPopCore","PlayPulseImpact","PlayPulseImpactCore","RestoreTileVisualState"));
source+=@"
int checks=0;Action<bool,string> check=(ok,message)=>{checks++;if(!ok)throw new Exception(message);};
// Control: alpha-only hiding is undone by the real board refresh method.
var b=new BoardController();var anim=new TileAnimator(b);var control=new TileView();SpecialVisualService.HideTileVisualForCombo(control);control.KeepHiddenForCombo=false;b.RestoreTilePresentation(control);
check(control.gameObject.group.alpha==1,""alpha-only control reproduces the duplicate source icon"");
foreach(float dt in new[]{1f/30,1f/60,1f/120}) {
 Time.deltaTime=dt;var a=new TileView();var c=new TileView();SpecialVisualService.HideTileVisualForCombo(a);SpecialVisualService.HideTileVisualForCombo(c);
 for(int frame=0;frame<90;frame++) {
  b.RestoreTilePresentation(a);b.RestoreTilePresentation(c);anim.Restore(a);anim.Restore(c);
  check(a.gameObject.group.alpha==0&&c.gameObject.group.alpha==0,""both sources stay hidden throughout concurrent refreshes"");
 }
 new Routine(anim.PlayPop(a,.06f)).Finish();new Routine(anim.PlayPulseImpact(c,.1f,.2f)).Finish();
 check(a.gameObject.group.alpha==0&&c.gameObject.group.alpha==0&&b.bursts==0,""queued clears never revive consumed source images or spawn their pop bursts"");
 SpecialVisualService.HideTileVisualForCombo(a);check(a.gameObject.adds==1,""repeated hiding reuses CanvasGroup"");
 check(!a.gameObject.group.blocksRaycasts&&!a.gameObject.group.interactable,""hidden consumed source cannot intercept input"");
 var live=new TileView();var delayed=new Routine(anim.PlayPulseImpact(live,.1f,.2f));delayed.Step();SpecialVisualService.HideTileVisualForCombo(live);delayed.Finish();
 check(live.gameObject.group.alpha==0,""hide during impact delay wins"");
 foreach(bool pulse in new[]{false,true}) {
  live=new TileView();var running=new Routine(pulse?anim.PlayPulseImpact(live,0,.2f):anim.PlayPop(live,.06f,true));running.Step();SpecialVisualService.HideTileVisualForCombo(live);running.Finish();
  check(live.gameObject.group.alpha==0,""hide during an active clear stops later alpha writes"");
 }
 live=new TileView();var old=new Routine(anim.PlayPulseImpact(live,.1f,.2f));old.Step();SpecialVisualService.HideTileVisualForCombo(live);live.Reuse();old.Finish();b.RestoreTilePresentation(live);
 check(!live.KeepHiddenForCombo&&live.gameObject.group.alpha==1&&live.gameObject.group.blocksRaycasts&&live.gameObject.group.interactable,""production pool reset restores the next lifetime and stale impact cannot hide it"");
 new Routine(anim.PlayPop(live,.06f,true)).Finish();check(live.gameObject.group.alpha==0,""ordinary pop still runs on reused tile"");
 live=new TileView();SpecialVisualService.HideTileVisualForCombo(live);var cancelled=new Routine(anim.PlayPulseImpact(live,.1f,.2f));cancelled.Step();cancelled.Cancel();
 check(live.gameObject.group.alpha==0&&live.KeepHiddenForCombo,""cancelled presentation cannot release consumption-owned hiding"");
 live.Disable();check(!live.KeepHiddenForCombo&&live.gameObject.group.alpha==1,""production teardown reset clears hidden state"");
 live=new TileView();new Routine(anim.PlayPulseImpact(live,0,.2f)).Finish();check(live.gameObject.group.alpha<.01f,""ordinary pulse impact still animates"");
}
Console.WriteLine(""PASS: ""+checks+"" production combo-source visibility / concurrent refresh / pool reset checks. UI doubles; no Unity Play/build."");
";
await CSharpScript.RunAsync(source,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
