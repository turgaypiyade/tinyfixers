// Run with Mono csi. Exercises the real Attack coroutine with presentation doubles.
// No Unity/project build; rendering still needs Play-mode review.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

var attack = string.Join("\n", CSharpSyntaxTree.ParseText(File.ReadAllText(
    "Assets/_Project/Scripts/Grid/Board/BossDuelCharacterView.cs")).GetRoot().DescendantNodes()
    .OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == "Attack" || m.Identifier.ValueText == "FinisherArcOffset").Select(m => m.ToFullString()));
var harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
public static class Time { public static float deltaTime=.01f; }
public class WaitForSeconds { public float seconds; public WaitForSeconds(float value){seconds=value;} }
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero=>new Vector2();
 public static Vector2 operator*(Vector2 v,float f)=>new Vector2(v.x*f,v.y*f);
 public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>new Vector2(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);
}
public struct Vector3 { public float x; public Vector3(float x){this.x=x;} }
public static class Mathf {
 public static float Max(float a,float b)=>Math.Max(a,b); public static float Clamp(float v,float lo,float hi)=>Math.Max(lo,Math.Min(hi,v));
 public static float Clamp01(float v)=>Clamp(v,0,1);public static float Sign(float v)=>v<0?-1:1;public static float Abs(float v)=>Math.Abs(v);
}
public class Transform { public Vector3 InverseTransformPoint(Vector3 p)=>p; }
public class RectTransform { public Transform parent=new Transform();public Vector3 position=new Vector3(100),localPosition; }
public class Image { public RectTransform rectTransform=new RectTransform(); }
public class Profile {
 public class Pose { public object sprite=new object(); }
 public Pose windup=new Pose(),swing=new Pose(),strike=new Pose(),focused=new Pose(),victory=new Pose();
 public bool enableFinishingStrike=true;
 public float windupDuration=.24f,swingDuration=.18f,impactDuration=.09f,recoveryDuration=.2f,contactDistance=.6f;
 public float finisherWindupMultiplier=1.4f,finisherApproachDuration=.6f,finisherJumpHeight=.18f,finisherBurstDuration=.035f,finisherImpactHold=.065f;
}
public class Vfx {
 public int impacts,cancels,swingTicks;public bool finishing;public int power;
 public void BeginTrail(){}public void TickTrail(float dt){}public void BeginSwing(int power,Vector3 contact,RectTransform target){}
 public void TickSwing(float p){swingTicks++;}public void ReleaseSwing(){}public void CancelSwing(){cancels++;}
 public void PlayImpact(int value,bool finisher){impacts++;power=value;finishing=finisher;}
}
public class View {
 public Image body=new Image(); public Profile profile=new Profile();public Vfx attackVfx=new Vfx();
 public bool attacking,finished,victoryPending;public int poseVersion,restPoses;public float standingHeight=100;
 public List<Vector2> positions=new List<Vector2>();public Profile.Pose lastPose;public Vector2 lastMotion;
 public void Show(Profile.Pose pose,Vector2 motion=default(Vector2)){lastPose=pose;lastMotion=motion;positions.Add(motion);}
 public Vector2 Arc(Vector2 reach,float t,float height)=>FinisherArcOffset(reach,t,height);
 public void ShowRestPose(){restPoses++;}public Vector3 GetStrikeContact(Vector2 v)=>new Vector3(v.x);
 " + attack + @"
}
public static class Checks {
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 static List<float> Run(IEnumerator e,Action<int> afterYield=null){
  var waits=new List<float>();int n=0;
  try {while(e.MoveNext()){
   if(++n>500)throw new Exception(""Attack did not terminate"");
   if(e.Current is WaitForSeconds)waits.Add(((WaitForSeconds)e.Current).seconds);
   afterYield?.Invoke(n);
  }}finally{(e as IDisposable)?.Dispose();}return waits;
 }
 static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
 public static int RunAll(){
  var normal=new View();int hits=0,swings=0;
  var waits=Run(normal.Attack(new RectTransform(),()=>hits++,()=>false,8,onSwing:()=>{swings++;Check(hits==0,""Attack audio precedes contact"");}));
  Check(swings==1,""Ordinary swing audio fires once"");
  Check(hits==1&&!normal.attacking&&!normal.attackVfx.finishing,""Ordinary attack damage/cleanup unchanged"");
  Check(waits.Count==2&&Near(waits[0],.24f)&&Near(waits[1],.09f),""Ordinary attack timings unchanged"");
  var final=new View();hits=0;swings=0;
  var finalWaits=Run(final.Attack(new RectTransform(),()=>{Check(Near(final.lastMotion.y,0),""Finisher lands before damage"");hits++;final.victoryPending=true;},()=>false,8,()=>true,
   ()=>{swings++;Check(final.lastMotion.y>0&&final.attackVfx.swingTicks==0&&hits==0,""Finisher attack audio starts after slow hop, before fast contact"");}));
  Check(swings==1,""Finisher swing audio fires once"");
  Check(hits==1&&final.attackVfx.impacts==1&&final.attackVfx.power==8,""Finisher applies original power exactly once"");
  Check(Near(finalWaits[0],.336f)&&Near(finalWaits[1],.065f),""Slow anticipation and short impact hold"");
  Check(final.attackVfx.swingTicks<normal.attackVfx.swingTicks,""Contact burst is faster than ordinary swing"");
  Check(final.positions.Count>normal.positions.Count,""Finisher includes separate slow approach"");
  Check(final.attackVfx.finishing&&final.finished&&!final.attacking&&!final.victoryPending,""Victory deferred until return completes"");
  Check(final.lastPose==final.profile.victory,""Victory pose appears after returning home"");
  Check(normal.positions.TrueForAll(p=>Near(p.y,0)),""Ordinary attacks remain grounded"");
  Check(final.positions.Exists(p=>p.y>17.9f),""Player visibly rises to configured hop height"");
  foreach(float dir in new[]{-1f,1f}){
   var start=final.Arc(new Vector2(40*dir,0),0,18);
   var crest=final.Arc(new Vector2(40*dir,0),.5f,18);
   var end=final.Arc(new Vector2(40*dir,0),1,18);
   Check(Near(start.x,0)&&Near(start.y,0)&&Near(end.x,40*dir)&&Near(end.y,0),""Arc begins at home and lands at contact in both directions"");
   Check(Near(crest.x,20*dir)&&Near(crest.y,18),""Arc crests halfway"");
  }
  var disabled=new View();disabled.profile.enableFinishingStrike=false;
  waits=Run(disabled.Attack(new RectTransform(),()=>{},()=>false,8,()=>true));
  Check(Near(waits[0],.24f)&&!disabled.attackVfx.finishing,""Profile can disable finisher"");
  var shielded=new View();bool lethal=true;hits=0;
  waits=Run(shielded.Attack(new RectTransform(),()=>hits++,()=>false,8,()=>lethal),n=>{if(n==1)lethal=false;});
  Check(hits==1&&!shielded.attackVfx.finishing&&Near(waits[1],.09f),""Late shield pickup cancels finishing emphasis, not damage"");
  var cancel=new View();bool cancelled=false;hits=0;swings=0;
  Run(cancel.Attack(new RectTransform(),()=>hits++,()=>cancelled,8,()=>true,()=>swings++),n=>{if(n==5)cancelled=true;});
  Check(swings==0,""Cancelled slow approach does not play swing audio"");
  Check(hits==0&&!cancel.attacking&&cancel.attackVfx.cancels==1,""Cancellation during slow approach releases attack state"");
  var interrupted=new View();var routine=interrupted.Attack(new RectTransform(),()=>{},()=>false,8,()=>true);
  routine.MoveNext();((IDisposable)routine).Dispose();
  Check(!interrupted.attacking&&interrupted.attackVfx.cancels==1,""Disposed windup cannot leak attack state"");
  var lethalCancel=new View();bool won=false;hits=0;
  Run(lethalCancel.Attack(new RectTransform(),()=>{hits++;won=true;},()=>won,8,()=>true));
  Check(hits==1&&!lethalCancel.attacking&&lethalCancel.restPoses==1,""Lethal hit still completes return after cancellation predicate changes"");
  return count;
 }
}
Checks.RunAll()";
var result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.List<>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " isolated finishing-strike timing, impact, late-shield, cancellation and cleanup checks");
