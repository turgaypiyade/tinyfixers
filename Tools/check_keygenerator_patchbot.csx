// Run: csi Tools/check_keygenerator_patchbot.csx. Production targeting methods, no project build.
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

string Read(string file) => File.ReadAllText("Assets/_Project/Scripts/" + file);
string Source(string file) => Read(file).Replace("using UnityEngine;", "").Replace("using System.Collections.Generic;", "");
string Methods(string file, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(Read(file)).GetRoot()
    .DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
string Nested(string file, string name) => CSharpSyntaxTree.ParseText(Read(file)).GetRoot().DescendantNodes()
    .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == name).ToFullString().Replace("private sealed class", "public sealed class");

var hatEligibility = CSharpSyntaxTree.ParseText(Read("Grid/Board/Obstacles/ObstacleStateService.cs")).GetRoot()
    .DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "GetActiveMeaningfulHitsAt")
    .Body.Statements.OfType<IfStatementSyntax>().Single(i => i.Condition.ToString() == "id == ObstacleId.HatLauncher").ToFullString();

var harness = @"
using System;
using System.Collections.Generic;
public struct Vector2Int {public int x,y; public Vector2Int(int x,int y){this.x=x;this.y=y;} public static Vector2Int zero=>new Vector2Int();}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static int Abs(int a)=>Math.Abs(a);}
public static class Random {public static int Range(int a,int b)=>a;}
public static class Time {public static float unscaledTime;}
public enum ObstacleId {None,KeyGenerator,SpreadingGel,Hamster,Tube,EnergyContainer,HatLauncher,Grass,GrassFlower,Stone}
public enum TileType {Gear,Key}
public enum TileSpecial {None,PatchBot}
public enum LevelGoalTargetType {Obstacle,Tile,Collectible}
public enum CollectibleId {None,EnergyOrb}
public class TileView {public int X,Y;public TileType Type;public TileSpecial Special;public bool GelContaminated;public static implicit operator bool(TileView t)=>t!=null;public TileType GetTileType()=>Type;public TileSpecial GetSpecial()=>Special;}
public class TopHudController {
 public struct ActiveGoal {public LevelGoalTargetType targetType;public ObstacleId obstacleId;public TileType tileType;public CollectibleId collectibleId;}
 public bool GelGoal,OrbGoal=true;
 public bool HasGoalForCollectible(CollectibleId id)=>OrbGoal;
 public void GetActiveGoals(List<ActiveGoal> goals){goals.Add(new ActiveGoal{targetType=LevelGoalTargetType.Tile,tileType=TileType.Key});if(GelGoal)goals.Add(new ActiveGoal{targetType=LevelGoalTargetType.Obstacle,obstacleId=ObstacleId.SpreadingGel});}
}
public class ObstacleStateService {
 public ObstacleId[,] ids=new ObstacleId[3,3];public bool CanProduce=true;public Func<bool> HatLauncherCanProduceQuery;public Action<int> HatLauncherHitInterceptor;public int OriginReads;
 public Dictionary<int,int> Origins=new Dictionary<int,int>();
 public ObstacleId GetObstacleIdAt(int x,int y)=>ids[x,y];
 public int GetObstacleOriginAt(int x,int y){OriginReads++;return ids[x,y]==ObstacleId.None?-1:Origins.TryGetValue(x,out var origin)?origin:x;}
 public int GetActiveMeaningfulHitsAt(int x,int y){var id=ids[x,y];
" + hatEligibility + @"
 return id==ObstacleId.KeyGenerator?(CanProduce?1:0):1;}
 public bool IsExitAtBottomAt(int x,int y)=>false;public bool IsHitLockedAt(int x,int y)=>false;
 public bool IsFullyDisabledAt(int x,int y)=>false;public bool IsUnderTileObstacleAt(int x,int y)=>false;
 public bool HasGrassFlowerAt(int origin)=>false;public bool IsMovableObstacleAt(int x,int y)=>false;
}
public class BoardController {
 public int Width=3,Height=3;public bool KeyGeneratorProductionComplete;
 public TileView[,] Tiles=new TileView[3,3];public object[,] GridData=new object[3,3];public bool[,] Holes=new bool[3,3];
 public ObstacleStateService ObstacleStateService=new ObstacleStateService();public TopHudController TopHud=new TopHudController();
 public bool IsGelSpreadActiveThisMove;public SpreadingGelService SpreadingGelService=new SpreadingGelService();public bool IsMaskHoleCell(int x,int y)=>Holes[x,y];
 public BoardTargetPool TargetPool;public BoardController(){TargetPool=new BoardTargetPool(this);}
}
public class SpreadingGelService {public bool[,] Covered=new bool[3,3];public bool IsGelAt(int x,int y)=>Covered[x,y];public bool IsSpreadSourceAt(int x,int y)=>IsGelAt(x,y);}
public class HatProducer {
 public int totalGroupReleased,Capacity=2;public int TotalCapacity=>Capacity;public TopHudController topHud;
 public CollectibleId ResolveCollectibleId()=>CollectibleId.EnergyOrb;
" + Methods("Grid/Board/Obstacles/HatLauncherService.cs", "CanProduce").Replace("private bool", "public bool") + @"
}
public static class SpecialUtils {public static bool CanTargetTileContent(BoardController b,int x,int y)=>b.ObstacleStateService.GetObstacleIdAt(x,y)!=ObstacleId.KeyGenerator;}
public class PatchbotComboService {
 readonly BoardController board;public PatchbotComboService(BoardController b){board=b;}
 public bool HasObstacleAt(int x,int y)=>board.ObstacleStateService.GetObstacleIdAt(x,y)!=ObstacleId.None;
 " + Methods("Grid/Board/PatchbotComboService.cs", "ShouldPreferNonGelCells", "IsGelSpreadTarget") + @"
 public void AddCargoDropPathTarget(int x,int y,TileView partner,List<(int x,int y,TileView tile)> list,Func<TileView,bool> excluded){}
 public Action Arrival; public Vector2Int From,To;
 public void EnqueueDash(TileView from,int x,int y,TileView carried,Action start,Action arrived){From=new Vector2Int(from.X,from.Y);To=new Vector2Int(x,y);Arrival=arrived;}
" + Methods("Grid/Board/PatchbotComboService.cs", "EnqueueDashFromIntent", "IsInside") + @"
}
" + Source("Grid/Board/Targeting/BoardTargetPool.cs") + Source("Grid/Board/Actions/PatchBotTargetCoordinator.cs") + Source("Grid/Board/Actions/PatchbotLiveDashTargetRegistry.cs").Replace("using System;", "") + @"
public class Airborne {
 BoardController board;public Airborne(BoardController b){board=b;}
 public class AirborneBot {public PatchBotIntent intent;public Vector2Int sourceCell;public bool hasTarget;public int targetX,targetY;}
" + Methods("Grid/Board/Actions/OverridePatchBotAirborneGroupAction.cs", "RefreshDiveTarget").Replace("private bool", "public bool") + @"
}
public static class Checks {
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 static BoardController Board(){var b=new BoardController();b.ObstacleStateService.ids[0,0]=ObstacleId.KeyGenerator;
  b.Tiles[1,0]=new TileView{X=1,Y=0,Type=TileType.Key};b.GridData[1,0]=new object();return b;}
 public static int Run(){
  var b=Board();var service=new PatchbotComboService(b);var c=new PatchBotTargetCoordinator(b,service);
  var first=c.PickIntentFrom(Vector2Int.zero).intent;
  Check(first.IsObstacle&&first.InitialCell.x==0,""Active generator is eligible"");
  b.ObstacleStateService.OriginReads=0;
  for(int i=0;i<100;i++)c.ResolveIntentFrom(first,Vector2Int.zero);
  Check(b.ObstacleStateService.OriginReads==100,""Live obstacle resolution validates one cell per tick without repeated board scans"");
  b.KeyGeneratorProductionComplete=true;
  Check(!b.TargetPool.IsHittableObstacleCell(0,0)&&!b.TargetPool.IsAnyHitTarget(0,0),""Completed generator is excluded even if stage/query still reports a hit"");
  var next=c.ResolveIntentFrom(first,Vector2Int.zero);
  Check(next.hasCell&&next.intent.TargetTile==b.Tiles[1,0],""Existing generator intent retargets to a remaining key"");
  Check(b.TargetPool.ReservedObstacleHits(0)==0&&c.ActiveBotCount==1,""Retarget releases old reservation exactly once"");
  c.ReleaseIntent(next.intent);
  b.ObstacleStateService.ids[1,0]=ObstacleId.SpreadingGel;
  var gelPick=c.PickIntentFrom(Vector2Int.zero);
  Check(gelPick.intent.IsTile&&!gelPick.intent.IsObstacle&&gelPick.intent.IsAlive(b),""Key on gel remains a live tile intent"");
  c.ReleaseIntent(gelPick.intent);
  b.KeyGeneratorProductionComplete=false;b.ObstacleStateService.CanProduce=false;
  Check(b.TargetPool.ObstacleCapacityAt(0,0)==0,""Quota committed but keys still flying excludes the generator"");
  b.ObstacleStateService.CanProduce=true;b.ObstacleStateService.ids[1,0]=ObstacleId.None;
  var bot=new Airborne.AirborneBot{intent=c.PickIntentFrom(Vector2Int.zero).intent};
  var airborne=new Airborne(b);b.KeyGeneratorProductionComplete=true;
  Check(airborne.RefreshDiveTarget(bot,c)&&bot.targetX==1,""Pulse combo switches away from a generator exhausted during its dive"");
  b.GridData[1,0]=null;b.Tiles[1,0]=null;
  Check(!airborne.RefreshDiveTarget(bot,c)&&bot.intent==null&&c.ActiveBotCount==0,""No replacement clears airborne intent without duplicate release"");
  b=Board();service=new PatchbotComboService(b);c=new PatchBotTargetCoordinator(b,service);
  var source=new TileView{X=2,Y=0};first=c.PickIntentFrom(new Vector2Int(2,0)).intent;
  int hitX=99;PatchBotIntent arrivalIntent=null;
  service.EnqueueDashFromIntent(source,first,c,onArrived:(x,y,i)=>{hitX=x;arrivalIntent=i;c.ReleaseIntent(i);});
  PatchbotLiveDashTargetRegistry.TryAcquireLiveResolver(service.From,service.To,out var resolve);
  Check(resolve().Value.x==0,""Dash starts with generator intent"");
  b.KeyGeneratorProductionComplete=true;
  Check(resolve().Value.x==1,""Live solo/line dash retargets to remaining key"");
  b.GridData[1,0]=null;b.Tiles[1,0]=null;
  Check(!resolve().HasValue,""No valid target returns null"");service.Arrival();
  Check(hitX==-1&&arrivalIntent==null&&c.ActiveBotCount==0,""Cancelled arrival does not reuse generator coordinates or release twice"");
  b=Board();service=new PatchbotComboService(b);c=new PatchBotTargetCoordinator(b,service);
  first=c.PickIntentFrom(Vector2Int.zero).intent;
  service.EnqueueDashFromIntent(source,first,c,onArrived:(x,y,i)=>{hitX=x;c.ReleaseIntent(i);});
  b.KeyGeneratorProductionComplete=true;service.Arrival();
  Check(hitX==1&&c.ActiveBotCount==0,""No-VFX arrival also resolves completed generator to key"");
  b=Board();b.ObstacleStateService.ids[0,0]=ObstacleId.None;b.ObstacleStateService.ids[2,0]=ObstacleId.Stone;
  b.ObstacleStateService.Origins[2]=0;
  var moved=new PatchBotIntent{ObstacleOriginIndex=0,InitialCell=Vector2Int.zero};
  Check(moved.CurrentCell(b).x==2,""Changed footprint falls back to a surviving cell"");
  b.ObstacleStateService.OriginReads=0;
  Check(moved.CurrentCell(b).x==2&&b.ObstacleStateService.OriginReads==1,""Surviving footprint cell becomes the new fast path"");
  b=new BoardController();service=new PatchbotComboService(b);c=new PatchBotTargetCoordinator(b,service);
  var hat=new HatProducer{topHud=b.TopHud};b.ObstacleStateService.ids[0,0]=ObstacleId.HatLauncher;
  b.ObstacleStateService.HatLauncherCanProduceQuery=hat.CanProduce;b.ObstacleStateService.HatLauncherHitInterceptor=_=>{};
  b.Tiles[1,0]=new TileView{X=1,Y=0};b.GridData[1,0]=new object();
  first=c.PickIntentFrom(Vector2Int.zero).intent;
  Check(first.IsObstacle,""Active hat selected"");hat.totalGroupReleased=hat.Capacity;
  next=c.ResolveIntentFrom(first,Vector2Int.zero);
  Check(next.hasCell&&!next.intent.IsObstacle&&b.TargetPool.ReservedObstacleHits(0)==0,""Exhausted hat retargets while last orbs still flying"");c.ReleaseIntent(next.intent);
  hat.totalGroupReleased=0;b.TopHud.OrbGoal=false;
  Check(!hat.CanProduce()&&!b.TargetPool.IsAnyHitTarget(0,0),""HUD goal completion excludes hat even if another producer supplied the orbs"");
  b.TopHud.OrbGoal=true;b.ObstacleStateService.HatLauncherHitInterceptor=null;
  Check(b.TargetPool.ObstacleCapacityAt(0,0)==0,""Disconnected/exhausted hat cannot consume a hit even with query still true"");
  b=new BoardController();service=new PatchbotComboService(b);c=new PatchBotTargetCoordinator(b,service);
  b.TopHud.GelGoal=true;b.IsGelSpreadActiveThisMove=true;
  var falling=new TileView{X=1,Y=1};b.Tiles[1,1]=falling;b.GridData[1,1]=new object();
  b.Tiles[2,0]=new TileView{X=2,Y=0,Type=TileType.Key};b.GridData[2,0]=new object();b.SpreadingGelService.Covered[2,0]=true;
  first=c.PickIntentFrom(Vector2Int.zero).intent;
  Check(first.TargetsGelSpreadCell&&first.InitialCell.x==1&&first.InitialCell.y==1,""Gel bot prioritizes clean cell before an already covered goal tile"");
  falling.Y=0;b.Tiles[1,0]=falling;b.GridData[1,0]=new object();
  b.Tiles[1,1]=new TileView{X=1,Y=1};b.GridData[1,1]=new object();
  next=c.ResolveIntentFrom(first,Vector2Int.zero);
  Check(next.cell.y==1&&next.intent==first,""Gel target stays at selected clean cell when original tile falls one row"");
  Check(b.TargetPool.IsCellReserved(new Vector2Int(1,1))&&!b.TargetPool.IsTileReserved(falling),""Gel reservation stays on cell and frees falling tile"");
  var second=c.PickIntentFrom(Vector2Int.zero).intent;
  Check(second.InitialCell.y==0&&second.InitialCell.x==1,""Simultaneous gel bot cannot reserve the same refill cell"");c.ReleaseIntent(second);
  b.IsGelSpreadActiveThisMove=false;b.SpreadingGelService.Covered[1,1]=true;
  next=c.ResolveIntentFrom(first,Vector2Int.zero);
  Check(next.intent.TargetsGelSpreadCell&&next.cell.x==1&&next.cell.y==0,""Covered fixed cell retargets to another clean cell with source already consumed"");
  Check(!b.TargetPool.IsCellReserved(new Vector2Int(1,1)),""Retarget releases old fixed cell reservation"");
  c.ReleaseIntent(next.intent);Check(c.ActiveBotCount==0&&!b.TargetPool.IsCellReserved(new Vector2Int(1,0)),""Arrival/cancellation releases fixed cell reservation"");
  b.TargetPool.ReserveCell(new Vector2Int(1,1));Time.unscaledTime=9;
  Check(!b.TargetPool.IsCellReserved(new Vector2Int(1,1)),""Abandoned fixed cell reservation expires"");Time.unscaledTime=0;
  b.ObstacleStateService.ids[0,0]=ObstacleId.Stone;b.IsGelSpreadActiveThisMove=true;
  first=c.PickIntentFrom(Vector2Int.zero).intent;
  Check(first.IsObstacle,""Breakable obstacles retain priority before gel coverage"");c.ReleaseIntent(first);
  return count;
 }
}
Checks.Run()";
var result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " KeyGenerator/HatLauncher/gel PatchBot regression checks");
