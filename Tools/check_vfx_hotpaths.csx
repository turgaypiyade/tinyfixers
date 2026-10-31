// Isolated production VFX methods with rendering doubles. No Unity/player/project build.
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

string path = "Assets/_Project/Scripts/VFX/";
string Read(string file) => File.ReadAllText(path + file + ".cs");
// Fixed before-change excerpts keep comparisons meaningful after this work is committed.
string Baseline(string file) => File.ReadAllText("Tools/Fixtures/VfxHotpaths/"+file+".cs.txt");
string Methods(string source, params string[] names) => string.Join("\n",CSharpSyntaxTree.ParseText(source).GetRoot()
 .DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
foreach(var file in new[]{"LightiningBeam","PulseCoreExplosionFX","PulsePulseExplosionVfx","TileClearBurstVfx","Emitters/UiFxPool","Emitters/LineTravelSplitSwapTest","PatchbotDashUI","../Grid/Board/BoardAnimator"}){
 var errors=CSharpSyntaxTree.ParseText(Read(file),new CSharpParseOptions(LanguageVersion.Preview)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(file+": "+string.Join("\n",errors.Select(x=>x.ToString())));
}
string stubs=@"
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
public static class Time {public static float deltaTime=.02f,unscaledDeltaTime=.02f,time;public static int frameCount;}
public static class Mathf {
 public const float PI=(float)Math.PI,Deg2Rad=PI/180f;
 public static float Sin(float x)=>(float)Math.Sin(x);public static float Cos(float x)=>(float)Math.Cos(x);
 public static float Pow(float x,float y)=>(float)Math.Pow(x,y);public static float Max(float x,float y)=>Math.Max(x,y);public static int Max(int x,int y)=>Math.Max(x,y);
 public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));public static float Clamp(float x,float lo,float hi)=>Math.Max(lo,Math.Min(hi,x));
 public static float Lerp(float a,float b,float k)=>a+(b-a)*Clamp01(k);
 public static float PerlinNoise(float x,float y)=>(float)(.5+.25*Math.Sin(x+y));
}
public static class Random {
 static System.Random rng=new System.Random(5);public static void Reset(){rng=new System.Random(5);}
 public static float value=>(float)rng.NextDouble();public static Vector2 insideUnitCircle=>new Vector2(.3f,.2f);
 public static float Range(float a,float b)=>a+(b-a)*value;public static int Range(int a,int b)=>a+(int)(value*(b-a));
}
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero=>new Vector2();public static Vector2 one=>new Vector2(1,1);public static Vector2 up=>new Vector2(0,1);
 public Vector2 normalized {get{float n=(float)Math.Sqrt(x*x+y*y);return n==0?zero:new Vector2(x/n,y/n);}}
 public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 public static Vector2 LerpUnclamped(Vector2 a,Vector2 b,float t)=>new Vector2(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);
 public static implicit operator Vector3(Vector2 a)=>new Vector3(a.x,a.y,0);public static explicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);
}
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
 public static Vector3 zero=>new Vector3();public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);
 public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
 public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
 public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);public static Vector3 operator/(Vector3 a,float b)=>a*(1/b);
 public static Vector3 LerpUnclamped(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
}
public struct Quaternion {public Vector3 euler;public static Quaternion identity=>new Quaternion();public static Quaternion Euler(float x,float y,float z)=>new Quaternion{euler=new Vector3(x,y,z)};}
public struct Color {
 public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
 public static Color white=>new Color(1,1,1);public static Color Lerp(Color a,Color b,float t){t=Mathf.Clamp01(t);return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
}
public struct GradientAlphaKey {public float alpha,time;public GradientAlphaKey(float a,float t){alpha=a;time=t;}}
public struct GradientColorKey {public Color color;public float time;public GradientColorKey(Color c,float t){color=c;time=t;}}
public class Gradient {public GradientColorKey[] colors;public GradientAlphaKey[] alphas;public void SetKeys(GradientColorKey[] c,GradientAlphaKey[] a){colors=c;alphas=a;}}
public class LineRenderer {
 public int positionCount=18,submissions;public Gradient colorGradient;public Vector3[] points=new Vector3[64];
 public void SetPosition(int i,Vector3 p){points[i]=p;submissions++;}public void SetPositions(Vector3[] p){Array.Copy(p,points,p.Length);submissions++;}
}
public struct Rect {public Vector2 center;public Rect(float x,float y,float w,float h){center=new Vector2(x+w*.5f,y+h*.5f);}}
public class CanvasRenderer {}
public class MissingReferenceException:Exception {}
public enum HideFlags {DontUnloadUnusedAsset}
public class Texture2D {public HideFlags hideFlags;}
public class Sprite {public HideFlags hideFlags;public static Sprite Create(Texture2D t,Rect r,Vector2 p,float pixels)=>new Sprite();}
public class Transform {
 public GameObject gameObject;public Transform parent;public Vector3 localScale=Vector3.one,position,localPosition;public Quaternion localRotation;
 public void SetParent(Transform p,bool keep=false){parent=p;}public void SetAsLastSibling(){}
 public bool IsChildOf(Transform owner){for(var t=this;t!=null;t=t.parent)if(t==owner)return true;return false;}
 public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
 public Vector3 InverseTransformPoint(Vector3 p)=>p-position;public Vector3 TransformPoint(Vector3 p)=>p+position;
}
public class RectTransform:Transform {
 public Vector2 anchorMin,anchorMax,pivot,anchoredPosition,sizeDelta;public Rect rect=>new Rect();
 public void GetWorldCorners(Vector3[] corners){corners[0]=position-Vector3.one;corners[2]=position+Vector3.one;}
}
public class Image {
 public enum Type {Simple}public Type type;public GameObject gameObject;public Color color=Color.white;public Sprite sprite;public bool raycastTarget,preserveAspect;
 public RectTransform rectTransform=>(RectTransform)gameObject.transform;
}
public class GameObject {
 public string name;public int rentalOrder;public bool activeSelf=true;public Transform transform;public Image image;
 public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
 public GameObject(string name){this.name=name;transform=new RectTransform{gameObject=this};image=new Image{gameObject=this};}
 public T GetComponent<T>() where T:class=>typeof(T)==typeof(Image)?image as T:transform as T;
 public void SetActive(bool value){activeSelf=value;}
}
public static class UiVfxPool {
 static Dictionary<string,Stack<GameObject>> pools=new Dictionary<string,Stack<GameObject>>();
 public static int created,returns,outstanding;public static GameObject lastRoot;
 public static List<GameObject> all=new List<GameObject>();static int rentalOrder;
 static GameObject Rent(string key,Transform parent,string name){
  if(!pools.TryGetValue(key,out var stack))pools[key]=stack=new Stack<GameObject>();
  GameObject go;if(stack.Count>0)go=stack.Pop();else{created++;go=new GameObject(name);all.Add(go);}
  go.rentalOrder=++rentalOrder;
  go.transform.SetParent(parent);go.transform.localScale=Vector3.one;go.transform.localRotation=Quaternion.identity;go.transform.localPosition=Vector3.zero;
  go.SetActive(true);outstanding++;return go;
 }
 public static Image RentImage(string key,Transform parent,string name){var go=Rent(key,parent,name);go.image.color=Color.white;return go.image;}
 public static GameObject RentRect(string key,Transform parent,string name){lastRoot=Rent(key,parent,name);return lastRoot;}
 public static void Return(string key,GameObject go,int max=96){
  if(go==null)return;if(!go.activeSelf)throw new Exception(""double return"");
  returns++;outstanding--;go.SetActive(false);go.transform.SetParent(null);
  if(!pools.TryGetValue(key,out var stack))pools[key]=stack=new Stack<GameObject>();stack.Push(go);
 }
}
public class TileView {public Image IconImage;public RectTransform RectTransform;}
public class BoardController {public int TileSize=100;public Transform Parent;public Vector2 WorldToAnchoredIn(RectTransform rt,Vector3 pos)=>(Vector2)rt.InverseTransformPoint(pos);}
";
string beamFields=@"
 public LineRenderer lr=new LineRenderer(),innerLr=new LineRenderer();
 GradientColorKey[] innerColorKeys={new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.3f,.1f),1)};
 Gradient innerGradient,persistentGradient;
 GradientAlphaKey[] innerAlphaKeys,persistentAlphaKeys;
 GradientColorKey[] persistentColorKeys;Vector3[] positions;
 Vector3 a=new Vector3(12,23),b=new Vector3(244,121);float seed=12.3f;
 float jaggednessRatio=.15f,noiseScale=4,edgeShimmerAmount=.07f,edgeShimmerFrequency=24,edgeShimmerSpeed=10;bool liveFlicker=true;
 public void Step(float alpha){ApplyInnerAlpha(alpha);BuildLightning();}
 public void Persistent(Color c){ApplyPersistentGradient(c);}
";
string beam="public class NewBeam {"+beamFields+Methods(Read("LightiningBeam"),"ApplyInnerAlpha","BuildLightning","ApplyPersistentGradient")+"}\n"
 +"public class OldBeam {"+beamFields+Methods(Baseline("LightiningBeam"),"ApplyInnerAlpha","BuildLightning","ApplyPersistentGradient")+"}\n";
var driverTree=CSharpSyntaxTree.ParseText(Read("Emitters/UiFxPool")).GetRoot();
string driver="public class UiFxPool { public const string ChargeSparkKey=\"PulseFuseSpark\";const int MaxChargeSparksPerKey=256,MaxGhostsPerKey=512,MaxTimedPerKey=128;"
 +"static UiFxPool _instance=new UiFxPool();static UiFxPool Instance=>_instance;readonly List<ChargeSpark> chargeSparks=new List<ChargeSpark>(128);"
 +"readonly List<PatchbotSpark> patchbotSparks=new List<PatchbotSpark>(128);"
 +"readonly List<Ghost> ghosts=new List<Ghost>(256);readonly List<Timed> timed=new List<Timed>(64);private void UpdatePulseFuseSparks(float dt){}private static string KeyFor(string prefix,GameObject prefab)=>prefix;"
 +string.Join("\n",driverTree.DescendantNodes().OfType<StructDeclarationSyntax>().Where(n=>new[]{"ChargeSpark","Ghost","Timed","PatchbotSpark"}.Contains(n.Identifier.ValueText)).Select(n=>n.ToFullString()))
 +Methods(Read("Emitters/UiFxPool"),"TrackChargeSpark","AdvanceChargeSpark","UpdateChargeSparks","ReleaseChargeSparks","RemoveAtSwap","TrackGhost","TrackLinearGhost","ReleaseGhosts","Update","TrackPatchbotSpark","AdvancePatchbotSpark","UpdatePatchbotSparks","ReleasePatchbotSparks")
 +"public static void Tick(){Instance.UpdateChargeSparks(Time.deltaTime);}public static void TickAll(){Instance.Update();}public static int Pending=>Instance.chargeSparks.Count;public static int GhostCount=>Instance.ghosts.Count;public static int PatchCount=>Instance.patchbotSparks.Count;}\n";
string oldSpark="public class OldSpark {float sparkLifetime=.5f;const string FuseSparkPoolKey=\"PulseFuseSpark\";const int FuseSparkPoolMax=256;"
 +Methods(Baseline("PulsePulseExplosionVfx"),"CoAnimateChargeSpark").Replace("private IEnumerator", "public IEnumerator")+"}\n";
string oldPatch="public class OldPatch {float impactSparkLife=.48f;static void Destroy(GameObject go){go.SetActive(false);}"
 +Methods(Baseline("PatchbotDashUI"),"SparkRoutine").Replace("private IEnumerator", "public IEnumerator")+"}\n";
string Burst(string src,string className){
 string s=src.Substring(0,src.IndexOf("    // ========== SPRITE GENERATION"));
 s=s.Replace("using UnityEngine;","").Replace("using UnityEngine.UI;","").Replace("using System.Collections;","").Replace("using System.Collections.Generic;","");
 return s.Replace("public static class TileClearBurstVfx", "public static class "+className)+"static readonly Sprite sprite=new Sprite();static Sprite GetSoftCircleSprite()=>sprite;static Sprite GetStarSprite()=>sprite;static Sprite GetShardSprite()=>sprite;}\n";
}
string bursts=Burst(Read("TileClearBurstVfx"),"NewBurst")+Burst(Baseline("TileClearBurstVfx"),"OldBurst");
var cacheTree=CSharpSyntaxTree.ParseText(Read("PulseCoreExplosionFX")).GetRoot();
string cache="public class PulseCoreExplosionFX {const int VariantsPerKey=3;"
 +cacheTree.DescendantNodes().OfType<StructDeclarationSyntax>().Single(n=>n.Identifier.ValueText=="VariantKey").ToFullString()
 +cacheTree.DescendantNodes().OfType<FieldDeclarationSyntax>().Single(n=>n.Declaration.Variables.Any(v=>v.Identifier.ValueText=="variantCache")).ToFullString()
 .Replace("new();","new Dictionary<VariantKey, List<Sprite>>();")
 +"public Image sunburstRays=new GameObject(\"rays\").image,shockwaveRing=new GameObject(\"ring\").image;public int rayTexSize=256,noiseTexSize=128,rayCount=16;"
 +"public float rayThinness=.82f,raySoftness=.05f,rayReach=.95f,rayIrregularity=.6f,rayIrregFreq=5,ringThickness=.22f,noiseAmplitude=.25f,noiseFrequency=5,edgeSoftness=.15f;"
 +"public static int textures;Texture2D GenerateSunburstTexture(int size){textures++;return new Texture2D();}Texture2D GenerateNoisyRingTexture(int size,float seed){textures++;return new Texture2D();}public void Play(){ApplySunburstSprite();ApplyNoisyRingSprite();}"
 +Methods(Read("PulseCoreExplosionFX"),"GetOrCreateVariant","ApplySunburstSprite","ApplyNoisyRingSprite")+"}\n";
string tests=@"
public static class Checks {
 static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
 static bool Same(Vector3 a,Vector3 b)=>Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.z,b.z);
 static void CompareBurst(GameObject a,GameObject b){
  var left=UiVfxPool.all.Where(g=>g.activeSelf&&g.transform.parent==a.transform).OrderBy(g=>g.rentalOrder).ToArray();
  var right=UiVfxPool.all.Where(g=>g.activeSelf&&g.transform.parent==b.transform).OrderBy(g=>g.rentalOrder).ToArray();
  Check(left.Length==13&&right.Length==13,""normal match keeps one ring, five stars and seven shards"");
  for(int i=0;i<13;i++){
   var x=(RectTransform)left[i].transform;var y=(RectTransform)right[i].transform;
   Check(Same(x.anchoredPosition,y.anchoredPosition)&&Same(x.sizeDelta,y.sizeDelta)&&Same(x.localScale,y.localScale)
    &&Near(x.localRotation.euler.z,y.localRotation.euler.z)&&Near(left[i].image.color.a,right[i].image.color.a),""normal match burst keeps positions, scale, rotation and alpha every frame"");
  }
 }
 static void Finish(IEnumerator routine){while(routine.MoveNext()){}(routine as IDisposable)?.Dispose();}
 public static void Run(){
  var old=new OldBeam();var next=new NewBeam();
  for(int frame=0;frame<300;frame++){
   Time.time=frame*.02f;float alpha=(frame%51)/50f;old.Step(alpha);next.Step(alpha);
   for(int i=0;i<18;i++)Check(Same(old.lr.points[i],next.lr.points[i])&&Same(old.innerLr.points[i],next.innerLr.points[i]),""bulk line upload preserves every outer/inner point"");
   Check(Near(old.innerLr.colorGradient.alphas[0].alpha,next.innerLr.colorGradient.alphas[0].alpha)&&Near(old.innerLr.colorGradient.alphas[1].alpha,next.innerLr.colorGradient.alphas[1].alpha),""cached gradient preserves fade"");
  }
  Check(old.lr.submissions==300*18&&next.lr.submissions==300,""native submissions reduced from 18 to one per line"");
  var c=new Color(.3f,.7f,.8f,.6f);old.Persistent(c);next.Persistent(c);
  for(int i=0;i<3;i++)Check(Near(old.lr.colorGradient.alphas[i].alpha,next.lr.colorGradient.alphas[i].alpha)&&Near(old.lr.colorGradient.colors[i].time,next.lr.colorGradient.colors[i].time),""persistent gradient keys unchanged"");
  long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++)old.Step(.5f);long oldBeam=GC.GetAllocatedBytesForCurrentThread()-start;
  start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++)next.Step(.5f);long newBeam=GC.GetAllocatedBytesForCurrentThread()-start;
  Check(oldBeam>0&&newBeam==0,""warm line shape and gradient updates allocate no managed objects in doubles"");
  var owner=new GameObject(""charge"").transform;var owner2=new GameObject(""other charge"").transform;
  var a=UiVfxPool.RentImage(UiFxPool.ChargeSparkKey,owner,""old spark"");var b=UiVfxPool.RentImage(UiFxPool.ChargeSparkKey,owner,""new spark"");
  var previous=new OldSpark();Random.Reset();var animation=previous.CoAnimateChargeSpark(a.rectTransform,a,30);animation.MoveNext();
  Random.Reset();UiFxPool.TrackChargeSpark(b,owner,.5f,30);UiFxPool.Tick();
  Check(Same(a.rectTransform.anchoredPosition,b.rectTransform.anchoredPosition)&&Near(a.color.a,b.color.a),""spawn frame advances once, matching synchronous coroutine start"");
  for(int frame=0;frame<24;frame++){
   Time.frameCount++;animation.MoveNext();UiFxPool.Tick();
   Check(Near(a.color.a,b.color.a)&&Same(a.rectTransform.sizeDelta,b.rectTransform.sizeDelta)&&Same(a.rectTransform.anchoredPosition,b.rectTransform.anchoredPosition),""shared charge-spark driver preserves original drift, size and alpha curves"");
  }
  Finish(animation);UiFxPool.ReleaseChargeSparks(owner);
  Check(UiFxPool.Pending==0&&UiVfxPool.outstanding==0,""spark completion releases all rented images"");
  a=UiVfxPool.RentImage(UiFxPool.ChargeSparkKey,owner,""a"");b=UiVfxPool.RentImage(UiFxPool.ChargeSparkKey,owner2,""b"");
  UiFxPool.TrackChargeSpark(a,owner,.5f,30);UiFxPool.TrackChargeSpark(b,owner2,.5f,30);
  UiFxPool.ReleaseChargeSparks(owner);int returns=UiVfxPool.returns;UiFxPool.ReleaseChargeSparks(owner);
  Check(UiVfxPool.returns==returns&&UiFxPool.Pending==1&&b.gameObject.activeSelf&&!a.gameObject.activeSelf,""cancel affects only its owner and cannot double-return an image"");
  owner2.gameObject.SetActive(false);Time.frameCount++;UiFxPool.Tick();
  Check(UiFxPool.Pending==0&&!b.gameObject.activeSelf,""inactive owner cancels remaining sparks"");owner2.gameObject.SetActive(true);
  int created=UiVfxPool.created;
  start=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<2000;i++){
   a=UiVfxPool.RentImage(UiFxPool.ChargeSparkKey,owner,""reused"");UiFxPool.TrackChargeSpark(a,owner,.5f,30);UiFxPool.ReleaseChargeSparks(owner);
  }
  long sparkBytes=GC.GetAllocatedBytesForCurrentThread()-start;
  Check(UiVfxPool.created==created&&sparkBytes==0&&UiFxPool.Pending==0,""warm spawn/cancel reuses image and tracking storage without managed allocations"");
  a=UiVfxPool.RentImage(""patch ghost"",owner,""ghost"");a.color=new Color(.6f,.88f,1,.8f);
  UiFxPool.TrackLinearGhost(a.gameObject,""patch ghost"",a,.3f,owner);UiFxPool.TickAll();
  Check(Near(a.color.a,.8f*(1-.02f/.3f)),""PatchBot afterimage applies its original first fade step exactly once"");
  for(int i=1;i<15;i++){
   Time.frameCount++;UiFxPool.TickAll();Check(Near(a.color.a,.8f*Math.Max(0,1-(i+1)*.02f/.3f)),""shared PatchBot afterimage follows the original linear alpha curve"");
  }
  UiFxPool.ReleaseGhosts(owner);Check(UiFxPool.GhostCount==0,""PatchBot disable cancels only its tracked afterimages"");
  var trail=new GameObject(""trail"").transform;trail.SetParent(owner);
  a=UiVfxPool.RentImage(""LineTravelGhost:"",trail,""line child"");b=UiVfxPool.RentImage(""LineTravelGhost:"",owner2,""external tail"");
  UiFxPool.TrackGhost(a.gameObject,null,a,a.rectTransform,.3f,1.15f,1.8f,owner);
  UiFxPool.TrackGhost(b.gameObject,null,b,b.rectTransform,.3f,1.15f,1.8f,owner);
  UiFxPool.ReleaseGhosts(owner,true);
  Check(!a.gameObject.activeSelf&&b.gameObject.activeSelf&&UiFxPool.GhostCount==1,""rocket teardown recycles child trails while preserving externally parented tails"");
  UiFxPool.ReleaseGhosts(owner);Check(UiFxPool.GhostCount==0&&UiVfxPool.outstanding==0,""ghost cleanup leaves no tracked or rented objects"");
  a=UiVfxPool.RentImage(""patch spark"",owner,""old patch spark"");b=UiVfxPool.RentImage(""patch spark"",owner,""new patch spark"");
  var from=new Vector2(4,5);var to=new Vector2(54,17);
  var oldPatch=new OldPatch().SparkRoutine(a.gameObject,a.rectTransform,a,from,to,.48f,""patch spark"");oldPatch.MoveNext();
  UiFxPool.TrackPatchbotSpark(b,owner,from,to,.48f,""patch spark"");UiFxPool.TickAll();
  for(int i=0;i<22;i++){
   Check(Near(a.color.a,b.color.a)&&Same(a.rectTransform.localScale,b.rectTransform.localScale)&&Same(a.rectTransform.anchoredPosition,b.rectTransform.anchoredPosition),""PatchBot launch/impact sparks preserve original motion, pop and fade curves"");
   Time.frameCount++;oldPatch.MoveNext();UiFxPool.TickAll();
  }
  Finish(oldPatch);UiFxPool.ReleasePatchbotSparks(owner);Check(UiFxPool.PatchCount==0&&UiVfxPool.outstanding==0,""PatchBot spark cancellation returns its objects once"");
  var pulse=new PulseCoreExplosionFX();for(int i=0;i<4;i++)pulse.Play();
  Check(PulseCoreExplosionFX.textures==6,""PulseCore keeps three procedural variants for each texture kind"");
  start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++)pulse.Play();long pulseBytes=GC.GetAllocatedBytesForCurrentThread()-start;
  Check(PulseCoreExplosionFX.textures==6&&pulseBytes==0,""warm PulseCore cache lookup creates no strings, closures, textures or managed objects; bytes=""+pulseBytes);
  pulse.ringThickness=.3f;pulse.Play();Check(PulseCoreExplosionFX.textures==7,""changed ring settings select a distinct cache key"");
  var parent=new GameObject(""board"").transform;var board=new BoardController();
  var before=OldBurst.CoPlayBurstAtWorldPosition(Vector3.zero,parent,board,.3f);
  var after=NewBurst.CoPlayBurstAtWorldPosition(Vector3.zero,parent,board,.3f);
  Random.Reset();before.MoveNext();var beforeRoot=UiVfxPool.lastRoot;
  Random.Reset();after.MoveNext();var afterRoot=UiVfxPool.lastRoot;CompareBurst(beforeRoot,afterRoot);
  while(true){bool x=before.MoveNext(),y=after.MoveNext();Check(x==y,""normal match burst keeps original frame duration"");if(!x)break;CompareBurst(beforeRoot,afterRoot);}
  Check(UiVfxPool.outstanding==0,""completed comparison returns all pieces"");
  var oldBurst=OldBurst.CoPlayBurstAtWorldPosition(Vector3.zero,parent,board,.3f);var newBurst=NewBurst.CoPlayBurstAtWorldPosition(Vector3.zero,parent,board,.3f);
  Random.Reset();oldBurst.MoveNext();Random.Reset();newBurst.MoveNext();
  Check(UiVfxPool.outstanding==28,""concurrent bursts own separate root/ring/five stars/seven shards"");
  (oldBurst as IDisposable).Dispose();Check(UiVfxPool.outstanding==28,""baseline cancellation retains pieces until scene teardown"");
  (newBurst as IDisposable).Dispose();Check(UiVfxPool.outstanding==14,""new cancellation returns its own fourteen objects"");
  // Complete baseline after a fresh run; reset its intentionally abandoned fake scene objects.
  UiVfxPool.outstanding=0;
  for(int i=0;i<4;i++){Finish(OldBurst.CoPlayBurstAtWorldPosition(Vector3.zero,parent,board,.3f));Finish(NewBurst.CoPlayBurstAtWorldPosition(Vector3.zero,parent,board,.3f));}
  start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)Finish(OldBurst.CoPlayBurstAtWorldPosition(Vector3.zero,parent,board,.3f));long oldBurstBytes=GC.GetAllocatedBytesForCurrentThread()-start;
  created=UiVfxPool.created;
  start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)Finish(NewBurst.CoPlayBurstAtWorldPosition(Vector3.zero,parent,board,.3f));long newBurstBytes=GC.GetAllocatedBytesForCurrentThread()-start;
  Check(newBurstBytes<oldBurstBytes&&UiVfxPool.created==created&&UiVfxPool.outstanding==0,""warm bursts reuse scratch buffers and pooled UI while reducing managed allocation"");
  Console.WriteLine(""PASS: ""+checks+"" VFX behavior/reuse/cancellation checks. Doubles allocation: 2000 beam frames ""+oldBeam+"" -> ""+newBeam+"" B; 2000 spark spawn/cancel ""+sparkBytes+"" B; 1000 bursts ""+oldBurstBytes+"" -> ""+newBurstBytes+"" B. Not a Unity/Android profile."");
 }
}
Checks.Run();";
await CSharpScript.RunAsync(stubs+beam+driver+oldSpark+oldPatch+bursts+cache+tests,
 ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
