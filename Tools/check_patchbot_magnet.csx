// Run: csi Tools/check_patchbot_magnet.csx. Extracted production methods; no Unity/project build.
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
string Read(string p)=>File.ReadAllText("Assets/_Project/Scripts/"+p);
string Method(string p,string name)=>CSharpSyntaxTree.ParseText(Read(p)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText==name).ToFullString();
var intent=CSharpSyntaxTree.ParseText(Read("Grid/Board/Actions/PatchBotTargetCoordinator.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.ValueText=="PatchBotIntent").ToFullString();
string harness=@"
using System;
using System.Collections.Generic;
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);}
public static class Mathf {public static int Min(int a,int b)=>Math.Min(a,b);public static int Max(int a,int b)=>Math.Max(a,b);}
public enum ObstacleId {None,SpreadingGel,Hamster,KeyGenerator,Tube,Magnet,Safe,Stone,Wall,MetalWall}
public enum TileSpecial {None,LineH,LineV,PulseCore}
public class TileView {public int X,Y;}
public class PatchbotComboService {public bool IsGelSpreadTarget(int x,int y,TileView tile)=>false;}
public static class SpecialUtils {public static bool CanTargetTileContent(BoardController b,int x,int y)=>false;}
public class ObstacleStateService {
 public ObstacleId[,] Ids=new ObstacleId[9,9];public int[,] Origins=new int[9,9];public HashSet<int> Endpoints=new HashSet<int>();public int Reads;
 public ObstacleId GetObstacleIdAt(int x,int y)=>Ids[x,y];
 public int GetObstacleOriginAt(int x,int y){Reads++;return Ids[x,y]==ObstacleId.None?-1:Origins[x,y];}
 public bool IsMagnetEndpoint(int x,int y)=>Endpoints.Contains(y*9+x);
 public bool IsUnbreakableObstacleAt(int x,int y)=>false;public bool IsTarpStatueCoveredAt(int x,int y)=>false;
 public bool IsHitLockedAt(int x,int y)=>false;public bool IsFullyDisabledAt(int x,int y)=>false;public int GetActiveMeaningfulHitsAt(int x,int y)=>1;
}
public class BoardController {
 public int Width=9,Height=9;public bool KeyGeneratorProductionComplete;public ObstacleStateService ObstacleStateService=new ObstacleStateService();
 public TileView[,] Tiles=new TileView[9,9];public object[,] GridData=new object[9,9];public BoardTargetPool TargetPool;
 public BoardController(){TargetPool=new BoardTargetPool(this);}public Vector3 GetCellWorldPosition(int x,int y)=>new Vector3(x*10,y*10,0);
}
public class BoardTargetPool {BoardController board;public BoardTargetPool(BoardController b){board=b;}
"+Method("Grid/Board/Targeting/BoardTargetPool.cs","IsHittableObstacleCell")+@"
}
"+intent+@"
public static class Dash {
"+Method("VFX/PatchbotDashUI.cs","AimWorldPosition")+@"
}
public class Chain {BoardController board;public Chain(BoardController b){board=b;}
"+Method("Grid/Board/Actions/SpecialChainRunner.cs","TryGetObstacleFootprintCenter").Replace("private bool","public bool")+@"
}
public static class Checks {
 static int count;static void Check(bool ok,string label){count++;if(!ok)throw new Exception(label);}
 static BoardController Path(ObstacleId id,int length,bool vertical){var b=new BoardController();for(int i=0;i<length;i++){int x=vertical?2:i+1,y=vertical?i+1:2;b.ObstacleStateService.Ids[x,y]=id;b.ObstacleStateService.Origins[x,y]=vertical?11:19;}b.ObstacleStateService.Endpoints.Add(vertical?11:19);b.ObstacleStateService.Endpoints.Add(vertical?(length*9+2):(18+length));return b;}
 public static int Run(){
  foreach(bool vertical in new[]{false,true})foreach(int length in new[]{3,4,7}){
   var b=Path(ObstacleId.Magnet,length,vertical);var obs=b.ObstacleStateService;
   for(int i=0;i<length;i++){
    int x=vertical?2:i+1,y=vertical?i+1:2;
    Check(b.TargetPool.IsHittableObstacleCell(x,y)==(i==0||i==length-1),""Only current magnet endpoints consume targeted hits"");
   }
   foreach(int i in new[]{0,length-1}){
    int x=vertical?2:i+1,y=vertical?i+1:2;obs.Reads=0;
    var world=Dash.AimWorldPosition(b,x,y);
    Check(world.x==x*10&&world.y==y*10,""Solo/line/airborne bot aims at actual endpoint"");
    Check(obs.Reads==0,""Endpoint presentation does not scan board"");
    Vector2Int center;Check(!new Chain(b).TryGetObstacleFootprintCenter(new Vector2Int(x,y),out center)&&center.x==x&&center.y==y,""Carried pulse explodes at endpoint, not pipe center"");
   }
   var intent=new PatchBotIntent{ObstacleOriginIndex=vertical?11:19,InitialCell=new Vector2Int(vertical?2:1,vertical?1:2)};
   int ax=vertical?2:1,ay=vertical?1:2;
   obs.Ids[ax,ay]=ObstacleId.None;obs.Endpoints.Remove(ay*9+ax);
   int nx=vertical?2:2,ny=vertical?2:2;obs.Endpoints.Add(ny*9+nx);
   var live=intent.CurrentCell(b);Check(obs.IsMagnetEndpoint(live.x,live.y),""Shrink during flight resolves to surviving endpoint"");
   obs.Reads=0;intent.CurrentCell(b);Check(obs.Reads==1,""Updated endpoint uses cached cell on next tick"");
   for(int x=0;x<9;x++)for(int y=0;y<9;y++)obs.Ids[x,y]=ObstacleId.None;
   Check(!intent.IsAlive(b),""Destroyed magnet invalidates intent for retarget/cancellation"");
  }
  var tube=Path(ObstacleId.Tube,7,true);var tubeWorld=Dash.AimWorldPosition(tube,2,1);Vector2Int ignored;
  Check(tubeWorld.x==20&&tubeWorld.y==10&&!new Chain(tube).TryGetObstacleFootprintCenter(new Vector2Int(2,1),out ignored),""Tube base retains cell-based aim and payload"");
  var safe=new BoardController();for(int x=2;x<=3;x++)for(int y=2;y<=3;y++){safe.ObstacleStateService.Ids[x,y]=ObstacleId.Safe;safe.ObstacleStateService.Origins[x,y]=20;}
  var w=Dash.AimWorldPosition(safe,2,2);Check(w.x==25&&w.y==25,""Safe still uses visual footprint center"");
  Check(new Chain(safe).TryGetObstacleFootprintCenter(new Vector2Int(2,2),out ignored)&&ignored.x==3&&ignored.y==3,""Safe payload still uses center cell"");
  return count;
 }
}
Checks.Run()";
Console.WriteLine("PASS: "+CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult()+" PatchBot/magnet endpoint checks");
