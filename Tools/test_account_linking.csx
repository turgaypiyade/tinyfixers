// Execute real account state-machine methods with deterministic SDK and filesystem fixtures.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#r "System.Web.Extensions"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
class MainThreadCalls:CSharpSyntaxRewriter {
 public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node){
  var visited=(InvocationExpressionSyntax)base.VisitInvocationExpression(node);
  var member=visited.Expression as MemberAccessExpressionSyntax;
  if(member==null||member.Name.Identifier.Text!="ContinueWithOnMainThread")return visited;
  return SyntaxFactory.InvocationExpression(SyntaxFactory.ParseExpression("Extensions.ContinueWithOnMainThread"),SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]{SyntaxFactory.Argument(member.Expression)}.Concat(visited.ArgumentList.Arguments))));
 }
}
string Read(string f)=>File.ReadAllText("Assets/_Project/Scripts/Core/Backend/"+f+".cs");
string Production(string f){var root=CSharpSyntaxTree.ParseText(Read(f)).GetCompilationUnitRoot();return new MainThreadCalls().Visit(root.WithUsings(SyntaxFactory.List<UsingDirectiveSyntax>())).ToFullString().Replace("UnityEngine.Object.DontDestroyOnLoad", "MonoBehaviour.DontDestroyOnLoad");}
var auth=CSharpSyntaxTree.ParseText(Read("FirebaseAuthService")).GetRoot();
string session=string.Join("\n",auth.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"SuspendForAccountSwitch","ResumeCurrentSession","Complete"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
string script=File.ReadAllText("Tools/account_linking_harness.txt").Replace("/* AUTH SESSION METHODS */",session)
 .Replace("/* CURRENCY */",Production("CurrencyLedger")).Replace("/* LOCAL DATA */",Production("LocalAccountData"))
 .Replace("/* CLOUD SAVE */",Production("FirebaseCloudSaveService")).Replace("/* AUTH LINK */",Production("AuthLinkService"))
 .Replace("/* NONCE */",Production("AuthNonce"));
await CSharpScript.RunAsync(script,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly,typeof(System.Web.Script.Serialization.JavaScriptSerializer).Assembly));
