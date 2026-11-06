// Actual normal-tile MatchFinder + Dimerdal conversion/collection methods, with Unity doubles.
#load "check_dimerdal.csx"
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
var tileSpecial=parse("Assets/_Project/Scripts/Grid/TileModel.cs").DescendantNodes().OfType<EnumDeclarationSyntax>().Single(e=>e.Identifier.Text=="TileSpecial").ToFullString();
var setSpecial=parse("Assets/_Project/Scripts/Grid/TileView.cs").DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="SetSpecial").ToFullString();
var matchSource=source.Replace("public enum TileSpecial {None,LineH}",tileSpecial)
 .Replace("public class TileModel {public TileType type;", "public class TileModel {public TileSpecial special;public void SetSpecial(TileSpecial value){special=value;}public TileType type;")
 .Replace("public TileType GetTileType()=>color;", "public void SetCoords(int x,int y){X=x;Y=y;}public TileType GetTileType()=>color;")
 .Replace("public TileSpecial special;public bool HasActiveLandingVisual;public bool CageGrace", "public TileSpecial special{get=>model.special;set=>model.special=value;}public BoardController board;public void RefreshIcon(){}public void ApplyTileSize(int size){}"+setSpecial+"public bool HasActiveLandingVisual;public bool CageGrace")
 .Replace("public object[,]GridData=new object[8,8];", "public TileData[,]GridData=new TileData[8,8];")
 .Replace("GridData[x,y]=tiles[x,y]!=null?new object():null;", "var tile=tiles[x,y];GridData[x,y]=tile!=null?new TileData(x,y,tile.GetTileType()):null;if(tile!=null)GridData[x,y].SetSpecial(tile.GetSpecial());")
 .Replace("b.GridData[6,6]=new object();", "b.SyncTileData(6,6);")
 .Replace("public bool IsInteractionLockedAt(int x,int y)", "public bool IsOilAt(int x,int y)=>false;public bool IsInteractionLockedAt(int x,int y)")
 .Replace("public static int Max(int a,int b)", "public static int Abs(int a)=>Math.Abs(a);public static int Max(int a,int b)")
 .Replace("public struct Vector2Int {", "public struct Vector2Int {public static bool operator==(Vector2Int a,Vector2Int b)=>a.x==b.x&&a.y==b.y;public static bool operator!=(Vector2Int a,Vector2Int b)=>!(a==b);");
matchSource+=production("Grid/TileData.cs")+production("Grid/Board/MatchFinder.cs");
var matchChecks=@"
public static class DiamondMatchChecks {
 static int count;static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
 static BoardController Board(){var b=new BoardController{ObstacleStateService=new ObstacleStateService{level=new LevelData{goals=new[]{new LevelGoalDefinition{targetType=LevelGoalTargetType.Collectible,collectibleId=CollectibleId.Diamond,amount=20}}}}};b.ObstacleStateService.board=b;DimerdalService.Ensure(b).Build(b.ActiveLevelData);return b;}
 static TileView Put(BoardController b,int x,int y,TileType type){var t=new TileView{board=b,X=x,Y=y,color=type};b.tiles[x,y]=t;b.SyncTileData(x,y);return t;}
 static void Tick(BoardController b){for(int i=0;i<40;i++){Time.frameCount++;typeof(DimerdalService).GetMethod(""Update"",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b.Dimerdals,null);}}
 public static void Run(){
  foreach(var color in new[]{TileType.DiamondBlue,TileType.DiamondGreen,TileType.DiamondYellow,TileType.DiamondRed}){
   var b=Board();var m=new MatchFinder(b);for(int x=1;x<=3;x++)Put(b,x,3,color);
   Check(m.FindAllMatches(false).Count==3,""three same-color diamonds match via actual MatchFinder"");
   Check(m.FindMatchesAt(2,3).Count==3,""diamond view matching agrees with data matching"");
   Check(!b.ObstacleStateService.HasObstacleAt(2,3),""diamond is ordinary tile content, no obstacle stamp"");
   Put(b,4,3,color);m.InvalidateRunCache();Check(m.DecideSpecialAt(2,3)==TileSpecial.LineH,""four diamonds can create a normal line special"");
   Put(b,5,3,color);m.InvalidateRunCache();Check(m.DecideSpecialAt(2,3)==TileSpecial.SystemOverride,""five diamonds can create Override"");
   var t=b.tiles[2,3];t.SetSpecial(TileSpecial.LineH);Check(b.jobs==1&&b.TopHud.collected==0,""special formation collects consumed diamond on arrival"");b.CollectDimerdalDiamond(t);Tick(b);Check(b.TopHud.collected==1,""later special clear cannot collect base diamond twice"");
  }
  foreach(var other in new[]{TileType.Bolt,TileType.Plate,TileType.Gear,TileType.Core,TileType.Key,TileType.DiamondGreen,TileType.DiamondYellow,TileType.DiamondRed}){
   var b=Board();Put(b,1,3,TileType.DiamondBlue);Put(b,2,3,TileType.DiamondBlue);Put(b,3,3,other);Check(new MatchFinder(b).FindAllMatches(false).Count==0,""diamonds never mix with ordinary stones, keys or other colors"");
  }
  {
   var b=Board();Put(b,1,3,TileType.Bolt);Put(b,2,3,TileType.Bolt);Put(b,3,3,TileType.Bolt);var d=Put(b,2,4,TileType.DiamondBlue);var m=new MatchFinder(b);Check(m.FindAllMatches(false).Count==3&&d.GetTileType()==TileType.DiamondBlue&&!d.DimerdalCollectionStarted,""neighbor normal match leaves diamond intact"");
  }
  {
   var b=Board();Put(b,1,3,TileType.DiamondBlue);Put(b,2,3,TileType.DiamondBlue);Put(b,3,3,TileType.Core);Put(b,3,4,TileType.DiamondBlue);var m=new MatchFinder(b);Check(m.IsPlayableSwap(3,3,3,4)&&m.HasAnyPlayableSwap(),""normal-to-diamond swap and playable-move detection work"");
   var t=b.tiles[3,4];b.CollectDimerdalDiamond(t);b.CollectDimerdalDiamond(t);Tick(b);Check(b.TopHud.collected==1,""normal clear collection callback counts once"");
  }
  Console.WriteLine(""PASS: ""+count+"" real MatchFinder / diamond special-formation and collection checks."");
 }
}
";
await CSharpScript.RunAsync(matchSource+matchChecks+"\nDiamondMatchChecks.Run();",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
