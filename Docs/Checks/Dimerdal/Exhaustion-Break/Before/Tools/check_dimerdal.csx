// Focused production-code checks with Unity doubles; no Unity/project build or board simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#load "check_dimerdal_scopes.csx"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
Func<string,CompilationUnitSyntax> parse=p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),path:p).GetCompilationUnitRoot();
var paths=new[]{"Core/LevelData.cs","Editor/LevelDataEditor.cs","Grid/GridSpawner.cs","Grid/TileView.cs","Grid/Board/BoardController.cs","Grid/Board/Actions/ClearPresentationPlan.cs","Grid/Board/Actions/MatchClearAction.cs","Grid/Board/Actions/PatchBotTargetCoordinator.cs","Grid/Board/Obstacles/ObstacleStateService.cs","Grid/Board/Obstacles/DimerdalRules.cs","Grid/Board/Obstacles/DimerdalService.cs","Grid/Board/Obstacles/DimerdalView.cs","Grid/Board/Obstacles/DimerdalProductionFx.cs","Grid/Board/Obstacles/DimerdalCollectionFx.cs","Grid/AncientSealCollectFx.cs"};
foreach(var p in paths){var root=parse("Assets/_Project/Scripts/"+p);var errors=root.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));}
var hintErrors=parse("Assets/_Project/Scripts/Tutorial/ObstacleHintManager.cs").GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(hintErrors.Length>0)throw new Exception(string.Join("\n",hintErrors.Select(d=>d.ToString())));
var level=parse("Assets/_Project/Scripts/Core/LevelData.cs");
var enums=string.Join("\n",level.DescendantNodes().OfType<EnumDeclarationSyntax>().Where(e=>new[]{"ObstacleId","CollectibleId","LevelGoalTargetType"}.Contains(e.Identifier.Text)).Select(e=>e.ToFullString()));
var contexts=parse("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs").DescendantNodes().OfType<EnumDeclarationSyntax>().Single(e=>e.Identifier.Text=="ObstacleHitContext").ToFullString();
Func<string,string> production=p=>string.Join("\n",parse("Assets/_Project/Scripts/"+p).Members.Select(m=>m.ToFullString()));
var convert=string.Join("\n",parse("Assets/_Project/Scripts/Grid/Board/BoardController.cs").DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"TryConvertDimerdalDiamond","CanConvertDimerdalDiamond","CollectDimerdalDiamond"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var source=File.ReadAllText("Tools/dimerdal_harness.txt").Replace("/* ENUMS */",enums+contexts+File.ReadAllText("Assets/_Project/Scripts/Grid/TileType.cs")).Replace("/* CONVERT */",convert).Replace("/* PLAN */",production("Grid/Board/Actions/ClearPresentationPlan.cs")).Replace("/* RULES */",production("Grid/Board/Obstacles/DimerdalRules.cs")).Replace("/* VIEW */",production("Grid/Board/Obstacles/DimerdalView.cs")).Replace("/* FLIGHT */",production("Grid/Board/Obstacles/DimerdalProductionFx.cs").Replace("UnityEngine.Random.Range", "TestRandom.Range")).Replace("/* COLLECTION */",production("Grid/Board/Obstacles/DimerdalCollectionFx.cs")).Replace("/* SERVICE */",production("Grid/Board/Obstacles/DimerdalService.cs"));
source=source.Replace("/* APPLY */",parse("Assets/_Project/Scripts/Grid/TileView.cs").DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"ApplyDimerdalDiamond","ApplyDimerdalIconBounds"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()).Aggregate((a,b)=>a+"\n"+b));
var matchMethods=string.Join("\n",parse("Assets/_Project/Scripts/Grid/Board/Actions/MatchClearAction.cs").DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"IsOriginalMatch","ExcludeDimerdalReplacements"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString())).Replace("private void ExcludeDimerdalReplacements", "public void ExcludeDimerdalReplacements");
source=source.Replace("/* MATCH GUARD */",matchMethods);
var intent=parse("Assets/_Project/Scripts/Grid/Board/Actions/PatchBotTargetCoordinator.cs").Members.OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.Text=="PatchBotIntent").ToFullString();
source=source.Replace("/* PATCHBOT */",intent+production("Grid/Board/Specials/SpecialUtils.cs"));
await CSharpScript.RunAsync(source+"\nChecks.Run();",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
