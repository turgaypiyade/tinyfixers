// Production layer lookup + attachment + actual pyramid construction/count update, with Unity doubles.
// No Unity/player/project build; methods run in an isolated in-memory script.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var before=Environment.GetCommandLineArgs().Contains("--before");
var statePath="Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs";
var spawnPath="Assets/_Project/Scripts/Grid/GridSpawner.cs";
var viewPath="Assets/_Project/Scripts/Grid/Board/PaintCanBoxView.cs";
Func<string,CompilationUnitSyntax> parse=p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),path:p).GetCompilationUnitRoot();
var state=parse(statePath);var spawn=parse(before?"Docs/Checks/PaintCanBox-Cover-Reveal/Before/GridSpawner.cs":spawnPath);var view=parse(viewPath);
foreach(var path in new[]{statePath,spawnPath,viewPath}){
 var errors=parse(path).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var stateNames=new[]{"TryGetLayerRemainingHitsAt","GetRemainingHitsAt","IsValidCell","RegisterStampedBeneath"};
var stateMethods=string.Join("\n",state.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>stateNames.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var stamp=state.DescendantNodes().OfType<StructDeclarationSyntax>().Single(s=>s.Identifier.Text=="StampedBeneath").ToFullString();
var spawnMethods=string.Join("\n",spawn.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"AttachPaintCanBoxView","UpdatePaintCanBoxView","ResolveObstacleViewRemainingHits"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var fields=new[]{"CanSpritePath","FloorY","MaxPyramidHeight","CanWidth","RowOverlap","RowCounts","RemovalOrder","canSprites","slots","count","cellSize","color","wobble"};
var vf=string.Join("\n",view.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f=>f.Declaration.Variables.Any(v=>fields.Contains(v.Identifier.Text))).Select(f=>f.ToFullString()));
var vm=string.Join("\n",view.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"Ensure","AutoColorForRow","CanSprite","Build","SetSlotVisible","SetCount"}.Contains(m.Identifier.Text)).Select(m=>m.Identifier.Text=="Build"?m.WithBody(m.Body.WithStatements(m.Body.Statements.Insert(0,SyntaxFactory.ParseStatement("BuildCalls++;")))).ToFullString():m.ToFullString()));
var capacity=view.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single(p=>p.Identifier.Text=="Capacity").ToFullString();
var source=File.ReadAllText("Tools/paintcan_cover_reveal_harness.txt").Replace("/* STATE METHODS */",stateMethods).Replace("/* STAMP */",stamp).Replace("/* SPAWN METHODS */",spawnMethods).Replace("/* VIEW FIELDS */",vf).Replace("/* VIEW METHODS */",vm).Replace("/* CAPACITY */",capacity);
// C# script code cannot declare namespaces; flatten only the Unity double containers.
var flat=CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
source=string.Join("\n",flat.Usings.Where(u=>!u.Name.ToString().StartsWith("UnityEngine")).Select(u=>u.ToFullString()))+"\n"+
 string.Join("\n",flat.Members.SelectMany(m=>m is NamespaceDeclarationSyntax ns?ns.Members.AsEnumerable():new[]{m}).Select(m=>m.ToFullString()));
await CSharpScript.RunAsync(source+"\nChecks.Run("+(before?"true":"false")+");",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
