// Production region/path code with Unity doubles and all eight authored wonders. No Unity build/render.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;using System.Globalization;using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
string dir="Assets/_Project/";
foreach(var file in new[]{"Scripts/UI/WonderTaskRegions.cs","Scripts/UI/WonderRevealView.cs","Scripts/UI/Journey/JourneyScreenController.cs"}) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(dir+file)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var paths=Directory.GetFiles(dir+"Settings/Wonders","Wonder_mis*.asset").OrderBy(p=>p).ToArray();
var definitions=paths.Select(p=> {
 string body=File.ReadAllText(p).Split(new[]{"  tasks:\n"},StringSplitOptions.None)[1].Split(new[]{"  starCosts:"},StringSplitOptions.None)[0];
 var tasks=body.Split(new[]{"  - name: "},StringSplitOptions.None).Skip(1).Select(chunk=> {
  int action=int.Parse(Regex.Match(chunk,@"    action: (\d+)").Groups[1].Value);
  var rects=Regex.Matches(chunk,@"      x: (\S+)\n      y: (\S+)\n      width: (\S+)\n      height: (\S+)").Cast<Match>()
   .Select(m=>"new Rect("+string.Join(",",Enumerable.Range(1,4).Select(i=>float.Parse(m.Groups[i].Value,CultureInfo.InvariantCulture).ToString("R",CultureInfo.InvariantCulture)+"f"))+")");
  return "new WonderTask{action=(WonderTaskAction)"+action+",regions=new Rect[]{"+string.Join(",",rects)+"}}";
 });
 return "new WonderDefinition{wonderId=\""+Path.GetFileNameWithoutExtension(p)+"\",tasks=new[]{"+string.Join(",",tasks)+"}}";
});
string shader=File.ReadAllText(dir+"Shaders/UI_WonderReveal.shader");
if(!shader.Contains("float4 _TaskRects[24]")||!shader.Contains("rectangle - (_RegionCut.w - 1.0)")
 ||!shader.Contains("_TaskAnim.z > 4.5 && clean >= 0.999")||!shader.Contains("saturate(_ContextReveal)")
 ||!shader.Contains("rectangle < 0.0 || abs(owner - _PieceTask)"))throw new Exception("Shader region/context/shine contract missing");
string view=File.ReadAllText(dir+"Scripts/UI/WonderRevealView.cs");
if(!view.Contains("rectangle + 1f")||!view.Contains("UpdateRestorationWelder(cell, p, cutting: true)"))throw new Exception("View must send the traced rectangle and local progress");
var viewRoot=CSharpSyntaxTree.ParseText(view).GetRoot();
string contextMethods=string.Join("\n",viewRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
 .Where(m=>new[]{"SetContextProgress","ResetRestorationAnimation"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToString()))
 .Replace("void SetContextProgress","public void SetContextProgress").Replace("void ResetRestorationAnimation","public void ResetRestorationAnimation");
var source=@"
using System;using System.Linq;using System.Collections.Generic;
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
public struct Vector4 {public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static Vector4 zero=>new Vector4();}
public struct Rect {
 public float x,y,width,height;public Rect(float x,float y,float width,float height){this.x=x;this.y=y;this.width=width;this.height=height;}
 public float xMin=>x;public float yMin=>y;public float xMax=>x+width;public float yMax=>y+height;
 public static Rect MinMaxRect(float a,float b,float c,float d)=>new Rect(a,b,c-a,d-b);
}
public static class Mathf {
 public const float PI=(float)Math.PI;public static float Sin(float f)=>(float)Math.Sin(f);
 public static int FloorToInt(float f)=>(int)Math.Floor(f);public static int Max(int a,int b)=>Math.Max(a,b);
 public static int Clamp(int x,int a,int b)=>Math.Max(a,Math.Min(b,x));
 public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));public static float Lerp(float a,float b,float k)=>a+(b-a)*Clamp01(k);
 public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
}
public static class Shader {public static int PropertyToID(string s)=>s.GetHashCode();}
public class Material {
 public Dictionary<int,Vector4[]> vectors=new Dictionary<int,Vector4[]>();public Dictionary<int,float[]> arrays=new Dictionary<int,float[]>();public Dictionary<int,float> values=new Dictionary<int,float>();
 public void SetVector(int id,Vector4 value){}
 public void SetVectorArray(int id,Vector4[] data){vectors[id]=(Vector4[])data.Clone();}public void SetFloatArray(int id,float[] data){arrays[id]=(float[])data.Clone();}public void SetFloat(int id,float value){values[id]=value;}
}
public static class Debug {public static void LogWarning(string text){}}
public enum WonderTaskAction {Repair,Polish,Paint,Light,Clean,Shine}
public class WonderTask {public Rect[] regions;public WonderTaskAction action;}
public class WonderDefinition {
 public string wonderId;public WonderTask[] tasks;
 public bool HasTaskRegions=>tasks!=null&&tasks.Any(t=>t!=null&&t.regions!=null&&t.regions.Length>0);
}
";
source+=File.ReadAllText(dir+"Scripts/UI/WonderTaskRegions.cs").Replace("using UnityEngine;","");
source+=File.ReadAllText(dir+"Scripts/UI/WonderRestorationLayout.cs").Replace("using UnityEngine;","");
source+="public class ClearPiece {public bool cleared;public void Clear(){cleared=true;}} public class ContextPlayback {public Material _mat=new Material();public float _reveal=.25f;public ClearPiece _fallingPiece=new ClearPiece();static readonly int ContextRevealId=Shader.PropertyToID(\"_ContextReveal\"),RegionCutId=1,RegionFinishId=2,TorchId=3,TaskAnimId=4;"+contextMethods+"}";
source+=@"
public static class Checks {
 static int count;static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
 public static void Run(WonderDefinition[] wonders) {
  Check(WonderTaskRegions.From(null)==null,""null definition uses legacy layout"");
  Check(WonderTaskRegions.From(new WonderDefinition{tasks=new[]{new WonderTask()}})==null,""unmapped legacy wonder stays supported"");
  var playback=new ContextPlayback();float previous=0;
  for(int stage=0;stage<8;stage++)for(int step=0;step<=20;step++) {
   playback.SetContextProgress(stage,step/20f,8);float value=playback._mat.values[Shader.PropertyToID(""_ContextReveal"")];
   Check(value>=previous&&value<=(stage+1)/8f,""scenery progresses smoothly through every task"");previous=value;
  }
  Check(previous==1,""final task restores full scenery colour"");
  playback.ResetRestorationAnimation();Check(playback._fallingPiece.cleared&&playback._mat.values[Shader.PropertyToID(""_ContextReveal"")]==.25f,""cancellation clears the sheet and resets partial context progress"");
  foreach(var def in wonders) {
   var map=WonderTaskRegions.From(def);Check(map!=null&&map.TaskCount==8,""authored task count retained"");
   var material=new Material();map.ApplyTo(material);
   var rects=material.vectors[Shader.PropertyToID(""_TaskRects"")];var owners=material.arrays[Shader.PropertyToID(""_TaskRectOwners"")];
   int size=(int)material.values[Shader.PropertyToID(""_TaskRectCount"")];Check(size<=WonderTaskRegions.MaxRects,""shader capacity respected"");
   int[] coverage=new int[8];int context=0;
   for(int y=0;y<160;y++)for(int x=0;x<100;x++) {
    var uv=new Vector2((x+.5f)/100,(y+.5f)/160);int owner=-1;float smallest=float.PositiveInfinity;
    for(int i=0;i<size;i++) {var r=rects[i];float area=(r.z-r.x)*(r.w-r.y);
     if(uv.x>=r.x&&uv.x<=r.z&&uv.y>=r.y&&uv.y<=r.w&&area<smallest){owner=(int)owners[i];smallest=area;}}
    Check(owner>=-1&&owner<8,""pixel is a work surface or gradual scenery"");if(owner<0)context++;else coverage[owner]++;
   }
   Check(coverage.Max()+context/8f<4800&&coverage.Min()+context/8f>320,""all eight steps have meaningful, bounded colour contributions"");
   Check(coverage.Sum()+context==16000,""completed task sequence restores the entire image"");
   Check(context>0&&context/8f<context,""scenery is restored across all stages, never owned by the first task"");
   for(int task=0;task<8;task++) {
    Check(map.Action(task)==def.tasks[task].action,""authored action retained"");
    var visited=new HashSet<int>();
    for(int step=0;step<=200;step++) {
     int rectangle;float local;Vector2 p=map.TracePoint(task,step/200f,out rectangle,out local);
     Check(local>=0&&local<=1&&p.x>=0&&p.x<=1.000001f&&p.y>=0&&p.y<=1.000001f,""trace stays in normalized art"");
     if(rectangle<0)continue;
     visited.Add(rectangle);Check(rectangle<size&&(int)owners[rectangle]==task,""trace and shader share exact region index"");
     var r=rects[rectangle];const float epsilon=.00001f;
     Check(p.x>=r.x-epsilon&&p.x<=r.z+epsilon&&p.y>=r.y-epsilon&&p.y<=r.w+epsilon,""torch stays on this real region, never the union gap"");
     Check(Math.Abs(p.x-r.x)<epsilon||Math.Abs(p.x-r.z)<epsilon||Math.Abs(p.y-r.y)<epsilon||Math.Abs(p.y-r.w)<epsilon,""torch follows region perimeter"");
    }
    int expected=Enumerable.Range(0,size).Count(i=>(int)owners[i]==task);
    Check(visited.Count==expected,""every separate authored region is visited"");
    if(def.tasks[task].action==WonderTaskAction.Shine)Check(expected==0,""polishing does not claim or reopen future surfaces"");
   }
  }
  var split=new WonderDefinition{tasks=new[]{new WonderTask{regions=new[]{new Rect(.1f,.1f,.1f,.2f),new Rect(.7f,.5f,.1f,.2f)}},new WonderTask()}};
  var trace=WonderTaskRegions.From(split);int first,last;float fraction;
  var a=trace.TracePoint(0,.25f,out first,out fraction);var b=trace.TracePoint(0,.75f,out last,out fraction);
  Check(first==0&&last==1&&a.x<.21f&&b.x>.69f,""two distant towers are traced separately"");
  trace.TracePoint(0,1,out last,out fraction);Check(last==1&&fraction==1,""exact endpoint stays on final region"");
  split.tasks[1].regions=new[]{new Rect(.2f,.8f,.1f,.1f)};trace=WonderTaskRegions.From(split);
  var allTargets=new Material();trace.ApplyTo(allTargets);Check(allTargets.values[Shader.PropertyToID(""_TaskRectCount"")]==3,""last explicit target is retained when no remainder task exists"");
  var many=new WonderDefinition{tasks=new[]{new WonderTask{regions=Enumerable.Range(0,30).Select(i=>new Rect(.1f,.1f,.2f,.2f)).ToArray()},new WonderTask()}};
  trace=WonderTaskRegions.From(many);var m=new Material();trace.ApplyTo(m);Check(m.values[Shader.PropertyToID(""_TaskRectCount"")]==24,""truncated authoring remains bounded"");
  var end=trace.TracePoint(0,1,out last,out fraction);Check(last==23&&fraction>=.9999f,""trace excludes regions rejected by shader capacity"");
  Console.WriteLine(""PASS: ""+count+"" production task-region/path checks across all 64 tasks; shader/view contracts checked. No GPU render/build."");
 }
}
Checks.Run(new[]{"+string.Join(",",definitions)+"});";
await CSharpScript.RunAsync(source,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
