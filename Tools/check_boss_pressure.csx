// Mono csi; extracted production methods with small Unity/board doubles. No project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

string Extract(string path, params string[] names) => string.Join("\n",
    CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes()
        .OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
var pressure = Extract("Assets/_Project/Scripts/Grid/Board/BossDuelObstaclePressure.cs", "IsThrowable", "GetPool", "PickTargets");
var visual = Extract("Assets/_Project/Scripts/Grid/Board/BossDuelController.cs", "RefreshShieldVisual", "GetShieldCharacter");
var pose = Extract("Assets/_Project/Scripts/Grid/Board/BossDuelCharacterView.cs", "SetShieldActive");
var harness = @"
using System;
using System.Collections.Generic;
public struct Vector2Int {
 public int x,y; public Vector2Int(int x,int y){this.x=x;this.y=y;}
 public static Vector2Int one=>new Vector2Int(1,1);
 public static bool operator==(Vector2Int a,Vector2Int b)=>a.x==b.x&&a.y==b.y;
 public static bool operator!=(Vector2Int a,Vector2Int b)=>!(a==b);
 public override bool Equals(object o)=>o is Vector2Int && this==(Vector2Int)o;
 public override int GetHashCode()=>x*397^y;
}
public struct Vector2 { public static Vector2 one=>new Vector2(); public static Vector2 operator*(Vector2 v,float f)=>v; }
public static class Mathf {
 public static int Min(int a,int b)=>Math.Min(a,b); public static int Max(int a,int b)=>Math.Max(a,b);
 public static float Max(float a,float b)=>Math.Max(a,b);
 public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(b,v));
}
public static class Random { public static int Range(int a,int b)=>a; }
public enum ObstacleId { None,Oil,Mud,SpreadingGel,plastic,plastic_orange,Plastic_Yellow,Plastic_Blue,Plastic_Red,Plastic_Green,HelmetPorcelain,PlasticTwoStage,chest1,Cargo,PlayerShieldPickup,EnemyShieldPickup,GoldMoney,HatLauncher }
public enum TileSpecial { None,LineH }
public class Def { public Vector2Int size=Vector2Int.one; public object sprite=new object(); public object GetPreviewSprite()=>sprite; }
public class Library { public Def def=new Def(); public Def Get(ObstacleId id)=>def; }
public class LevelData { public Library obstacleLibrary=new Library(); public ObstacleId[] bossThrownObstacles; public int bossMaxPressureObstacles=8; }
public class Tile { public bool IsRuntimeIdle=true; public TileSpecial special; public TileSpecial GetSpecial()=>special; }
public class Service {
 public Dictionary<Vector2Int,ObstacleId> cells=new Dictionary<Vector2Int,ObstacleId>();
 public HashSet<Vector2Int> locked=new HashSet<Vector2Int>();
 public ObstacleId GetObstacleIdAt(int x,int y)=>cells.TryGetValue(new Vector2Int(x,y),out var id)?id:ObstacleId.None;
 public bool HasObstacleAt(int x,int y)=>GetObstacleIdAt(x,y)!=ObstacleId.None;
 public bool IsInteractionLockedAt(int x,int y)=>locked.Contains(new Vector2Int(x,y));
}
public class BoardController {
 public int Width=4,Height=4; public LevelData ActiveLevelData=new LevelData(); public Service ObstacleStateService=new Service();
 public bool[,] Holes=new bool[4,4]; public Tile[,] Tiles=new Tile[4,4]; public object[,] GridData=new object[4,4];
 public HashSet<Vector2Int> reserved=new HashSet<Vector2Int>(); public bool rejectAll;
 public BoardController(){for(int y=0;y<4;y++)for(int x=0;x<4;x++){Tiles[x,y]=new Tile();GridData[x,y]=new object();}}
 public bool IsReservedTileTargetCell(int x,int y)=>reserved.Contains(new Vector2Int(x,y));
 public bool HasAnyPlayableSwapWithAdditionalLockedCells(IReadOnlyCollection<Vector2Int> cells){
  // The board double has exactly one playable pair at (2,3)/(3,3).
  if(rejectAll)return false;foreach(var c in cells)if(c.y==3&&c.x>=2)return false;return true;
 }
}
public static class Pressure { " + pressure + @" }
public class GameObject { public bool active; public void SetActive(bool value){active=value;} }
public class Rect { public float width=100,height=100; }
public class RectTransform { public Rect rect=new Rect(); public Vector2 sizeDelta; }
public class Image { public GameObject gameObject=new GameObject(); public RectTransform rectTransform=new RectTransform(); }
public class BossDuelCharacterView {
 public bool shieldActive,attacking,finished,HasShieldPose; public int redraws;
 public void ShowRestPose(){redraws++;}
 " + pose + @"
}
public class Visual {
 public BossDuelCharacterView playerCharacterView,enemyCharacterView;
 public BossDuelCharacterView character { get=>enemyCharacterView; set=>enemyCharacterView=value; }
 public RectTransform playerRobot=new RectTransform(),enemyRobot=new RectTransform();
 public float shieldBubbleMinSize=180,shieldBubbleScale=1.25f;
 public void Refresh(RectTransform robot,Image bubble,int protection)=>RefreshShieldVisual(robot,bubble,protection);
 " + visual + @"
}
public static class Checks {
 static int count; static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 public static int Run(){
  var level=new LevelData();
  foreach(var id in new[]{ObstacleId.chest1,ObstacleId.Cargo,ObstacleId.PlayerShieldPickup,ObstacleId.EnemyShieldPickup,ObstacleId.GoldMoney,ObstacleId.HatLauncher})
   Check(!Pressure.IsThrowable(level,id),""No furniture, generator or reward projectiles"");
  Check(Pressure.GetPool(level)[0]==ObstacleId.Oil,""Legacy oil fallback"");
  level.bossThrownObstacles=new[]{ObstacleId.chest1};Check(Pressure.GetPool(level).Count==0,""Invalid explicit pool does not silently fall back"");
  level.bossThrownObstacles=new[]{ObstacleId.Oil,ObstacleId.Oil,ObstacleId.plastic};Check(Pressure.GetPool(level).Count==2,""Pool deduplicates"");
  level.obstacleLibrary.def.size=new Vector2Int(2,2);Check(!Pressure.IsThrowable(level,ObstacleId.Oil),""No multi-cell objects"");
  var b=new BoardController();var pool=new List<ObstacleId>{ObstacleId.Oil};
  Check(Pressure.PickTargets(b,pool,100).Count==4,""Quarter-board pressure cap"");
  b.ActiveLevelData.bossMaxPressureObstacles=2;Check(Pressure.PickTargets(b,pool,100).Count==2,""Authored cap"");
  b.ObstacleStateService.cells[new Vector2Int(0,0)]=ObstacleId.Oil;
  Check(Pressure.PickTargets(b,pool,100).Count==1,""Already present pressure consumes budget"");
  b.ObstacleStateService.cells[new Vector2Int(1,0)]=ObstacleId.Oil;
  Check(Pressure.PickTargets(b,pool,100).Count==0,""Full board budget skips volley"");
  b=new BoardController();b.Holes[0,0]=true;b.Tiles[1,0].special=TileSpecial.LineH;
  b.Tiles[2,0].IsRuntimeIdle=false;b.reserved.Add(new Vector2Int(3,0));
  b.ObstacleStateService.locked.Add(new Vector2Int(0,1));b.Tiles[1,1]=null;
  b.GridData[2,1]=null;b.ObstacleStateService.cells[new Vector2Int(3,1)]=ObstacleId.chest1;
  foreach(var target in Pressure.PickTargets(b,pool,100))Check(target.y>=2,""Skip holes, specials, moving/reserved/locked/occupied/empty cells"");
  b.rejectAll=true;Check(Pressure.PickTargets(b,pool,100).Count==0,""Never remove the last playable swap"");
  Check(Pressure.PickTargets(b,new List<ObstacleId>(),10).Count==0,""Empty pool safe"");
  var v=new Visual{character=new BossDuelCharacterView()};var bubble=new Image();var robot=v.enemyRobot;
  v.Refresh(robot,bubble,2);Check(v.character.shieldActive&&v.character.redraws==1&&!bubble.gameObject.active,""Profile without shield artwork never enables the old ring"");
  v.Refresh(robot,bubble,1);Check(v.character.shieldActive&&v.character.redraws==1,""Remaining protection keeps pose"");
  v.Refresh(robot,bubble,0);Check(!v.character.shieldActive&&v.character.redraws==2,""Consumed protection clears pose"");
  v.character.attacking=true;v.Refresh(robot,bubble,2);Check(v.character.shieldActive&&v.character.redraws==2,""Pickup during attack stores guard without replacing attack pose"");
  v.character=null;v.Refresh(robot,bubble,2);Check(bubble.gameObject.active,""Only legacy actors without a character view use the bubble"");
  v.Refresh(robot,bubble,0);Check(!bubble.gameObject.active,""Fallback clears when protection is gone"");
  return count;
 }
}
Checks.Run()";
var result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.Queue<>).Assembly, typeof(System.Collections.Generic.HashSet<>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " isolated projectile eligibility, target safety, pressure cap and shield-visibility checks");
