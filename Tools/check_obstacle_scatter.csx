// Isolated motion/lifetime checks and API metadata binding; no project build, emit or Unity Play.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
string root="Assets/_Project/Scripts/";
string[] files={"VFX/ObstacleScatterFx.cs","VFX/UiVfxPool.cs","VFX/ObstacleDebrisBounds.cs","VFX/ObstacleShatterFx.cs","VFX/ChestObstacleView.cs","Grid/Board/PaintCanBoxView.cs","Grid/SafeObstacleView.cs","Grid/TarpStatue/TarpStatueView.cs","VFX/WardrobeObstacleView.cs","Grid/RhodesStatueView.cs","UI/WonderDefinition.cs","UI/WonderTaskRegions.cs","UI/WonderRevealView.cs","UI/WonderRestorationLayout.cs","UI/WonderRestorationPieceGraphic.cs","UI/WonderWeldSparkGraphic.cs"};
var trees=files.Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText(root+f),path:f)).ToList();
foreach(var f in new[]{"Grid/GridSpawner.cs","Grid/Board/BoardController.cs","Grid/Board/BoardBreakFxService.cs","Grid/TarpStatue/TarpStatueService.cs","Grid/LaserCannon/LaserCannonService.cs"}) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(root+f)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error);
 if(errors.Any())throw new Exception(string.Join("\n",errors));
}
string stubs=@"
using System; using System.Collections.Generic; using UnityEngine;
public class BoardController:MonoBehaviour {
 public int Width,TileSize; public Vector3 GetCellWorldPosition(int x,int y)=>new Vector3(x*TileSize,-y*TileSize,0);public LevelData ActiveLevelData; public RectTransform UnclippedFxRoot,TilesTopOverlayRoot;
 public BreakFxStub BreakFx;public GameObject ObstacleBreakFxPrefab;public float ObstacleBreakFxLifetime;
}
public class BreakFxStub {public void PlaySplashFx(GameObject p,float l,Vector3 w,IReadOnlyList<Sprite> s){}}
public class SafeObstacleService {
 public BoardController Board;public const int LockCount=3;
 public event Action<int,int,int,int> OnSafeHit;public event Action<int,int> OnLockClosed;public event Action<int> OnSafeBroken;
 public int GetTotal(int o,int i)=>1;public int GetRemaining(int o,int i)=>1;public int GetActiveLock(int o)=>0;
 public SafeLockHitMode GetHitMode(int o)=>SafeLockHitMode.Ordered;
}
";
trees.Add(CSharpSyntaxTree.ParseText(stubs));
string unity="/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine";
var refs=new List<MetadataReference>{MetadataReference.CreateFromFile(typeof(object).Assembly.Location),MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)};
refs.AddRange(Directory.GetFiles(unity,"*.dll").Select(p=>MetadataReference.CreateFromFile(p)));
foreach(var f in new[]{"UnityEngine.UI.dll","Unity.TextMeshPro.dll","Assembly-CSharp.dll"})refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Library/ScriptAssemblies/"+f)));
refs.Add(MetadataReference.CreateFromFile("/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Facades/netstandard.dll"));
var compilation=CSharpCompilation.Create("ObstacleScatterApiCheck",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var apiErrors=compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(apiErrors.Length>0)throw new Exception(string.Join("\n",apiErrors.Select(e=>e.ToString())));
Console.WriteLine("PASS: complete scatter driver, pool and six obstacle views bind to installed Unity/UI/TMP APIs (project collaborators doubled); caller syntax valid. No assembly emitted.");
// Exercise the unmodified driver with lightweight UI/pool/owner doubles; no gameplay simulation.
string doubles=@"
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
namespace UnityEngine {
 public class DisallowMultipleComponent:Attribute{}
 public class Object {public bool destroyed;public static void Destroy(Object o){if(o!=null)o.destroyed=true;}public static implicit operator bool(Object o)=>o!=null&&!o.destroyed;}
 public class Component:Object {public bool TryGetComponent<T>(out T c)where T:Component=>gameObject.TryGetComponent<T>(out c);public GameObject gameObject;public Transform transform=>gameObject.transform;}
 public class Coroutine{}
 public class MonoBehaviour:Component {public Coroutine StartCoroutine(System.Collections.IEnumerator c)=>new Coroutine();public int stopped;public void StopCoroutine(Coroutine c){stopped++;}public bool enabled=true;public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;}
 public class GameObject:Object {
  public string name;public bool active=true;public int layer;public RectTransform transform;
  readonly Dictionary<Type,Component> cs=new Dictionary<Type,Component>();
  public GameObject(){transform=new RectTransform{gameObject=this};}
  public bool activeSelf=>active;public bool activeInHierarchy=>active&&!destroyed&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
  public void SetActive(bool v){active=v;}public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};cs[typeof(T)]=c;return c;}
  public bool TryGetComponent<T>(out T c)where T:Component{Component x;bool found=cs.TryGetValue(typeof(T),out x);c=found?(T)x:null;return found;}
 }
 public class Transform:Component {public Vector3 TransformVector(Vector3 v)=>v;public Transform parent;public Vector3 localPosition;public Vector3 position,localScale=Vector3.one;public Quaternion localRotation;}
 public class RectTransform:Transform {public Vector2 anchoredPosition,sizeDelta;public Rect rect=new Rect();public void SetAsLastSibling(){}public Vector3 InverseTransformPoint(Vector3 p)=>p;public Vector3 TransformPoint(Vector3 p)=>position+(Vector3)anchoredPosition+p;public Vector3 TransformVector(Vector3 v)=>v;public Vector3 InverseTransformVector(Vector3 v)=>v;}
 public class Rect{public Vector2 size=>new Vector2(width,height);public float x,y,width=100,height=100;public Vector2 center=new Vector2();public Rect(){}public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}}
 public class Texture2D:Object{}
 public class Sprite:Object{public string name;public Texture2D texture=new Texture2D();public Rect textureRect=new Rect();public float pixelsPerUnit=100;public static Sprite Create(Texture2D t,Rect r,Vector2 pivot,float ppu)=>new Sprite{texture=t,textureRect=r,pixelsPerUnit=ppu};}
 public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);}
 public struct Quaternion{public float z;public static Quaternion identity=>new Quaternion();public static Quaternion Euler(float x,float y,float z)=>new Quaternion{z=z};}
 public struct Vector2 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public float magnitude=>(float)Math.Sqrt(x*x+y*y);public static Vector2 down=>new Vector2(0,-1);
  public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
  public static implicit operator Vector3(Vector2 a)=>new Vector3(a.x,a.y,0);
 }
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 right=>new Vector3(1,0,0);public static Vector3 zero=>new Vector3();public static Vector3 one=>new Vector3(1,1,1);
  public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
 }
 public static class Mathf{public static float Cos(float t)=>(float)Math.Cos(t);public static float Min(float a,float b)=>Math.Min(a,b);public static float Pow(float a,float b)=>(float)Math.Pow(a,b);public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(b,v));public const float PI=(float)Math.PI;public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float v,float a,float b)=>Math.Max(a,Math.Min(b,v));public static float Clamp01(float v)=>Clamp(v,0,1);public static float Lerp(float a,float b,float k)=>a+(b-a)*Clamp01(k);public static float Sin(float v)=>(float)Math.Sin(v);}
 public static class Random{public static float value=.75f;public static float Range(float a,float b)=>(a+b)*.5f;}
 public static class Time{public static float deltaTime,time;public static int frameCount;}
 public static class Application{public static bool isPlaying=true;}
}
namespace UnityEngine.UI{public class Graphic:UnityEngine.Component{public UnityEngine.Color color;}public class Image:Graphic{public bool enabled=true;public UnityEngine.Sprite sprite;public bool preserveAspect;public UnityEngine.RectTransform rectTransform=>(UnityEngine.RectTransform)transform;}}
public class LevelData{}
public class BoardController:UnityEngine.MonoBehaviour {
 public int Width,TileSize;public UnityEngine.Vector3 GetCellWorldPosition(int x,int y)=>new UnityEngine.Vector3(x*TileSize,-y*TileSize,0);public UnityEngine.RectTransform UnclippedFxRoot=new UnityEngine.GameObject().transform;public LevelData ActiveLevelData=new LevelData();public BreakFxStub BreakFx=new BreakFxStub();public UnityEngine.GameObject ObstacleBreakFxPrefab=new UnityEngine.GameObject();public float ObstacleBreakFxLifetime=.4f;
 public bool TryGetComponent<T>(out T c)where T:UnityEngine.Component=>gameObject.TryGetComponent<T>(out c);
}
public class BreakFxStub{public int bursts;public UnityEngine.Vector3 last;public void PlaySplashFx(UnityEngine.GameObject p,float l,UnityEngine.Vector3 w,IReadOnlyList<UnityEngine.Sprite>s){if(s[0]==null)throw new Exception(""Missing burst sprite"");bursts++;last=w;}}
public static class UiVfxPool{
 public static readonly Stack<UnityEngine.UI.Image> pool=new Stack<UnityEngine.UI.Image>();public static int created,returned,active;public static UnityEngine.UI.Image latest;
 public static UnityEngine.UI.Image RentImage(string k,UnityEngine.Transform parent,string name){active++;UnityEngine.UI.Image im;if(pool.Count>0)im=pool.Pop();else{created++;im=new UnityEngine.GameObject().AddComponent<UnityEngine.UI.Image>();}im.gameObject.SetActive(true);im.gameObject.transform.parent=parent;im.gameObject.name=name;im.rectTransform.anchoredPosition=new UnityEngine.Vector2();im.rectTransform.localScale=UnityEngine.Vector3.one;latest=im;return im;}
 public static void Return(string k,UnityEngine.GameObject go){UnityEngine.UI.Image im;go.TryGetComponent(out im);if(!go.active)throw new Exception(""Duplicate return"");go.SetActive(false);go.transform.parent=null;pool.Push(im);returned++;active--;}
}
";
string driver=File.ReadAllText(root+files[0])+"\n"+string.Join("\n",CSharpSyntaxTree.ParseText(File.ReadAllText(root+"VFX/ObstacleDebrisBounds.cs")).GetCompilationUnitRoot().Members.Select(m=>m.ToFullString()));
// Keep production declarations and methods verbatim; move using directives above the doubles.
var driverTree=CSharpSyntaxTree.ParseText(driver).GetCompilationUnitRoot();
doubles=doubles.Replace("namespace UnityEngine {", "").Replace("namespace UnityEngine.UI{", "").Replace("UnityEngine.UI.", "").Replace("UnityEngine.", "").Replace("\n}\npublic class Graphic", "\npublic class Graphic").Replace("rectTransform=>(RectTransform)transform;}}", "rectTransform=>(RectTransform)transform;}");
var tarpTree=CSharpSyntaxTree.ParseText(File.ReadAllText(root+"Grid/TarpStatue/TarpStatueView.cs"));
string tagMethods=string.Join("\n",tarpTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Where(m=>new[]{"CoDropTag","ResetTag","OnDisable"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
string tagHarness=@"
public class TagHarness:MonoBehaviour {public BoardController boundBoard;
 public RectTransform tagRoot=new GameObject().transform;public Graphic[] tagGraphics={new GameObject().AddComponent<Image>(),new GameObject().AddComponent<Image>()};
 public Vector2 tagStart=new Vector2(8,9);public Coroutine tagDropCo=new Coroutine();public bool covered=false;
 public System.Collections.IEnumerator Run()=>CoDropTag();public void Disable()=>OnDisable();
 /* METHODS */
}
".Replace("/* METHODS */",tagMethods);
var wardrobeTree=CSharpSyntaxTree.ParseText(File.ReadAllText(root+"VFX/WardrobeObstacleView.cs"));
string wardrobeFields=string.Join("\n",wardrobeTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>().Select(f=>f.WithAttributeLists(default).ToFullString()));
string wardrobeMethods=string.Join("\n",wardrobeTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Where(m=>new[]{"RemoveFrontItem","ItemVelocity","PlayFinalBreak"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
string wardrobeHarness=@"
public class WardrobeHarness:MonoBehaviour {
 /* FIELDS */
 public void Setup(){_rootImage=gameObject.AddComponent<Image>();_rootImage.sprite=new Sprite();_rootImage.color=Color.white;}
 public Image AddItem(){var im=new GameObject().AddComponent<Image>();im.sprite=new Sprite();im.color=Color.white;im.transform.parent=transform;_frontToBack.Add(im);return im;}
 /* METHODS */
}
".Replace("/* FIELDS */",wardrobeFields).Replace("/* METHODS */",wardrobeMethods);
var rhodesTree=CSharpSyntaxTree.ParseText(File.ReadAllText(root+"Grid/RhodesStatueView.cs"));
string transition=rhodesTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="DebrisForTransition").ToFullString();
string rhodesFlow=string.Join("\n",rhodesTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Where(m=>new[]{"OnHit","StageForRemaining","HitMotion","ResetAttachments","OnDisable","StartSwing","OnNearbyMatch"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
string rhodesHarness=@"
public class RhodesHarness:MonoBehaviour {
 public RectTransform sword=new GameObject().transform,shield=new GameObject().transform;
 public Image swordImage=new GameObject().AddComponent<Image>(),shieldImage=new GameObject().AddComponent<Image>();
 public int stage=1;private Coroutine hitCo;private int lastNearbyFrame=-1;private float lastSwingTime=-10;public float shieldScale;public readonly List<int> debris=new List<int>();
 public void Setup(){swordImage.sprite=new Sprite();shieldImage.sprite=new Sprite();}
 private void SetStage(int s){stage=s;if(s>=3)shieldImage.sprite=null;if(s>=4)swordImage.sprite=null;}
 private void PlayStoneChips(BoardController b){debris.Add(1);}
 private void PlayAttachmentBreak(BoardController b,Image source,int cols,int rows,float scale){
  if(source.sprite==null)throw new Exception(""Attachment hidden before copying"");
  if(source==shieldImage&&cols==2&&rows==1){debris.Add(2);shieldScale=scale;}else if(source==swordImage&&cols==1&&rows==4)debris.Add(3);else throw new Exception(""Wrong part layout"");
 }
 public System.Collections.IEnumerator Swing()=>HitMotion();public void Disable()=>OnDisable();
 /* METHODS */
}
".Replace("/* METHODS */",transition+rhodesFlow);
var wonderTree=CSharpSyntaxTree.ParseText(File.ReadAllText(root+"UI/WonderRevealView.cs"));
var torchGate=wonderTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="HasRhodesTorch").ToFullString();
var fxTree=CSharpSyntaxTree.ParseText(File.ReadAllText(root+"Grid/Board/BoardBreakFxService.cs"));
var suppress=fxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="ShouldSuppressGenericContentObstacleFx").ToFullString().Replace("private static", "public static");
string fxHarness="public enum ObstacleId {ColorChest,Wardrobe,KeyGenerator,Hamster,RhodesStatue,TarpStatue,Stone} public class ObstacleVisualChange{public ObstacleId obstacleId;public bool cleared;} public static class FxHarness {"+suppress+torchGate+"}";
var boardTree=CSharpSyntaxTree.ParseText(File.ReadAllText(root+"Grid/Board/BoardController.cs"));
var reactionMethod=boardTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="ReactToRejectedRhodesMatch").ToFullString();
string reactionHarness=@"
public enum ObstacleHitContext{NormalMatch,SpecialActivation,Booster,Scripted}
public struct Vector2Int {public int x,y;}
public class ObstacleDamageRequest{public Vector2Int cell;public ObstacleHitContext context;}
public class RhodesStatueView:Component{public int reactions;public void OnNearbyMatch(){reactions++;}}
public class ReactionState{public ObstacleId id=ObstacleId.RhodesStatue;public int hp=4;public int GetObstacleOriginAt(int x,int y)=>2;public ObstacleId GetObstacleIdAt(int x,int y)=>id;}
public class ReactionProbe{
 public ReactionState obstacleStateService=new ReactionState();public Func<int,Image> ObstacleViewByOriginLookup;
 public void Notify(ObstacleDamageRequest r,bool hit)=>ReactToRejectedRhodesMatch(r,hit);
 /* METHOD */
}
".Replace("/* METHOD */",reactionMethod);
string source=doubles+"\n"+string.Join("\n",driverTree.Members.Select(m=>m.ToFullString()));
string cases=@"
int checks=0;Action<bool,string> Check=(ok,m)=>{checks++;if(!ok)throw new Exception(m);};
Action<ObstacleScatterFx,string> Call=(d,m)=>typeof(ObstacleScatterFx).GetMethod(m,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(d,null);
Func<BoardController> Board=()=>new GameObject().AddComponent<BoardController>();
var b=Board();var root=new GameObject().transform;
var m=new ObstacleScatterFx.Motion{travel=new Vector2(300,400),gravity=1400,spin=450,delay=.12f,life=.75f};
var off=ObstacleScatterFx.Offset(m,.25f);Check(Math.Abs(off.x-75)<.001&&Math.Abs(off.y-56.25f)<.001,""Ballistic path uses analytic time"");
Func<BoardController,ObstacleScatterFx.Motion,bool> Play=(board,motion)=>ObstacleScatterFx.Play(board,root,new Sprite(),new Vector3(50,70,0),new Vector2(90,100),Color.white,""Throw"",motion);
Check(Play(b,m),""Launch succeeds"");ObstacleScatterFx d;b.TryGetComponent(out d);Time.deltaTime=.1f;Call(d,""LateUpdate"");
Check(Math.Abs(UiVfxPool.latest.rectTransform.anchoredPosition.x)<.001,""Wobble delay holds position"");
Time.deltaTime=.27f;Call(d,""LateUpdate"");Check(Math.Abs(UiVfxPool.latest.rectTransform.anchoredPosition.x-75)<.001,""Flight starts after delay"");
Time.deltaTime=1;Call(d,""LateUpdate"");Check(UiVfxPool.returned==1&&b.BreakFx.bursts==0,""Fading piece returned once without burst"");
int created=UiVfxPool.created;Check(Play(b,m)&&UiVfxPool.created==created,""Next throw reuses pooled Image"");
Check(UiVfxPool.latest.rectTransform.localScale.x==1&&UiVfxPool.latest.color.a==1,""Reused image starts with clean scale and alpha"");ObstacleScatterFx.Cancel(b);
var bulb=new ObstacleScatterFx.Motion{travel=new Vector2(220,250),gravity=1300,life=.42f,burst=true};
Check(Play(b,bulb),""Whole bulb launched"");Time.deltaTime=.3f;Call(d,""LateUpdate"");Check(b.BreakFx.bursts==0&&UiVfxPool.latest.color.a==1,""Bulb stays whole and opaque during flight"");
Time.deltaTime=.2f;Call(d,""LateUpdate"");Check(b.BreakFx.bursts==1,""Exactly one burst at flight end"");Check(Math.Abs(b.BreakFx.last.x-142.4f)<.01f,""Burst is emitted at landing position rather than original chest center"");Call(d,""LateUpdate"");Check(b.BreakFx.bursts==1,""Completed bulb never bursts twice"");
// A source obstacle can disappear immediately: driver retains only a copied Image.
var src=new GameObject().AddComponent<Image>();src.sprite=new Sprite();src.color=Color.white;
Check(ObstacleScatterFx.PlayCopy(b,root,src,""FinalBulb"",bulb),""Copy starts before final chest destruction"");src.gameObject.destroyed=true;Time.deltaTime=.5f;Call(d,""LateUpdate"");Check(b.BreakFx.bursts==2,""Final bulb survives source destruction and bursts"");
for(int i=0;i<20;i++)Check(Play(b,bulb),""Concurrent launch"");int returns=UiVfxPool.returned;ObstacleScatterFx.Cancel(b);Check(UiVfxPool.returned-returns==20&&b.BreakFx.bursts==2,""Explicit level reset returns all flights without deferred bursts"");
returns=UiVfxPool.returned;ObstacleScatterFx.Cancel(b);Check(UiVfxPool.returned==returns,""Cancellation is idempotent"");
Play(b,bulb);b.ActiveLevelData=new LevelData();Call(d,""LateUpdate"");Check(b.BreakFx.bursts==2&&!UiVfxPool.latest.gameObject.active,""Changed level cancels old bulb"");
Play(b,bulb);b.enabled=false;Call(d,""LateUpdate"");Check(b.BreakFx.bursts==2&&!UiVfxPool.latest.gameObject.active,""Disabled board cancels flight"");b.enabled=true;
Play(b,bulb);root.gameObject.active=false;Call(d,""LateUpdate"");Check(b.BreakFx.bursts==2&&!UiVfxPool.latest.gameObject.active,""Disabled FX root cancels flight"");root.gameObject.active=true;
Play(b,bulb);Call(d,""OnDisable"");Check(!UiVfxPool.latest.gameObject.active,""Driver teardown returns image"");returns=UiVfxPool.returned;Call(d,""OnDestroy"");Check(UiVfxPool.returned==returns,""Destroy after disable never double-returns"");
var e=new ObstacleScatterFx.Motion{travel=new Vector2(165,0),gravity=130,life=.6f,eased=true};off=ObstacleScatterFx.Offset(e,.3f);Check(Math.Abs(off.x-123.75f)<.001&&Math.Abs(off.y+16.25f)<.001,""Safe ease-out and gravity preserved"");
var b2=Board();Play(b,bulb);Play(b2,bulb);ObstacleScatterFx.Cancel(b);ObstacleScatterFx d2;b2.TryGetComponent(out d2);Time.deltaTime=.5f;Call(d2,""LateUpdate"");Check(b.BreakFx.bursts==2&&b2.BreakFx.bursts==1,""Boards have independent ownership and cancellation"");
int before=UiVfxPool.created;Application.isPlaying=false;Check(!Play(b,m)&&UiVfxPool.created==before,""Edit/stop cannot acquire new FX"");Application.isPlaying=true;
Check(!ObstacleScatterFx.Play(b,root,null,new Vector3(),new Vector2(),Color.white,""Missing"",m),""Missing art safely declines launch"");
Console.WriteLine(""PASS: ""+checks+"" production scatter motion, delayed burst, pool reuse, final-source destruction and cancellation checks with UI doubles. No Unity Play/build."");
";
string tagCases=@"
var tag=new GameObject().AddComponent<TagHarness>();tag.tagRoot.anchoredPosition=tag.tagStart;tag.tagGraphics[0].color=Color.white;tag.tagGraphics[1].color=new Color(.3f,.1f,.1f,1);
Time.deltaTime=.1f;var co=tag.Run();co.MoveNext();co.MoveNext();co.MoveNext();
Check(tag.tagRoot.anchoredPosition.x>tag.tagStart.x+20,""Tag travels sideways rather than falling straight down"");Check(tag.tagRoot.anchoredPosition.y>tag.tagStart.y,""Loose tag initially jumps upward"");
Check(tag.tagRoot.localRotation.z!=0,""Loose tag spins during flight"");
while(co.MoveNext()){}Check(!tag.tagRoot.gameObject.active&&tag.tagDropCo==null,""Completed tag hides and releases its coroutine handle"");
Check(Math.Abs(tag.tagRoot.anchoredPosition.x-8)<.001&&Math.Abs(tag.tagRoot.anchoredPosition.y-9)<.001&&tag.tagRoot.localRotation.z==0,""Completed tag restores its base transform"");
Check(tag.tagGraphics.All(g=>g.color.a==1)&&Math.Abs(tag.tagGraphics[1].color.r-.3f)<.001,""Tag restores alpha without changing counter-shadow tint"");
tag.tagRoot.gameObject.SetActive(true);tag.tagDropCo=new Coroutine();co=tag.Run();co.MoveNext();co.MoveNext();tag.Disable();
Check(tag.stopped==1&&!tag.tagRoot.gameObject.active&&tag.tagDropCo==null,""Midflight disable stops and hides the tag"");
Check(tag.tagRoot.anchoredPosition.x==8&&tag.tagRoot.localRotation.z==0&&tag.tagGraphics.All(g=>g.color.a==1),""Midflight disable restores transform and alpha for reuse"");
Console.WriteLine(""PASS: 8 production TarpStatue tag trajectory, completion and interrupted-reset checks (""+checks+"" combined behavior checks)."");
";
string newCases=@"
var wb=Board();var w=new GameObject().AddComponent<WardrobeHarness>();w.Setup();var a1=w.AddItem();var a2=w.AddItem();w.AddItem();
w.RemoveFrontItem(wb,2);var flying1=UiVfxPool.latest;w.RemoveFrontItem(wb,1);var flying2=UiVfxPool.latest;
Check(flying1!=flying2&&a1.gameObject.destroyed&&a2.gameObject.destroyed,""Item visuals transfer to independent pooled images"");
Check(Math.Abs(flying1.rectTransform.sizeDelta.x-128)<.001&&Math.Abs(flying2.rectTransform.sizeDelta.x-128)<.001,""Flying nuts render 28 percent larger"");
ObstacleScatterFx wd;wb.TryGetComponent(out wd);Time.deltaTime=.25f;Call(wd,""LateUpdate"");
Check(flying1.rectTransform.anchoredPosition.x<0&&flying2.rectTransform.anchoredPosition.x>0,""Two simultaneous nuts always separate into opposite lanes"");
Check(flying1.rectTransform.anchoredPosition.y>100&&flying2.rectTransform.anchoredPosition.y>flying1.rectTransform.anchoredPosition.y,""Both nuts jump higher and the second takes a higher arc"");
returns=UiVfxPool.active;w.RemoveFrontItem(wb,0);Check(returns==UiVfxPool.active,""Final item removal does not launch another nut"");
int activeBefore=UiVfxPool.active;w.PlayFinalBreak(wb);Check(UiVfxPool.active==activeBefore+4,""Final cabinet emits exactly four large pieces"");
Check(UiVfxPool.latest.rectTransform.sizeDelta.x==50&&UiVfxPool.latest.rectTransform.sizeDelta.y==50,""Cabinet chunks are full quarter-size"");
activeBefore=UiVfxPool.active;w.PlayFinalBreak(wb);Check(UiVfxPool.active==activeBefore,""Duplicate final notifications cannot produce a second burst"");
w.gameObject.destroyed=true;Time.deltaTime=.9f;Call(wd,""LateUpdate"");Check(UiVfxPool.active==0,""Items and cabinet quarters finish after source destruction"");
var art=new Sprite{textureRect=new Rect(10,20,200,400)};var quarters=ObstacleScatterFx.GetQuarters(wb,art);
Check(quarters.Length==4&&quarters[0].textureRect.width==100&&quarters[0].textureRect.height==200&&quarters[3].textureRect.x==110&&quarters[3].textureRect.y==220,""Cached quarters cut actual source texture bounds"");
Check(object.ReferenceEquals(quarters,ObstacleScatterFx.GetQuarters(wb,art)),""Cabinets share generated sprites within a board"");
var halves=ObstacleScatterFx.GetSlices(wb,art,2,1);var blade=ObstacleScatterFx.GetSlices(wb,art,1,4);
Check(halves.Length==2&&blade.Length==4&&blade[0].textureRect.width==200&&blade[0].textureRect.height==100,""Shield and sword use separate cut layouts"");
Call(wd,""OnDestroy"");Check(quarters.All(p=>p.destroyed)&&halves.All(p=>p.destroyed)&&blade.All(p=>p.destroyed)&&!art.destroyed,""Board teardown destroys generated slices while preserving original art"");
for(int oldStage=1;oldStage<=4;oldStage++)for(int newStage=1;newStage<=4;newStage++){
 int expected=newStage==oldStage+1&&oldStage<=3?oldStage:0;
 Check(RhodesHarness.DebrisForTransition(oldStage,newStage)==expected,""Rhodes debris order only follows real forward transitions"");
}
var rh=new GameObject().AddComponent<RhodesHarness>();rh.Setup();rh.OnHit(3,wb);rh.OnHit(3,wb);rh.OnHit(2,wb);rh.OnHit(1,wb);
Check(rh.debris.SequenceEqual(new[]{1,2,3}),""Production OnHit emits stone, shield, sword in order, captures before hiding, and ignores duplicates"");
var swing=new GameObject().AddComponent<RhodesHarness>();swing.Setup();Time.deltaTime=.1f;var sway=swing.Swing();sway.MoveNext();Check(Math.Abs(swing.sword.localRotation.z)==72&&Math.Abs(swing.shield.localRotation.z)==56,""Immediate full-amplitude attachment kick"");sway.MoveNext();
Check(Math.Abs(swing.sword.localRotation.z)>30&&Math.Abs(swing.shield.localRotation.z)>15,""First-hit sword and shield recoil is visibly stronger"");swing.Disable();
Check(swing.sword.localRotation.z==0&&swing.shield.localRotation.z==0&&swing.shield.localScale.x==1,""Interrupted Rhodes motion resets both attachments"");
Check(FxHarness.ShouldSuppressGenericContentObstacleFx(new ObstacleVisualChange{obstacleId=ObstacleId.Wardrobe,cleared=true}),""Wardrobe final uses only cabinet chunks, without old generic particles"");
Check(FxHarness.ShouldSuppressGenericContentObstacleFx(new ObstacleVisualChange{obstacleId=ObstacleId.Wardrobe}),""Wardrobe nuts have no overlapping generic particle burst"");
Check(FxHarness.ShouldSuppressGenericContentObstacleFx(new ObstacleVisualChange{obstacleId=ObstacleId.RhodesStatue})&&FxHarness.ShouldSuppressGenericContentObstacleFx(new ObstacleVisualChange{obstacleId=ObstacleId.RhodesStatue,cleared=true}),""Rhodes uses staged chunks and final body shatter without stray generic dust"");
foreach(var n in new[]{""RodosDevi"",""GBG2"",""GBG13_HarborPier"",""S1_Base"",""rodosdevi""})Check(FxHarness.HasRhodesTorch(new Sprite{name=n})==(n==""RodosDevi""),""Flame is exclusive to the exact RodosDevi background art"");Check(!FxHarness.HasRhodesTorch(null),""Missing background never enables flame"");
var reactive=new GameObject().AddComponent<RhodesHarness>();reactive.Setup();Time.frameCount=10;Time.time=2;reactive.OnNearbyMatch();
Check(reactive.stage==1&&reactive.debris.Count==0,""Neighbor matches react without stage change or debris"");int stopped=reactive.stopped;reactive.OnNearbyMatch();Check(reactive.stopped==stopped,""Duplicate same-frame neighbor requests do not restart the recoil"");
Time.frameCount++;Time.time+=.05f;reactive.OnNearbyMatch();Check(reactive.stopped==stopped,""Dense adjacent clears preserve the initial kick"");Time.frameCount++;Time.time+=.2f;reactive.OnNearbyMatch();Check(reactive.stopped==stopped+1,""Later adjacent matches restart the swing"");
reactive.stage=4;Time.frameCount++;Time.time+=1;stopped=reactive.stopped;reactive.OnNearbyMatch();Check(reactive.stopped==stopped,""Bare scaffold cannot swing vanished attachments"");
int edgeChecks=0;
foreach(float x in new[]{0f,40f,160f,400f,760f,900f})foreach(float vx in new[]{-500f,-120f,0f,120f,500f})foreach(bool eased in new[]{false,true}) {
 var limits=new ObstacleDebrisBounds{valid=true,left=0,right=900};var pos=new Vector2(x,50);var travel=new Vector2(vx,100);var sz=new Vector2(110,220);limits.Fit(ref pos,ref travel,ref sz,.9f,eased);float radius=sz.magnitude*.5f;
 for(int step=0;step<=30;step++){var mo=new ObstacleScatterFx.Motion{travel=travel,life=.9f,eased=eased};var q=pos+ObstacleScatterFx.Offset(mo,step*.9f/30f);Check(q.x-radius>=-.001&&q.x+radius<=900.001,""Complete rotating debris envelope stays inside both board edges"");edgeChecks++;}
 if(x>=760&&vx>0)Check(travel.x<=0,""Right-edge outward throw turns inward"");if(x<=40&&vx<0)Check(travel.x>=0,""Left-edge outward throw turns inward"");
}
var narrow=new ObstacleDebrisBounds{valid=true,left=0,right=90};var cp=new Vector2(80,0);var cv=new Vector2(700,200);var cs=new Vector2(120,200);narrow.Fit(ref cp,ref cv,ref cs,1);Check(cs.magnitude<=88.201f,""Oversized pieces fit narrow boards rather than protruding"");
var shifted=new ObstacleDebrisBounds{valid=true,left=-450,right=450}.WithAnchorOffset(125);Check(shifted.left==-575&&shifted.right==325,""FX anchor offset does not shift the board boundary"");
Check(rh.shieldScale==2.8f,""Shield halves use more than twice the previous linear render scale"");
var reactiveGo=new GameObject();var reactiveImage=reactiveGo.AddComponent<Image>();var reactiveView=reactiveGo.AddComponent<RhodesStatueView>();var reaction=new ReactionProbe{ObstacleViewByOriginLookup=(origin)=>reactiveImage};
reaction.Notify(new ObstacleDamageRequest{context=ObstacleHitContext.NormalMatch},false);Check(reactiveView.reactions==1&&reaction.obstacleStateService.hp==4,""Real board reaction hook leaves Rhodes HP intact on rejected normal match"");
foreach(var context in new[]{ObstacleHitContext.SpecialActivation,ObstacleHitContext.Booster,ObstacleHitContext.Scripted})reaction.Notify(new ObstacleDamageRequest{context=context},false);
reaction.Notify(new ObstacleDamageRequest{context=ObstacleHitContext.NormalMatch},true);Check(reactiveView.reactions==1,""Successful hits and non-match contexts do not add duplicate neighbor recoil"");
reaction.obstacleStateService.id=ObstacleId.Stone;reaction.Notify(new ObstacleDamageRequest{context=ObstacleHitContext.NormalMatch},false);Check(reactiveView.reactions==1,""Other obstacles do not inherit Rhodes reactions"");
Console.WriteLine(""PASS: ""+edgeChecks+"" horizontal trajectory samples including spinning-piece extents and eased throws."");
Console.WriteLine(""PASS: ""+checks+"" combined production behavior checks, including paired nut separation, larger arcs, empty-cabinet finale, cached-sprite cleanup and Rhodes transition order."");
";
await CSharpScript.RunAsync(source+tagHarness+wardrobeHarness+rhodesHarness+fxHarness+reactionHarness+cases+tagCases+newCases,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
