// Isolated production coroutine checks with UI doubles; no Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

var text=File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Actions/GelLauncherFireAction.cs");
var tree=CSharpSyntaxTree.ParseText(text);
var syntaxErrors=tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(syntaxErrors.Length>0)throw new Exception(string.Join("\n",syntaxErrors.Select(d=>d.ToString())));
var cls=tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.ValueText=="GelLauncherFireAction");
var production=cls.ToFullString().Replace("public sealed class GelLauncherFireAction", "public sealed partial class GelLauncherFireAction");
var stubs=@"
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
public class Sprite {public string name;public Sprite(string n){name=n;}}
public struct Vector2Int {
 public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}
 public static Vector2Int operator+(Vector2Int a,Vector2Int b)=>new Vector2Int(a.x+b.x,a.y+b.y);
 public static Vector2Int operator-(Vector2Int a,Vector2Int b)=>new Vector2Int(a.x-b.x,a.y-b.y);
 public static bool operator==(Vector2Int a,Vector2Int b)=>a.x==b.x&&a.y==b.y;public static bool operator!=(Vector2Int a,Vector2Int b)=>!(a==b);
 public override bool Equals(object o)=>o is Vector2Int v&&v==this;public override int GetHashCode()=>x*397^y;
}
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator*(Vector2 a,float k)=>new Vector2(a.x*k,a.y*k);
 public float magnitude=>(float)Math.Sqrt(x*x+y*y);
 public static implicit operator Vector3(Vector2 a)=>new Vector3(a.x,a.y,0);
 public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);
}
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 one=>new Vector3(1,1,1);
 public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
 public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
 public static Vector3 operator*(Vector3 a,float k)=>new Vector3(a.x*k,a.y*k,a.z*k);
 public static Vector3 LerpUnclamped(Vector3 a,Vector3 b,float k)=>a+(b-a)*k;
 public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
}
public struct Quaternion {public float angle;public static Quaternion identity=>new Quaternion();public static Quaternion Euler(float x,float y,float z)=>new Quaternion{angle=z};}
public struct Color {public float a;public static Color white=>new Color{a=1};}
public struct Rect {public float width,height;public Vector2 center;}
public static class Mathf {
 public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
 public static float Abs(float a)=>Math.Abs(a);public static float Pow(float a,int b)=>(float)Math.Pow(a,b);
 public static float Lerp(float a,float b,float k)=>a+(b-a)*Math.Max(0,Math.Min(1,k));
}
public static class Time {public static float deltaTime=1f/60;}
public class Object {public static void Destroy(object o){if(o is GameObject g){g.destroyed=true;g.active=false;}}}
public class GameObject {public int layer;public bool active=true,destroyed;public RectTransform transform;public Image image;}
public class RectTransform {
 public GameObject gameObject;public RectTransform parent;public Vector2 anchorMin,anchorMax,pivot=new Vector2(.5f,.5f),sizeDelta;
 public Vector3 position,localScale=Vector3.one;public Quaternion localRotation;
 public Rect rect=>new Rect{width=sizeDelta.x,height=sizeDelta.y,center=new Vector2((.5f-pivot.x)*sizeDelta.x,(.5f-pivot.y)*sizeDelta.y)};
 public void SetParent(RectTransform p,bool keep){parent=p;}public void SetAsLastSibling(){}
 public Vector3 TransformPoint(Vector3 p)=>position+new Vector3(p.x*localScale.x,p.y*localScale.y,p.z);
 public Vector3 InverseTransformPoint(Vector3 p)=>p-position;public Vector3 InverseTransformVector(Vector3 p)=>p;
 public T GetComponent<T>() where T:class=>typeof(T)==typeof(Image)?gameObject.image as T:gameObject.image.GetComponent<T>();
 public void GetWorldCorners(Vector3[] c){c[0]=TransformPoint(new Vector3(-rect.width/2,-rect.height/2,0));c[2]=TransformPoint(new Vector3(rect.width/2,rect.height/2,0));}
}
public class Image {
 public GameObject gameObject;public RectTransform rectTransform;public Sprite sprite;public bool raycastTarget,preserveAspect;public Color color=Color.white;
 public GelLauncherIdleFx idle;public GelLauncherPressView press;
 public Image(){gameObject=new GameObject();rectTransform=new RectTransform{gameObject=gameObject};gameObject.transform=rectTransform;gameObject.image=this;}
 public T GetComponent<T>() where T:class=>typeof(T)==typeof(GelLauncherPressView)?press as T:idle as T;
}
public class GelLauncherIdleFx {public bool enabled=true,stopped;public void StopAllCoroutines(){stopped=true;}}
public static class UiVfxPool {
 public static Dictionary<string,Stack<Image>> pool=new Dictionary<string,Stack<Image>>();public static int created,returns;
 public static Image body,cap;
 public static Image RentImage(string key,RectTransform parent,string name){
  if(!pool.TryGetValue(key,out var s))pool[key]=s=new Stack<Image>();
  Image i;if(s.Count>0)i=s.Pop();else{i=new Image();created++;}
  i.gameObject.active=true;i.gameObject.destroyed=false;i.sprite=null;i.color=Color.white;i.preserveAspect=false;i.press=null;
  i.rectTransform.localScale=Vector3.one;i.rectTransform.pivot=new Vector2(.5f,.5f);i.rectTransform.SetParent(parent,false);
  if(key.EndsWith(""Body""))body=i;else cap=i;return i;
 }
 public static void Return(string key,GameObject g,int max){if(!g.active)throw new Exception(""double pool return"");g.active=false;pool[key].Push(g.image);returns++;}
}
public static class Resources {public static bool missingCap;public static T Load<T>(string path) where T:class=>missingCap?null:new Sprite(path) as T;}
public enum ObstacleId {GelLauncherUp,GelLauncherDown,GelLauncherLeft,GelLauncherRight}
public class GelLauncherPressView {
 public static bool UsePress;public bool compressed;public float opacity=1;
 public Sprite FrameSprite=new Sprite(""press-frame""),HeadSprite=new Sprite(""gold-head"");
 public static GelLauncherPressView Ensure(Image image,ObstacleId id,int hits){if(!UsePress)return null;var v=new GelLauncherPressView();image.press=v;image.sprite=v.FrameSprite;return v;}
 public IEnumerator CompressForFire(){yield return new WaitForSeconds(.45f);compressed=true;}
 public void SetOpacity(float value){opacity=value;}
}
public class Stage {public Sprite sprite=new Sprite(""cracked"");}
public class Definition {public List<Stage> stages=new List<Stage>{new Stage(),new Stage()};}
public class ObstacleLibrary {public Definition def=new Definition();public Definition Get(ObstacleId id)=>def;}
public class LevelData {public ObstacleLibrary obstacleLibrary=new ObstacleLibrary();}
public class ActionSequencer{}
public abstract class BoardAction {public abstract IEnumerator ExecuteVisuals(ActionSequencer s);}
public class CellHold:IDisposable {public BoardController board;bool done;public void Dispose(){if(!done){done=true;board.holds--;}}}
public class Job:IDisposable {public BoardController board;bool done;public void Dispose(){if(!done){done=true;board.jobs--;}}}
public class WaitForSeconds {public float seconds;public WaitForSeconds(float s){seconds=s;}}
public sealed class Coroutine:IDisposable {
 public Stack<IEnumerator> stack=new Stack<IEnumerator>();public float wait;public bool done;
 public Coroutine(IEnumerator r){stack.Push(r);}
 public void Tick(){if(done)return;if(wait>0){wait-=Time.deltaTime;if(wait>0)return;}
  while(stack.Count>0){var r=stack.Peek();if(!r.MoveNext()){(r as IDisposable)?.Dispose();stack.Pop();continue;}
   if(r.Current is IEnumerator nested){stack.Push(nested);continue;}
   if(r.Current is WaitForSeconds w)wait=w.seconds;
   return;
  }done=true;
 }
 public void Dispose(){if(done)return;while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();done=true;}
}
public class BoardController {
 public enum BoardJobKind{ObstacleSpread}
 public LevelData LevelData=new LevelData();public bool isActiveAndEnabled=true;public int Width=5,Height=7,jobs,holds,resolves;
 public float TileSize=100;public RectTransform BreakFxParent=new Image().rectTransform;
 public Vfx BoardVfxPlayer;public ObstacleState ObstacleStateService=new ObstacleState();
 public List<Vector2Int> painted=new List<Vector2Int>();public List<Coroutine> routines=new List<Coroutine>();
 public IDisposable BeginJob(BoardJobKind k){jobs++;return new Job{board=this};}
 public CellHold HoldForFlow(List<Vector2Int> f){holds++;return new CellHold{board=this};}
 public Vector3 GetCellWorldCenterPosition(int x,int y)=>new Vector3(x*100,-y*100,0);
 public Coroutine StartCoroutine(IEnumerator r){var c=new Coroutine(r);routines.Add(c);c.Tick();return c;}
 public void StopCoroutine(Coroutine c){c.Dispose();}
 public void Tick(){foreach(var c in routines.ToArray())c.Tick();routines.RemoveAll(c=>c.done);}
 public void EnqueueBoardAction(GelLauncherBlastAction b){b.Completed=true;}
 public bool IsMaskHoleCell(int x,int y)=>false;public void PaintGelAt(int x,int y){painted.Add(new Vector2Int(x,y));}
 public void RequestResolveAfterActionSequence(){resolves++;}
}
public class Vfx {public RectTransform VfxRoot;}
public class ObstacleState {public bool IsCellBlocked(int x,int y)=>false;}
public class GelLauncherBlastAction {public bool Completed;public GelLauncherBlastAction(BoardController b,LevelData l,Vector2Int c){}}
public static class WaterTankFx {
 public static Vector2 WorldToLocal(Vector3 p,RectTransform rt)=>p;
 public static void SpawnShards(BoardController b,RectTransform r,Vector2 c,float size,Sprite s,int count,float scale){}
}
public static class GelLauncherFx {
 public static Image pending;public static int steam;
 public static bool IsVertical(ObstacleId id)=>id==ObstacleId.GelLauncherUp||id==ObstacleId.GelLauncherDown;
 public static Vector2Int Direction(ObstacleId id)=>id==ObstacleId.GelLauncherUp?new Vector2Int(0,-1):id==ObstacleId.GelLauncherDown?new Vector2Int(0,1):id==ObstacleId.GelLauncherLeft?new Vector2Int(-1,0):new Vector2Int(1,0);
 public static Vector2 UiDirection(ObstacleId id){var d=Direction(id);return new Vector2(d.x,-d.y);}
 public static Image TakeBody(int i){var p=pending;pending=null;return p;}
 public static Sprite LoadOpenSprite(ObstacleId id)=>new Sprite(""open"");
 public static void SpawnSteam(BoardController b,RectTransform r,Vector2 p,Vector2 d,float size,int count,float scale){steam+=count;}
}
";
var tests=@"
public sealed partial class GelLauncherFireAction {
 static int checks;
 static void Check(bool v,string message){checks++;if(!v)throw new Exception(message);}
 static float Along(Vector3 p,Vector3 home,Vector3 step)=>Vector3.Dot(p-home,step)/step.sqrMagnitude;
 static void Reset(){UiVfxPool.body=null;UiVfxPool.cap=null;GelLauncherFx.pending=null;Resources.missingCap=false;capSprite=null;}
 public static void RunChecks(){
  foreach(float fps in new[]{30f,60f,120f})foreach(ObstacleId id in Enum.GetValues(typeof(ObstacleId))){
   Reset();Time.deltaTime=1/fps;var b=new BoardController();var a=new GelLauncherFireAction(b,new Vector2Int(2,3),id);
   var dir=GelLauncherFx.Direction(id);var fp=Footprint(a.origin,id);var front=FrontCell(fp,dir);var back=front-dir;
   var home=(b.GetCellWorldCenterPosition(front.x,front.y)+b.GetCellWorldCenterPosition(back.x,back.y))*.5f;
   var step=b.GetCellWorldCenterPosition(front.x,front.y)-b.GetCellWorldCenterPosition(back.x,back.y);
   var run=new Coroutine(a.ExecuteVisuals(null));float min=0,maxBulge=1;bool launched=false;int frames=0;
   while(!run.done){
    run.Tick();
    if(UiVfxPool.body!=null&&UiVfxPool.body.gameObject.active){
     var rt=UiVfxPool.body.rectTransform;min=Math.Min(min,Along(rt.position,home,step));
     maxBulge=Math.Max(maxBulge,GelLauncherFx.IsVertical(id)?rt.localScale.x:rt.localScale.y);
     if(UiVfxPool.cap==null)Check(UiVfxPool.body.sprite.name==""cracked"",""closed cracked art retained through pullback/lunge"");
    }
    if(!launched&&UiVfxPool.cap!=null){
     launched=true;Check(min<-.27f&&maxBulge>1.23f,""whole body retreats and widens before cap exists"");
     var rt=UiVfxPool.body.rectTransform;float forward=Along(rt.position,home,step);
     Check(forward>.13f&&forward<.16f,""cap releases just past original center"");
     Check(UiVfxPool.body.sprite.name==""open"",""open art appears only at cap release"");
     Check((UiVfxPool.cap.rectTransform.position-a.MouthWorldPosition(rt)).sqrMagnitude<.001f,""cap starts at the deformed mouth in every direction"");
     var first=UiVfxPool.cap.rectTransform.position;b.Tick();run.Tick();
     Check(Along(UiVfxPool.cap.rectTransform.position,first,step)>.05f,""cap immediately travels outward without a pop pause"");
    }else b.Tick();
    Check(++frames<fps*4,""shot and body fade finish within bounds"");
   }
   Check(launched&&a.bodyVisualDone,""body finale completes even on short firing lanes"");
   Check(b.jobs==0&&b.holds==0&&b.resolves==1,""normal completion releases flow/job exactly once"");
   Check(!UiVfxPool.cap.gameObject.active&&!UiVfxPool.body.gameObject.active,""cap and fallback body returned"");
   Check(UiVfxPool.cap.sprite==null&&UiVfxPool.body.sprite==null,""pool retains no sprite references"");
   Check(b.painted.Count==fp.Count+a.BuildPath(front,dir).Count,""gel still covers footprint and the original firing lane"");
   int created=UiVfxPool.created;var oldCap=UiVfxPool.cap;var oldBody=UiVfxPool.body;
   var repeat=new Coroutine(new GelLauncherFireAction(b,a.origin,id).ExecuteVisuals(null));
   for(int i=0;!repeat.done&&i<fps*4;i++){repeat.Tick();b.Tick();}
   Check(repeat.done&&UiVfxPool.created==created&&ReferenceEquals(oldCap,UiVfxPool.cap)&&ReferenceEquals(oldBody,UiVfxPool.body),""repeat shot reuses cap/body objects"");
  }
  foreach(float cancelTime in new[]{.16f,.43f,.53f,.8f}){
   Reset();Time.deltaTime=1f/120;var b=new BoardController();var a=new GelLauncherFireAction(b,new Vector2Int(2,3),ObstacleId.GelLauncherUp);
   var run=new Coroutine(a.ExecuteVisuals(null));for(float t=0;t<cancelTime;t+=Time.deltaTime){run.Tick();b.Tick();}
   run.Dispose();b.Tick();int returns=UiVfxPool.returns,painted=b.painted.Count;
   Check(b.jobs==0&&b.holds==0&&b.routines.Count==0,""cancellation stops child drivers before returning visuals"");
   for(int i=0;i<100;i++)b.Tick();run.Dispose();
   Check(UiVfxPool.returns==returns&&b.painted.Count==painted,""no double return, late gel painting or pooled-view mutation"");
  }
  foreach(bool disable in new[]{false,true}){
   Reset();var b=new BoardController();var run=new Coroutine(new GelLauncherFireAction(b,new Vector2Int(2,3),ObstacleId.GelLauncherRight).ExecuteVisuals(null));
   for(int i=0;i<12;i++){run.Tick();b.Tick();}
   if(disable)b.isActiveAndEnabled=false;else b.LevelData=new LevelData();
   for(int i=0;i<20&&!run.done;i++){run.Tick();b.Tick();}
   Check(run.done&&b.jobs==0&&b.holds==0,""disable/scene-level change releases live body and holds"");
  }
  Reset();{
   var b=new BoardController();var img=new Image();img.rectTransform.sizeDelta=new Vector2(100,200);
   img.rectTransform.position=new Vector3(200,-350,0);img.idle=new GelLauncherIdleFx();GelLauncherFx.pending=img;
   var run=new Coroutine(new GelLauncherFireAction(b,new Vector2Int(2,3),ObstacleId.GelLauncherUp).ExecuteVisuals(null));run.Tick();
   Check(img.idle.stopped&&!img.idle.enabled,""idle coroutine cannot overwrite firing motion in the same frame"");
   run.Dispose();Check(img.gameObject.destroyed&&b.jobs==0&&b.holds==0,""claimed real body destroyed, never put into plain-image pool"");
  }
  Reset();Resources.missingCap=true;{
   var b=new BoardController();var run=new Coroutine(new GelLauncherFireAction(b,new Vector2Int(2,3),ObstacleId.GelLauncherUp).ExecuteVisuals(null));
   for(int i=0;i<400&&!run.done;i++){run.Tick();b.Tick();}
   Check(run.done&&b.jobs==0&&b.holds==0,""missing cap art preserves completion and cleanup"");
  }
  Resources.missingCap=false;
  GelLauncherPressView.UsePress=true;
  foreach(ObstacleId id in Enum.GetValues(typeof(ObstacleId))){
   Reset();var b=new BoardController();var a=new GelLauncherFireAction(b,new Vector2Int(1,2),id);
   var fp=Footprint(a.origin,id);var dir=GelLauncherFx.Direction(id);var front=FrontCell(fp,dir);
   var run=new Coroutine(a.ExecuteVisuals(null));bool sawCap=false;int frames=0;
   while(!run.done){run.Tick();
    if(UiVfxPool.cap!=null&&UiVfxPool.cap.gameObject.active){sawCap=true;Check(UiVfxPool.body.press.compressed,""final stroke precedes projectile"");Check(UiVfxPool.cap.sprite.name==""gold-head"",""new golden projectile"");Check(UiVfxPool.body.sprite.name==""press-frame"",""no flash of legacy blue art"");}
    b.Tick();Check(++frames<500,""press shot bounded"");
   }
   Check(sawCap&&b.jobs==0&&b.holds==0,""press finale releases job and footprint"");
   Check(b.painted.Count==fp.Count+a.BuildPath(front,dir).Count,""same gel lane in four directions"");
   Check(UiVfxPool.body.press.opacity==0,""layered press fades completely"");
  }
  Reset();{
   var b=new BoardController();var run=new Coroutine(new GelLauncherFireAction(b,new Vector2Int(1,2),ObstacleId.GelLauncherUp).ExecuteVisuals(null));
   for(int i=0;i<8;i++){run.Tick();b.Tick();}run.Dispose();
   Check(b.jobs==0&&b.holds==0,""cancel during compression releases ownership"");Check(UiVfxPool.body==null||!UiVfxPool.body.gameObject.active,""cancel returns fallback body"");
  }
  GelLauncherPressView.UsePress=false;
  Console.WriteLine(""PASS: ""+checks+"" production motion samples, four directions at 30/60/120 Hz, cap timing, pool reuse, cancellation and cleanup checks. UI doubles; no Unity playback/performance measurement."");
 }
}
GelLauncherFireAction.RunChecks();
";
var script=CSharpScript.Create(stubs+production+tests,ScriptOptions.Default.WithReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Take(20).Select(d=>d.ToString())));
script.RunAsync().GetAwaiter().GetResult();
if(text.Contains("yield return CapPop")||text.Contains("new GameObject(\"GelLauncherCap\"")||text.Contains("new GameObject(\"GelLauncherBody\""))throw new Exception("Delayed cap pop or per-shot UI object construction returned");
Console.WriteLine("PASS: complete production file syntax; no delayed cap pop or direct cap/body GameObject construction.");
