// Actual shared shatter iterator with Unity doubles. No Unity build or board simulation.
#load "check_dimerdal_feedback.csx"
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var shatterTree=parse("Assets/_Project/Scripts/VFX/ObstacleShatterFx.cs");
var shatterMethods=string.Join("\n",shatterTree.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Where(m=>new[]{"Run","Acquire","Release"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString())).Replace("Random.","ShatterRandom.");
var shatterSource=source
 .Replace("public bool activeInHierarchy=true;public int layer;","public bool activeInHierarchy=true;public int layer;public void SetActive(bool active){activeInHierarchy=active;}")
 .Replace("public class RectTransform:Component {","public class RectTransform:Component {public RectTransform parent;public Vector3 localPosition;public int GetSiblingIndex()=>0;public void SetSiblingIndex(int n){}")
 .Replace("public void SetParent(object parent,bool stay){}","public void SetParent(object value,bool stay){parent=value as RectTransform;}")
 .Replace("public struct Rect {","public struct Rect {public float x,y,w,h;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;this.w=w;this.h=h;}")
 .Replace("public float width=>200;public float height=>200;public Vector2 center=>Vector2.zero;","public float width=>w;public float height=>h;public Vector2 center=>new Vector2(x+w/2,y+h/2);")
 .Replace("public static Vector2 zero=>", "public float sqrMagnitude=>x*x+y*y;public Vector2 normalized=>new Vector2(x/(float)Math.Sqrt(sqrMagnitude),y/(float)Math.Sqrt(sqrMagnitude));public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);public static Vector2 zero=>")
 .Replace("public static Vector3 one=>", "public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);public static Vector3 one=>")
 .Replace("public static Quaternion identity=>", "public static Quaternion operator*(Quaternion a,Quaternion b)=>new Quaternion{z=a.z+b.z};public static Quaternion identity=>");
var shatterFixture=@"
public static class FlightTrailFx {public static Sprite GlowSprite()=>new Sprite();}
public static class ShatterRandom {public static float Range(float a,float b)=>a;public static Vector2 insideUnitCircle=>new Vector2(1,1);}
public struct ObstacleDebrisBounds {
 public bool fail;public void Fit(ref Vector2 p,ref Vector2 v,ref Vector2 size,float life){if(fail)throw new Exception(""injected teardown"");}
 public Vector3 ClampCenter(Vector3 p,Vector2 size)=>p;
}
public static class ShatterFixture {
 const float Life=.8f,FlashTime=.22f,Overlap=1.04f;
 static readonly Stack<Image> pool=new Stack<Image>();
 public static void CheckRun(){
  var root=new GameObject().transform;var pieces=new[]{new Sprite(),new Sprite(),new Sprite()};var layout=new[]{new Rect(0,0,.5f,1),new Rect(.5f,0,.5f,.5f),new Rect(.5f,.5f,.5f,.5f)};
  var fx=Run(root,pieces,layout,Vector2.zero,new Vector2(200,200),Color.white,new ObstacleDebrisBounds());
  if(!fx.MoveNext())throw new Exception(""shatter has no visible frames"");((IDisposable)fx).Dispose();
  if(pool.Count!=4||pool.Any(i=>i.gameObject.activeInHierarchy))throw new Exception(""cancellation did not return all shards/flash"");
  int created=GameObject.created;
  fx=Run(root,pieces,layout,Vector2.zero,new Vector2(200,200),Color.white,new ObstacleDebrisBounds());while(fx.MoveNext()){}((IDisposable)fx).Dispose();
  if(pool.Count!=4||GameObject.created!=created)throw new Exception(""repeat shatter failed object reuse/normal cleanup"");
  fx=Run(root,pieces,layout,Vector2.zero,new Vector2(200,200),Color.white,new ObstacleDebrisBounds{fail=true});
  try{fx.MoveNext();throw new Exception(""expected injected failure"");}catch(Exception e){if(e.Message!=""injected teardown"")throw;}finally{((IDisposable)fx).Dispose();}
  if(pool.Count!=4||pool.Any(i=>i.gameObject.activeInHierarchy))throw new Exception(""partial setup leaked a shard"");
  Console.WriteLine(""PASS: actual shared shatter normal completion, explicit cancellation, partial-setup cleanup and repeat object reuse."");
 }
"+shatterMethods+"}\nShatterFixture.CheckRun();";
await CSharpScript.RunAsync(shatterSource+shatterFixture,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
var destruction=parse("Assets/_Project/Scripts/Grid/GridSpawner.cs").DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="HandleObstacleDestroyed").ToFullString();
if(!destruction.Contains("dimerdal.PrepareForBreak()")||destruction.IndexOf("dimerdal.PrepareForBreak()")>destruction.IndexOf("Destroy(image.gameObject)"))throw new Exception("Dimerdal opaque frame must be captured in the destruction handler before its host is destroyed.");
Console.WriteLine("PASS: actual GridSpawner destruction handler captures the opaque Dimerdal frame before host removal.");
