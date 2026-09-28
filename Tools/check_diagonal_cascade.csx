// Run: csi Tools/check_diagonal_cascade.csx (no Unity/project build).
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

var source = File.ReadAllText("Assets/_Project/Scripts/Grid/Board/CascadeLogic.cs");
var root = CSharpSyntaxTree.ParseText(source).GetRoot();
var names = new[] {
 "ProcessVerticalGravityAndSpawn", "DoDiagonalSlidePass", "TrySlideIntoTopEntry",
 "TrySlideInto", "ArrivalCost", "FindSlideSource", "ApplySlide", "RestsOnTemporarySupport",
 "PruneVerticalOnlyGaps", "HasVerticalFillPathFor", "FindSegmentTopY", "TryGetCellState",
 "IsTileSlotCell", "IsSlotEmpty", "GapExpectsVerticalFill", "IsGravityBlockedCell",
 "IsSegmentConnectedToSpawnEdge", "IsDiagonalPassableCell", "ComputeGravityReachableMask",
 "HasReachableSlideEntry", "IsSlideSourceAtCorner"
};
var methods = string.Join("\n", root.DescendantNodes().OfType<MethodDeclarationSyntax>()
 .Where(m => names.Contains(m.Identifier.Text)).Select(m => m.ToFullString()));
var virtualTile = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
 .Single(c => c.Identifier.Text == "VirtualTile").ToFullString();
var driver = File.ReadAllText("Assets/_Project/Scripts/Grid/Board/ColumnFlowEngine.cs")
 .Replace("using System.Collections.Generic;", "").Replace("using UnityEngine;", "");
var harness = @"
using System;
using System.Collections.Generic;
public struct Vector2Int { public int x,y; public Vector2Int(int x,int y){this.x=x;this.y=y;} }
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public float magnitude=>(float)Math.Sqrt(x*x+y*y);
 public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
}
public enum TileType { Gear }
public enum TileSpecial { None, Line }
public enum ObstacleId { None }
public class RectTransform { public Vector2 anchoredPosition; }
public class TileView {
 public int X,Y; public bool IsRuntimeIdle=true, pendingMotion;
 public RectTransform RectTransform=new RectTransform(); public TileSpecial special;
 public TileSpecial GetSpecial()=>special;
 public Vector2 GetFallCellPosition(int x,int y,int size)=>new Vector2(x*size,-y*size);
}
public struct BoardCellStateSnapshot {
 public bool canContainTile,isObstacleBlocked,isPassThroughVoid,allowsDiagonalPassThrough;
}
public class Obstacles {public bool HoldsTileAt(int x,int y)=>false;}
public class MotionStub {public bool HasPendingMotion(TileView t)=>t.pendingMotion;}
public class BoardController {
 public bool UseFlowPump; public MotionStub FallMotion=new MotionStub();
 public int Width,Height,TileSize=100,SpawnFeedGap=0,MaxDiagonalSlidesPerCascade=2;
 public bool[,] blocked,holes,pending; public Obstacles ObstacleStateService=new Obstacles();
 public BoardController(int w,int h){Width=w;Height=h;blocked=new bool[w,h];holes=new bool[w,h];pending=new bool[w,h];}
 public bool IsPendingTriggeredSpecialCell(int x,int y)=>pending[x,y];
 public bool IsObstacleBlockedCell(int x,int y)=>blocked[x,y];
 public bool IsMaskHoleCell(int x,int y)=>holes[x,y];
 public bool IsSpawnPassThroughCell(int x,int y)=>holes[x,y];
 public bool TryGetCellState(int x,int y,out BoardCellStateSnapshot s){
  s=default;if(x<0||y<0||x>=Width||y>=Height)return false;
  s.isObstacleBlocked=blocked[x,y];s.canContainTile=!blocked[x,y]&&!holes[x,y]&&!pending[x,y];
  s.isPassThroughVoid=holes[x,y]&&!blocked[x,y];s.allowsDiagonalPassThrough=s.canContainTile;return true;
 }
}
public partial class CascadeLogic {
 readonly BoardController board; int spawnSequence; bool[,] diagonalFeedReachable;
 public CascadeLogic(BoardController b){board=b;}
 TileType PickRefillType(VirtualTile[,] vb,int x,int y)=>TileType.Gear;
 bool TryPickMovableGoalToSpawn(int x,bool spawned,Dictionary<ObstacleId,int> counts,out ObstacleId id){id=ObstacleId.None;return false;}
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 VirtualTile[,] Simulate(bool perColumn,VirtualTile[,] vb=null){
  if(vb==null)vb=new VirtualTile[board.Width,board.Height];
  diagonalFeedReachable=ComputeGravityReachableMask();
  var gaps=new HashSet<Vector2Int>();var counts=new Dictionary<ObstacleId,int>();bool spawned=false;
  if(perColumn)RunPerColumnSimulation(vb,gaps,ref spawned,counts);
  else for(int i=0;i<96;i++){
   bool changed=false;
   for(int x=0;x<board.Width;x++)changed|=ProcessVerticalGravityAndSpawn(vb,x,ref spawned,counts);
   bool slid=DoDiagonalSlidePass(vb,gaps,true);
   if(!slid)slid=DoDiagonalSlidePass(vb,gaps,false);
   PruneVerticalOnlyGaps(vb,gaps);if(!changed&&!slid)break;
  }
  return vb;
 }
 static VirtualTile Stone(int x,int y,float visualX,float visualY){
  return new VirtualTile {View=new TileView {X=x,Y=y,RectTransform=new RectTransform {anchoredPosition=new Vector2(visualX*100,-visualY*100)}},
   Path=new List<Vector2Int>{new Vector2Int(x,y)}};
 }
 static void CapturedPlan(bool perColumn,bool mirror){
  // Saved LevelP_00310, g=131 at 36.523s. The lower stone is physically at
  // (6,1.35); the stones assigned to (5,5)/(5,4) are still above column 7.
  string[] rows={""#######TT"",""######TTT"",""##.##TTTT"",""####T.TTT"",""..##T.TTT"",""..##T.TTT"",""...T##TTT"",""..TT###TT""};
  var b=new BoardController(9,8);b.MaxDiagonalSlidesPerCascade=3;
  var vb=new VirtualTile[9,8];
  for(int y=0;y<8;y++)for(int x=0;x<9;x++){
   int bx=mirror?8-x:x;
   b.blocked[bx,y]=rows[y][x]=='#';
   if(rows[y][x]=='T')vb[bx,y]=Stone(bx,y,bx,y);
  }
  int donor=mirror?2:6,shadow=mirror?3:5,feed=mirror?1:7;
  var lower=vb[donor,2];lower.View.RectTransform.anchoredPosition=new Vector2(donor*100,-135);
  vb[shadow,2].View.RectTransform.anchoredPosition=new Vector2(feed*100,76);
  vb[donor,1].View.RectTransform.anchoredPosition=new Vector2(feed*100,168);
  var c=new CascadeLogic(b);c.Simulate(perColumn,vb);
  Check(vb[donor,2]==lower,""Captured lower donor must stay in its column instead of cutting ahead of pending shadow arrivals"");
  for(int y=2;y<=5;y++){
   Check(vb[shadow,y]!=null,""Captured shadow is still filled"");
   var path=vb[shadow,y].Path;
   for(int i=1;i<path.Count;i++)if(path[i-1].x==donor&&path[i].x==shadow)
    Check(path[i].y==2,""Captured shadow uses its top inlet, including after upstream refill"");
  }
 }
 static void FallbackChecks(bool live){
  // An unreachable upper donor must not prevent an isolated lower tile from sliding.
  var b=new BoardController(2,5);b.UseFlowPump=live;b.blocked[0,0]=b.blocked[1,0]=true;
  var c=new CascadeLogic(b);c.diagonalFeedReachable=c.ComputeGravityReachableMask();
  var vb=new VirtualTile[2,5];var lower=Stone(0,2,0,2);vb[0,2]=lower;
  vb[0,3]=Stone(0,3,0,3);vb[0,4]=Stone(0,4,0,4);
  Check(c.TrySlideIntoTopEntry(vb,1,4,true,new HashSet<Vector2Int>(),null),""Isolated lower pocket still supplies a diagonal"");
  Check(vb[1,3]==lower,""Fallback uses the available lower source"");

  // A cargo at a reachable mouth is an actual ineligible donor, not a temporary vacancy.
  b=new BoardController(2,5);b.UseFlowPump=live;b.blocked[1,0]=true;c=new CascadeLogic(b);
  c.diagonalFeedReachable=c.ComputeGravityReachableMask();vb=new VirtualTile[2,5];
  for(int y=0;y<5;y++)vb[0,y]=Stone(0,y,0,y);
  vb[0,0].IsStraightFallOnly=true;var normal=vb[0,1];
  Check(c.TrySlideIntoTopEntry(vb,1,4,true,new HashSet<Vector2Int>(),null),""Cargo at upper inlet preserves lower normal fallback"");
  Check(vb[1,2]==normal&&vb[0,0].IsStraightFallOnly,""Cargo stays vertical"");

  b=new BoardController(2,5);b.UseFlowPump=live;b.blocked[1,0]=true;c=new CascadeLogic(b);
  c.diagonalFeedReachable=c.ComputeGravityReachableMask();vb=new VirtualTile[2,5];
  for(int y=0;y<5;y++)vb[0,y]=Stone(0,y,0,y);
  vb[0,0].View.special=TileSpecial.Line;
  Check(c.TrySlideIntoTopEntry(vb,1,4,true,new HashSet<Vector2Int>(),null)&&vb[1,2]!=null,""Normal stones retain priority over upper specials"");
 }
 static void LiveCornerChecks(bool perColumn,bool mirror){
  var b=new BoardController(3,5);b.UseFlowPump=true;
  int feed=mirror?0:2,middle=1,shadow=mirror?2:0;
  b.blocked[shadow,0]=b.blocked[shadow,1]=b.blocked[middle,0]=true;
  var vb=new VirtualTile[3,5];
  for(int y=0;y<5;y++)vb[feed,y]=Stone(feed,y,feed,y);
  for(int y=1;y<5;y++)vb[middle,y]=Stone(middle,y,middle,y);
  var incoming=vb[middle,1];incoming.View.IsRuntimeIdle=false;incoming.View.pendingMotion=true;
  incoming.View.RectTransform.anchoredPosition=new Vector2(feed*100,200);
  var c=new CascadeLogic(b);c.Simulate(perColumn,vb);
  Check(vb[middle,1]==incoming,""An incoming stone cannot be preassigned past its next corner"");
  Check(vb[shadow,2]==null&&vb[shadow,3]==null&&vb[shadow,4]==null,""Lower donor cannot bypass the pending top inlet"");

  // New hole opens while this stone is approaching its corner (saved g=216 case).
  incoming.View.IsRuntimeIdle=true;incoming.View.pendingMotion=false;
  incoming.View.RectTransform.anchoredPosition=new Vector2(middle*100,-100);
  vb[middle,2]=null;
  c.Simulate(perColumn,vb);
  Check(vb[middle,2]==incoming,""The first corner arrival fills the newly opened hole below instead of taking its old sideways route"");
  Check(incoming.Path.Count==2&&incoming.Path[1].x==middle,""New hole takes vertical priority at the actual corner"");
 }
 static void LiveDrainChecks(bool perColumn,bool mirror){
  // Replay the topology from g=200, where a long chain used to be committed from
  // the spawn queue before g=216 opened a new hole in its intermediate column.
  string[] rows={""###TTTTTT"",""####TTTTT"",""##.TTTTTT"",""###TT.TTT"",""......TTT"","".....TTTT"",""...TTTTTT"",""..TTTTTTT""};
  var b=new BoardController(9,8);b.UseFlowPump=true;b.MaxDiagonalSlidesPerCascade=3;
  var vb=new VirtualTile[9,8];
  for(int y=0;y<8;y++)for(int x=0;x<9;x++){
   int bx=mirror?8-x:x;b.blocked[bx,y]=rows[y][x]=='#';
   if(rows[y][x]=='T')vb[bx,y]=Stone(bx,y,bx,y);
  }
  var c=new CascadeLogic(b);bool finished=false;
  for(int pass=0;pass<100;pass++){
   c.Simulate(perColumn,vb);bool moved=false;
   for(int x=0;x<9;x++)for(int y=0;y<8;y++){
    var tile=vb[x,y];if(tile==null)continue;
    int turns=0;
    for(int i=1;i<tile.Path.Count;i++)if(tile.Path[i].x!=tile.Path[i-1].x)turns++;
    Check(turns<=1,""Live route cannot commit to a later corner before arriving there"");
    if(tile.IsSpawned)Check(turns==0,""Spawn queue keeps vertical destinations until corner arrival"");
    moved|=tile.Path.Count>1;
    // Finish this batch and expose its actual arrivals to the next pump pass.
    if(tile.View==null)tile.View=Stone(x,y,x,y).View;
    tile.View.X=x;tile.View.Y=y;tile.View.IsRuntimeIdle=true;tile.View.pendingMotion=false;
    tile.View.RectTransform.anchoredPosition=new Vector2(x*100,-y*100);
    tile.IsSpawned=false;tile.DiagonalSlideCount=0;tile.Path=new List<Vector2Int>{new Vector2Int(x,y)};
   }
   if(!moved){finished=true;break;}
  }
  Check(finished,""Local corner decisions terminate without an infinite cascade"");
  for(int x=0;x<9;x++)for(int y=0;y<8;y++)
   if(c.diagonalFeedReachable[x,y])Check(vb[x,y]!=null,""Live corner decisions eventually fill every reachable cell"");
 }
 public static int Run(){
  FallbackChecks(false);FallbackChecks(true);
  foreach(bool perColumn in new[]{false,true})foreach(bool mirror in new[]{false,true}){
   LiveCornerChecks(perColumn,mirror);
   LiveDrainChecks(perColumn,mirror);
   CapturedPlan(perColumn,mirror);
   var b=new BoardController(3,7);
   int feed=mirror?2:0,first=1,second=mirror?0:2;
   b.blocked[first,1]=true;b.blocked[second,1]=true;
   var c=new CascadeLogic(b);var vb=c.Simulate(perColumn);
   for(int x=0;x<b.Width;x++)for(int y=0;y<b.Height;y++){
    if(b.blocked[x,y])continue;
    if(!c.diagonalFeedReachable[x,y])continue;
    Check(vb[x,y]!=null,""Reachable shadow must fill completely at ""+x+"",""+y);
    var path=vb[x,y].Path;
    for(int i=1;i<path.Count;i++){
     var from=path[i-1];var to=path[i];
     if(from.x==first&&to.x==second)
      Check(from.y==2&&to.y==3,""Second shadow must be fed at its top corner; got ""+from.y+"" -> ""+to.y+"" (mirror=""+mirror+"", perColumn=""+perColumn+"")"");
     Check(!(from.x==second&&to.x==first),""Shadow flow must not turn back into its feeding column"");
    }
   }
  }
  return checks;
 }
 /*PRODUCTION*/
}
";
harness = harness.Replace("/*PRODUCTION*/", virtualTile + "\n" + methods);
var options = ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly);
try {
 var result = CSharpScript.EvaluateAsync<int>(harness + driver + "\nCascadeLogic.Run()", options).GetAwaiter().GetResult();
 Console.WriteLine("PASS: " + result + " diagonal cascade checks");
} catch(Exception ex) {
 Console.Error.WriteLine(ex);
 Environment.Exit(1);
}
