// Deterministic unit harness; no Unity/project build and no store/network calls.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
string Source(string path)
{
 var parse=new CSharpParseOptions(preprocessorSymbols:new[]{"UNITY_ANDROID","DEVELOPMENT_BUILD","TF_IAP","TF_ADMOB"});
 var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/"+path),parse).GetCompilationUnitRoot();
 var trivia=root.DescendantTrivia().Where(t=>t.IsKind(SyntaxKind.DisabledTextTrivia)||t.IsDirective).ToArray();
 root=root.ReplaceTrivia(trivia,(a,b)=>default(SyntaxTrivia)).WithUsings(SyntaxFactory.List<UsingDirectiveSyntax>());
 return root.ToFullString().Replace("UnityEngine.Object.DontDestroyOnLoad","GlobalObject.DontDestroyOnLoad");
}
var script=File.ReadAllText("Tools/monetization_harness.txt")
 .Replace("/* STORE */",Source("UI/Shop/StoreIap.cs"))
 .Replace("/* PURCHASE */",Source("UI/Shop/ShopPurchaseService.cs"))
 .Replace("/* ADS */",Source("Core/Backend/AdService.cs"))
 .Replace("/* CONFIG */",Source("Core/Backend/MonetizationConfig.cs"));
await CSharpScript.RunAsync(script,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly,typeof(System.Text.RegularExpressions.Regex).Assembly));
