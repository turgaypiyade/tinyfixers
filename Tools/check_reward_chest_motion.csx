// Production motion sampling/cancellation; no Unity launch or project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var root=(CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/UI/RewardChestMotion.cs")).GetRoot();
var source=string.Join("\n",root.Members.Select(m=>m.ToFullString()));
var stubs=@"
using System;using System.Collections;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public struct Quaternion {
 public float angle;public static Quaternion Euler(float x,float y,float z)=>new Quaternion{angle=z};
 public static Quaternion operator*(Quaternion a,Quaternion b)=>new Quaternion{angle=a.angle+b.angle};
}
public class Rect {public float width=520;}
public class RectTransform {
 public Vector2 anchoredPosition;public Vector3 localScale;public Quaternion localRotation;public Rect rect=new Rect();
}
public static class Time {public static float unscaledDeltaTime=1f/60;}
public static class Mathf {
 public const float PI=(float)Math.PI;
 public static float Sin(float v)=>(float)Math.Sin(v);public static float Abs(float v)=>Math.Abs(v);
 public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Clamp01(float v)=>Math.Max(0,Math.Min(1,v));
 public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
 public static float InverseLerp(float a,float b,float v)=>Clamp01((v-a)/(b-a));
 public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
}
";
var tests=@"
int checks=0;Action<bool,string> check=(ok,name)=>{checks++;if(!ok)throw new Exception(name);};
float minX=2,maxX=0,minY=2,maxY=0,left=0,right=0;int crossingsEarly=0,crossingsLate=0;float previous=0;
for(int i=0;i<=1000;i++){
 float t=i/1000f;RewardChestMotion.Sample(t,out var p,out var s,out float angle);
 check(!float.IsNaN(s.x)&&s.x>0&&s.y>0,""finite positive scale"");
 minX=Math.Min(minX,s.x);maxX=Math.Max(maxX,s.x);minY=Math.Min(minY,s.y);maxY=Math.Max(maxY,s.y);
 left=Math.Min(left,angle);right=Math.Max(right,angle);
 if(previous*angle<0){if(t<0.5f)crossingsEarly++;else crossingsLate++;}previous=angle;
}
check(minX<0.97f&&maxX>1.18f,""visible widening and relaxing"");
check(minY<0.98f&&maxY>1.05f,""height breathes separately"");
check(left< -4&&right>4,""rocking goes both directions"");
check(crossingsLate>crossingsEarly,""rocking accelerates"");
RewardChestMotion.Sample(1,out var endPosition,out var endScale,out var endAngle);
check(Math.Abs(endAngle)<0.001f&&Math.Abs(endPosition.x)<0.001f&&endScale.x>endScale.y,""final compression is centered"");
var rt=new RectTransform{anchoredPosition=new Vector2(30,-320),localScale=new Vector3(2,.75f,1),localRotation=Quaternion.Euler(0,0,12)};
var routine=RewardChestMotion.Anticipate(rt,1.05f);for(int i=0;i<20;i++)check(routine.MoveNext(),""anticipation stays alive before burst"");
check(rt.localScale.x!=2,""animation modifies actual target"");
((IDisposable)routine).Dispose();
check(rt.anchoredPosition.x==30&&rt.anchoredPosition.y==-320&&rt.localScale.x==2&&rt.localScale.y==.75f&&rt.localRotation.angle==12,""interruption restores authored position, nonuniform scale and rotation"");
routine=RewardChestMotion.Anticipate(rt,0);int frames=0;while(routine.MoveNext()){if(++frames>100)throw new Exception(""invalid duration hangs"");}
check(rt.localScale.x==2&&frames<100,""zero duration is bounded and restores home"");
check(!RewardChestMotion.Anticipate(null).MoveNext(),""destroyed/missing target finishes without work"");
Console.WriteLine(""PASS: ""+checks+"" chest motion samples/cancellation checks (production source, no Unity build)."");
";
await CSharpScript.RunAsync(stubs+source+tests,ScriptOptions.Default);
foreach(var file in new[]{"Assets/_Project/Scripts/UI/RewardChestRevealOverlay.cs","Assets/_Project/Scripts/Workshop/RepairProgressBar.cs","Assets/_Project/Scripts/UI/RegionProgressBar.cs"}){
 var s=File.ReadAllText(file);if(!s.Contains("RewardChestMotion.Anticipate"))throw new Exception("Missing shared motion: "+file);
 if(!file.Contains("Overlay")&&(!s.Contains("StopAllCoroutines();")||!s.Contains("chestHome.Restore();")))throw new Exception("Missing cancellation: "+file);
}
Console.WriteLine("PASS: all three reward chest opening paths use shared anticipation and retain cleanup.");
