// Real state reveal gate + full production GridSpawner creation/destruction methods.
// Deterministic callbacks with Unity doubles; no Unity run, board simulation or project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var before=Environment.GetCommandLineArgs().Contains("--before");
Func<string,CompilationUnitSyntax> parse=p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),path:p).GetCompilationUnitRoot();
var state=parse("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs");
var spawn=parse("Assets/_Project/Scripts/Grid/GridSpawner.cs");
foreach(var root in new[]{state,spawn}){
 var errors=root.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var gate=before?File.ReadAllText("Docs/Checks/Covered-Origin-View-Reveal/ReinitRestoredBeneathOrigin-before.cs.txt"):
 state.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="ReinitRestoredBeneathOrigin").ToFullString();
var names=new[]{"HandleObstacleCreatedDynamic","HandleObstacleDestroyed","IsTrackedObstacleViewFor"};
var methods=string.Join("\n",spawn.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var source=File.ReadAllText("Tools/covered_origin_view_reveal_harness.txt").Replace("/* GATE */",gate).Replace("/* SPAWNER */",methods);
await CSharpScript.RunAsync(source+"\nTests.Run("+(before?"true":"false")+");",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
