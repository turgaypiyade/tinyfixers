// Isolated SDK binding and profile-sync regression checks; no Unity/project build or network writes.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
string root="Assets/_Project/Scripts/";
string[] files={"Core/Backend/AccountDeletionService.cs","Core/Backend/PlayerDirectoryService.cs","Core/Backend/FirebaseLeaderboardService.cs","Core/Backend/FirebaseCloudSaveService.cs","Core/Teams/FirebaseTeamCloud.cs","Core/Backend/FirebaseTeamService.cs","Core/Social/FriendState.cs","UI/Leaderboard/LeaderboardScreenController.cs","UI/Team/TeamScreenController.cs","UI/Team/TeamModels.cs"};
string[] collaborators={"Core/Backend/FirebaseAuthService.cs","Core/Backend/BackendServices.cs","Core/Teams/SimTeamService.cs","UI/Team/MockTeamService.cs","Core/Teams/TeamLifeInbox.cs","UI/Team/TeamLifeRequestRow.cs","UI/Team/TeamChatRow.cs"};
string[] accountDependencies={"Core/Backend/AuthLinkService.cs","Core/Backend/LocalAccountData.cs","Core/Backend/CurrencyLedger.cs","Core/Backend/LevelTelemetryStore.cs","Core/Backend/PlayerStatsCloudReporter.cs","Core/Backend/LevelTelemetryProjection.cs","Core/LevelAttemptStats.cs","Core/LevelAssist.cs","Core/LivesManager.cs","Core/WelcomeBackReward.cs","Core/ComebackBoost.cs","Core/BoosterInventory.cs"};
var trees=files.Concat(collaborators).Concat(accountDependencies).Distinct().Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText(root+f),path:f)).ToList();
string unity="/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine";
var refs=new List<MetadataReference>{MetadataReference.CreateFromFile(typeof(object).Assembly.Location),MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),MetadataReference.CreateFromFile(typeof(System.Net.WebClient).Assembly.Location)};
refs.AddRange(Directory.GetFiles(unity,"*.dll").Select(p=>MetadataReference.CreateFromFile(p)));
foreach(var f in new[]{"UnityEngine.UI.dll","Unity.TextMeshPro.dll","Assembly-CSharp.dll"}) refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Library/ScriptAssemblies/"+f)));
foreach(var f in new[]{"Firebase.App.dll","Firebase.Auth.dll","Firebase.Firestore.dll","Firebase.TaskExtension.dll"}) refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Assets/Firebase/Plugins/"+f)));
refs.Add(MetadataReference.CreateFromFile("/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Facades/netstandard.dll"));
var compilation=CSharpCompilation.Create("SocialProfileApiCheck",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var errors=compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
Console.WriteLine("PASS: ten changed files bind to installed Unity/Firebase APIs; no assembly emitted.");
// Rewrite only SDK completion dispatch to a deterministic pump; production bodies stay intact.
class MainThreadCalls:CSharpSyntaxRewriter {
 public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node){
  var visited=(InvocationExpressionSyntax)base.VisitInvocationExpression(node);
  var member=visited.Expression as MemberAccessExpressionSyntax;
  if(member==null||member.Name.Identifier.Text!="ContinueWithOnMainThread")return visited;
  return SyntaxFactory.InvocationExpression(SyntaxFactory.ParseExpression("Extensions.ContinueWithOnMainThread"),SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]{SyntaxFactory.Argument(member.Expression)}.Concat(visited.ArgumentList.Arguments))));
 }
}
string Production(string f){var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(root+f)).GetCompilationUnitRoot();return new MainThreadCalls().Visit(tree.WithUsings(SyntaxFactory.List<UsingDirectiveSyntax>())).ToFullString();}
var teamTree=CSharpSyntaxTree.ParseText(File.ReadAllText(root+"Core/Teams/FirebaseTeamCloud.cs")).GetRoot();
string teamSync=teamTree.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n=>n.Identifier.Text=="SyncSelfProfileIfChanged").ToFullString();
teamSync=new MainThreadCalls().Visit(CSharpSyntaxTree.ParseText("class C{"+teamSync+"}").GetRoot()).DescendantNodes().OfType<MethodDeclarationSyntax>().Single().ToFullString();
string source=File.ReadAllText("Tools/social_profile_sync_harness.txt");
source=source.Replace("/* PLAYER DIRECTORY */",Production("Core/Backend/PlayerDirectoryService.cs"))
 .Replace("/* FRIEND STATE */",Production("Core/Social/FriendState.cs"))
 .Replace("/* LEADERBOARD */",Production("Core/Backend/FirebaseLeaderboardService.cs"))
 .Replace("/* TEAM SYNC */",teamSync);
await CSharpScript.RunAsync(source,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
