// Bind changed production code to installed SDK metadata. No project build or assembly output.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
using System;using System.IO;using System.Linq;using System.Collections.Generic;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;
string[] files={"Core/Backend/AppleSignInBridge.cs","Core/Backend/AccountDeletionService.cs","Core/Backend/FirebaseLeaderboardService.cs","Core/Backend/AuthLinkService.cs","Core/Backend/LocalAccountData.cs","Core/Backend/FirebaseAuthService.cs","Core/Backend/FirebaseCloudSaveService.cs","Core/Backend/CurrencyLedger.cs","Core/Backend/LevelTelemetryStore.cs","Core/Backend/PlayerStatsCloudReporter.cs","Core/Social/FriendState.cs","Core/LivesManager.cs","Core/BootLoader.cs","Core/LevelAttemptStats.cs","Core/LevelAssist.cs","Core/WelcomeBackReward.cs","Core/ComebackBoost.cs","Core/BoosterInventory.cs","Core/Backend/LevelTelemetryProjection.cs","UI/LoadingScreenManager.cs","Core/Settings/SaveProgressPopup.cs","Core/Backend/AuthNonce.cs","Core/Backend/SocialSignInConfig.cs","Core/Backend/GoogleSignInBridge.cs","Core/Backend/FacebookSignInBridge.cs"};
var trees=files.Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/"+f),path:f)).ToList();
var refs=new List<MetadataReference>{MetadataReference.CreateFromFile(typeof(object).Assembly.Location),MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),MetadataReference.CreateFromFile(typeof(System.Net.WebClient).Assembly.Location)};
string unity="/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine";
refs.AddRange(Directory.GetFiles(unity,"*.dll").Select(p=>MetadataReference.CreateFromFile(p)));
foreach(var f in new[]{"Assembly-CSharp.dll","AppleAuth.dll","UnityEngine.UI.dll","Unity.TextMeshPro.dll","Unity.RenderPipelines.Core.Runtime.dll"}) refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Library/ScriptAssemblies/"+f)));
foreach(var f in new[]{"Firebase.App.dll","Firebase.Auth.dll","Firebase.Firestore.dll","Firebase.TaskExtension.dll"}) refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Assets/Firebase/Plugins/"+f)));
refs.Add(MetadataReference.CreateFromFile("/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Facades/netstandard.dll"));
foreach(var f in new[]{"Assets/FacebookSDK/Plugins/Facebook.Unity.dll","Assets/FacebookSDK/Plugins/Settings/Facebook.Unity.Settings.dll"}) refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath(f)));
var c=CSharpCompilation.Create("AccountLinkApiCheck",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var errors=c.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
foreach(var platform in new[]{"UNITY_IOS","UNITY_ANDROID","UNITY_IOS,TF_APPLE_SIGNIN_CAPABILITY"})
{
    var parse=new CSharpParseOptions(preprocessorSymbols:platform.Split(','));
    var platformTrees=files.Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/"+f),parse,path:f));
    var build=CSharpCompilation.Create("AccountApi_"+platform,platformTrees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    var platformErrors=build.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
    if(platformErrors.Length>0)throw new Exception(string.Join("\n",platformErrors.Select(e=>e.ToString())));
}
Console.WriteLine("PASS: "+files.Length+" production files bind to installed Unity/Firebase/Facebook APIs for default, iOS and Android; no assembly emitted.");
var editorRefs = refs.ToList();
editorRefs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Assets/FacebookSDK/Plugins/Editor/Facebook.Unity.Editor.dll")));
foreach (var path in new[] {
 "/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll" })
 editorRefs.Add(MetadataReference.CreateFromFile(path));
foreach (var symbols in new[] { new[] { "UNITY_EDITOR", "UNITY_ANDROID" }, new[] { "UNITY_EDITOR", "UNITY_IOS" } })
{
 var parse = new CSharpParseOptions(preprocessorSymbols:symbols);
 var sources = new[] { "Assets/_Project/Editor/Auth/SocialSignInBuildSetup.cs", "Assets/_Project/Scripts/Core/Backend/SocialSignInConfig.cs" }
  .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path:path));
 var check = CSharpCompilation.Create("SocialBuildSetupApi",sources,editorRefs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
 var failures = check.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(failures.Length>0) throw new Exception(string.Join("\n",failures.Select(e=>e.ToString())));
}
Console.WriteLine("PASS: Android and iOS editor build hooks bind to installed SDK APIs; hooks not invoked, no build or assembly output.");
