// csi Tools/check_level167_hidden_gel.csx — production stamp/clear/reveal methods, no Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;
var spawner=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/GridSpawner.cs")).GetRoot();
var state=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs")).GetRoot();
string Method(Microsoft.CodeAnalysis.SyntaxNode root,string name)=>root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text==name).ToFullString();
var sealBranch=spawner.DescendantNodes().OfType<IfStatementSyntax>().Single(n=>n.Condition.ToString()=="p.beneathId == ObstacleId.SpreadingGel").ToFullString();
var stamped=state.DescendantNodes().OfType<StructDeclarationSyntax>().Single(n=>n.Identifier.Text=="StampedBeneath").ToFullString();
var stateMethods=string.Join("\n",new[]{"RegisterStampedBeneath","TryPopStampedBeneathLayer","RestoreCellObstacle","FreeTubeCell","ClearObstacleFromLevel","ReinitRestoredBeneathOrigin"}.Select(n=>Method(state,n)));
var code=@"
using System;using System.Collections.Generic;
public enum ObstacleId {None=0,Mud=25,ColorChest=21,BatteryBox=20,OverrideBatteryBox=43,Wardrobe=27,Grass=42,Wall=55,MetalWall=56,SpreadingGel=46,LaserCannon=65}
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public struct StackedObstacleEntry {public int originCellIndex;public ObstacleId obstacleId;}
public class Def {public int hits=4;public bool GetBlocksCellsForRemainingHits(int h)=>hits==4;}
public class Library {public Def Get(ObstacleId id)=>new Def{hits=id==ObstacleId.SpreadingGel?1:4};}
public class LevelData {public int width=9,height=11;public int[] obstacles=new int[99],obstacleOrigins=new int[99];public Library obstacleLibrary=new Library();public Vector2Int GetObstacleFootprint(ObstacleId id,int origin,Def def)=>new Vector2Int(2,2);}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);}
public static class Time {public static float unscaledTime=3;}
public class Gel {
 public bool present,sealedCell,hidden;public int x,y;public List<string> events;
 public bool AddGelImmediate(int x,int y){this.x=x;this.y=y;present=true;return true;}
 public void SealCell(int x,int y,bool hideVisual){sealedCell=true;hidden=hideVisual;}
 public void AddGel(int x,int y){AddGelImmediate(x,y);events.Add(""add"");}
 public void UnsealCell(int x,int y){sealedCell=false;hidden=false;events.Add(""unseal"");}
 public int Count=>present&&!sealedCell?1:0;
}
public class Spawner {
 public List<(int cell,ObstacleId beneathId,int beneathOrigin,int overOrigin)> pendingStampedBeneath=new List<(int,ObstacleId,int,int)>();
 public Gel spreadingGelOverlayService;
 bool IsMovableContent(LevelData l,int c)=>false;bool HasPendingCoveringOverlay(int c)=>false;
 List<(int cell,ObstacleId overlayId)> pendingCoveringOverlays=new List<(int,ObstacleId)>();
 void MaterializePendingCoveringOverlay(LevelData l,int c){}
 /* STAMP */
 /* REVEAL */
 public void Stamp(LevelData l){StampStackedEntry(l,new StackedObstacleEntry{originCellIndex=81,obstacleId=ObstacleId.LaserCannon});}
 public void PreDraw(){int gelSpawned=0;bool coverHidesGround=true;
  foreach(var p in pendingStampedBeneath){int bx=p.cell%9,by=p.cell/9;/* SEAL */}
 }
 public void Reveal(int x,int y){RevealGelCell(x,y);}
}
public class State {
 public LevelData level;public Library library=new Library();
 public Action<int> OnCellUnlocked;public Action<int,ObstacleId> OnObstacleDestroyed;public Action<int,int> RequestObstacleViewCreate;
 public int[] remainingHitsByOrigin=new int[99];
 const float RevealGraceSeconds=.6f;Dictionary<int,float> _revealGraceUntil=new Dictionary<int,float>();
 Dictionary<int,StampedBeneath> _underTileBeneathMovable=new Dictionary<int,StampedBeneath>();
 Dictionary<int,List<StampedBeneath>> _stampedBeneathByCell=new Dictionary<int,List<StampedBeneath>>();
 Dictionary<(ObstacleId,int),int> wallRemainingHits=new Dictionary<(ObstacleId,int),int>();
 Dictionary<int,int> _chestColorStates=new Dictionary<int,int>(),_batteryHitsByOrigin=new Dictionary<int,int>(),_overrideBatteryHitsByOrigin=new Dictionary<int,int>(),_overrideBatteryProgressByOrigin=new Dictionary<int,int>(),_wardrobeItemCounts=new Dictionary<int,int>();
 bool RemoveStampedObstacleBeneath(int cell,ObstacleId id,int origin)=>false;
 void InitObstacleStateAt(int origin,int remainingOverride=-1){remainingHitsByOrigin[origin]=1;}
 void ShedGrassFlowerIfAny(int origin){}void CompleteWaterLily(int origin){}
 /* STRUCT */
 /* STATE */
 public void Break(){ClearObstacleFromLevel(81,ObstacleId.LaserCannon);}
}
var level=new LevelData();for(int i=0;i<99;i++)level.obstacleOrigins[i]=-1;
level.obstacles[90]=46;level.obstacleOrigins[90]=90;
level.obstacles[85]=65;level.obstacleOrigins[85]=85; // independent neighbor must survive
var events=new List<string>();var gel=new Gel{events=events};var sp=new Spawner{spreadingGelOverlayService=gel};
int checks=0;Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
sp.Stamp(level);
foreach(int i in new[]{81,82,90,91})check(level.obstacles[i]==65&&level.obstacleOrigins[i]==81,""laser stamps full 2x2 footprint"");
var stateProbe=new State{level=level};
foreach(var p in sp.pendingStampedBeneath)stateProbe.RegisterStampedBeneath(p.cell,p.beneathId,p.beneathOrigin,p.overOrigin);
sp.PreDraw();check(gel.present&&gel.sealedCell&&gel.hidden&&gel.Count==0,""buried seed exists but is invisible and does not count/spread before break"");
check(gel.x==0&&gel.y==10,""only bottom-left cell is seeded"");
var unlocked=new List<int>();stateProbe.OnCellUnlocked=i=>unlocked.Add(i);
stateProbe.OnObstacleDestroyed=(i,id)=>events.Add(""removeLaser"");stateProbe.RequestObstacleViewCreate=sp.Reveal;
stateProbe.Break();
check(level.obstacles[90]==46&&level.obstacleOrigins[90]==90,""breaking laser restores original gel cell"");
foreach(int i in new[]{81,82,91})check(level.obstacles[i]==0&&level.obstacleOrigins[i]==-1,""other cover cells restore empty normal spaces"");
check(gel.Count==1&&!gel.hidden&&!gel.sealedCell,""gel is revealed and can count/spread only after break"");
check(string.Join("","",events)==""removeLaser,add,unseal"",""cover removal is notified before creating/unsealing gel view"");
check(unlocked.Count==4&&level.obstacles[85]==65,""refill is notified for four cells and neighbor laser survives"");
Console.WriteLine(""PASS: ""+checks+"" production laser stamp, hidden seed, cover clear and gel reveal checks."");
";
code=code.Replace("/* STAMP */",Method(spawner,"StampStackedEntry")).Replace("/* REVEAL */",Method(spawner,"RevealGelCell"))
 .Replace("/* SEAL */",sealBranch).Replace("/* STRUCT */",stamped).Replace("/* STATE */",stateMethods);
await CSharpScript.RunAsync(code,Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<>).Assembly));
