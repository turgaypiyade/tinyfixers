// Actual destruction + promotion with typed-view registries; no Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var before=Environment.GetCommandLineArgs().Contains("--before");
Func<string,CompilationUnitSyntax> read=p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetCompilationUnitRoot();
var spawn=read(before?"Docs/Checks/Layered-Obstacle-Contents-2026-11-04/Before/GridSpawner.cs":"Assets/_Project/Scripts/Grid/GridSpawner.cs");
var state=read("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs");
var methods=string.Join("\n",spawn.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"HandleObstacleDestroyed","HandleObstacleCreatedDynamic","IsTrackedObstacleViewFor"}.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var gate=state.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="ReinitRestoredBeneathOrigin").ToFullString();
var source=File.ReadAllText("Tools/covered_origin_view_reveal_harness.txt");source=source.Substring(0,source.IndexOf("public static class Tests {"));
source=source.Replace("/* GATE */",gate).Replace("/* SPAWNER */",methods+@"
 public object RememberContent(int origin,ObstacleId id){var marker=new object();Registry(id)[origin]=marker;return marker;}
 System.Collections.Generic.Dictionary<int,object> Registry(ObstacleId id)=>id==ObstacleId.ColorChest?_chestViews:id==ObstacleId.BatteryBox?_batteryBoxViews:_wardrobeViews;
 public bool HasContent(int origin,ObstacleId id,object marker)=>Registry(id).TryGetValue(origin,out var actual)&&actual==marker;
");
source+=@"
public static class Tests {
 static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 public static void Run(bool before){
  foreach(var id in new[]{ObstacleId.BatteryBox,ObstacleId.ColorChest,ObstacleId.Wardrobe})foreach(var order in new[]{new[]{74,75,83,84},new[]{83,84,75,74}}){
   var level=new Level();var state=new State{level=level};var p=new Spawner{resolvedLevel=level};state.Connect(p);
   var pre=new Image();p.beneathViewsByCell[74]=pre;var marker=p.RememberContent(74,id);
   foreach(int cell in order){level.obstacles[cell]=(int)ObstacleId.Grass;level.obstacleOrigins[cell]=cell;p.Add(cell,ObstacleId.Grass);}
   foreach(int cell in order){level.obstacles[cell]=(int)id;level.obstacleOrigins[cell]=74;p.Retire(cell,ObstacleId.Grass);state.Reveal(74,id);}
   if(before){Check(!p.HasContent(74,id,marker),""legacy battery registry must be removed by cover cleanup"");Console.WriteLine(""REPRODUCED: covered BatteryBox loses its content registry when Grass at the shared origin is destroyed."");return;}
   Check(p.HasContent(74,id,marker),""cover destruction must preserve the actual content view reference"");
   Check(p.obstacleViewsByOrigin[74]==pre&&!pre.gameObject.destroyed,""promotion must retain the same prefab root"");
   p.Retire(74,ObstacleId.Grass);Check(p.HasContent(74,id,marker),""late cover events cannot unregister replacement contents"");
   p.Retire(74,id);Check(!p.HasContent(74,id,marker)&&pre.gameObject.destroyed,""actual owner destruction retires its own registry"");
   p.Retire(74,id);Check(!p.HasContent(74,id,marker)&&p.newViews==0,""repeat cleanup cannot rebuild or retain a dead typed view"");
  }
  Console.WriteLine(""PASS: ""+checks+"" production content-registry ownership checks (chest, battery and wardrobe; both reveal orders)."");
 }
}
";
await CSharpScript.RunAsync(source+"\nTests.Run("+(before?"true":"false")+");",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
