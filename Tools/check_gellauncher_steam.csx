// Run: csi Tools/check_gellauncher_steam.csx. Isolated production coroutine tests; no Unity build.
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

string Read(string file) => File.ReadAllText("Assets/_Project/Scripts/" + file);
string Members(string file, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(Read(file)).GetRoot()
    .DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
var tint = CSharpSyntaxTree.ParseText(Read("Grid/Board/GelLauncherFx.cs")).GetRoot().DescendantNodes()
    .OfType<FieldDeclarationSyntax>().Single(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == "SteamTint")).ToFullString();
var live = CSharpSyntaxTree.ParseText(Read("VFX/PatchbotDashUI.cs")).GetRoot().DescendantNodes()
    .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "LiveTargetState").ToFullString().Replace("private sealed", "public sealed");
var harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static Vector2 operator*(Vector2 a,float k)=>new Vector2(a.x*k,a.y*k);
 public static bool operator==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y;public static bool operator!=(Vector2 a,Vector2 b)=>!(a==b);
 public override bool Equals(object o)=>o is Vector2&&this==(Vector2)o;public override int GetHashCode()=>0;
 public float sqrMagnitude=>x*x+y*y;public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*t;
}
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 one=>new Vector3(1,1,1);public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y,0);
 public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator*(Vector3 a,float f)=>new Vector3(a.x*f,a.y*f,a.z*f);
}
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
public static class Time {public static float deltaTime=.1f;}
public static class Random {public static float Range(float a,float b)=>a;}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Lerp(float a,float b,float t)=>a+(b-a)*t;public static float Exp(float f)=>(float)Math.Exp(f);}
public class Component {public GameObject gameObject;}
public class RectTransform:Component {public Vector2 anchorMin,anchorMax,pivot,sizeDelta;public Vector3 localPosition,localScale;public void SetParent(RectTransform p,bool keep){}public void SetAsLastSibling(){}}
public class CanvasRenderer:Component {}
public class Sprite {}
public class Image:Component {public Sprite sprite;public bool preserveAspect,raycastTarget;public Color color;}
public class GameObject {
 public static List<GameObject> All=new List<GameObject>();public bool destroyed,activeInHierarchy=true;public float expiry=-1;public int layer;public RectTransform transform;
 Dictionary<Type,Component> components=new Dictionary<Type,Component>();
 public GameObject(string name,params Type[] types){All.Add(this);transform=new RectTransform{gameObject=this};components[typeof(RectTransform)]=transform;foreach(var t in types)if(t!=typeof(RectTransform)){var c=(Component)Activator.CreateInstance(t);c.gameObject=this;components[t]=c;}}
 public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components[typeof(T)]=c;return c;}
 public T GetComponent<T>() where T:Component=>components.TryGetValue(typeof(T),out var c)?(T)c:null;
}
public class Object {public static void Destroy(GameObject go){go.destroyed=true;}public static void Destroy(GameObject go,float seconds){go.expiry=seconds;}}
public class MonoBehaviour:Component {
 public bool isActiveAndEnabled=true;public List<IEnumerator> routines=new List<IEnumerator>();
 public void StartCoroutine(IEnumerator routine){routines.Add(routine);}
 public void Tick(){foreach(var r in new List<IEnumerator>(routines))if(!r.MoveNext())routines.Remove(r);}
 public static void Destroy(GameObject go)=>Object.Destroy(go);public static void Destroy(GameObject go,float t)=>Object.Destroy(go,t);
 public static void DontDestroyOnLoad(GameObject go){}
}
public static class UiVfxPool {
 public static Stack<GameObject> Idle=new Stack<GameObject>();public static int Returns;
 public static Image RentImage(string key,RectTransform parent,string name){var go=Idle.Count>0?Idle.Pop():new GameObject(name,typeof(Image));go.activeInHierarchy=true;return go.GetComponent<Image>();}
 public static void Return(string key,GameObject go,int max){Returns++;go.activeInHierarchy=false;if(Idle.Count<max)Idle.Push(go);else Object.Destroy(go);}
}
" + Read("Grid/Board/GelLauncherSteamFx.cs").Replace("using System.Collections.Generic;", "").Replace("using UnityEngine;", "").Replace("using UnityEngine.UI;", "") + @"
public static class GelLauncherFx {
" + tint + @"
 static Sprite RandomSteamSprite()=>new Sprite();
" + Members("Grid/Board/GelLauncherFx.cs", "SpawnSteam") + @"
}
public class Dash {
 float liveRetargetInterval=.05f,retargetSteerSpeed=9f;
" + live + Members("VFX/PatchbotDashUI.cs", "TickLiveRetarget").Replace("private void", "public void") + @"
}
public static class Checks {
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 static void Invoke(GelLauncherSteamFx driver,string method)=>typeof(GelLauncherSteamFx).GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(driver,null);
 public static int Run(){
  var emitter=new GameObject(""emitter"").AddComponent<MonoBehaviour>();var parent=new GameObject(""parent"").transform;
  GelLauncherFx.SpawnSteam(emitter,parent,new Vector2(0,0),new Vector2(0,1),100,2);
  var clouds=GameObject.All.FindAll(g=>g.GetComponent<Image>()!=null);
  var drivers=GameObject.All.FindAll(g=>g.GetComponent<GelLauncherSteamFx>()!=null);
  var driver=drivers[0].GetComponent<GelLauncherSteamFx>();
  Check(clouds.Count==2&&drivers.Count==1&&emitter.routines.Count==0&&driver.routines.Count==0,""One shared driver, no per-cloud components or coroutines"");
  Object.Destroy(emitter.gameObject);emitter.routines.Clear();
  foreach(var cloud in clouds){
   var img=cloud.GetComponent<Image>();
   Check(cloud.expiry<0,""No delayed destroy can kill a future pooled lifetime"");
   Check(img.color.a==1&&img.color.r<.7f&&img.color.g<.5f,""Smoke is opaque and darker purple"");
  }
  for(int i=0;i<4;i++)Invoke(driver,""Update"");
  Check(clouds[0].GetComponent<Image>().color.a<1&&!clouds[0].destroyed,""Steam fades after emitter destruction"");
  for(int i=0;i<10;i++)Invoke(driver,""Update"");
  Check(UiVfxPool.Idle.Count==2&&UiVfxPool.Returns==2&&!clouds[0].destroyed,""Expired clouds return exactly once"");
  emitter=new GameObject(""next emitter"").AddComponent<MonoBehaviour>();int allocated=GameObject.All.Count;
  for(int cycle=0;cycle<100;cycle++){
   GelLauncherFx.SpawnSteam(emitter,parent,new Vector2(5,7),new Vector2(0,1),100,2);
   Check(clouds[0].GetComponent<Image>().color.a==1&&clouds[0].transform.localScale.x==.35f,""Reuse resets fade and scale"");
   for(int i=0;i<10;i++)Invoke(driver,""Update"");
  }
  Check(GameObject.All.Count==allocated&&UiVfxPool.Idle.Count==2,""100 repeated emissions allocate no new game objects after warmup"");
  GelLauncherFx.SpawnSteam(emitter,parent,new Vector2(),new Vector2(0,1),100,2);
  foreach(var cloud in clouds)cloud.activeInHierarchy=false;
  Invoke(driver,""Update"");Check(UiVfxPool.Idle.Count==2,""Disabled board reclaims active clouds"");
  GelLauncherFx.SpawnSteam(emitter,parent,new Vector2(),new Vector2(0,1),100,2);
  Invoke(driver,""OnDisable"");Check(UiVfxPool.Idle.Count==2,""Driver teardown returns active clouds"");
  int returns=UiVfxPool.Returns;Invoke(driver,""OnDisable"");Check(UiVfxPool.Returns==returns,""Repeated teardown cannot duplicate pool entries"");
  var dash=new Dash();var state=new Dash.LiveTargetState{target=new Vector2(1,0),goal=new Vector2(1,0),resolve=()=>null};
  dash.TickLiveRetarget(state,.1f);Check(!state.HasTarget&&!state.goal.HasValue,""Missing live target clears stale generator destination"");
  var fixedTarget=new Dash.LiveTargetState{target=new Vector2(1,0)};
  dash.TickLiveRetarget(fixedTarget,.1f);Check(fixedTarget.HasTarget,""Legacy static dash retains its target"");
  return count;
 }
}
Checks.Run()";
var result=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" steam lifetime and stale visual target checks");

string[] changed={"Grid/Board/GelLauncherFx.cs","Grid/Board/GelLauncherSteamFx.cs","Grid/Board/Targeting/BoardTargetPool.cs",
 "Grid/Board/Actions/PatchBotTargetCoordinator.cs","Grid/Board/Actions/OverridePatchBotAirborneGroupAction.cs",
 "Grid/Board/Actions/PatchbotLiveDashTargetRegistry.cs","Grid/Board/PatchbotComboService.cs","Grid/Board/Specials/PatchBotSpecial.cs",
 "Grid/Board/Combos/LineHPatchBotCombo.cs","Grid/Board/Combos/LineVPatchBotCombo.cs","VFX/PatchbotDashUI.cs"};
foreach(var file in changed){var errors=CSharpSyntaxTree.ParseText(Read(file)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(file+": "+string.Join("\n",errors.Select(e=>e.ToString())));}
Console.WriteLine("PASS: changed C# source syntax");
