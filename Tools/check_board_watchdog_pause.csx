// csi Tools/check_board_watchdog_pause.csx — production lifecycle clock/recovery branches, no Unity build.
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
var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BoardController.cs")).GetRoot();
var resolve=root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="ResolveBoard");
string Methods(params string[] names)=>string.Join("\n",root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var clock=root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single(p=>p.Identifier.ValueText=="ActiveWatchdogDeltaTime").ToFullString();
var clockFields=string.Join("\n",root.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f=>f.Declaration.Variables.Any(v=>v.Identifier.ValueText.StartsWith("watchdog"))).Select(f=>f.ToFullString()));
var stall=resolve.DescendantNodes().OfType<IfStatementSyntax>().Single(n=>n.Condition.ToString()=="IsFlowPumpActive").ToFullString();
var jobs=resolve.DescendantNodes().OfType<IfStatementSyntax>().Single(n=>n.Condition.ToString()=="BlockingBackgroundJobs > 0 || actionSequencer.IsPlaying").ToFullString();
var loop=resolve.DescendantNodes().OfType<WhileStatementSyntax>().First().Statement as BlockSyntax;
var clockStep=string.Join("\n",loop.Statements.TakeWhile(n=>!(n is IfStatementSyntax i&&i.Condition.ToString().Contains("ignoreDynamicSwapGuard"))).Select(n=>n.ToFullString()));
var source=@"
using System;
using System.Collections;
using System.Collections.Generic;
public static class Time {public static int frameCount=10;public static float unscaledDeltaTime=.1f,timeScale=1,maximumDeltaTime=1f/3;}
public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);}
public static class GameDiagnostics {public static bool CriticalErrors=>false;}
public static class Debug {public static int warnings,errors;public static void LogWarning(string s){warnings++;}public static void LogError(string s){errors++;}}
public class Motion {public int resets;public void Reset(){resets++;}}
public class Cascade {public bool IsGravityBusy=true;}
public class Pump {public bool IsQuiet;}
public class Sequence {public bool IsPlaying;}
public class Board {
 /* FIELDS */
 public int lastResolveLoopFrame,lastFlowPumpWorkFrame=-1000,FlowJobsInFlight=1,BlockingBackgroundJobs=1,ActiveBackgroundJobs=1,drains,repairs;
 public bool IsFlowPumpActive=true,IsActionSequencePlaying;
 public Dictionary<int,int> cellHolds=new Dictionary<int,int>();
 public Motion fallMotion=new Motion();public Cascade cascadeLogic=new Cascade();public Pump FlowPump=new Pump();public Sequence actionSequencer=new Sequence();
 public void ForceDrainAllJobs(){drains++;}public void ClearAllPendingTriggeredSpecialCells(){}
 public void RepairTileGridIntegrity(string s,bool snapPositions){repairs++;}
 /* CLOCK */
 public float Delta=>ActiveWatchdogDeltaTime;
 public void Pause(bool p)=>OnApplicationPause(p);public void Focus(bool f)=>OnApplicationFocus(f);
 public IEnumerator Wait(bool flow){float flowStallTime=0,backgroundJobWaitTime=0;bool backgroundJobWarningLogged=false,previousIterationWasJobWait=false;
  while(true){/* STEP */ if(flow){/* STALL */}else{/* JOBS */}yield return null;}
 }
}
public static class Checks {
 static int count;static void Check(bool ok,string s){count++;if(!ok)throw new Exception(s);}
 static void Step(IEnumerator wait,int frames=1,float dt=.1f){Time.unscaledDeltaTime=dt;for(int i=0;i<frames;i++){Time.frameCount++;if(!wait.MoveNext())throw new Exception(""stopped waiting"");}}
 static Board Fresh(){Time.timeScale=1;Time.unscaledDeltaTime=.1f;return new Board();}
 public static void Run(){
  var b=Fresh();Check(b.Delta==.1f,""foreground clock advances"");
  b.Pause(true);Time.frameCount+=100;Check(b.Delta==0,""paused background clock stops"");
  b.Focus(false);b.Pause(false);Time.frameCount+=5;Check(b.Delta==0,""pause resume cannot override lost focus"");
  b.Pause(true);b.Focus(true);Time.frameCount+=5;Check(b.Delta==0,""focus resume cannot override OS pause"");
  b.Pause(false);Check(b.Delta==0,""transition frame ignored"");Time.frameCount++;Check(b.Delta==0,""first following frame ignored"");Time.frameCount++;Check(b.Delta==.1f,""normal frame counting resumes"");
  Time.timeScale=0;Check(b.Delta==0,""in-game timescale pause stops watchdog"");Time.timeScale=1;
  Time.unscaledDeltaTime=120;Check(b.Delta<=Time.maximumDeltaTime,""uncaptured Editor/OS gap cannot add suspended minutes"");
  foreach(bool flow in new[]{true,false}){
   b=Fresh();var wait=b.Wait(flow);Step(wait,60);int baseline=b.drains;int errors=Debug.errors;
   b.Pause(true);b.Focus(false);Step(wait,120);
   b.Pause(false);Time.unscaledDeltaTime=120;Step(wait,1,120);
   Check(b.drains==baseline&&Debug.errors==errors,""background plus partial resume never drains ""+flow);
   b.Focus(true);Step(wait,1,120);Step(wait,1,120);Step(wait,140);
   Check(b.drains==baseline,""resume discards previous stall budget and delta spike ""+flow);
   Step(wait,15);Check(b.drains==baseline+1,""genuine continuous foreground stall still recovers ""+flow);
   if(flow)Check(b.fallMotion.resets==1&&b.repairs==1,""real flow recovery keeps motion repair"");
   b=Fresh();wait=b.Wait(flow);Step(wait,100);Time.timeScale=0;Step(wait,200);
   Check(b.drains==0,""timescale pause never drains ""+flow);Time.timeScale=1;Step(wait,140);
   Check(b.drains==0,""timescale pause resets stall budget ""+flow);Step(wait,15);Check(b.drains==1,""recovery re-arms after game pause ""+flow);
   b=Fresh();wait=b.Wait(flow);Step(wait,80);b.Pause(true);b.Focus(false);
   // Mobile suspension may run no frames at all; only the callback and resume gap are observed.
   b.Pause(false);b.Focus(true);Step(wait,1,180);Step(wait,1);Step(wait,140);
   Check(b.drains==0,""no-background-frame suspension is safe ""+flow);
  }
  Console.WriteLine(""PASS: ""+count+"" board watchdog focus, pause, resume-gap and genuine-stall checks."");
 }
}
Checks.Run();";
source=source.Replace("/* FIELDS */",clockFields).Replace("/* CLOCK */",clock+Methods("OnApplicationPause","OnApplicationFocus")).Replace("/* STEP */",clockStep).Replace("/* STALL */",stall).Replace("/* JOBS */",jobs);
var script=CSharpScript.Create(source,ScriptOptions.Default.WithReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Take(15).Select(d=>d.ToString())));
script.RunAsync().GetAwaiter().GetResult();
