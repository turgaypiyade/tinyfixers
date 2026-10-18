// Run: csi Tools/check_cells_impact.csx (isolated production actions, no Unity/project build).
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

string Source(string path) => File.ReadAllText("Assets/_Project/Scripts/" + path);
string Methods(string path, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(Source(path)).GetRoot()
    .DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
var actionPath = "Grid/Board/Actions/CellsImpactAction.cs";
var action = Source(actionPath).Replace("using System.Collections;", "").Replace("using System.Collections.Generic;", "").Replace("using UnityEngine;", "");
var utils = Source("Grid/Board/Specials/SpecialUtils.cs").Replace("using UnityEngine;", "");
var marking = Methods("Grid/Board/Resolver/SpecialCellUtils.cs", "MarkAffectedCell", "MarkPatchBotImpactCell",
    "TryRouteMovableToImpact", "TryAddMagnetEndpointImpact", "TryAddObstacleImpact", "TryMarkEmitterImpact");
var patchbot = Methods("Grid/Board/PatchbotComboService.cs", "HasObstacleAt", "ResolveTargetImpact", "HitCellOnce");
var trigger = Methods("Grid/Board/BoardController.cs", "TriggerSpecialTileFromBoardEffect");
var harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public enum TileSpecial {None,LineH,LineV,PatchBot,PulseCore,SystemOverride}
public enum TileType {Core}
public enum TileRuntimeState {Idle,Swapping}
public enum ObstacleId {None,Mud,Grass,Stone,HatLauncher,EnergyContainer}
public enum ObstacleHitContext {SpecialActivation}
public class TileData {public int X,Y;public TileType Type;public TileSpecial Special;}
public class TileView {
 public int X,Y;public TileSpecial special;public bool IsSpecialLocked,CageGrace;
 public TileRuntimeState RuntimeState;
 public TileSpecial GetSpecial()=>special;public TileType GetTileType()=>TileType.Core;
 public static implicit operator bool(TileView t)=>t!=null;
}
public class LevelData {}
public abstract class BoardAction {public abstract IEnumerator ExecuteVisuals(ActionSequencer s);}
public class ActionSequencer {public BoardController Board;}
public class Locks {public int notes;public void NoteCovered(TileView t){notes++;}}
public class ResolutionContext {
 public HashSet<Vector2Int> AffectedCells=new HashSet<Vector2Int>();
 public HashSet<TileView> Affected=new HashSet<TileView>();
 public List<Vector2Int> ImpactCells=new List<Vector2Int>();
}
public class ObstacleStateService {
 public ObstacleId id;public bool movable,locked,blocked,under;
 public bool HasObstacleAt(int x,int y)=>id!=ObstacleId.None;
 public bool IsMovableObstacleAt(int x,int y)=>movable;
 public bool IsUnderTileObstacleAt(int x,int y)=>under;
 public bool IsUnbreakableMovableAt(int x,int y)=>false;
 public bool IsInteractionLockedAt(int x,int y)=>locked;
 public bool IsOverTileBlockerAt(int x,int y)=>blocked;
 public bool IsSpecialTargetableBlockerAt(int x,int y)=>false;
 public bool IsMagnetEndpoint(int x,int y)=>false;
 public bool HasCoveringOverlayOnUnbreakableMovableAt(int x,int y)=>false;
 public ObstacleId GetObstacleIdAt(int x,int y)=>id;
}
public class SpecialResolver {
 public List<TileView> triggered=new List<TileView>();
 public List<BoardAction> ResolveSpecialSolo(TileView t){triggered.Add(t);return new List<BoardAction>{new Chain()};}
 class Chain:BoardAction {public override IEnumerator ExecuteVisuals(ActionSequencer s){yield break;}}
}
public class BoardController {
 public int Width=3,Height=1,resolves,sequences,forcedHits,obstacleHits;
 public bool isActiveAndEnabled=true;
 public LevelData LevelData=new LevelData();public Locks SpecialLocks=new Locks();
 public TileView[,] Tiles=new TileView[3,1];public TileData[,] GridData=new TileData[3,1];public bool[,] Holes=new bool[3,1];
 public ObstacleStateService ObstacleStateService=new ObstacleStateService();
 public SpecialResolver specialResolver=new SpecialResolver();
 public HashSet<Vector2Int> pending=new HashSet<Vector2Int>();
 public List<TileView> cleared=new List<TileView>();
 public void RequestResolveAfterActionSequence(){resolves++;}
 public void MarkPatchBotForcedObstacleHit(int x,int y){forcedHits++;}
 public bool IsPendingTriggeredSpecialCell(int x,int y)=>pending.Contains(new Vector2Int(x,y));
 public void StartImmediateActionSequence(List<BoardAction> actions){sequences++;}
 public void ApplyObstacleDamageAt(int x,int y,ObstacleHitContext c){obstacleHits++;}
 public void SyncTileData(int x,int y){var t=Tiles[x,y];GridData[x,y]=new TileData{X=x,Y=y,Type=t.GetTileType(),Special=t.GetSpecial()};}
 public TileView Put(int x,TileSpecial s=TileSpecial.None){var t=new TileView{X=x,Y=0,special=s};Tiles[x,0]=t;SyncTileData(x,0);return t;}
 " + trigger + @"
}
public class PatchbotComboService {
 BoardController board;public PatchbotComboService(BoardController b){board=b;}
 " + patchbot + @"
}
public static class SpecialCellUtils {
 " + marking + @"
}
public class MatchClearAction:BoardAction {
 HashSet<TileView> tiles;IReadOnlyList<Vector2Int> impacts;
 public MatchClearAction(HashSet<TileView> tiles,bool doShake,HashSet<Vector2Int> affectedCells,
  bool includeAdjacentOverTileBlockerDamage,float staggerAnimTime,bool isSpecialPhase,
  IReadOnlyList<Vector2Int> impactCells,bool enqueueCascadeOnComplete){
  if(includeAdjacentOverTileBlockerDamage||!isSpecialPhase||staggerAnimTime!=0||enqueueCascadeOnComplete)throw new Exception(""Impact clear contract changed"");
  this.tiles=tiles;impacts=impactCells;
 }
 public override IEnumerator ExecuteVisuals(ActionSequencer s){
  foreach(var t in tiles){if(t.GetSpecial()!=TileSpecial.None)throw new Exception(""REGRESSION: special consumed as normal tile"");s.Board.cleared.Add(t);}
  foreach(var cell in impacts.Distinct())if(s.Board.ObstacleStateService.HasObstacleAt(cell.x,cell.y))s.Board.obstacleHits++;
  yield return null;
 }
}
" + utils + action + @"
public static class Checks {
 static int count;static void Check(bool v,string why){count++;if(!v)throw new Exception(why);}
 static List<Vector2Int> Cells(params int[] xs)=>xs.Select(x=>new Vector2Int(x,0)).ToList();
 static CellsImpactAction Run(BoardController b,params int[] xs){var a=new CellsImpactAction(b,b.LevelData,Cells(xs));Drain(a.ExecuteVisuals(new ActionSequencer{Board=b}));return a;}
 static void Drain(IEnumerator e){try{while(e.MoveNext()){if(e.Current is IEnumerator child)Drain(child);}}finally{(e as IDisposable)?.Dispose();}}
 public static int Run(){
  foreach(var special in new[]{TileSpecial.PulseCore,TileSpecial.LineH,TileSpecial.LineV,TileSpecial.PatchBot,TileSpecial.SystemOverride}){
   var b=new BoardController();var t=b.Put(0,special);var a=Run(b,0);
   Check(b.specialResolver.triggered.SequenceEqual(new[]{t})&&b.sequences==1,""Special activates through the production board-effect trigger: ""+special);
   Check(b.cleared.Count==0&&a.Completed&&b.resolves==1,""Special avoids plain clear and impact completes: ""+special);
  }
  {
   var b=new BoardController();var pulse=b.Put(0,TileSpecial.PulseCore);var normal=b.Put(1);Run(b,0,0,0,1,1);
   Check(b.specialResolver.triggered.SequenceEqual(new[]{pulse})&&b.cleared.SequenceEqual(new[]{normal}),""Duplicate destinations credit one special/normal hit"");
  }
  {
   var b=new BoardController();var old=b.Put(0);var action=new CellsImpactAction(b,b.LevelData,Cells(0));var replacement=b.Put(0,TileSpecial.PulseCore);
   Drain(action.ExecuteVisuals(new ActionSequencer{Board=b}));
   Check(b.specialResolver.triggered.SequenceEqual(new[]{replacement})&&!b.cleared.Contains(old),""Queued impact reads current contents after a fall/replacement"");
  }
  {
   var b=new BoardController();b.Put(0,TileSpecial.PulseCore);b.pending.Add(new Vector2Int(0,0));Run(b,0);
   Check(b.sequences==0&&b.cleared.Count==0,""Pending special remains owned by its existing chain"");
  }
  foreach(int protection in new[]{0,1,2}){
   var b=new BoardController();var t=b.Put(0,TileSpecial.PulseCore);
   t.IsSpecialLocked=protection==0;t.CageGrace=protection==1;t.RuntimeState=protection==2?TileRuntimeState.Swapping:TileRuntimeState.Idle;
   Run(b,0);Check(b.sequences==0&&b.cleared.Count==0,""Caged/grace/swapping specials survive"");
   Check(b.SpecialLocks.notes==(protection==0?1:0),""Only a caged special notifies lock coverage"");
  }
  foreach(bool over in new[]{false,true}){
   var b=new BoardController();var t=b.Put(0,TileSpecial.PulseCore);var o=b.ObstacleStateService;
   o.id=over?ObstacleId.Stone:ObstacleId.Grass;o.blocked=over;o.locked=!over;o.under=!over;Run(b,0);
   Check(b.sequences==0&&b.cleared.Count==0&&b.Tiles[0,0]==t,""Covered special is neither activated nor consumed"");
   Check(b.obstacleHits==1&&b.forcedHits==1,""Cover receives exactly one normal impact"");
  }
  {
   var b=new BoardController();b.Put(0,TileSpecial.PulseCore);b.ObstacleStateService.id=ObstacleId.Stone;b.ObstacleStateService.movable=true;Run(b,0);
   Check(b.sequences==0&&b.cleared.Count==0&&b.obstacleHits==1,""Movable obstacle keeps its obstacle-only route"");
  }
  {
   var b=new BoardController();var t=b.Put(0,TileSpecial.PulseCore);b.ObstacleStateService.id=ObstacleId.Mud;b.ObstacleStateService.under=true;Run(b,0);
   Check(b.specialResolver.triggered.SequenceEqual(new[]{t})&&b.cleared.Count==0,""Uncovered special above Mud activates through its own chain"");
  }
  {
   var b=new BoardController();var a=Run(b,-1,3,2);Check(a.Completed&&b.sequences==0&&b.cleared.Count==0,""Invalid and empty targets safely complete"");
   b.isActiveAndEnabled=false;a=Run(b,0);Check(a.Completed&&b.resolves==1,""Disabled board does not apply damage or request a new resolve"");
  }
  {
   var b=new BoardController();b.Put(0,TileSpecial.PulseCore);var a=new CellsImpactAction(b,b.LevelData,Cells(0));b.LevelData=new LevelData();
   Drain(a.ExecuteVisuals(new ActionSequencer{Board=b}));Check(a.Completed&&b.sequences==0&&b.resolves==0,""Old-level impact cannot damage a new level"");
  }
  {
   var b=new BoardController();b.Put(0);var a=new CellsImpactAction(b,b.LevelData,Cells(0));var e=a.ExecuteVisuals(new ActionSequencer{Board=b});
   Check(e.MoveNext(),""Normal impact yields its clear action"");(e as IDisposable).Dispose();
   Check(a.Completed&&b.resolves==1,""Disposing interrupted impact runs completion cleanup"");
  }
  return count;
 }
}
Checks.Run()";
var options = ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly);
var count = CSharpScript.EvaluateAsync<int>(harness, options).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + count + " production CellsImpactAction regression checks");
foreach (var path in new[] { actionPath, "Grid/Board/Actions/EggBirdHatchAction.cs", "Grid/Board/Resolver/SpecialResolver.cs" })
{
    var errors = CSharpSyntaxTree.ParseText(Source(path)).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    if (errors.Length > 0) throw new Exception(path + ": " + string.Join("\n", errors.Select(e => e.ToString())));
}
Console.WriteLine("PASS: action, EggBird and resolver source syntax");
