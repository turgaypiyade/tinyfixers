// Real target scorer/coordinator/pool/intent/aim/chain methods; no board simulation or Unity build.
#load "check_keygenerator_patchbot.csx"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var before=Environment.GetCommandLineArgs().Contains("--before");
var beforeRoot="Docs/Checks/PatchBot-Wall-WeakPoint-2026-11-06/Before/Assets/_Project/Scripts/";
string OldSource(string p)=>File.ReadAllText(beforeRoot+p).Replace("using UnityEngine;", "").Replace("using System.Collections.Generic;", "").Replace("using System;", "");
string GetMethod(string p,string n)=>CSharpSyntaxTree.ParseText(before?File.ReadAllText(beforeRoot+p):Read(p)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText==n).ToFullString();
var focused="using System.Linq;\n"+harness.Substring(0,harness.LastIndexOf("Checks.Run()",StringComparison.Ordinal));
if(before){
 foreach(var p in new[]{"Grid/Board/Actions/PatchBotImpactScorer.cs","Grid/Board/Actions/PatchBotTargetCoordinator.cs"})
  focused=focused.Replace(Source(p).Replace("using System;", "").Replace("UnityEngine.Random.Range", "Random.Range"),OldSource(p).Replace("UnityEngine.Random.Range","Random.Range"));
}
focused+=@"
public static class Dash {
"+GetMethod("VFX/PatchbotDashUI.cs","AimWorldPosition")+@"
}
public class Chain {BoardController board;public Chain(BoardController b){board=b;}
"+GetMethod("Grid/Board/Actions/SpecialChainRunner.cs","TryGetObstacleFootprintCenter").Replace("private bool","public bool")+@"
}
public static class WallChecks {
 static int count;static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
 static BoardController Wall(ObstacleId id,int variant,bool normalTiles=true){
  var b=new BoardController(9,11);b.TopHud.ExtraGoals.Add(new TopHudController.ActiveGoal{targetType=LevelGoalTargetType.Obstacle,obstacleId=id});
  if(normalTiles)for(int x=0;x<9;x++)for(int y=0;y<11;y++){b.Tiles[x,y]=new TileView{X=x,Y=y,Type=TileType.Gear};b.GridData[x,y]=new object();}
  for(int x=1;x<=7;x++)for(int y=1;y<=9;y++){
   bool member=variant==0||variant==1&&(x<=2||y>=8)||variant==2&&(x>=6||y<=2);
   if(!member)continue;b.ObstacleStateService.ids[x,y]=id;b.ObstacleStateService.UnitOrigins[y*9+x]=10;b.Tiles[x,y]=null;b.GridData[x,y]=null;
  }
  b.Walls.Stages[10]=id==ObstacleId.Wall?4:3;b.ObstacleStateService.Hits[10]=1;return b;
 }
 public static void Run(bool before){
  if(before){
   var b=Wall(ObstacleId.Wall,0);b.Walls.Stages.Clear();b.Walls.Stages[88]=4;
   var s=new PatchbotComboService(b);var c=new PatchBotTargetCoordinator(b,s,TileSpecial.PulseCore);var i=c.PickIntentFrom(Vector2Int.zero).intent;
   Check(i.InitialCell.x!=7||i.InitialCell.y!=9,""Old combo must ignore the unique weakest wall cell"");
   var w=Dash.AimWorldPosition(b,7,9);Check(w.x!=70||w.y!=90,""Old rectangular aim drifts away from the selected wall cell"");
   Vector2Int center;Check(new Chain(b).TryGetObstacleFootprintCenter(new Vector2Int(7,9),out center)&&!(center.x==7&&center.y==9),""Old pulse recenters the weak wall target"");
   Console.WriteLine(""REPRODUCED: combo picks intact dense wall center, dash recenters, carried pulse shifts away from weak point."");return;
  }
  foreach(var id in new[]{ObstacleId.Wall,ObstacleId.MetalWall})foreach(int shape in new[]{0,1,2})foreach(bool normal in new[]{false,true})foreach(var payload in new[]{TileSpecial.None,TileSpecial.LineH,TileSpecial.LineV,TileSpecial.PulseCore}){
   var b=Wall(id,shape,normal);var s=new PatchbotComboService(b);var c=new PatchBotTargetCoordinator(b,s,payload);var i=c.PickIntentFrom(new Vector2Int(8,10)).intent;
   Check(i!=null&&i.IsObstacle&&i.InitialCell.x==1&&i.InitialCell.y==1,""Solo and carried H/V/Pulse must choose unique weakest hittable wall cell, including sparse shapes and filler tiles"");
   var live=c.ResolveIntentFrom(i,new Vector2Int(8,10));Check(live.hasCell&&live.cell.x==1&&live.cell.y==1,""Airborne resolution retains weakest selected cell"");
   var w=Dash.AimWorldPosition(b,live.cell.x,live.cell.y);Check(w.x==10&&w.y==10,""Dash and airborne combo presentation aim at exact weak cell"");
   Vector2Int center;Check(!new Chain(b).TryGetObstacleFootprintCenter(live.cell,out center)&&center.x==1&&center.y==1,""Carried pulse must explode at weak cell, never rectangle center"");
   c.ReleaseIntent(i);Check(c.ActiveBotCount==0&&b.TargetPool.ReservedObstacleHits(10)==0,""Completion/cancellation releases shared wall reservation once"");
  }
  foreach(var payload in new[]{TileSpecial.LineH,TileSpecial.LineV,TileSpecial.PulseCore}){
   var b=Wall(ObstacleId.Wall,0);b.ObstacleStateService.Locked.Add(10);b.Walls.Stages[2*9+2]=3;b.ObstacleStateService.Hits[10]=2;
   var c=new PatchBotTargetCoordinator(b,new PatchbotComboService(b),payload);var i=c.PickIntentFrom(Vector2Int.zero).intent;
   Check(i.InitialCell.x==2&&i.InitialCell.y==2,""A covered weaker point is ineligible; choose best exposed stage"");c.ReleaseIntent(i);
   b.ObstacleStateService.Locked.Clear();i=c.PickIntentFrom(Vector2Int.zero).intent;Check(i.InitialCell.x==1&&i.InitialCell.y==1,""New selection reads current cover eligibility, not cached weakness"");c.ReleaseIntent(i);
   b.Walls.Stages[10]=0;b.Walls.Stages[7*9+6]=4;i=c.PickIntentFrom(Vector2Int.zero).intent;Check(i.InitialCell.x==6&&i.InitialCell.y==7,""New combo selection follows changed weakest stage"");c.ReleaseIntent(i);
  }
  foreach(var payload in new[]{TileSpecial.None,TileSpecial.LineH,TileSpecial.LineV,TileSpecial.PulseCore}){
   var b=new BoardController(9,11);b.TopHud.ExtraGoals.Add(new TopHudController.ActiveGoal{targetType=LevelGoalTargetType.Obstacle,obstacleId=ObstacleId.Flour});
   for(int x=0;x<9;x++)for(int y=0;y<11;y++)b.ObstacleStateService.ids[x,y]=ObstacleId.Flour;
   for(int x=6;x<=7;x++)for(int y=7;y<=8;y++){b.ObstacleStateService.ids[x,y]=ObstacleId.Windmill;b.ObstacleStateService.UnitOrigins[y*9+x]=69;}
   var c=new PatchBotTargetCoordinator(b,new PatchbotComboService(b),payload);var i=c.PickIntentFrom(Vector2Int.zero).intent;
   Check(b.ObstacleStateService.GetObstacleIdAt(i.InitialCell.x,i.InitialCell.y)==ObstacleId.Windmill,""Flour goal promotes its mill source before already-spread flour, for solo and every payload"");
   c.ReleaseIntent(i);for(int x=6;x<=7;x++)for(int y=7;y<=8;y++)b.ObstacleStateService.ids[x,y]=ObstacleId.None;
   i=c.PickIntentFrom(Vector2Int.zero).intent;Check(i!=null&&b.ObstacleStateService.GetObstacleIdAt(i.InitialCell.x,i.InitialCell.y)==ObstacleId.Flour,""After mill clears, remaining flour becomes available again"");c.ReleaseIntent(i);
  }
  {
   var b=new BoardController(9,11);b.TopHud.ExtraGoals.Clear();b.TopHud.ExtraGoals.Add(new TopHudController.ActiveGoal{targetType=LevelGoalTargetType.Obstacle,obstacleId=ObstacleId.Wall});
   b.ObstacleStateService.ids[0,0]=ObstacleId.Wall;b.ObstacleStateService.UnitOrigins[0]=0;b.Walls.Stages[0]=4;b.ObstacleStateService.Hits[0]=1;
   b.ObstacleStateService.ids[8,10]=ObstacleId.Wall;b.ObstacleStateService.UnitOrigins[98]=98;b.ObstacleStateService.Hits[98]=1;
   var c=new PatchBotTargetCoordinator(b,new PatchbotComboService(b),TileSpecial.PulseCore);var a=c.PickIntentFrom(Vector2Int.zero).intent;var d=c.PickIntentFrom(Vector2Int.zero).intent;
   Check(a!=null&&d!=null&&a.ObstacleOriginIndex!=d.ObstacleOriginIndex,""Shared reservations keep last-hit walls from receiving duplicate combo bots"");c.ReleaseIntent(a);c.ReleaseIntent(d);
  }
  {
   var b=Wall(ObstacleId.Wall,0);var c=new PatchBotTargetCoordinator(b,new PatchbotComboService(b),TileSpecial.PulseCore);var i=c.PickIntentFrom(Vector2Int.zero).intent;c.ReleaseIntent(i);
   var field=typeof(PatchBotTargetCoordinator).GetField(""impactScorer"",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);var scorer=field.GetValue(c);
   var arrays=new[]{""cells"",""footprints"",""seenOrigins""}.Select(n=>scorer.GetType().GetField(n,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)).ToArray();
   var refs=arrays.Select(f=>f.GetValue(scorer)).ToArray();
   for(int n=0;n<40;n++){i=c.PickIntentFrom(Vector2Int.zero).intent;c.ReleaseIntent(i);}
   Check(object.ReferenceEquals(scorer,field.GetValue(c))&&arrays.Select((f,n)=>object.ReferenceEquals(refs[n],f.GetValue(scorer))).All(v=>v),""Weak-point stages reuse the existing snapshot/scorer arrays after warmup"");
   b.ObstacleStateService.OriginReads=0;b.Walls.Reads=0;i=c.PickIntentFrom(Vector2Int.zero).intent;b.ObstacleStateService.OriginReads=0;b.Walls.Reads=0;
   for(int n=0;n<80;n++)c.ResolveIntentFrom(i,Vector2Int.zero);
   Check(b.Walls.Reads==0&&b.ObstacleStateService.OriginReads==80,""Flight ticks keep O(1) eligibility checks; no added wall-stage or board scans"");c.ReleaseIntent(i);
  }
  Console.WriteLine(""PASS: ""+count+"" production weak-wall/solo/H/V/Pulse/aim/cover/flour-source/reservation/reuse checks."");
 }
}
WallChecks.Run("+(before?"true":"false")+");";
await CSharpScript.RunAsync(focused,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
