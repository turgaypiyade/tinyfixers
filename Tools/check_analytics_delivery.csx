// csi Tools/check_analytics_delivery.csx — actual analytics code with SDK/storage doubles; no network or Unity build.
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
var source = File.ReadAllText("Assets/_Project/Scripts/Core/Backend/GameAnalytics.cs");
var parsed = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(source,
    new CSharpParseOptions(preprocessorSymbols: new[]{"TF_FIREBASE_ANALYTICS"})).GetRoot();
var production = string.Join("\n", parsed.Members.Select(m => m.ToFullString()));
// Strip disabled preprocessor branches after parsing, preserving the active production code.
production = string.Concat(parsed.Members.SelectMany(m => m.DescendantTokens()).Select(t => t.Text + " "));
var stubs = @"
using System;
using System.Collections.Generic;
using System.Reflection;
public static class Application { public static bool isEditor; }
public static class Debug { public static bool isDebugBuild; }
public static class RuntimeSimulationSession { public static bool IsActive; }
public static class MasterLeague { public static bool IsPlaying; }
public static class CurrentLevel { public static int Global=1, Played=1; }
public static class Time { public static float realtimeSinceStartup; }
public static class Mathf { public static int Max(int a,int b)=>Math.Max(a,b); public static int RoundToInt(float x)=>(int)Math.Round(x); }
public static class SystemInfo { public static int systemMemorySize=4096,processorCount=8; public static string graphicsDeviceName=""test""; }
public static class Screen { public struct Resolution {public int width,height;} public static Resolution currentResolution; }
public enum RuntimeInitializeLoadType {AfterSceneLoad}
public class RuntimeInitializeOnLoadMethodAttribute:Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
public static class PlayerPrefs {
 public static Dictionary<string,string> data=new Dictionary<string,string>();
 public static int GetInt(string k,int d=0)=>data.ContainsKey(k)?int.Parse(data[k]):d;
 public static string GetString(string k,string d="""")=>data.ContainsKey(k)?data[k]:d;
 public static void SetInt(string k,int v)=>data[k]=v.ToString();
 public static void SetString(string k,string v)=>data[k]=v;
 public static bool HasKey(string k)=>data.ContainsKey(k);
 public static void DeleteKey(string k)=>data.Remove(k);
 public static void Save(){}
}
public struct Result { public int Level,Attempt,Seconds,MovesLeft,AssistTier,FailsBefore,MovesTotal,StrugglesBefore,Continues,ExtraMoves,ContinueCoinsSpent,AdContinues; public bool Won,Struggled; }
public static class LevelAttemptStats { public static event Action<int,int> OnAttemptStarted; public static event Action<Result> OnAttemptEnded; public static event Action<int,int,int,int,int> OnAttemptPaused; }
public static class FirebaseAuthService {
 public static bool IsReady;public static string UserId=""test""; static Action ready;
 public static event Action OnReady {add {ready+=value;if(IsReady)value();} remove{ready-=value;} }
 public static void Reset(){ready=null;IsReady=false;}
 public static void Ready(){IsReady=true;ready?.Invoke();}
}
public class Parameter { public string name;public object value;public Parameter(string n,string v){name=n;value=v;}public Parameter(string n,long v){name=n;value=v;} }
public static class FirebaseAnalytics {
 public static List<string> events=new List<string>(); public static List<Parameter[]> payloads=new List<Parameter[]>();public static bool fail,collection;
 public static void SetAnalyticsCollectionEnabled(bool v){collection=v;}
 public static void SetUserId(string x){} public static void SetUserProperty(string x,string y){}
 public static void LogEvent(string n,Parameter[] p){if(fail)throw new Exception(""SDK failure"");events.Add(n);payloads.Add(p);}
}
";
var tests = @"
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine(""PASS: ""+label);}
void Invoke(string method,params object[] args)=>typeof(GameAnalytics).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
void Restart(){FirebaseAuthService.Reset();typeof(GameAnalytics).GetField(""onboardingHooked"",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);}
Debug.isDebugBuild=true;GameAnalytics.ConfigureCollection();GameAnalytics.OnboardingStep(1);Invoke(""LogLevel3Won"",3);Invoke(""LogFirstGameStart"",1,1);GameAnalytics.LogEvent(""ad_offer"");
Check(!FirebaseAnalytics.collection && PlayerPrefs.data.Count==0 && FirebaseAnalytics.events.Count==0,""Development build neither sends nor consumes flags"");
Debug.isDebugBuild=false;Application.isEditor=true;GameAnalytics.OnboardingStep(1);GameAnalytics.LogEvent(""ad_offer"");
Check(PlayerPrefs.data.Count==0 && FirebaseAnalytics.events.Count==0,""Editor suppressed"");Application.isEditor=false;
RuntimeSimulationSession.IsActive=true;GameAnalytics.OnboardingStep(1);GameAnalytics.LogEvent(""ad_offer"");Check(PlayerPrefs.data.Count==0,""Simulation suppressed"");RuntimeSimulationSession.IsActive=false;
GameAnalytics.ConfigureCollection();Check(FirebaseAnalytics.collection,""Release re-enables persisted collection setting"");
Time.realtimeSinceStartup=4;GameAnalytics.OnboardingStep(1);Time.realtimeSinceStartup=9;GameAnalytics.OnboardingStep(2);
Check(FirebaseAnalytics.events.Count==0 && PlayerPrefs.HasKey(""onboarding_pending_step_v1_1""),""Not ready: steps persisted"");
Restart();Time.realtimeSinceStartup=1;Invoke(""Bootstrap"");FirebaseAnalytics.fail=true;FirebaseAuthService.Ready();
Check(PlayerPrefs.HasKey(""onboarding_pending_step_v1_1"") && FirebaseAnalytics.events.Count==0,""Restart plus SDK failure retains pending steps"");
FirebaseAnalytics.fail=false;GameAnalytics.OnboardingStep(2);
Check(FirebaseAnalytics.events.Count==2 && (long)FirebaseAnalytics.payloads[0][1].value==4 && (long)FirebaseAnalytics.payloads[1][1].value==9,""Retry preserves original step order and elapsed times"");
Check(!PlayerPrefs.HasKey(""onboarding_pending_step_v1_1"") && !PlayerPrefs.HasKey(""onboarding_pending_step_v1_2""),""Successful SDK handoff removes pending steps"");
Restart();Invoke(""EnsureOnboardingHook"");FirebaseAuthService.Ready();GameAnalytics.OnboardingStep(1);GameAnalytics.OnboardingStep(2);Check(FirebaseAnalytics.events.Count==2,""Acknowledged steps do not replay after restart"");
PlayerPrefs.SetInt(""onboarding_step_sent"",7);GameAnalytics.OnboardingStep(7);Check(FirebaseAnalytics.events.Count==2,""Legacy watermark does not fabricate historical events"");
Invoke(""LogLevel3Won"",2);MasterLeague.IsPlaying=true;Invoke(""LogLevel3Won"",3);MasterLeague.IsPlaying=false;Invoke(""LogLevel3Won"",3);Invoke(""LogLevel3Won"",3);
Check(FirebaseAnalytics.events.FindAll(x=>x==""seviye3_kazanildi"").Count==1,""Level 3 milestone once; other levels and league excluded"");
Invoke(""LogFirstGameStart"",1,1);Invoke(""LogFirstGameStart"",1,1);
Check(FirebaseAnalytics.events.FindAll(x=>x==""oyuna_basladi"").Count==1,""First start once"");
PlayerPrefs.DeleteKey(""ga_oyuna_basladi_logged"");CurrentLevel.Global=5;Invoke(""LogFirstGameStart"",5,1);
Check(FirebaseAnalytics.events.FindAll(x=>x==""oyuna_basladi"").Count==1,""Existing player not counted as first start"");
";
var script=CSharpScript.Create(stubs+production+tests,ScriptOptions.Default);
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
script.RunAsync().GetAwaiter().GetResult();
