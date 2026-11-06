// Actual editor paint/group/erase/cover/draw methods with presentation doubles.
// No Unity/project build, Play or board/gravity/cascade simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
Func<string,CompilationUnitSyntax> read=p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),path:p).GetCompilationUnitRoot();
var data=read("Assets/_Project/Scripts/Core/LevelData.cs");var editor=read("Assets/_Project/Scripts/Editor/LevelDataEditor.cs");
var before=Environment.GetCommandLineArgs().Contains("--before");
var oldEditor=before?read("Docs/Checks/Wall-Overlay-Editor-2026-11-06/Before/Assets/_Project/Scripts/Editor/LevelDataEditor.cs"):editor;
var state=read("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs");
foreach(var r in new[]{data,editor,state}){var errors=r.GetDiagnostics().Where(x=>x.Severity==DiagnosticSeverity.Error);if(errors.Any())throw new Exception(string.Join("\n",errors));}
Func<CompilationUnitSyntax,string,string> method=(r,n)=>r.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text==n).ToFullString();
var entry=data.DescendantNodes().OfType<StructDeclarationSyntax>().Single(s=>s.Identifier.Text=="StackedObstacleEntry");
var names=new[]{"IsWallId","IsStackedWallId","StackedWallPieceNumber","NextWallPieceNumber","RecomputeStackedWallOrigins","RecomputeWallOrigins","PlacementFootprint","PlaceStackedObstacle","RemoveStackedAtCell","RemoveStackedWallAtCell","RectCells","SimulateTopLayers","ValidateFullCover","NextStackOrder","CountGoalObstacles","DrawStackedWallPieces","WallPieceColor"};
var source=File.ReadAllText("Tools/wall_overlay_editor_harness.txt")
 .Replace("/* ENUM */",data.DescendantNodes().OfType<EnumDeclarationSyntax>().Single(e=>e.Identifier.Text=="ObstacleId").ToFullString())
 .Replace("/* ENTRY */",entry.RemoveNodes(entry.DescendantNodes().OfType<AttributeListSyntax>(),SyntaxRemoveOptions.KeepNoTrivia).ToFullString())
 .Replace("/* METHODS */",string.Join("\n",names.Select(n=>oldEditor.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(m=>m.Identifier.Text==n)?method(oldEditor,n):method(editor,n))))
 .Replace("/* SEE THROUGH */",method(state,"IsSeeThroughLayer"));
var lib=File.ReadAllText("Assets/_Project/Settings/ObstacleLibrary.asset");
var seed="";
foreach(Match m in Regex.Matches(lib,@"(?ms)^  - id: (\d+)\n(.*?)(?=^  - id:|\z)")){
 var size=Regex.Match(m.Groups[2].Value,@"(?m)^    size: \{x: (\d+), y: (\d+)\}");
 var hits=Regex.Match(m.Groups[2].Value,@"(?m)^    hits: (\d+)");
 var behavior=Regex.Match(m.Groups[2].Value,@"(?m)^      behavior: (\d+)");
 seed+="defs[(ObstacleId)"+m.Groups[1].Value+"]=new ObstacleDef{size=new Vector2Int("+size.Groups[1].Value+","+size.Groups[2].Value+"),hits="+hits.Groups[1].Value+",IsMovableObstacle="+(behavior.Groups[1].Value=="3"?"true":"false")+"};\n";
}
source=source.Replace("/* LIBRARY */",seed);
await CSharpScript.RunAsync(source+"\nChecks.Run("+(before?"true":"false")+");",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
