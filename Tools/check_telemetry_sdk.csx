// Validate declarations against the repository's actual Firebase DLLs, without Unity/project build.
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
var files = new[]{"LevelTelemetryStore.cs", "LevelTelemetryProjection.cs", "PlayerStatsCloudReporter.cs"};
var body=string.Join("\n",files.Select(f=>{
 var root=(CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Core/Backend/"+f)).GetRoot();
 return string.Join("\n",root.Members.Select(m=>m.ToFullString())).Replace("UnityEngine.Object", "ObjectStub");
}));
var stubs=@"
using System;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
public static class Application { public static string version=""test"", platform=""Editor""; }
public static class RuntimeSimulationSession { public static bool IsActive; }
public static class CurrentLevel {public static int Global=1;}
public static class Debug {public static bool isDebugBuild;public static void LogWarning(object o){} }
public static class PlayerPrefs {
 public static string GetString(string k,string d)=>d;
 public static void SetString(string k,string v){}
 public static void Save(){}
}
public static class JsonUtility {public static T FromJson<T>(string s)=>default(T);public static string ToJson(object o)=>"""";}
public static class PlayerWallet {public static int Coins;}
public static class PlayerStats {public static DateTime FirstLaunchDate=DateTime.UtcNow;}
public static class FirebaseAuthService {public static bool IsReady;public static string UserId;public static event Action OnReady;}
public class MonoBehaviour {}
public class GameObject {public GameObject(string s){}public T AddComponent<T>()=>default(T);}
public static class ObjectStub {public static void DontDestroyOnLoad(object o){} }
public static class Time {public static float unscaledDeltaTime, realtimeSinceStartup;}
public static class Mathf {public static int FloorToInt(float f)=>(int)f;public static float Min(float a,float b)=>Math.Min(a,b);}
public enum RuntimeInitializeLoadType {BeforeSceneLoad}
public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
";
var references=new[]{"Firebase.App.dll","Firebase.Firestore.dll","Firebase.TaskExtension.dll","Firebase.Platform.dll"}
 .Select(f=>MetadataReference.CreateFromFile(Path.GetFullPath("Assets/Firebase/Plugins/"+f)));
var script=CSharpScript.Create(stubs+body,ScriptOptions.Default.AddReferences(references));
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
Console.WriteLine("PASS: telemetry API/type checks against installed Firebase DLLs (no project build, no network writes).");
