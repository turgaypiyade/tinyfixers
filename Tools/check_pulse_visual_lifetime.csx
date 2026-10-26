// csi Tools/check_pulse_visual_lifetime.csx — production VFX code with UI doubles, no Unity build.
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

string grid = "Assets/_Project/Scripts/Grid/";
string pulse = File.ReadAllText(Args.Count > 0 ? Args[0] : grid + "PulseFuseSparkleView.cs");
string tile = File.ReadAllText(Args.Count > 1 ? Args[1] : grid + "TileView.cs");
foreach (string source in new[]{pulse, tile}) {
 var errors = CSharpSyntaxTree.ParseText(source).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
 if (errors.Length > 0) throw new Exception(string.Join("\n", errors.Select(e => e.ToString())));
}
string setType = CSharpSyntaxTree.ParseText(tile).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
 .Single(m => m.Identifier.ValueText == "SetType").ToFullString();
var driver = CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/VFX/Emitters/UiFxPool.cs")).GetRoot();
var driverErrors = driver.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
if (driverErrors.Length > 0) throw new Exception(string.Join("\n", driverErrors.Select(d => d.ToString())));
string sparkDriver = driver.DescendantNodes().OfType<StructDeclarationSyntax>().Single(s => s.Identifier.ValueText == "PulseFuseSpark").ToFullString()
 + string.Join("\n", driver.DescendantNodes().OfType<MethodDeclarationSyntax>()
 .Where(m => new[]{"TrackPulseFuseSpark", "UpdatePulseFuseSparks", "RemoveAtSwap"}.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
var doubles = @"
using System;using System.Collections;using System.Collections.Generic;using System.Reflection;
public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}
public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
public class SerializeField:Attribute {}
public class RangeAttribute:Attribute {public RangeAttribute(float a,float b){}}
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero=>new Vector2();public static Vector2 one=>new Vector2(1,1);
 public Vector2 normalized=>this;
 public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
}
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
 public static Vector3 one=>new Vector3(1,1,1);
 public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
}
public struct Color {public float a;public Color(float r,float g,float b,float a){this.a=a;}public static Color white=>new Color(1,1,1,1);public static Color Lerp(Color a,Color b,float k)=>a;}
public class Sprite {public string name;public Sprite(string name=""sprite""){this.name=name;}}
public class CanvasRenderer {}
public class Transform {
 public GameObject gameObject;public Transform parent;public Vector3 localScale=Vector3.one,position;
 readonly List<Transform> children=new List<Transform>();public int childCount=>children.Count;
 public void SetParent(Transform next,bool world=false){parent?.children.Remove(this);parent=next;next?.children.Add(this);}
 public Transform GetChild(int i)=>children[i];public void SetAsLastSibling(){}
 public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
}
public class RectTransform:Transform {public Vector2 anchorMin,anchorMax,pivot,anchoredPosition,sizeDelta;}
public class GameObject {
 public string name;public int layer;public bool activeSelf=true,pendingDestroy;
 public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
 public Transform transform;Image image;public Action onDisable;
 public GameObject(string name=""go"",params Type[] types){this.name=name;transform=new RectTransform{gameObject=this};}
 public T GetComponent<T>() where T:class {
  if(typeof(T)==typeof(Image)){if(image==null)image=new Image{gameObject=this};return image as T;}
  return transform as T;
 }
 public T AddComponent<T>() where T:class,new(){if(typeof(T)==typeof(RectTransform))return transform as T;return GetComponent<T>()??new T();}
 public void SetActive(bool active){bool was=activeSelf;activeSelf=active;if(was&&!active)onDisable?.Invoke();}
}
public class Image {public GameObject gameObject;public Sprite sprite;public Color color=Color.white;public bool raycastTarget,preserveAspect;public Transform transform=>gameObject.transform;public RectTransform rectTransform=>(RectTransform)transform;}
public class Coroutine {public IEnumerator iterator;public bool stopped;public float wait;}
public class MonoBehaviour {
 public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;
 public bool isActiveAndEnabled=>gameObject.activeInHierarchy;public TileView parentTile;
 readonly List<Coroutine> routines=new List<Coroutine>();
 public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
 public T GetComponentInParent<T>() where T:class=>parentTile as T;
 public Coroutine StartCoroutine(IEnumerator e){var c=new Coroutine{iterator=e};routines.Add(c);Advance(c);return c;}
 static void Advance(Coroutine c){if(!c.iterator.MoveNext())c.stopped=true;else if(c.iterator.Current is WaitForSeconds w)c.wait=w.seconds;}
 public void StopCoroutine(Coroutine c){c.stopped=true;}
 public void StopAllCoroutines(){foreach(var c in routines)c.stopped=true;}
 public static void Destroy(GameObject go){go.pendingDestroy=true;}
 public void Tick(){foreach(var c in routines.ToArray())if(!c.stopped){c.wait-=Time.deltaTime;if(c.wait<=0)Advance(c);}}
}
public class WaitForSeconds {public float seconds;public WaitForSeconds(float s){seconds=s;}}
public static class Time {public static float deltaTime=.02f;}
public static class Mathf {
 public const float PI=(float)Math.PI;public static float Sin(float x)=>(float)Math.Sin(x);
 public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));
 public static int Clamp(int x,int a,int b)=>Math.Max(a,Math.Min(b,x));
 public static float Clamp01(float x)=>Clamp(x,0,1);public static float Max(float a,float b)=>Math.Max(a,b);
 public static int Max(int a,int b)=>Math.Max(a,b);public static int RoundToInt(float x)=>(int)Math.Round(x);
 public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
}
public static class Random {public static float value=>.5f;public static Vector2 insideUnitCircle=>Vector2.zero;public static float Range(float a,float b)=>a;}
public static class TileClearBurstVfx {public static Sprite SoftCircleHaloSprite=new Sprite();}
public enum TileSpecial {None,PulseCore,PatchBot,LineH}
public enum TileType {Red,Blue}
public enum TileRuntimeState {Idle,Falling,Clearing}
public class Model {public TileSpecial special;public TileType type;}
public class BoardController {
 public TileView current;public bool IsBusy,InputLocked,pendingMotion,settled=true;public int releases;
 public Transform TilesTopOverlayRoot=new GameObject(""overlay"").transform;
 public Sprite GetIcon(TileType t)=>new Sprite(""normal"");
 public TileView GetTileViewAt(int x,int y)=>current;
 public bool IsTileReadyForContinuousMatch(TileView t)=>current==t&&t.RuntimeState==TileRuntimeState.Idle&&!pendingMotion&&settled;
 public void ReleaseTile(TileView t){releases++;current=null;}
}
public class TileView {
 public Model model=new Model();public BoardController board=new BoardController();public BoardController Board=>board;
 public bool KeepHiddenForCombo,WasDragging,isMovableObstacleTile;public Sprite movableObstacleSprite;
 public int LifetimeVersion=1,X,Y,LastAppliedTileSize=96;public TileRuntimeState RuntimeState;
 public GameObject gameObject=new GameObject();public Sprite icon=new Sprite(""pulse"");
 public TileSpecial GetSpecial()=>model.special;public TileType GetTileType()=>model.type;
 public bool IsCurrentLifetime(int lifetime)=>LifetimeVersion==lifetime&&gameObject.activeInHierarchy;
 public void SetIcon(Sprite sprite){icon=sprite;}
 public void RefreshIcon(){icon=new Sprite(model.special==TileSpecial.PulseCore?""pulse"":""normal"");}
 /* SET_TYPE */
}
public static class GameDiagnostics {public static bool CriticalErrors;}
public static class Debug {public static void LogError(string s,object context){}}
public static class UiVfxPool {
 static readonly Dictionary<string,Stack<GameObject>> pools=new Dictionary<string,Stack<GameObject>>();
 public static int created,returns;
 public static Image RentImage(string key,Transform parent,string name){
  GameObject go;if(pools.TryGetValue(key,out var stack)&&stack.Count>0)go=stack.Pop();else{created++;go=new GameObject(name);}
  go.transform.SetParent(parent);go.SetActive(true);return go.GetComponent<Image>();
 }
 public static void Return(string key,GameObject go,int max=96){returns++;go.SetActive(false);go.transform.SetParent(null);if(!pools.TryGetValue(key,out var stack))pools[key]=stack=new Stack<GameObject>();stack.Push(go);}
}
public class UiFxPool {
 internal const string PulseFuseSparkKey=""PulseCore:fuseSpark"";const int MaxPulseSparksPerKey=512;
 static readonly UiFxPool Instance=new UiFxPool();readonly List<PulseFuseSpark> pulseFuseSparks=new List<PulseFuseSpark>();
 public static void Tick(float dt){Instance.UpdatePulseFuseSparks(dt);}public static int Pending=>Instance.pulseFuseSparks.Count;
 /* SPARK_DRIVER */
}
".Replace("/* SET_TYPE */",setType).Replace("/* SPARK_DRIVER */",sparkDriver);
var tests = @"
public static class Checks {
 static int checks;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Call(object v,string name)=>v.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(v,null);
 static object Field(object v,string name)=>v.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(v);
 static void Set(object v,string name,object value)=>v.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(v,value);
 static PulseFuseSparkleView View(out TileView tile) {
  tile=new TileView();tile.model.special=TileSpecial.PulseCore;tile.board.current=tile;
  var v=new PulseFuseSparkleView{parentTile=tile};
  Set(v,""spinFrames"",new[]{new Sprite()});
  Set(v,""breathTarget"",new GameObject(""icon"").GetComponent<RectTransform>());
  v.gameObject.onDisable=()=>Call(v,""OnDisable"");Call(v,""OnEnable"");return v;
 }
 public static void Run() {
  var v=View(out var tile);tile.SetType(TileType.Red);
  Check(tile.icon.name==""pulse"",""changing base colour must preserve the special's icon"");
  tile.KeepHiddenForCombo=true;v.PlayIdleSpin(96);
  Check(Field(v,""spinImage"")==null,""hidden consumed PulseCore cannot start a detached idle spin"");
  tile.KeepHiddenForCombo=false;tile.model.special=TileSpecial.None;v.PlayCreationSpin(96);
  Check(Field(v,""spinImage"")==null,""normal tile cannot play stale PulseCore creation"");
  v.gameObject.SetActive(false);
  foreach(bool inFlight in new[]{false,true}) {
   v=View(out tile);if(inFlight)tile.board.pendingMotion=true;else tile.board.current=new TileView();
   v.PlayIdleSpin(96);Check(Field(v,""spinImage"")==null,""idle animation requires the settled registered tile"");
   var watch=(IEnumerator)v.GetType().GetMethod(""CoIdleSpinWatch"",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(v,null);
   for(int i=0;i<5;i++)watch.MoveNext();
   Check(tile.board.releases==0,""VFX must never release a game tile based on coordinates"");
   v.gameObject.SetActive(false);
  }
  v=View(out tile);v.PlayCreationSpin(96);
  var spin=(Image)Field(v,""spinImage"");var halo=(Image)Field(v,""haloImage"");
  Check(spin.gameObject.activeSelf&&halo.gameObject.activeSelf,""valid creation still plays"");
  tile.KeepHiddenForCombo=true;Call(v,""LateUpdate"");
  Check(!spin.gameObject.activeSelf&&!halo.gameObject.activeSelf,""hiding a running source immediately hides detached overlays"");
  var external=new GameObject(""combo"").GetComponent<RectTransform>();
  v.StartExternalFuse(external,1,1,0,v);var fuse=(Coroutine)Field(v,""externalFuseRoutine"");Call(v,""LateUpdate"");
  Check(!fuse.stopped,""hidden source preserves the separate combo fuse"");
  v.gameObject.SetActive(false);Check(fuse.stopped,""source-owned external fuse cancels on release"");
  int warmed=UiVfxPool.created;
  for(int i=0;i<100;i++) {
   v=View(out tile);v.PlayCreationSpin(96);spin=(Image)Field(v,""spinImage"");halo=(Image)Field(v,""haloImage"");
   var routine=(Coroutine)Field(v,""spinRoutine"");var icon=(RectTransform)Field(v,""breathTarget"");
   tile.LifetimeVersion++;icon.localScale=Vector3.one*1.7f;Call(v,""LateUpdate"");
   Check(routine.stopped&&!spin.gameObject.activeSelf&&!halo.gameObject.activeSelf,""old lifetime cancels and hides both overlays synchronously"");
   Check(icon.localScale.x==1.7f,""old cleanup cannot reset the next lifetime's icon scale"");
   Check(Field(v,""spinImage"")==null&&Field(v,""haloImage"")==null,""released overlay references are cleared"");
   Check(UiVfxPool.created==warmed,""repeated creation/release reuses warm overlays"");
   int returns=UiVfxPool.returns;Call(v,""OnDestroy"");Check(UiVfxPool.returns==returns,""teardown cannot return an overlay twice"");
  }
  v=View(out tile);v.PlayIdleSpin(96);spin=(Image)Field(v,""spinImage"");
  for(int i=0;i<30;i++)v.Tick();
  Check(!spin.gameObject.activeSelf,""normal idle spin completes and hides its overlay"");
  v.gameObject.SetActive(false);
  v=View(out tile);Set(v,""sparkleSprite"",new Sprite());
  var parent=(RectTransform)v.transform;
  v.SpawnSparkAt(parent,Vector2.zero,1,1);var spark=parent.GetChild(parent.childCount-1).GetComponent<Image>();
  Check(spark.color.a==0,""new sparks cannot flash at full opacity before the shared update"");
  UiFxPool.Tick(.65f*.25f);Check(Math.Abs(spark.color.a-1f)<.001f,""shared spark driver preserves quarter-life fade-in"");
  UiFxPool.Tick(.65f*.25f);Check(Math.Abs(spark.color.a-2f/3f)<.001f,""shared spark driver preserves half-life fade-out"");
  UiFxPool.Tick(.65f);Check(!spark.gameObject.activeSelf&&UiFxPool.Pending==0,""expired spark returns to the shared pool"");
  warmed=UiVfxPool.created;
  for(int i=0;i<100;i++) {
   v.SpawnSparkAt(parent,Vector2.zero,1,1);UiFxPool.Tick(1);
   Check(UiVfxPool.created==warmed&&UiFxPool.Pending==0,""spark spawn/expiry reuses the pooled image"");
  }
  v.SpawnSparkAt(parent,Vector2.zero,1,1);spark=parent.GetChild(parent.childCount-1).GetComponent<Image>();
  v.Stop();Check(!spark.gameObject.activeSelf,""source cleanup hides sparks before same-frame overlay reuse"");
  UiFxPool.Tick(.01f);Check(UiFxPool.Pending==0,""cancelled local spark tracking is removed"");
  external=new GameObject(""carried pulse"").GetComponent<RectTransform>();var emitter=new MonoBehaviour();
  v.StartExternalFuse(external,1,1,0,emitter);v.gameObject.SetActive(false);emitter.Tick();
  Check(external.childCount>0&&UiFxPool.Pending>0,""board-owned combo fuse survives source release"");
  external.gameObject.SetActive(false);UiFxPool.Tick(.01f);emitter.Tick();
  Check(UiFxPool.Pending==0,""combo target teardown stops external emission and returns its sparks"");
  Console.WriteLine(""PASS: ""+checks+"" production PulseCore lifetime/visibility/pool checks (UI doubles, no Unity Play/build)."");
 }
}
Checks.Run();
";
await CSharpScript.RunAsync(doubles + pulse.Replace("using System.Collections;", "").Replace("using UnityEngine;", "").Replace("using UnityEngine.UI;", "") + tests,
 ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
