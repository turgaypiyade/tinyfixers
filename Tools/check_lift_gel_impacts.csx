// Production shared Lift/GelLauncher rules with board doubles; no Unity build/Play.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.Scripting;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;
var source=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BoosterCellImpactPass.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single().ToFullString();
var harness=@"
using System;using System.Collections.Generic;
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public override int GetHashCode()=>x*397^y;public override bool Equals(object o)=>o is Vector2Int v&&v.x==x&&v.y==y;}
public enum TileType {Gear}public enum TileSpecial {None,LineH,SystemOverride}public enum ObstacleId {None,Stone,Magnet}public enum ObstacleHitContext {Booster,SpecialActivation}
public class TileView {public int X,Y;public TileSpecial special;public bool gel;public TileSpecial GetSpecial()=>special;}
public class ObstacleVisualChange {}
public struct Hit {public bool didHit;public ObstacleVisualChange visualChange;}
public class Obstacles {
 public int hits=3,origin=7,damage;public bool blocked=true,locked,movable,unbreakable,perCell,reject,magnet,endpoint=true;
 public bool HasObstacleAt(int x,int y)=>hits>0;public bool IsUnbreakableMovableAt(int x,int y)=>unbreakable;
 public bool IsCellBlocked(int x,int y)=>hits>0&&blocked;public bool IsInteractionLockedAt(int x,int y)=>hits>0&&locked;
 public bool IsMovableObstacleAt(int x,int y)=>hits>0&&movable;public ObstacleId GetObstacleIdAt(int x,int y)=>magnet?ObstacleId.Magnet:ObstacleId.Stone;
 public bool TakesPerCellHits(int x,int y)=>perCell;public int GetObstacleOriginAt(int x,int y)=>origin;
 public bool IsMagnetEndpoint(int x,int y)=>endpoint;
}
public class BreakFx {public int breaks;public void PlayTileBreak(TileView t){breaks++;}}
public class BoardController {
 public int Width=3,Height=3,clears,specials,notifications,visuals,paints;public bool pending;
 public TileView[,] Tiles=new TileView[3,3];public bool[,] Holes=new bool[3,3];public Obstacles ObstacleStateService=new Obstacles();public BreakFx BreakFx=new BreakFx();
 public void TriggerSpecialTileFromBoardEffect(TileView t){specials++;}public bool IsPendingTriggeredSpecialCell(int x,int y)=>pending;
 public void MarkBoosterGelHit(TileView t){t.gel=true;}public void PaintGelForBoosterCell(int x,int y){paints++;}
 public void ClearAndDestroyTile(TileView t,Dictionary<TileType,int> counts){Tiles[t.X,t.Y]=null;clears++;counts[TileType.Gear]=1;}
 public void NotifyTilesCleared(TileType t,int n){notifications+=n;}public void TriggerObstacleVisualChange(ObstacleVisualChange c){visuals++;}
 public Hit ApplyObstacleDamageAt(int x,int y,ObstacleHitContext ctx){if(ctx!=ObstacleHitContext.Booster)throw new Exception(""wrong context"");var o=ObstacleStateService;if(o.reject)return default(Hit);o.damage++;o.hits--;return new Hit{didHit=true,visualChange=new ObstacleVisualChange()};}
}
/* SOURCE */
public static class Tests {
 static int count;static void Check(bool b,string text){count++;if(!b)throw new Exception(text);}
 static Vector2Int C(int x=0,int y=0)=>new Vector2Int(x,y);
 static TileView Tile(BoardController b,int x=0,TileSpecial sp=TileSpecial.None){var t=new TileView{X=x,Y=0,special=sp};b.Tiles[x,0]=t;return t;}
 public static void Run(){
  var b=new BoardController();Tile(b);var pass=new BoosterCellImpactPass(b);pass.HitCell(C());pass.HitCell(C());
  Check(b.ObstacleStateService.damage==1&&b.ObstacleStateService.hits==2,""multi-hit obstacle receives one Lift hit, never six repeated hits"");
  Check(b.clears==0,""covered tile survives impact"");
  pass.HitCell(C(1));Check(b.ObstacleStateService.damage==1,""multi-cell obstacle deduplicated per sweep origin"");
  b=new BoardController();b.ObstacleStateService.hits=1;var t=Tile(b,0,TileSpecial.SystemOverride);new BoosterCellImpactPass(b).HitCell(C());
  Check(b.ObstacleStateService.hits==0&&b.specials==0&&b.clears==0&&b.Tiles[0,0]==t,""uncovering hit preserves newly revealed special"");
  b=new BoardController();b.ObstacleStateService.hits=0;t=Tile(b,0,TileSpecial.SystemOverride);new BoosterCellImpactPass(b).HitCell(C());
  Check(b.specials==1&&b.clears==0&&t.gel,""exposed special activates and carries gel"");
  b=new BoardController();b.ObstacleStateService.hits=0;t=Tile(b);new BoosterCellImpactPass(b).HitCell(C());
  Check(b.clears==1&&b.notifications==1&&b.BreakFx.breaks==1&&t.gel,""normal tile clear and reward/gel bookkeeping preserved"");
  b=new BoardController();b.ObstacleStateService.perCell=true;pass=new BoosterCellImpactPass(b);pass.HitCell(C());pass.HitCell(C(1));
  Check(b.ObstacleStateService.damage==2,""wall/magnet per-cell damage remains independent"");
  b=new BoardController();b.ObstacleStateService.magnet=true;b.ObstacleStateService.endpoint=false;new BoosterCellImpactPass(b).HitCell(C());
  Check(b.ObstacleStateService.damage==0,""magnet body is not an endpoint hit"");
  b=new BoardController();b.ObstacleStateService.reject=true;new BoosterCellImpactPass(b).HitCell(C());Check(b.visuals==0&&b.ObstacleStateService.damage==0,""rejected obstacle rules preserved"");
  b=new BoardController();b.ObstacleStateService.unbreakable=true;Tile(b);new BoosterCellImpactPass(b).HitCell(C());Check(b.clears==0&&b.visuals==0,""collectible/unbreakable obstacle survives"");
  b=new BoardController();b.ObstacleStateService.hits=0;b.pending=true;Tile(b,0,TileSpecial.LineH);new BoosterCellImpactPass(b).HitCell(C());Check(b.specials==0&&b.clears==0,""pending special is not double-triggered"");
  b=new BoardController();b.ObstacleStateService.hits=0;t=Tile(b,0,TileSpecial.LineH);int reserved=0,launched=0;
  new BoosterCellImpactPass(b,tile=>{reserved++;return true;},tile=>{Check(reserved==1,""reservation precedes launch"");launched++;}).HitCell(C());Check(launched==1,""Lift root retains its own anchored subchain ownership"");
  Console.WriteLine(""PASS: ""+count+"" shared Lift/GelLauncher impact checks"");
 }
}
".Replace("/* SOURCE */",source);
CSharpScript.RunAsync(harness+"\nTests.Run();", ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly)).GetAwaiter().GetResult();
