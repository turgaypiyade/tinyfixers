// Run from the repo root: csi Tools/check_tile_lifetime.csx
// Executes production coroutine/clear methods with Unity doubles; no project build.
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

string Methods(string path, params string[] names) => string.Join("\n",
    CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes()
        .OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
var lifetime = Methods("Assets/_Project/Scripts/Grid/TileView.cs", "IsCurrentLifetime", "RunForCurrentLifetime", "RunForLifetime");
var pop = Methods("Assets/_Project/Scripts/Grid/TileAnimator.cs", "PlayPop", "PlayPopCore", "EmptyAnimation");
var clear = string.Join("\n", CSharpSyntaxTree.ParseText(File.ReadAllText(
    "Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs")).GetRoot().DescendantNodes()
    .OfType<LocalFunctionStatementSyntax>().Where(m => m.Identifier.ValueText == "IsOriginalTile" || m.Identifier.ValueText == "FinalizeTileClear")
    .Select(m => m.ToFullString()));
var handoff = string.Join("\n", CSharpSyntaxTree.ParseText(File.ReadAllText(
    "Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs")).GetRoot().DescendantNodes()
    .OfType<LocalFunctionStatementSyntax>().Where(m => new[]{"PlayTileClear", "ClearCellDataAfterDelay"}.Contains(m.Identifier.ValueText))
    .Select(m => m.ToFullString()));
var harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public static bool operator==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y;
 public static bool operator!=(Vector2 a,Vector2 b)=>!(a==b);
 public override bool Equals(object o)=>o is Vector2 && this==(Vector2)o;
 public override int GetHashCode()=>0;
}
public struct Vector3 {
 public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 zero=>new Vector3();public static Vector3 one=>new Vector3(1,1,1);
}
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public class WaitForSeconds {public float seconds;public WaitForSeconds(float s){seconds=s;}}
public struct Quaternion { public static Quaternion identity=>new Quaternion(); }
public class Transform { public Vector3 localScale=Vector3.one;public Quaternion localRotation; }
public class RectTransform:Transform {public Vector2 pivot;public static bool operator!(RectTransform t)=>t==null;public static implicit operator bool(RectTransform t)=>t!=null;}
public class CanvasGroup {public float alpha=1;}
public class GameObject {public bool activeInHierarchy=true;public CanvasGroup group=new CanvasGroup(); public T AddComponent<T>() where T:new()=>new T();}
public class MissingReferenceException:Exception {}
public static class Time { public static float deltaTime=.03f; }
public static class Mathf {
 public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(x,b));
 public static float Clamp01(float x)=>Clamp(x,0,1);public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
}
public static class Debug {public static void Log(string s){}}
public enum TileType { Gear }
public enum TileSpecial { None }
public enum ClearAnimationMode {Default,GoalFlyToHud}
public enum ObstacleHitContext {NormalMatch,SpecialActivation}
public class TileView {
 public int LifetimeVersion {get;private set;} public GameObject gameObject=new GameObject();
 public RectTransform RectTransform=new RectTransform();public Transform transform=>RectTransform;
 public int X,Y;public TileType GetTileType()=>TileType.Gear;public TileSpecial GetSpecial()=>TileSpecial.None;
 public T GetComponent<T>() where T:class=>gameObject.group as T;
 public static implicit operator bool(TileView tile)=>tile!=null;public static bool operator!(TileView tile)=>tile==null;
 public void Release(){LifetimeVersion++;gameObject.activeInHierarchy=false;}
 public void Reuse(){LifetimeVersion++;gameObject.activeInHierarchy=true;transform.localScale=Vector3.one;gameObject.group.alpha=1;}
 " + lifetime + @"
}
public class BreakFx {public int calls;public void PlayTileBreak(TileView tile){calls++;}}
public class BoardController {
 public int Width=1,Height=1,clears;public bool IsSpecialActivationPhase;public TileView[,] Tiles=new TileView[1,1];public BreakFx BreakFx=new BreakFx();
 public bool UseContinuousFallMotion=true,releasedVisible;public int releases;public List<Routine> routines=new List<Routine>();
 public void StartCoroutine(IEnumerator e){var r=new Routine(e);routines.Add(r);r.Step();}
 public void Pump(){foreach(var r in routines.ToArray())r.Step();}
 public float GetClearDurationForCurrentPass()=>.06f;
 public void TryPaintGelForClearedTile(TileView t){}
 public void ClearCellDataOnly(Vector2Int cell){var t=Tiles[cell.x,cell.y];if(t==null)return;releases++;releasedVisible|=t.gameObject.group.alpha>0&&t.transform.localScale.x>0;Tiles[cell.x,cell.y]=null;}
 public void ClearAndDestroyTile(TileView tile,Dictionary<TileType,int> counts){clears++;tile.Release();}
}
public class Routine {
 readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();float wait;public bool Done=>stack.Count==0;
 public Routine(IEnumerator e){stack.Push(e);}
 public void Step(){
  if(wait>0){wait-=Time.deltaTime;if(wait>0)return;}
  while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}
   if(e.Current is IEnumerator inner){stack.Push(inner);continue;}
   if(e.Current is WaitForSeconds w)wait=w.seconds;return;
  }
 }
}
public static class TileClearBurstVfx {public static IEnumerator CoPlayBurst(TileView t,BoardController b,float d){yield break;}}
public class TileAnimator {
 static readonly Vector2 CenterPivot=new Vector2(.5f,.5f);readonly BoardController board=null;
 const float BURST_DURATION=.06f,TILE_SHRINK_MID=.55f,TILE_SHRINK_END=0,BURST_VFX_DURATION=.3f;
 static void SetPivotWithoutVisualJump(RectTransform rt,Vector2 p){rt.pivot=p;}
 " + pop + @"
}
public class TileClearEffectOrchestrator {
 readonly TileAnimator animator=new TileAnimator();public bool finishGoal;
 public IEnumerator Play(TileView tile,ClearAnimationMode mode,float delay,float duration,bool suppressBurst){
  if(delay>0)yield return new WaitForSeconds(delay);
  if(mode==ClearAnimationMode.Default)yield return animator.PlayPop(tile,duration,suppressBurst);
  else while(!finishGoal)yield return null;
 }
}
public class ClearPass {
 Dictionary<TileView,int> tileLifetimes=new Dictionary<TileView,int>();HashSet<TileView> finalizedTiles=new HashSet<TileView>();
 HashSet<TileView> skipBreakFxTiles=new HashSet<TileView>();Dictionary<TileType,int> clearedByType=new Dictionary<TileType,int>();
 bool trace=false;bool isSpecialPhase;BoardController board;
 ObstacleHitContext damageContext=ObstacleHitContext.NormalMatch;HashSet<Vector2Int> tileClearOwnedObstacleCells=new HashSet<Vector2Int>();
 bool IsSameCellObstacleDamageOwnedByTileClear(Vector2Int c)=>false;
 public readonly TileClearEffectOrchestrator clearEffectOrchestrator=new TileClearEffectOrchestrator();
 public ClearPass(TileView tile,BoardController board){this.board=board;isSpecialPhase=board.IsSpecialActivationPhase;tileLifetimes[tile]=tile.LifetimeVersion;}
 public void Clear(TileView tile)=>FinalizeTileClear(tile);
 public IEnumerator Animate(TileView tile,ClearAnimationMode mode=ClearAnimationMode.Default,float delay=0)=>PlayTileClear(tile,mode,delay,true);
 public IEnumerator LegacyEarlyRelease(TileView tile)=>ClearCellDataAfterDelay(tile,0);
 " + handoff + @"
 " + clear + @"
}
public static class Checks {
 static int count,writes,cleanups;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 static void Drain(IEnumerator e){int n=0;while(e.MoveNext())if(++n>100)throw new Exception(""Coroutine stuck"");}
 static IEnumerator DelayedWrite(){yield return new object();writes++;}
 static IEnumerator Nested(){try{yield return DelayedWrite();writes++;}finally{cleanups++;}}
 static IEnumerator LastFrame(){writes++;yield return null;writes++;}
 static IEnumerator Throwing(){try{yield return null;throw new InvalidOperationException();}finally{cleanups++;}}
 static bool Visible(TileView t)=>t.gameObject.group.alpha==1&&t.transform.localScale.x==1;
 public static int Run(){
  var tile=new TileView();writes=0;var queued=tile.RunForCurrentLifetime(DelayedWrite());tile.Release();tile.Reuse();Drain(queued);
  Check(writes==0,""Queued old animation never starts on reused tile"");
  var nested=tile.RunForCurrentLifetime(Nested());Check(nested.MoveNext(),""Nested wait begins"");tile.Release();tile.Reuse();Drain(nested);
  Check(writes==0&&cleanups==1,""Nested delayed writer cancelled and cleanup disposed"");
  var final=tile.RunForCurrentLifetime(LastFrame());Check(final.MoveNext()&&writes==1,""Final frame reached"");tile.Release();tile.Reuse();Drain(final);
  Check(writes==1,""No final-frame writes after reuse"");
  writes=0;Drain(tile.RunForCurrentLifetime(Nested()));Check(writes==2&&cleanups==2,""Current lifetime completes parent and child"");
  var inactive=tile.RunForCurrentLifetime(DelayedWrite());tile.gameObject.activeInHierarchy=false;Drain(inactive);Check(writes==2,""Inactive view is untouched"");tile.Reuse();
  var failure=tile.RunForCurrentLifetime(Throwing());failure.MoveNext();bool threw=false;try{failure.MoveNext();}catch(InvalidOperationException){threw=true;}
  Check(threw&&cleanups==3,""Exceptions propagate and dispose child cleanup"");
  var animator=new TileAnimator();var stalePop=animator.PlayPop(tile,.06f,true);Check(stalePop.MoveNext(),""Pop begins"");tile.Release();tile.Reuse();Drain(stalePop);
  Check(Visible(tile),""Old pop cannot hide the refill tile"");
  var finalPop=animator.PlayPop(tile,.06f,true);finalPop.MoveNext();finalPop.MoveNext();tile.Release();tile.Reuse();Drain(finalPop);
  Check(Visible(tile),""Old pop final scale-zero/alpha-zero write is cancelled"");
  var queuedPop=animator.PlayPop(tile,.06f,true);tile.Release();tile.Reuse();Drain(queuedPop);
  Check(Visible(tile),""Pop captures lifetime at scheduling, not first MoveNext"");
  Drain(animator.PlayPop(tile,.06f,true));Check(tile.gameObject.group.alpha==0&&tile.transform.localScale.x==0,""Uninterrupted pop still finishes hidden"");tile.Reuse();
  var board=new BoardController();board.Tiles[0,0]=tile;var pass=new ClearPass(tile,board);var overlapping=new ClearPass(tile,board);
  pass.Clear(tile);pass.Clear(tile);Check(board.clears==1&&board.BreakFx.calls==1,""Implode callback plus final sweep clear/count once"");
  tile.Reuse();pass.Clear(tile);overlapping.Clear(tile);
  Check(board.clears==1&&Visible(tile)&&tile.gameObject.activeInHierarchy,""Old and overlapping clear passes cannot release refill tile"");
  var fresh=new ClearPass(tile,board);fresh.Clear(tile);Check(board.clears==2,""Fresh pass can clear new lifetime"");
  // Reproduce the previous handoff using the production release method: the cell
  // opened while the old sprite was full-size, before its pop was even scheduled.
  tile=new TileView();board=new BoardController();board.Tiles[0,0]=tile;pass=new ClearPass(tile,board);
  board.StartCoroutine(pass.LegacyEarlyRelease(tile));
  Check(board.releasedVisible&&board.Tiles[0,0]==null,""Old eager handoff exposes a cell still occupied by a visible body"");
  foreach(float dt in new[]{1f/30,1f/60,1f/120,1f/600}){
   Time.deltaTime=dt;tile=new TileView();board=new BoardController();board.Tiles[0,0]=tile;pass=new ClearPass(tile,board);
   var animation=new Routine(pass.Animate(tile));bool safe=true;
   for(int i=0;i<100&&!animation.Done;i++){animation.Step();board.Pump();safe&=!board.releasedVisible;}
   Check(safe&&animation.Done&&board.releases==1&&board.Tiles[0,0]==null,
    ""Continuous clear opens each cell only after actual PlayPop hides the body, including slow motion"");
  }
  Time.deltaTime=.01f;tile=new TileView();board=new BoardController();board.Tiles[0,0]=tile;pass=new ClearPass(tile,board);
  var waiting=new Routine(pass.Animate(tile,ClearAnimationMode.Default,.1f));waiting.Step();
  Check(board.Tiles[0,0]==tile&&board.releases==0,""Delayed pop keeps its cell until its visual starts and completes"");
  tile.Release();tile.Reuse();for(int i=0;i<40;i++)waiting.Step();
  Check(board.Tiles[0,0]==tile&&board.releases==0,""Stale delayed handoff cannot clear the next pooled lifetime"");
  tile=new TileView();board=new BoardController();board.Tiles[0,0]=tile;pass=new ClearPass(tile,board);
  var replacing=new Routine(pass.Animate(tile));replacing.Step();var replacement=new TileView();board.Tiles[0,0]=replacement;
  for(int i=0;i<20;i++)replacing.Step();
  Check(board.Tiles[0,0]==replacement&&board.releases==0,""Visual completion cannot erase a replacement tile"");
  tile=new TileView();var delayedTile=new TileView{Y=1};
  board=new BoardController{Height=2,Tiles=new TileView[1,2]};board.Tiles[0,0]=tile;board.Tiles[0,1]=delayedTile;
  var firstPass=new ClearPass(tile,board);var delayedPass=new ClearPass(delayedTile,board);
  var firstBody=new Routine(firstPass.Animate(tile));var delayedBody=new Routine(delayedPass.Animate(delayedTile,ClearAnimationMode.Default,.25f));
  for(int i=0;i<10;i++){firstBody.Step();delayedBody.Step();board.Pump();}
  Check(firstBody.Done&&!delayedBody.Done&&board.Tiles[0,0]==null&&board.Tiles[0,1]==delayedTile,
   ""Each cleared body releases independently without waiting for the other delayed clear"");
  tile=new TileView();board=new BoardController();board.Tiles[0,0]=tile;pass=new ClearPass(tile,board);
  var goal=new Routine(pass.Animate(tile,ClearAnimationMode.GoalFlyToHud));goal.Step();
  Check(board.releases==1&&!goal.Done,""Long HUD flight does not hold the board cell until flight completion"");
  tile=new TileView();board=new BoardController{UseContinuousFallMotion=false};board.Tiles[0,0]=tile;pass=new ClearPass(tile,board);
  new Routine(pass.Animate(tile)).Step();
  Check(board.releases==1,""Legacy motion retains early data release"");
  return count;
 }
}
Checks.Run()";
var options=ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly);
var result=CSharpScript.EvaluateAsync<int>(harness,options).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" tile lifetime and clear handoff checks (including production PlayPop at 30/60/120 FPS and slow motion)");
