// API checks only: no Unity/project build, assembly output or build hooks invoked.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
using System;using System.IO;using System.Linq;using System.Collections.Generic;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;
var files=new[]{"UI/Shop/StoreIap.cs","UI/Shop/ShopScreenController.cs","UI/Shop/ShopPurchaseService.cs","Core/Backend/AdService.cs","Core/Backend/MonetizationConfig.cs","Core/Settings/SettingsPanel.cs","Core/LivesManager.cs","Core/LevelAssist.cs","Core/LevelAttemptStats.cs","Core/Backend/LevelTelemetryStore.cs","Core/WelcomeBackReward.cs","Core/ComebackBoost.cs","Core/BoosterInventory.cs"};
var refs=new List<MetadataReference>{MetadataReference.CreateFromFile(typeof(object).Assembly.Location),MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),MetadataReference.CreateFromFile(typeof(System.Text.RegularExpressions.Regex).Assembly.Location)};
string unity="/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting/Managed";
refs.AddRange(Directory.GetFiles(unity+"/UnityEngine","*.dll").Select(p=>MetadataReference.CreateFromFile(p)));
foreach(var name in new[]{"Assembly-CSharp.dll","UnityEngine.UI.dll","Unity.TextMeshPro.dll","Unity.Purchasing.dll","Unity.Purchasing.Stores.dll","Unity.Purchasing.Utilities.dll","Unity.Purchasing.SecurityCore.dll","Unity.Purchasing.SecurityStub.dll"})refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Library/ScriptAssemblies/"+name)));
refs.Add(MetadataReference.CreateFromFile("/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Facades/netstandard.dll"));
refs.AddRange(Directory.GetFiles("Assets/GoogleMobileAds","*.dll").Select(p=>MetadataReference.CreateFromFile(Path.GetFullPath(p))));
var combinations=new[]{"UNITY_ANDROID,TF_IAP,TF_ADMOB","UNITY_IOS,TF_IAP,TF_ADMOB","UNITY_ANDROID,DEVELOPMENT_BUILD,TF_IAP,TF_ADMOB","UNITY_EDITOR,UNITY_ANDROID,TF_IAP,TF_ADMOB","UNITY_EDITOR","UNITY_IOS"};
foreach(var symbols in combinations)
{
 var parse=new CSharpParseOptions(preprocessorSymbols:symbols.Split(','));
 var trees=files.Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/"+f),parse,path:f));
 var c=CSharpCompilation.Create("MonetizationApi",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
 var errors=c.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(symbols+"\n"+string.Join("\n",errors.Select(e=>e.ToString())));
 Console.WriteLine("PASS runtime SDK binding: "+symbols);
}
var editorRefs=refs.ToList();
editorRefs.AddRange(Directory.GetFiles(unity,"UnityEditor.CoreModule.dll").Select(p=>MetadataReference.CreateFromFile(p)));
foreach(var symbols in new[]{"UNITY_EDITOR,UNITY_ANDROID,TF_IAP,TF_ADMOB","UNITY_EDITOR,UNITY_IOS,TF_IAP,TF_ADMOB"})
{
 var parse=new CSharpParseOptions(preprocessorSymbols:symbols.Split(','));
 var sources=files.Select(f=>"Assets/_Project/Scripts/"+f).Concat(new[]{"Assets/_Project/Editor/MonetizationBuildSetup.cs"});
 var trees=sources.Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText(f),parse,path:f));
 var c=CSharpCompilation.Create("MonetizationEditorApi",trees,editorRefs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
 var errors=c.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
 Console.WriteLine("PASS catalog/build-check SDK binding: "+symbols);
}
Console.WriteLine("No build started; no assembly emitted; build hooks not invoked.");
