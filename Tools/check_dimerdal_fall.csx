// Actual continuous-fall motor with diamonds placed while idle, before gravity starts.
#load "check_continuous_fall.csx"
using System;using System.Linq;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;
var applyDiamond=string.Join("\n",tileRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"ApplyDimerdalDiamond","ApplyDimerdalIconBounds"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var dimerdalMotorHarness=harness
 .Replace("public class TileModel {public TileSpecial special;}",File.ReadAllText("Assets/_Project/Scripts/Grid/TileType.cs")+" public enum ObstacleId {None} public class Sprite {} public class Image {public Sprite sprite;public bool preserveAspect;public RectTransform rectTransform=new RectTransform();} public class TileModel {public TileSpecial special;public TileType type;public void SetType(TileType value){type=value;}}"+File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Obstacles/DimerdalRules.cs"))
 .Replace("public struct Vector2 {", "public struct Vector2 {public static Vector2 one=>new Vector2(1,1);public static Vector2 zero=>new Vector2();")
 .Replace("public class RectTransform {", "public struct Vector3 {public static Vector3 one=>new Vector3();}public class RectTransform {public Vector3 localScale;public Vector2 anchorMin,anchorMax,pivot,sizeDelta;")
 .Replace("public bool isMovableObstacleTile,useFullCellIcon=true;", "public bool isMovableObstacleTile,useFullCellIcon=true,isFullCellMovableSprite,DimerdalCollectionStarted;public int lastAppliedTileSize=100;public Sprite movableObstacleSprite;public Image iconImage=new Image();"+applyDiamond);
var motorChecks=@"
public static class DimerdalMotorChecks {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static Vector2 P(int x,int y)=>new Vector2(x*100,-y*100);
 static TileView Tile(BoardController b,int x,int y,Vector2 start){var t=new TileView{board=b,X=x,Y=y};t.RectTransform.anchoredPosition=start;b.Tiles[x,y]=t;return t;}
 static void ConvertIdle(TileView t){Check(t.IsRuntimeIdle,""diamond placed while idle"");int lifetime=t.LifetimeVersion,token=t.token;var pos=t.RectTransform.anchoredPosition;t.ApplyDimerdalDiamond(TileType.DiamondBlue,new Sprite());Check(t.LifetimeVersion==lifetime&&t.token==token&&(t.RectTransform.anchoredPosition-pos).sqrMagnitude==0,""placement preserves root and owner"");Check(!t.isMovableObstacleTile&&t.model.type==TileType.DiamondBlue,""diamond enters normal tile layer"");}
 static TileFallMotionSystem.Ticket Start(TileFallMotionSystem m,TileView t,Vector2[] path)=>m.Start(t,t.LifetimeVersion,t.PlannedFallGeneration,path,new Vector2Int(t.X,t.Y),false,0,true,.18f,.5f);
 public static int Run(){
  var routes=new[]{new[]{P(1,0),P(1,7)},new[]{P(0,-2),P(0,1),P(1,1),P(1,7)},new[]{P(0,0),P(0,2),P(1,2),P(1,4),P(2,4),P(2,7)}};
  foreach(var route in routes)foreach(float speed in new[]{5f,8f,12f}){
   var a=new BoardController{LandingResponseEnabled=true,v0=speed};var b=new BoardController{LandingResponseEnabled=true,v0=speed};var ma=new TileFallMotionSystem(a);var mb=new TileFallMotionSystem(b);
   int x=(int)(route[route.Length-1].x/100);var normal=Tile(a,x,7,route[0]);var diamond=Tile(b,x,7,route[0]);ConvertIdle(diamond);var ta=Start(ma,normal,route);var tb=Start(mb,diamond,route);
   for(int frame=0;frame<300;frame++){
    ma.Tick(.01f);mb.Tick(.01f);
    Check((normal.RectTransform.anchoredPosition-diamond.RectTransform.anchoredPosition).sqrMagnitude<.0001f,""diamond follows identical normal vertical/diagonal/spawn trajectory"");
    Check(normal.token==diamond.token&&normal.RuntimeState==diamond.RuntimeState&&ta.Done==tb.Done,""normal ticket/state/token remain in lockstep"");
   }
   Check(tb.Done&&diamond.arrivals==normal.arrivals&&diamond.RuntimeState==TileRuntimeState.Idle,""diamond completes fall normally: speed=""+speed+"" route=""+route.Length+"" done=""+tb.Done+"" normalDone=""+ta.Done+"" state=""+diamond.RuntimeState+"" normalState=""+normal.RuntimeState+"" y=""+diamond.RectTransform.anchoredPosition.y);Check(b.reservations.Count==0&&!mb.HasWork,""diamond releases all motion reservations"");
  }
  {
   var a=new BoardController{v0=10,accel=0,vmax=10};var b=new BoardController{v0=10,accel=0,vmax=10};var ma=new TileFallMotionSystem(a);var mb=new TileFallMotionSystem(b);
   var al=Tile(a,0,5,P(0,0));var au=Tile(a,0,4,P(0,-1));var bl=Tile(b,0,5,P(0,0));var bu=Tile(b,0,4,P(0,-1));ConvertIdle(bl);ConvertIdle(bu);Start(ma,al,new[]{P(0,0),P(0,5)});Start(ma,au,new[]{P(0,-1),P(0,4)});Start(mb,bl,new[]{P(0,0),P(0,5)});Start(mb,bu,new[]{P(0,-1),P(0,4)});
   for(int frame=0;frame<180;frame++){
    if(frame==20){al.Y=bl.Y=7;al.PlannedFallGeneration=bl.PlannedFallGeneration=2;Start(ma,al,new[]{P(0,5),P(0,7)});Start(mb,bl,new[]{P(0,5),P(0,7)});}
    ma.Tick(.01f);mb.Tick(.01f);Check((al.RectTransform.anchoredPosition-bl.RectTransform.anchoredPosition).sqrMagnitude<.0001f&&(au.RectTransform.anchoredPosition-bu.RectTransform.anchoredPosition).sqrMagnitude<.0001f,""route extension and follower spacing equal normal stones"");
   }
   Check(!mb.HasWork&&b.reservations.Count==0,""extended diamond routes finish without stuck jobs"");
  }
  return checks;
 }
}
";
var diamondMotionCount=await CSharpScript.EvaluateAsync<int>("using System.Collections;\n"+dimerdalMotorHarness+motor+coordinator+motorChecks+"\nDimerdalMotorChecks.Run()",options);
Console.WriteLine("PASS: "+diamondMotionCount+" actual motor comparisons for normal diamond gravity, diagonal flow, follower spacing and cleanup.");
