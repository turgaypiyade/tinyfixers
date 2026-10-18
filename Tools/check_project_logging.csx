// Console logging audit + real settings/cache checks. No Unity build or assembly output.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;
var symbols=new[]{"ENABLE_INPUT_SYSTEM","ENABLE_LEGACY_INPUT_MANAGER","OBSTACLE_FX_DEBUG","TF_ADMOB","TF_APPLE_SIGNIN_CAPABILITY","TF_FIREBASE_ANALYTICS","TF_IAP","TF_PLAY_REVIEW"};
var variants=new[]{new[]{"UNITY_EDITOR","DEVELOPMENT_BUILD","UNITY_IOS"},new[]{"UNITY_EDITOR","UNITY_ANDROID"},new[]{"UNITY_IOS","DEVELOPMENT_BUILD"},new[]{"UNITY_ANDROID","DEVELOPMENT_BUILD"},new[]{"UNITY_IOS"},new[]{"UNITY_ANDROID"},new string[0]};
var flags=new[]{"CriticalErrors","BoardFlow","BoardIntegrity","SpecialChains","Performance","Backend","AnalyticsEvents","EditorTools"};
var locations=new HashSet<string>();var fileCount=0;var categories=new Dictionary<string,int>();var argumentMethods=new HashSet<string>();
foreach(var path in Directory.GetFiles("Assets/_Project","*.cs",SearchOption.AllDirectories)){
 fileCount++;string source=File.ReadAllText(path);
 foreach(var variant in variants.SelectMany(platform=>new[]{platform.Concat(symbols).ToArray(),platform})){
  var root=CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.Latest,preprocessorSymbols:variant),path).GetRoot();
  var errors=root.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
  foreach(var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>()){
   var member=call.Expression as MemberAccessExpressionSyntax;if(member==null||!new[]{"Debug","UnityEngine.Debug","global::UnityEngine.Debug"}.Contains(member.Expression.ToString())||!new[]{"Log","LogWarning","LogError","LogException","LogFormat","LogWarningFormat","LogErrorFormat"}.Contains(member.Name.Identifier.ValueText))continue;
   string key=path+":"+call.SpanStart;if(!locations.Add(key))continue;
   var guard=call.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault(n=>flags.Any(f=>n.Condition.ToString()=="GameDiagnostics."+f));
   if(guard==null)throw new Exception("Unguarded project console call: "+path+":"+(call.GetLocation().GetLineSpan().StartLinePosition.Line+1));
   string category=guard.Condition.ToString();if(!categories.ContainsKey(category))categories[category]=0;categories[category]++;
   foreach(var nested in call.ArgumentList.DescendantNodes().OfType<InvocationExpressionSyntax>())argumentMethods.Add(nested.Expression.ToString());
  }
 }
}
string settingsSource=File.ReadAllText("Assets/_Project/Scripts/Core/GameDiagnosticsSettings.cs").Replace("using UnityEngine;","");
string harness=@"
using System;using System.Reflection;
public class ScriptableObject{}
public class CreateAssetMenuAttribute:Attribute {public string fileName,menuName;}
public class TooltipAttribute:Attribute {public TooltipAttribute(string value){}}
public enum RuntimeInitializeLoadType {SubsystemRegistration,BeforeSceneLoad}
public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
public static class Resources {public static object asset;public static int loads;public static T Load<T>(string path)where T:class{loads++;if(path!=""GameDiagnostics"")throw new Exception(""wrong path"");return asset as T;}}
/* SETTINGS */
public static class Checks {
 public static void Run(){
  var names=new[]{""CriticalErrors"",""BoardFlow"",""BoardIntegrity"",""SpecialChains"",""Performance"",""Backend"",""AnalyticsEvents"",""EditorTools""};
  var reset=typeof(GameDiagnostics).GetMethod(""ResetSession"",BindingFlags.Static|BindingFlags.NonPublic);
  reset.Invoke(null,null);Resources.asset=null;Resources.loads=0;
  foreach(var n in names)if((bool)typeof(GameDiagnostics).GetProperty(n).GetValue(null))throw new Exception(""missing asset must fail closed"");
  if(Resources.loads!=1)throw new Exception(""missing asset must not trigger repeated Resources lookups"");
  var asset=new GameDiagnosticsSettings();Resources.asset=asset;reset.Invoke(null,null);Resources.loads=0;
  foreach(var n in names)if((bool)typeof(GameDiagnostics).GetProperty(n).GetValue(null))throw new Exception(""default flag must be disabled"");
  foreach(var enabled in names){
   var field=typeof(GameDiagnosticsSettings).GetField(char.ToLowerInvariant(enabled[0])+enabled.Substring(1));field.SetValue(asset,true);
   foreach(var n in names)if((bool)typeof(GameDiagnostics).GetProperty(n).GetValue(null)!=(n==enabled))throw new Exception(""flags must be independent and live-editable"");
   field.SetValue(asset,false);
  }
  for(int i=0;i<1000;i++){var unused=GameDiagnostics.BoardFlow||GameDiagnostics.CriticalErrors;}
  if(Resources.loads!=1)throw new Exception(""hot flag reads must reuse the cached asset"");
  int expensiveArguments=0;if(GameDiagnostics.BoardFlow){string ignored=$""{++expensiveArguments}"";}if(GameDiagnostics.CriticalErrors){string ignored=$""{++expensiveArguments}"";}
  if(expensiveArguments!=0)throw new Exception(""disabled logs must not evaluate expensive arguments"");
  reset.Invoke(null,null);Resources.asset=null;if(GameDiagnostics.Backend)throw new Exception(""domain-reload-off reset must discard old settings"");
 }
}
Checks.Run();";
var script=CSharpScript.Create(harness.Replace("/* SETTINGS */",settingsSource));var diagnostics=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(diagnostics.Length>0)throw new Exception(string.Join("\n",diagnostics.Select(d=>d.ToString())));script.RunAsync().GetAwaiter().GetResult();
var assetText=File.ReadAllText("Assets/_Project/Resources/GameDiagnostics.asset");
foreach(var flag in flags.Where(f=>f!="AnalyticsEvents")){string field=char.ToLowerInvariant(flag[0])+flag.Substring(1);if(!Regex.IsMatch(assetText,@"^  "+field+@": 0$",RegexOptions.Multiline))throw new Exception("Enabled shipped flag: "+flag);}
var board=File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BoardController.cs");
var match=File.ReadAllText("Assets/_Project/Scripts/Grid/Board/MatchFinder.cs");
if(!match.Contains("if (!GameDiagnostics.BoardIntegrity) return;")||!board.Contains("if (GameDiagnostics.Performance)")||!board.Contains("var _traceId = GameDiagnostics.BoardFlow &&"))throw new Exception("Heavy diagnostics are not gated at entry");
if(!File.ReadAllText("Assets/_Project/Scripts/Core/Backend/GameAnalytics.cs").Contains("FirebaseAnalytics.LogEvent(eventName, p)"))throw new Exception("Console cleanup must not remove analytics");
Console.WriteLine("PASS: "+locations.Count+" retained console calls gated across "+fileCount+" files / 14 conditional variants; seven normal switches disabled; analytics payload tracing is separately controlled.");
Console.WriteLine("PASS: independent flags, one cached Resources lookup, missing asset, session reset, argument short-circuit, heavy trace entry gates and analytics preserved.");
foreach(var pair in categories.OrderBy(p=>p.Key))Console.WriteLine(pair.Key+": "+pair.Value);
File.WriteAllLines("/private/tmp/logging_argument_methods.txt",argumentMethods.OrderBy(x=>x));
