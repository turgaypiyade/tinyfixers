// Actual layer lookup, stateful-view routing, press Configure/ApplyPose/Motion and Wardrobe.OpenDoor.
// Unity presentation doubles; no project build or board/gravity/cascade simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var before=Environment.GetCommandLineArgs().Contains("--before");
Func<string,CompilationUnitSyntax> read=p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),path:p).GetCompilationUnitRoot();
var state=read("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs");var spawn=read("Assets/_Project/Scripts/Grid/GridSpawner.cs");
var press=read("Assets/_Project/Scripts/Grid/Board/GelLauncherPressView.cs");var motion=read("Assets/_Project/Scripts/Grid/Board/GelLauncherPressMotion.cs");
var tarp=read("Assets/_Project/Scripts/Grid/TarpStatue/TarpStatueService.cs");var wardrobe=read("Assets/_Project/Scripts/VFX/WardrobeObstacleView.cs");
foreach(var root in new[]{state,spawn,press,motion,tarp,wardrobe}){var errors=root.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error);if(errors.Any())throw new Exception(string.Join("\n",errors));}
Func<CompilationUnitSyntax,string[],string> methods=(r,ns)=>string.Join("\n",r.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>ns.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var configure=methods(spawn,new[]{"ConfigureObstacleContentView"});
if(before){
 var old=read("Docs/Checks/Layered-Obstacle-Contents-2026-11-04/Before/GridSpawner.cs");
 var draw=old.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="DrawObstacleImage");
 var statements=draw.Body.Statements.OfType<IfStatementSyntax>().Where(s=>new[]{"LaserCannon","RhodesStatue","IsGelLauncher","TarpStatue"}.Any(id=>s.Condition.ToString().Contains(id)));
 configure="private void ConfigureObstacleContentView(Image img,ObstacleDef def,int x,int y){var rt=img.rectTransform;int w=def.size.x,h=def.size.y;"+string.Join("\n",statements.Select(s=>s.ToFullString()))+"}";
 tarp=read("Docs/Checks/Layered-Obstacle-Contents-2026-11-04/Before/TarpStatueService.cs");
}
var source=File.ReadAllText("Tools/layered_obstacle_contents_harness.txt")
 .Replace("/* STAMP */",state.DescendantNodes().OfType<StructDeclarationSyntax>().Single(s=>s.Identifier.Text=="StampedBeneath").ToFullString())
 .Replace("/* STATE */",methods(state,new[]{"TryGetLayerRemainingHitsAt","GetRemainingHitsAt","IsValidCell","RegisterStampedBeneath"}))
 .Replace("/* SPAWNER */",methods(spawn,new[]{"ResolveObstacleViewRemainingHits","AttachPaintCanBoxView","HandleWardrobeOpened"})+configure)
 .Replace("/* PRESS */",methods(press,new[]{"Configure","ApplyPose"}))
 .Replace("/* MOTION */",motion.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().ToFullString())
 .Replace("/* TARP */",methods(tarp,new[]{"Register"}))
 .Replace("/* WARDROBE */",methods(wardrobe,new[]{"OpenDoor","SetClosedSprite"}));
await CSharpScript.RunAsync(source+"\nChecks.Run("+(before?"true":"false")+");",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
