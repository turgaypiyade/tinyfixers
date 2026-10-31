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
 public List<Vector2Int> impacts=new List<Vector2Int>();public List<string> events=new List<string>();
 public List<Vector2Int> painted=new List<Vector2Int>();public List<Coroutine> routines=new List<Coroutine>();
 public IDisposable BeginJob(BoardJobKind k){jobs++;return new Job{board=this};}
 public CellHold HoldForFlow(List<Vector2Int> f){holds++;return new CellHold{board=this};}
 public Vector3 GetCellWorldCenterPosition(int x,int y)=>new Vector3(x*100,-y*100,0);
 public Coroutine StartCoroutine(IEnumerator r){var c=new Coroutine(r);routines.Add(c);c.Tick();return c;}
 public void StopCoroutine(Coroutine c){c.Dispose();}
 public void Tick(){foreach(var c in routines.ToArray())c.Tick();routines.RemoveAll(c=>c.done);}
 
 public bool IsMaskHoleCell(int x,int y)=>false;public void PaintGelAt(int x,int y){painted.Add(new Vector2Int(x,y));events.Add(""paint:""+x+"",""+y);}
 public void RequestResolveAfterActionSequence(){resolves++;}
}
public class Vfx {public RectTransform VfxRoot;}
public class ObstacleState {public bool IsCellBlocked(int x,int y)=>false;}
public class BoosterCellImpactPass {readonly BoardController b;public BoosterCellImpactPass(BoardController board){b=board;}public void HitCell(Vector2Int cell){b.impacts.Add(cell);b.events.Add(""hit:""+cell.x+"",""+cell.y);}}
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
 static Image Body(BoardController b,GelLauncherFireAction a){
  var fp=Footprint(a.origin,a.launcherId);var img=new Image();
  img.rectTransform.sizeDelta=GelLauncherFx.IsVertical(a.launcherId)?new Vector2(100,200):new Vector2(200,100);
  img.rectTransform.position=(b.GetCellWorldCenterPosition(fp[0].x,fp[0].y)+b.GetCellWorldCenterPosition(fp[1].x,fp[1].y))*.5f;
  img.sprite=new Sprite(""cracked"");img.idle=new GelLauncherIdleFx();GelLauncherFx.pending=img;return img;
 }
 public static void RunChecks(){
  GelLauncherPressView.UsePress=true;
  foreach(float fps in new[]{30f,60f,120f})foreach(ObstacleId id in Enum.GetValues(typeof(ObstacleId))){
   Time.deltaTime=1/fps;var b=new BoardController();var a=new GelLauncherFireAction(b,new Vector2Int(2,3),id);var body=Body(b,a);
   var dir=GelLauncherFx.Direction(id);var fp=Footprint(a.origin,id);var front=FrontCell(fp,dir);var path=a.BuildPath(front,dir);
   var run=new Coroutine(a.ExecuteVisuals(null));bool launched=false;int frames=0;
   while(!run.done){run.Tick();
    if(UiVfxPool.cap!=null&&UiVfxPool.cap.gameObject.active){
     launched=true;Check(body.press!=null&&body.press.compressed,""compression finishes before cap departs"");
     Check(UiVfxPool.cap.sprite==body.press.HeadSprite,""real press head is the projectile"");
    }
    b.Tick();Check(++frames<fps*4,""shot and real body fade finish within bounds"");
   }
   Check(launched&&a.bodyVisualDone&&body.press.opacity==0,""real press body completes fade even on short firing lanes"");
   Check(b.jobs==0&&b.holds==0&&b.resolves==1,""normal completion releases body/path holds and job exactly once"");
   Check(body.gameObject.destroyed&&!UiVfxPool.cap.gameObject.active&&UiVfxPool.cap.sprite==null,""real body destroyed; detached cap returned without sprite retention"");
   Check(b.impacts.SequenceEqual(path),""every arrived cap cell immediately uses shared Lift impact in order"");
   Check(b.painted.Count==fp.Count+path.Count,""body and original firing lane receive gel exactly once"");
   foreach(var cell in path){var suffix=cell.x+"",""+cell.y;
    Check(b.events.IndexOf(""paint:""+suffix)>b.events.IndexOf(""hit:""+suffix),""path gel follows its arrived impact, never leads cap"");
   }
   int created=UiVfxPool.created;var oldCap=UiVfxPool.cap;
   var repeat=new GelLauncherFireAction(b,a.origin,id);Body(b,repeat);var replay=new Coroutine(repeat.ExecuteVisuals(null));
   for(int i=0;!replay.done&&i<fps*4;i++){replay.Tick();b.Tick();}
   Check(replay.done&&UiVfxPool.created==created&&ReferenceEquals(oldCap,UiVfxPool.cap),""subsequent shot reuses the projectile Image"");
  }
  foreach(float cancelTime in new[]{.16f,.43f,.53f,.8f}){
   Time.deltaTime=1f/120;var b=new BoardController();var a=new GelLauncherFireAction(b,new Vector2Int(2,3),ObstacleId.GelLauncherUp);var body=Body(b,a);
   var run=new Coroutine(a.ExecuteVisuals(null));for(float t=0;t<cancelTime;t+=Time.deltaTime){run.Tick();b.Tick();}
   run.Dispose();b.Tick();int returns=UiVfxPool.returns,painted=b.painted.Count;
   Check(b.jobs==0&&b.holds==0&&b.routines.Count==0&&body.gameObject.destroyed,""cancel stops drivers then releases both holds and real body"");
   for(int i=0;i<100;i++)b.Tick();run.Dispose();Check(UiVfxPool.returns==returns&&b.painted.Count==painted,""no late painting, duplicate pool return or visual mutation"");
  }
  foreach(bool disable in new[]{false,true}){
   var b=new BoardController();var a=new GelLauncherFireAction(b,new Vector2Int(2,3),ObstacleId.GelLauncherRight);Body(b,a);
   var run=new Coroutine(a.ExecuteVisuals(null));for(int i=0;i<12;i++){run.Tick();b.Tick();}
   if(disable)b.isActiveAndEnabled=false;else b.LevelData=new LevelData();
   for(int i=0;i<240&&!run.done;i++){run.Tick();b.Tick();}
   Check(run.done&&b.jobs==0&&b.holds==0,""disable/level change releases projectile path ownership"");
  }
  Console.WriteLine(""PASS: ""+checks+"" production GelLauncher samples, four directions at 30/60/120 Hz, real press/head handoff, arrival gel, reuse and cancellation. UI doubles; no Unity rendering."");
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
