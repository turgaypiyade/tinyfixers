// Production cover destruction, preview promotion, chest-open routing and actual bulb visibility.
// Unity doubles only; does not run a build, gameplay moves, gravity or cascades.
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
var view=parse("Assets/_Project/Scripts/VFX/ChestObstacleView.cs");
foreach(var root in new[]{state,spawn,view}){
 var errors=root.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var gate=state.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="ReinitRestoredBeneathOrigin").ToFullString();
var names=new[]{"HandleObstacleCreatedDynamic","HandleObstacleDestroyed","IsTrackedObstacleViewFor","HandleChestOpened"};
var methods=string.Join("\n",spawn.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.Text)).Select(m=>before&&m.Identifier.Text=="HandleObstacleDestroyed"?File.ReadAllText("Docs/Checks/ColorChest-Cover-Reveal/HandleObstacleDestroyed-before.cs.txt"):m.ToFullString()));
var bulbNames=new[]{"OpenCabinet","ShowAll","HideAll","HideColor","GetColorImage","SetActive"};
var bulbMethods=string.Join("\n",view.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>bulbNames.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var source=File.ReadAllText("Tools/covered_origin_view_reveal_harness.txt");source=source.Substring(0,source.IndexOf("public static class Tests {"));
source=source.Replace("public bool destroyed;","public bool destroyed;public bool active=true;public void SetActive(bool value){active=value;}")
 .Replace("public bool raycastTarget;","public bool raycastTarget;public Sprite sprite;")
 .Replace("_chestViews=new Dictionary<int,object>(),","")
 .Replace("/* SPAWNER */",@"
 Dictionary<int,ChestObstacleView> _chestViews=new Dictionary<int,ChestObstacleView>();
 public ChestObstacleView AddBeneathChest(int origin){var view=new ChestObstacleView();view.HideAll();_chestViews[origin]=view;beneathViewsByCell[origin]=view.root;return view;}
 public bool TracksChest(int origin)=>_chestViews.ContainsKey(origin);
 public void Open(int origin)=>HandleChestOpened(origin);
 "+methods)
 .Replace("/* GATE */",gate);
source+=@"
public class Sprite {}
[Flags] public enum ChestColorMask {None=0,Gear=1,Core=2,Bolt=4,Plate=8}
public class ChestObstacleView {
 public Image root=new Image(),gearObject=new Image(),coreObject=new Image(),boltObject=new Image(),plateObject=new Image();
 BoardController board;Sprite openBodySprite=new Sprite(),leftDoorSprite=new Sprite(),rightDoorSprite=new Sprite();bool doorFallEnabled=true;
 public int doors;void SpawnFallingDoors(){doors++;}
 T GetComponent<T>()where T:class=>root as T;
 public int Visible=>new[]{gearObject,coreObject,boltObject,plateObject}.Count(i=>i.gameObject.active);
 /* BULBS */
}
public static class Tests {
 static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static System.Collections.Generic.IEnumerable<int[]> Orders(int[] cells){if(cells.Length==0){yield return cells;yield break;}foreach(int cell in cells)foreach(var tail in Orders(cells.Where(c=>c!=cell).ToArray()))yield return new[]{cell}.Concat(tail).ToArray();}
 public static void Run(bool before){
  int orders=0;
  // Level219 has two 2x2 chests at origins86/88 under individual grass covers.
  foreach(int origin in new[]{86,88})foreach(var order in Orders(new[]{origin,origin+1,origin+9,origin+10})){
   var level=new Level();var state=new State{level=level};var spawn=new Spawner{resolvedLevel=level,board=new BoardController{ObstacleStateService=state}};state.Connect(spawn);
   var chest=spawn.AddBeneathChest(origin);Check(chest.Visible==0,""covered chest starts closed"");
   foreach(int cell in order){level.obstacles[cell]=(int)ObstacleId.Grass;level.obstacleOrigins[cell]=cell;spawn.Add(cell,ObstacleId.Grass);}
   foreach(int cell in order){
    level.obstacles[cell]=(int)ObstacleId.ColorChest;level.obstacleOrigins[cell]=origin;
    spawn.Retire(cell,ObstacleId.Grass);state.Reveal(origin,ObstacleId.ColorChest);
   }
   spawn.Open(origin);
   if(before){Check(chest.Visible==0&&!spawn.TracksChest(origin),""old cleanup reproduces missing bulb registration"");Console.WriteLine(""REPRODUCED: grass retirement unregisters the underlying chest; opening still leaves all four bulbs hidden."");return;}
   Check(spawn.TracksChest(origin)&&chest.Visible==4&&chest.doors==1,""uncovered chest must route OpenCabinet and reveal four bulbs"");
   Check(spawn.obstacleViewsByOrigin[origin]==chest.root&&!chest.root.gameObject.destroyed,""promotion retains the original prefab image"");
   chest.HideColor(ChestColorMask.Gear);Check(chest.Visible==3,""one colour removal must hide only its bulb"");
   spawn.Retire(origin,ObstacleId.Grass);Check(spawn.TracksChest(origin)&&chest.Visible==3,""late cover event must retain partial chest visibility"");
   Check(!chest.root.gameObject.destroyed,""late cover event cannot destroy the promoted chest"");
   spawn.Retire(origin,ObstacleId.ColorChest);spawn.Retire(origin,ObstacleId.ColorChest);
   Check(!spawn.TracksChest(origin)&&chest.root.gameObject.destroyed,""real chest destruction releases image and typed registry exactly once"");
   Check(spawn.newViews==0&&spawn.beneathViewsByCell.Count==0,""no duplicate prefab or leaked preview"");orders++;
  }
  Console.WriteLine(""PASS: ""+checks+"" production chest/cover/bulb checks across ""+orders+"" clear orders."");
 }
}
".Replace("/* BULBS */",bulbMethods);
await CSharpScript.RunAsync(source+"\nTests.Run("+(before?"true":"false")+");",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
