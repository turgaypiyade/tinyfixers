// Run the actual shared TarpStatue collection routine and Dimerdal driver with Unity doubles.
#load "check_dimerdal.csx"
using System;using System.Linq;using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var sharedStart=source.IndexOf("public interface ICollectionFxTarget");
var sharedEnd=source.IndexOf("internal sealed class DimerdalCollectionFx",sharedStart);
var sharedSource=source.Substring(0,sharedStart)+production("Grid/AncientSealCollectFx.cs").Replace("UnityEngine.Object.Destroy","TestObject.Destroy").Replace("UnityEngine.Random.Range","TestRandom.Range")+source.Substring(sharedEnd);
sharedSource=sharedSource.Replace("public RectTransform transform=>gameObject.transform;", "public RectTransform transform=>gameObject.transform;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();")
 .Replace("public bool activeInHierarchy=true;public int layer;","public bool activeInHierarchy=true;public int layer;public void SetActive(bool active){activeInHierarchy=active;}")
 .Replace("public class RectTransform:Component {", "public class RectTransform:Component {public RectTransform parent;public Vector2 offsetMin,offsetMax;public System.Collections.Generic.List<RectTransform>children=new System.Collections.Generic.List<RectTransform>();public int childCount=>children.Count;public RectTransform GetChild(int i)=>children[i];public Vector3 TransformVector(Vector3 p)=>p;public Vector3 InverseTransformVector(Vector3 p)=>p;")
 .Replace("public void SetParent(object parent,bool stay){}", "public void SetParent(object value,bool stay){parent?.children.Remove(this);parent=value as RectTransform;parent?.children.Add(this);}")
 .Replace("public Vector2 InverseTransformPoint(Vector2 p)=>p;", "public Vector2 InverseTransformPoint(Vector2 p)=>p;public Vector2 InverseTransformPoint(Vector3 p)=>new Vector2(p.x,p.y);")
 .Replace("public struct Color {public Color(float r,float g,float b,float a=1){}}", "public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1);public static Color Lerp(Color a,Color b,float t)=>new Color();}")
 .Replace("public static Vector2 zero=>", "public float magnitude=>(float)Math.Sqrt(x*x+y*y);public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);public static Vector2 operator/(Vector2 a,float b)=>new Vector2(a.x/b,a.y/b);public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>LerpUnclamped(a,b,t);public static Vector2 zero=>")
 .Replace("public static Vector3 one=>", "public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);public static Vector3 right=>new Vector3(1,0,0);public static Vector3 operator/(Vector3 a,float b)=>new Vector3(a.x/b,a.y/b,a.z/b);public static Vector3 one=>")
 .Replace("public int jobs,resolves;", "public RectTransform TilesRoot=>ContentRoot;public int TileSize=100;public Sprite GetIcon(TileType t)=>new Sprite();public int jobs,resolves;")
 .Replace("public Vector3 worldSum;public int count;", "public Vector3 worldSum;public int count;public Vector3 WorldCenter=>count>0?worldSum/count:worldSum;")
 .Replace("public static int Range(int a,int b)=>0;", "public static int Range(int a,int b)=>0;public static float Range(float a,float b)=>a;");
var poolStart=sharedSource.IndexOf("public static class UiVfxPool");
var poolEnd=sharedSource.IndexOf("public interface IClearEffectDescriptor",poolStart);
sharedSource=sharedSource.Substring(0,poolStart)+@"
public static class UiVfxPool {
 static readonly Dictionary<string,Stack<GameObject>>pools=new Dictionary<string,Stack<GameObject>>();public static int rented,returned;
 public static Image RentImage(string key,RectTransform parent,string name){var go=Rent(key,parent,name,true);var img=go.GetComponent<Image>();img.enabled=true;img.color=Color.white;return img;}
 public static GameObject RentRect(string key,RectTransform parent,string name)=>Rent(key,parent,name,false);
 static GameObject Rent(string key,RectTransform parent,string name,bool image){rented++;GameObject go=pools.TryGetValue(key,out var stack)&&stack.Count>0?stack.Pop():new GameObject(name,image?new[]{typeof(Image)}:new Type[0]);go.SetActive(true);go.transform.SetParent(parent,false);go.transform.localScale=Vector3.one;return go;}
 public static void Return(string key,GameObject go){returned++;if(!pools.TryGetValue(key,out var stack))pools[key]=stack=new Stack<GameObject>();go.SetActive(false);go.transform.SetParent(null,false);stack.Push(go);}
}
public static class AncientSealService {public class GatherBatch {public Vector3 worldSum;public int count;public Vector3 WorldCenter=>count>0?worldSum/count:worldSum;}}
public static class TestObject {public static void Destroy(GameObject go){go.SetActive(false);go.transform.SetParent(null,false);}}
public class FlightTrailFx {
 public static Sprite GlowSprite()=>new Sprite();public static Color WithAlpha(Color c,float alpha){c.a=alpha;return c;}
 public static RectTransform CreateImage(RectTransform p,string n,Sprite s,Color c,float size){var go=new GameObject(n,typeof(Image));go.transform.SetParent(p,false);go.GetComponent<Image>().sprite=s;return go.transform;}
 public static FlightTrailFx Create(MonoBehaviour h,RectTransform r,Color c,float size)=>new FlightTrailFx();public void Step(Vector2 p,float progress){}public void Finish(){}
}
"+sharedSource.Substring(poolEnd);
await CSharpScript.RunAsync(sharedSource+"\nChecks.Run();",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
Console.WriteLine("PASS: actual AncientSealCollectFx (TarpStatue) pop/gather/orbit/launch/arrival routine, pooled root/images and Dimerdal driver exercised together (Unity doubles).");
