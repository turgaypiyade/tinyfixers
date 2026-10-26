// csi Tools/check_wonder_mission_designer.csx — source/API checks only; no Unity build or emit.
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

var editorFiles = new[] { "Assets/_Project/Editor/WonderMissionAuthoring.cs", "Assets/_Project/Editor/WonderMissionDesignerWindow.cs" };
var unity = "/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine";
var newtonsoft = Directory.GetFiles("Library/PackageCache", "Newtonsoft.Json.dll", SearchOption.AllDirectories).First(p => !p.Contains("/AOT/"));
var refs = new List<MetadataReference> {
 MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
 MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
 MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location),
 MetadataReference.CreateFromFile("/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Facades/netstandard.dll"),
 MetadataReference.CreateFromFile(Path.GetFullPath(newtonsoft)),
 MetadataReference.CreateFromFile(Path.GetFullPath("Library/ScriptAssemblies/Assembly-CSharp.dll")),
 MetadataReference.CreateFromFile(Path.GetFullPath("Library/ScriptAssemblies/UnityEngine.UI.dll"))
};
refs.AddRange(Directory.GetFiles(unity, "*.dll").Select(p => MetadataReference.CreateFromFile(p)));
var parseOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("UNITY_EDITOR");
var trees = editorFiles.Concat(new[] {
 "Assets/_Project/Scripts/UI/WonderDefinition.cs", "Assets/_Project/Scripts/UI/WonderCatalog.cs",
 "Assets/_Project/Scripts/UI/WonderTaskRegions.cs", "Assets/_Project/Scripts/UI/WonderRestorationLayout.cs"
}).Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), parseOptions, p)).ToList();
// Country-order lookup is irrelevant to authoring and belongs to the already-tested runtime catalog.
trees.Add(CSharpSyntaxTree.ParseText("public static class WonderOrder { public static int RawIndex(WonderCatalog c,int i)=>i; }"));
var compilation = CSharpCompilation.Create("WonderMissionDesignerApiCheck", trees, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
if (diagnostics.Length > 0) throw new Exception(string.Join("\n", diagnostics.Select(d => d.ToString())));
foreach (var path in editorFiles) {
 if (!path.Contains("/Editor/")) throw new Exception("Authoring code must remain Editor-only.");
 if (CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot().Members.Count != 0)
  throw new Exception("Authoring types survive without UNITY_EDITOR: " + path);
}
Console.WriteLine("PASS: tool binds to installed Unity Editor/runtime APIs; both files disappear without UNITY_EDITOR. No assembly emitted.");

// Execute the actual editor helper with light data doubles. Never load Unity or touch project assets.
var doubles = @"
using System; using System.IO; using System.Linq; using System.Text; using System.Collections.Generic;
using Newtonsoft.Json; using Newtonsoft.Json.Linq;
namespace UnityEngine {
 public struct Vector2 {public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Rect {public float x,y,width,height; public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
  public float xMin=>x; public float yMin=>y; public float xMax=>x+width; public float yMax=>y+height;
  public bool Contains(Vector2 p)=>p.x>=x&&p.x<xMax&&p.y>=y&&p.y<yMax;}
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static int RoundToInt(float f)=>(int)Math.Round(f,MidpointRounding.ToEven);}
}
public enum WonderTaskAction {Repair,Polish,Paint,Light,Clean,Shine}
public class WonderTask {public string name=""task"";public int starCost=1;public WonderTaskAction action;public UnityEngine.Rect[] regions;}
public class WonderDefinition {
 public string wonderId=""test"",displayName=""test"";public object backgroundSprite=new object();public WonderTask[] tasks;
 public int TaskCount=>tasks.Length;public int GetStarCost(int i)=>tasks[i].starCost;
}
public class WonderCatalog {
 public WonderDefinition[] wonders;
 public int firstWonderTotalStars=60,secondWonderTotalStars=75,lastWonderTotalStars=110;
 public int Count=>8;
 public int TotalStarsFor(int i)=>i==0?60:UnityEngine.Mathf.RoundToInt(75+(110-75)*(i-1f)/6);
}
public static class WonderTaskRegions {public const int MaxRects=24;}
";
string helper = File.ReadAllText(editorFiles[0]);
helper = string.Join("\n", helper.Split('\n').Where(l => !l.StartsWith("using ") && !l.StartsWith("#")));
string productionCatalog = File.ReadAllText("Assets/_Project/Scripts/UI/WonderCatalog.cs");
var catalogRoot = CSharpSyntaxTree.ParseText(productionCatalog).GetCompilationUnitRoot();
var costMethod = catalogRoot.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "GetStarCost").ToFullString();
var harness = @"
public class CostProbe {
 public WonderDefinition current; public WonderDefinition Get(int slot)=>current;
 public int TotalStarsFor(int slot)=>new WonderCatalog().TotalStarsFor(slot);
 COST_METHOD
}
public static class Probe {
 static int checks;
 static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 public static void Run(){
  var rng=new Random(42);
  for(int run=0;run<200;run++){
   var d=new WonderDefinition{tasks=Enumerable.Range(0,rng.Next(1,25)).Select(i=>new WonderTask{starCost=rng.Next(1,100)}).ToArray()};
   var p=new CostProbe{current=d};
   for(int slot=0;slot<8;slot++){
    var costs=WonderMissionAuthoring.Costs(d,p.TotalStarsFor(slot));
    Check(costs.Sum()==p.TotalStarsFor(slot),""exact budget"");
    for(int t=0;t<d.TaskCount;t++)Check(costs[t]==p.GetStarCost(slot,t),""editor/runtime cost parity"");
   }
  }
  var def=new WonderDefinition{tasks=new[]{
   new WonderTask{regions=new[]{new UnityEngine.Rect(0,0,1,1)}},
   new WonderTask{regions=new[]{new UnityEngine.Rect(.25f,.25f,.5f,.5f)}},
   new WonderTask{action=WonderTaskAction.Shine,regions=new[]{new UnityEngine.Rect(.4f,.4f,.1f,.1f)}}}};
  Check(WonderMissionAuthoring.Owner(def,new UnityEngine.Vector2(.45f,.45f))==1,""smallest physical region wins; Shine does not own"");
  var area=WonderMissionAuthoring.Coverage(def,100);
  Check(Math.Abs(area[0]-.75f)<.001&&Math.Abs(area[1]-.25f)<.001&&area[2]==0&&area[3]==0,""area ownership"");
  def.tasks[0].regions=new[]{new UnityEngine.Rect(0,0,.1f,.1f)};
  Check(WonderMissionAuthoring.Owner(def,new UnityEngine.Vector2(.9f,.9f))==-1,""unassigned environment preserved"");
  var en=new[]{""a"",""b"",""c""};
  var catalog=new WonderCatalog();
  Check(WonderMissionAuthoring.Errors(def,""Title"",en,catalog).Count==0,""valid data"");
  catalog.wonders=new[]{new WonderDefinition{wonderId=""other"",tasks=new[]{new WonderTask{starCost=1},new WonderTask{starCost=10000}}}};
  Check(WonderMissionAuthoring.Errors(def,""Title"",en,catalog).Any(s=>s.Contains(""other"")),""global budget validation protects other wonders"");
  catalog.wonders=null;
  def.tasks[0].regions=new[]{new UnityEngine.Rect(float.NaN,0,1,1)};
  Check(WonderMissionAuthoring.Errors(def,""Title"",en,catalog).Count>0,""reject NaN"");
  def.tasks[0].regions=Enumerable.Repeat(new UnityEngine.Rect(0,0,.1f,.1f),25).ToArray();
  Check(WonderMissionAuthoring.Errors(def,""Title"",en,catalog).Any(s=>s.Contains(""24"")),""reject shader overflow"");
  def.tasks[0].regions=new[]{new UnityEngine.Rect(0,0,.1f,.1f)};
  def.tasks[0].starCost=int.MaxValue;
  Check(WonderMissionAuthoring.Errors(def,""Title"",en,catalog).Any(s=>s.Contains(""çok büyük"")),""reject cost overflow"");
  string original=new JObject {
   [""unknown""]=42,[""languages""]=new JArray(
    new JObject{[""code""]=""tr"",[""entries""]=new JArray(new JObject{[""key""]=""untouched"",[""value""]=""koru""})},
    new JObject{[""code""]=""en"",[""entries""]=new JArray()},
    new JObject{[""code""]=""de"",[""entries""]=new JArray()})}.ToString();
  string merged=WonderMissionAuthoring.MergeLocalization(original,def,""English"",en);
  var json=JObject.Parse(merged);
  Check((int)json[""unknown""]==42,""unknown fields survive"");
  Check(WonderMissionAuthoring.ReadText(json,""tr"",""untouched"",""bad"")==""koru"",""unrelated localization survives"");
  Check(WonderMissionAuthoring.ReadText(json,""en"",""wonder_test_task_2"",""bad"")==""b"",""EN keys and order"");
  Check(WonderMissionAuthoring.MergeLocalization(merged,def,""English"",en)==merged,""idempotent localization merge"");
  Console.WriteLine(""PASS: ""+checks+"" cost, geometry, validation and localization checks; no project assets written."");
 }
}
Probe.Run();
".Replace("COST_METHOD",costMethod);
doubles = doubles.Replace("namespace UnityEngine {", "").Replace("\n}\npublic enum WonderTaskAction", "\npublic enum WonderTaskAction");
await CSharpScript.RunAsync((doubles + helper + harness).Replace("UnityEngine.", ""), ScriptOptions.Default.AddReferences(Path.GetFullPath(newtonsoft)));
