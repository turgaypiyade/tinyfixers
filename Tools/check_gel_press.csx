// Isolated presentation state tests, not a Unity build or board/cascade simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var paths=new[]{"Assets/_Project/Scripts/Grid/Board/GelLauncherPressMotion.cs","Assets/_Project/Scripts/Grid/Board/GelLauncherPressView.cs","Assets/_Project/Scripts/Grid/Board/GelLauncherPressRodGraphic.cs","Assets/_Project/Scripts/Grid/Board/GelLauncherFx.cs","Assets/_Project/Scripts/Grid/Board/Actions/GelLauncherFireAction.cs","Assets/_Project/Scripts/Grid/GridSpawner.cs"};
foreach(var p in paths){var es=CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(es.Length>0)throw new Exception(p+": "+string.Join("\n",es.Select(e=>e.ToString())));}
var motion=CSharpSyntaxTree.ParseText(File.ReadAllText(paths[0])).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single().ToFullString();
var script=@"
using System;
public static class Mathf {
 public static float Clamp01(float f)=>Math.Max(0,Math.Min(1,f));public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
 public static float LerpUnclamped(float a,float b,float t)=>a+(b-a)*t;public static float Repeat(float t,float n)=>t-(float)Math.Floor(t/n)*n;
 public static bool Approximately(float a,float b)=>Math.Abs(a-b)<1e-6;
}
"+motion+@"
int checks=0;Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
foreach(int fps in new[]{15,30,60,120}){
 var m=new GelLauncherPressMotion();m.Reset(false);check(m.Progress==0&&!m.Moving,""new launcher unpressed"");
 m.MoveTo(.5f,.32f,1.25f);float prev=0;int n=0;
 while(m.Moving){m.Step(1f/fps);check(m.Progress>=prev&&m.Progress<=.5f,""first hit monotonic, cannot pass halfway"");check(m.Phase>=0&&m.Phase<1,""phase bounded"");prev=m.Progress;check(++n<fps,""first hit bounded duration"");}
 check(m.Progress==.5f,""first hit exact halfway endpoint"");float phase=m.Phase;m.Step(10);check(m.Progress==.5f&&m.Phase==phase,""idle does not spin or drift"");
 m.MoveTo(1,.30f,1.75f);prev=m.Progress;n=0;
 while(m.Moving){m.Step(1f/fps);check(m.Progress>=prev&&m.Progress<=1,""second hit monotonic, no overshoot"");prev=m.Progress;check(++n<fps,""second hit bounded duration"");}
 check(m.Progress==1,""second hit fully pressed before firing"");
 m.Reset(true);check(m.Progress==.5f&&!m.Moving&&m.Phase==0,""damaged spawn/reuse has stable half pose"");
 m.MoveTo(1,.3f,2);m.Step(.06f);float stopped=m.Progress;m.Cancel();m.Step(10);check(m.Progress==stopped&&!m.Moving,""cancel prevents stale movement"");
 m.Reset(false);m.MoveTo(.5f,.32f,1);m.Step(.02f);float partial=m.Progress;m.MoveTo(.5f,.13f,.65f);m.Step(1);check(m.Progress==.5f,""rapid second hit completes first half from live pose"");m.MoveTo(1,.3f,1.75f);m.Step(1);check(m.Progress==1&&!m.Moving,""rapid hit continues through final stroke"");
 m.Reset(false);m.MoveTo(.5f,.32f,1);m.Step(0);check(m.Progress==0,""paused clock does not advance"");m.Step(-1);check(m.Progress==0,""negative delta ignored"");
}
Console.WriteLine(""PASS: ""+checks+"" production press-motion assertions; six C# files parsed. No Unity build/Play."");
";
await CSharpScript.RunAsync(script);
