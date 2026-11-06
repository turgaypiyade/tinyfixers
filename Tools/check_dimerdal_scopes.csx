// In-memory declaration-scope diagnostics only. No assembly emit, Unity or project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;
var before=Args.Contains("--before");
var files=new[]{"Grid/TileType.cs","Grid/Board/BoosterService.cs","Grid/Board/PaintBucketJoker.cs","Grid/Board/BoardBreakFxService.cs","Grid/Windmill/WindmillService.cs","Grid/Magnet/MagnetObstacleService.cs","Grid/Board/BossDuelObstaclePressure.cs","Core/LevelData.cs","Editor/LevelDataEditor.cs","Grid/GridSpawner.cs","Grid/TileView.cs","Grid/Board/BoardController.cs","Grid/Board/Actions/ClearPresentationPlan.cs","Grid/Board/Actions/MatchClearAction.cs","Grid/Board/Actions/PatchBotTargetCoordinator.cs","Grid/Board/Obstacles/ObstacleStateService.cs","Grid/Board/Obstacles/DimerdalRules.cs","Grid/Board/Obstacles/DimerdalService.cs","Grid/Board/Obstacles/DimerdalView.cs","Grid/Board/Obstacles/DimerdalProductionFx.cs","Grid/Board/Obstacles/DimerdalCollectionFx.cs","Grid/AncientSealCollectFx.cs","Tutorial/ObstacleHintManager.cs"};
var options=CSharpParseOptions.Default.WithPreprocessorSymbols("UNITY_EDITOR","DEVELOPMENT_BUILD");
var trees=files.Select(file=>{
 var path="Assets/_Project/Scripts/"+file;var source=File.ReadAllText(path);
 if(before&&file=="Grid/Board/Obstacles/ObstacleStateService.cs")
  source=source.Replace("int dimerdalRemaining = DimerdalRemainingHitsQuery", "int remaining = DimerdalRemainingHitsQuery").Replace("new ObstacleVisualChange(origin, id, false, dimerdalRemaining, null)","new ObstacleVisualChange(origin, id, false, remaining, null)");
 return CSharpSyntaxTree.ParseText(source,options,path);
}).ToArray();
var compilation=CSharpCompilation.Create("DimerdalDeclarationScopeAudit",trees,new[]{MetadataReference.CreateFromFile(typeof(object).Assembly.Location)},new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
// Missing Unity/project references are deliberately outside this isolated audit.
// The compiler still binds local declaration spaces in the actual enclosing methods.
foreach(var tree in trees){var errors=tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));}
var conflicts=compilation.GetDiagnostics().Where(d=>new[]{"CS0136","CS0128","CS0100"}.Contains(d.Id)).ToArray();
if(before){if(!conflicts.Any(d=>d.Id=="CS0136"&&d.GetMessage().Contains("remaining")))throw new Exception("Expected original remaining scope collision was not reproduced.");Console.WriteLine("REPRODUCED: original Dimerdal remaining collides with the later enclosing-method local.");}
else{if(conflicts.Length>0)throw new Exception(string.Join("\n",conflicts.Select(d=>d.ToString())));Console.WriteLine("PASS: syntax and enclosing declaration scopes in "+files.Length+" actual C# files.");}
