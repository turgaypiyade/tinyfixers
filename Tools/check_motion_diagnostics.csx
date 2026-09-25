// Isolated diagnostic checks; does not build or launch the Unity project.
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
var paths = new[] {
 "Assets/_Project/Scripts/Grid/Board/BoardMotionDiagnostics.cs",
 "Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs",
 "Assets/_Project/Scripts/Grid/Board/CascadeLogic.cs",
 "Assets/_Project/Scripts/Grid/Board/CascadeLogic.Motion.cs",
 "Assets/_Project/Scripts/Grid/Board/Actions/SpecialChainRunner.cs",
 "Assets/_Project/Scripts/Grid/Board/Actions/FallAction.cs"
};
foreach (var path in paths) {
 var errors = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0) throw new Exception(path+": "+string.Join("; ",errors.Select(e=>e.ToString())));
}
var production = CSharpSyntaxTree.ParseText(File.ReadAllText(paths[0])).GetRoot().DescendantNodes()
 .OfType<ClassDeclarationSyntax>().First().ToFullString()
 .Replace("[Conditional(\"UNITY_EDITOR\"), Conditional(\"DEVELOPMENT_BUILD\")]", "");
var harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public static float Distance(Vector2 a,Vector2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));
}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
public static class Time {public static float realtimeSinceStartup,unscaledDeltaTime=.016f,timeScale=1f;public static int frameCount;}
public static class Debug {public static List<string> messages=new List<string>();public static void Log(string s){messages.Add(s);}}
public enum TileRuntimeState {Idle,Falling,Clearing}
public class RectTransform {public Vector2 anchoredPosition;}
public class TileView {
 public int X,Y,LifetimeVersion; public TileRuntimeState RuntimeState; public RectTransform RectTransform=new RectTransform();
 public int GetInstanceID()=>GetHashCode();public string GetSpecial()=>""None"";
}
public class CascadeLogic {public bool fillable;public bool HasAnyResolvableEmptyPlayableCell()=>fillable;}
public class BoardController {
 public int Width=1,Height=1,TileSize=100,FallGeneration,ActiveBackgroundJobs,BlockingBackgroundJobs,PresentationFxInFlight,FlyingPatchBotDashes;
 public bool IsBusy,IsActionSequencePlaying,pin,reserved;
 public TileView[,] Tiles=new TileView[,]{{new TileView()}};
 public CascadeLogic CascadeLogic=new CascadeLogic();public IEnumerator observer;
 public int GetInstanceID()=>GetHashCode();
 public bool IsPendingTriggeredSpecialCell(int x,int y)=>pin;
 public bool IsReservedTileTargetCell(int x,int y)=>reserved;
 public void StartCoroutine(IEnumerator e){observer=e;e.MoveNext();}
 public void Tick(float time){Time.realtimeSinceStartup=time;Time.frameCount++;observer?.MoveNext();}
}
"+production+@"
public static class Checks {
 static int count;
 static void Check(bool value,string message){if(!value)throw new Exception(message);count++;}
 static string Last=>Debug.messages[Debug.messages.Count-1];
 static BoardController Start(){Time.realtimeSinceStartup=0;var b=new BoardController();BoardMotionDiagnostics.ChainBegin(b,""test"");return b;}
 static void Finish(BoardController b){BoardMotionDiagnostics.ChainEnd(b,""test"");b.Tick(0.01f);b.Tick(0.4f);}
 public static int Run(){
  var b=Start();Finish(b);Check(Last.Contains(""RESULT OK""),""Healthy board must report OK"");
  b=Start();b.Tiles[0,0].RectTransform.anchoredPosition=new Vector2(0,50);Finish(b);
  Check(Last.Contains(""RESULT ISSUE"")&&Last.Contains(""offGrid=1""),""Logical agreement cannot conceal airborne tile"");
  b=Start();b.pin=true;Finish(b);Check(Last.Contains(""RESULT ISSUE"")&&Last.Contains(""pins=1""),""Leaked anchor is reported"");
  b=Start();b.reserved=true;Finish(b);Check(Last.Contains(""RESULT ISSUE"")&&Last.Contains(""reservedTargets=1""),""Leaked target reservation is reported"");
  b=Start();b.CascadeLogic.fillable=true;Finish(b);Check(Last.Contains(""RESULT ISSUE""),""Fillable gap cannot report OK"");
  b=Start();b.Tiles[0,0].RuntimeState=TileRuntimeState.Falling;Finish(b);Check(Last.Contains(""nonIdle=1""),""Stale moving state is reported"");
  b=Start();BoardMotionDiagnostics.FallBegin(b,7,1,.3f);BoardMotionDiagnostics.CascadePlan(b);
  Check(Last.Contains(""duringActiveFall=True""),""Replan while fall active is reported"");
  BoardMotionDiagnostics.FallEnd(b,7,.3f,true);Finish(b);Check(Last.Contains(""replansDuringFall=1""),""Summary retains replan evidence"");
  b=Start();b.Tiles[0,0].RectTransform.anchoredPosition=new Vector2(0,50);b.Tick(.3f);b.Tick(.7f);
  Check(Last.Contains(""stationaryOffGrid=1""),""Motionless airborne tile is sampled while chain is active"");
  b.Tick(60.1f);Check(Last.Contains(""RESULT TIMEOUT""),""Stuck chain cannot report OK"");
  b=Start();BoardMotionDiagnostics.Event(b,""MOVE_OVERLAP"",""same tile"");Finish(b);
  Check(Last.Contains(""RESULT ISSUE"")&&Last.Contains(""settledClean=True"")&&Last.Contains(""moveOverlaps=1""),""A clean final board must retain the earlier motion overlap failure"");
  b=Start();BoardMotionDiagnostics.FallBegin(b,8,1,.3f);BoardMotionDiagnostics.FallEnd(b,8,.1f,false);Finish(b);
  Check(Last.Contains(""RESULT ISSUE"")&&Last.Contains(""interruptedFalls=1""),""Interrupted fall remains visible in final summary"");
  b=Start();BoardMotionDiagnostics.Event(b,""LINE_IMPACT_TIMEOUT"",""pendingCells=15"");Finish(b);
  Check(Last.Contains(""RESULT ISSUE"")&&Last.Contains(""lineTimeouts=1""),""Early line closure cannot report a clean result"");
  Time.unscaledDeltaTime=.1113f;b=Start();Finish(b);Time.unscaledDeltaTime=.016f;
  Check(Last.Contains(""RESULT PERF"")&&Last.Contains(""worstFrameMs=111.3""),""Frame stalls are flagged even when the board settles correctly"");
  return count;
 }
}
Checks.Run()";
var result=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" motion diagnostic checks; "+paths.Length+" touched C# files parsed without syntax errors");
