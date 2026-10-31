// Production central haptic policy; no device/build, native calls replaced by spies.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;
var path="Assets/_Project/Scripts/Core/Settings/Haptics.cs";
var cls=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
var methods=cls.Members.Where(m=>!(m is MethodDeclarationSyntax method&&new[]{"PlatformPlay","PlatformRumble","PlatformCancel"}.Contains(method.Identifier.ValueText)));
var production=string.Join("\n",methods.Select(m=>m.WithoutLeadingTrivia().ToFullString()));
var harness=@"
using System;
public class RuntimeInitializeOnLoadMethod:Attribute {public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType t){}}
public enum RuntimeInitializeLoadType {SubsystemRegistration}
public enum TileSpecial {None,LineV,LineH,PulseCore,SystemOverride,PatchBot}
public struct Vector2Int {}
public static class Time {public static float unscaledTime;}
public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);public static float Clamp01(float a)=>Math.Min(1,Math.Max(0,a));}
public static class GameSettings {static bool enabled=true;public static event Action<bool> OnVibrationChanged;public static bool VibrationEnabled {get=>enabled;set{enabled=value;OnVibrationChanged?.Invoke(value);}}}
public static class PulseBehaviorEvents {public static event Action<Vector2Int> PulseExplosionPlayed;public static void Fire()=>PulseExplosionPlayed?.Invoke(default(Vector2Int));}
public static class Haptics {
 /* PRODUCTION */
 static int calls,cancels,rumbles;static Strength last;
 static void PlatformPlay(Strength s){calls++;last=s;}static void PlatformCancel(){cancels++;}static void PlatformRumble(float a,float d){rumbles++;}
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 public static void Run(){
  HookVisualEvents();HookVisualEvents();Time.unscaledTime=0;
  PlayCombo(TileSpecial.SystemOverride,TileSpecial.PulseCore);Check(calls==1&&last==Strength.Heavy,""Override combo starts with one strong accent"");
  for(int i=1;i<20;i++){Time.unscaledTime=i*.1f;OnComboMoment(ComboMoment.Implant);OnComboMoment(ComboMoment.RocketLaunch);}
  Check(calls==1&&rumbles==0,""Override placements/launches do not add vibrations or continuous rumble"");
  Time.unscaledTime=3;OnLineCellBreak();OnObstacleHit();OnLineCellBreak();Check(calls==2,""same-frame cells and obstacle hits coalesce into one native call"");
  Time.unscaledTime=3.03f;OnLineCellBreak();Check(calls==2,""fast requests respect 60ms gate"");
  Time.unscaledTime=3.061f;OnLineCellBreak();Check(calls==3&&last==Strength.Light,""next real line break emits light impact"");
  Time.unscaledTime=4;PulseBehaviorEvents.Fire();Check(calls==4&&last==Strength.Heavy,""solo Pulse explosion works outside combo; duplicate hook prevented"");
  PulseBehaviorEvents.Fire();Check(calls==4,""duplicate same-frame Pulse visual event is coalesced"");
  Time.unscaledTime=5;OnObstacleHit();Check(calls==5,""generic successful obstacle hit works outside combo"");
  int stopped=cancels;GameSettings.VibrationEnabled=false;Check(cancels==stopped+1,""settings-off immediately cancels active native pattern"");
  Time.unscaledTime=6;OnLineCellBreak();OnObstacleHit();PulseBehaviorEvents.Fire();PlayCombo(TileSpecial.LineH,TileSpecial.LineV);PlayRumble(.5f,.4f);
  Check(calls==5&&rumbles==0,""all new effects obey disabled setting"");
  GameSettings.VibrationEnabled=true;Time.unscaledTime=7;OnLineCellBreak();Check(calls==6,""reenable resumes next eligible impact"");
  Time.unscaledTime=8;PlayCombo(TileSpecial.LineH,TileSpecial.PatchBot);Time.unscaledTime=8.1f;OnComboMoment(ComboMoment.PatchBotTakeoff);Check(calls==8,""existing PatchBot combo takeoff retained"");
  EndBoard();Time.unscaledTime=8.2f;OnComboMoment(ComboMoment.PatchBotTakeoff);Check(calls==8,""teardown clears combo session"");
  Time.unscaledTime=9;PlayCombo(TileSpecial.LineV,TileSpecial.LineH);Check(rumbles==0,""line combo relies on discrete arrived cell breaks"");
  long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++){Time.unscaledTime=10+i*.07f;OnLineCellBreak();OnObstacleHit();}
  long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
  Check(bytes==0,""warm policy/dispatch has zero managed allocations with platform doubles"");
  Console.WriteLine(""PASS: ""+checks+"" central haptic policy checks; 10000 warm frame pairs allocate ""+bytes+"" B (platform doubled)"");
 }
}
".Replace("/* PRODUCTION */",production);
CSharpScript.RunAsync(harness+"\nHaptics.Run();").GetAwaiter().GetResult();
// Parse all integration sources. No assembly emitted or project compilation.
foreach(var file in new[]{"Grid/Board/BoardController.cs","Grid/Board/BoardAnimator.cs","Grid/Board/Combos/LineVHPulseCoreCombo.cs","Grid/Board/Actions/SpecialChainRunner.cs","Grid/Board/BoosterCellImpactPass.cs","Grid/Board/Actions/GelLauncherFireAction.cs","Grid/GridSpawner.cs","Grid/RhodesStatueView.cs"}) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/"+file)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(file+": "+string.Join("\n",errors.Select(e=>e.ToString())));
}
Console.WriteLine("PASS: eight integration sources parse; device haptic feel/performance not measured.");
// Exercise the actual Android cache branch; all JNI objects/calls are doubled.
var androidClass=CSharpSyntaxTree.ParseText(File.ReadAllText(path),new CSharpParseOptions(preprocessorSymbols:new[]{"UNITY_ANDROID"})).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
var android="public static class Haptics {\n"+string.Join("\n",androidClass.Members.Select(m=>m.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()))+"\n}";
var androidStubs=@"
using System;
public class RuntimeInitializeOnLoadMethod:Attribute {public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType t){}}
public enum RuntimeInitializeLoadType {SubsystemRegistration}
public enum TileSpecial {None,LineV,LineH,PulseCore,SystemOverride,PatchBot}
public struct Vector2Int {}
public static class Time {public static float unscaledTime;}
public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);public static float Clamp01(float a)=>Math.Min(1,Math.Max(0,a));public static int Clamp(int a,int b,int c)=>Math.Min(c,Math.Max(a,b));public static int RoundToInt(float f)=>(int)Math.Round(f);}
public static class GameSettings {public static event Action<bool> OnVibrationChanged;static bool enabled=true;public static bool VibrationEnabled {get=>enabled;set{enabled=value;OnVibrationChanged?.Invoke(value);}}}
public static class PulseBehaviorEvents {public static event Action<Vector2Int> PulseExplosionPlayed;}
public static class Handheld {public static void Vibrate(){throw new Exception(""unexpected old hardware fallback"");}}
public class AndroidJavaObject:IDisposable {
 public static int factories,plays,cancels;public static object[] lastArgs;
 public void Dispose(){}
 public T GetStatic<T>(string name){if(typeof(T)==typeof(int))return (T)(object)29;return (T)(object)new AndroidJavaObject();}
 public T Call<T>(string name,params object[] args)=> (T)(object)new AndroidJavaObject();
 public T CallStatic<T>(string name,params object[] args){factories++;return (T)(object)new AndroidJavaObject();}
 public void Call(string name,params object[] args){if(name==""cancel""){cancels++;return;}if(name!=""vibrate"")throw new Exception(name);plays++;lastArgs=args;}
}
public class AndroidJavaClass:AndroidJavaObject {public AndroidJavaClass(string name){}}
";
var androidTests=@"
foreach(var strength in new[]{Haptics.Strength.Light,Haptics.Strength.Medium,Haptics.Strength.Heavy}){Time.unscaledTime+=1;Haptics.Play(strength);}
if(AndroidJavaObject.factories!=3)throw new Exception(""three Android impact effects must be warmed once"");
var args=AndroidJavaObject.lastArgs;
long before=GC.GetAllocatedBytesForCurrentThread();
for(int i=0;i<10000;i++){Time.unscaledTime+=.1f;Haptics.OnLineCellBreak();}
long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
if(AndroidJavaObject.factories!=3||!object.ReferenceEquals(args,AndroidJavaObject.lastArgs)||bytes!=0)throw new Exception(""Android cache allocates/rebuilds effects or JNI argument arrays"");
GameSettings.VibrationEnabled=false;int plays=AndroidJavaObject.plays;Haptics.OnLineCellBreak();
if(AndroidJavaObject.cancels!=1||AndroidJavaObject.plays!=plays)throw new Exception(""Android settings-off must cancel and gate playback"");
Console.WriteLine(""PASS: Android effect/JNI argument reuse; 10000 warm calls allocate ""+bytes+"" B with JNI doubles; immediate settings cancellation"");
";
CSharpScript.RunAsync(androidStubs+android+androidTests).GetAwaiter().GetResult();
