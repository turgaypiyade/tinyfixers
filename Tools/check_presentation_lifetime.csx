// Isolated production-method regression checks; no Unity build, Play, or board simulation.
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
string Dir = "Assets/_Project/Scripts/Grid/Board/Actions/";
string Types(string path) => string.Join("\n", CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().ChildNodes().OfType<MemberDeclarationSyntax>().Select(x => x.ToFullString()));
string Methods(string path, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x => names.Contains(x.Identifier.ValueText)).Select(x => x.ToFullString()));
var changed = new[]{Dir+"ClearPresentationPlan.cs",Dir+"ClearEffectPlaybackContext.cs",Dir+"MatchClearAction.cs",Dir+"PulseWaveEffectPlayer.cs",Dir+"OverrideRadialEffectPlayer.cs",Dir+"SpecialCreationFormationEffectPlayer.cs","Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs","Assets/_Project/Scripts/Grid/TileAnimator.cs"};
foreach (var path in changed) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0) throw new Exception(path+": "+string.Join("\n",errors.Select(e=>e.ToString())));
}
var actionRoot=CSharpSyntaxTree.ParseText(File.ReadAllText(Dir+"MatchClearAction.cs")).GetRoot();
if(!actionRoot.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Any(c=>c.ToString().Contains("CaptureTileLifetimes()")))throw new Exception("Queue-time lifetime capture missing");
var harness=@"
using System;using System.Collections;using System.Collections.Generic;
 public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
 public class WaitForSeconds {public WaitForSeconds(float f){}}
 public class RectTransform {}
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp01(float v)=>Math.Min(1,Math.Max(0,v));}
public enum TileType {Gear}
public enum TileSpecial {None,PulseCore}
public enum TileRuntimeState {Idle,Special,Falling}
public enum ObstacleHitContext {NormalMatch,SpecialActivation}
public class GameObject {public bool activeInHierarchy=true;}
public class TileView {
 public int LifetimeVersion=1;public GameObject gameObject=new GameObject();public int X,Y,reveals,layout;
 public TileSpecial special=TileSpecial.PulseCore;public TileRuntimeState RuntimeState;
 public void Reuse(){LifetimeVersion++;gameObject.activeInHierarchy=true;RuntimeState=TileRuntimeState.Falling;}
 public TileType GetTileType()=>TileType.Gear;public TileSpecial GetSpecial()=>special;
 public void SetRuntimeState(TileRuntimeState state){RuntimeState=state;}
 public void ApplyTileSize(int size){layout++;}public void PlaySpecialCreationReveal(TileSpecial sp,int size){reveals++;}
 /* LIFETIME */
}
public class Vfx {public RectTransform VfxRoot;}
public class BoardController {
 public BoardAnimator boardAnimatorRef=new BoardAnimator();public Vfx BoardVfxPlayer;public int TileSize=100;
 public List<IEnumerator> scheduled=new List<IEnumerator>();public void StartCoroutine(IEnumerator e){scheduled.Add(e);}
}
public class BoardAnimator {
 public int impacts,formations,implodes;public Action onFormation;
 public IEnumerator PlayPulseImpactSingle(TileView t,float seconds){impacts++;yield return null;}
 public IEnumerator PlayCreatedSpecialFormation(TileView t,IReadOnlyList<TileView> list,float seconds,ClearEffectPlaybackContext ctx=null){formations++;yield return null;onFormation?.Invoke();}
 public IEnumerator PlayTilesImplodeToCell(Vector2Int cell,IReadOnlyList<TileView> list,float seconds,float moment,Action<TileView> callback,ClearEffectPlaybackContext ctx=null){implodes++;foreach(var t in list)if(ctx.IsCurrentTile(t))callback(t);yield break;}
 public IEnumerator RunMany(IReadOnlyList<IEnumerator> routines){foreach(var e in routines)yield return e;}
}
".Replace("/* LIFETIME */",Methods("Assets/_Project/Scripts/Grid/TileView.cs","IsCurrentLifetime","RunForLifetime","RunForCurrentLifetime"));
harness+=Types(Dir+"IClearEffectDescriptor.cs")+Types(Dir+"IClearEffectPlayer.cs")+Types(Dir+"ClearPresentationPlan.cs")+Types(Dir+"ClearEffectPlaybackContext.cs")+Types(Dir+"PulseWaveEffectDescriptor.cs")+Types(Dir+"OverrideRadialEffectDescriptor.cs")+Types(Dir+"SpecialCreationFormationEffectDescriptor.cs")+Types(Dir+"SpecialCreationFormationEffectPlayer.cs");
harness+="public class PulseWaveEffectPlayer { void PlayPulseCenterVfx(BoardController b,Vector2Int c,int r){}"+Methods(Dir+"PulseWaveEffectPlayer.cs","Play","PlayTileImpact","CanPlay")+"}";
harness+="public class OverrideRadialEffectPlayer {const float LIGHTNING_HIT_MOMENT=.06f;void PlayOverrideCenterVfx(BoardController b,OverrideRadialEffectDescriptor d){}"+Methods(Dir+"OverrideRadialEffectPlayer.cs","Play","PlayTileImpactWithVfx","CanPlay")+"}";
harness+=@"
public class Routine {
 readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();public bool Done=>stack.Count==0;
 public Routine(IEnumerator e){stack.Push(e);}public void Step(){while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();(e as IDisposable)?.Dispose();continue;}if(e.Current is IEnumerator nested){stack.Push(nested);continue;}return;}}
 public void Finish(){for(int i=0;i<100&&!Done;i++)Step();if(!Done)throw new Exception(""Routine stalled"");}
}
public static class Checks {
 static int count;static void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
 static ClearPresentationPlan Plan(IClearEffectDescriptor e){var p=new ClearPresentationPlan();p.Effects.Add(e);p.CaptureTileLifetimes();return p;}
 static ClearEffectPlaybackContext Context(ClearPresentationPlan p)=>new ClearEffectPlaybackContext{CurrentTileLifetime=p.TryGetCurrentTileLifetime};
 static PulseWaveEffectDescriptor Pulse(TileView t)=>new PulseWaveEffectDescriptor(new[]{t},new[]{new Vector2Int(0,0)},new Dictionary<TileView,float>{{t,.1f}},.1f,new Vector2Int(0,0));
 public static int Run(){
  var t=new TileView();var p=Plan(Pulse(t));int version;
  Check(p.TryGetCurrentTileLifetime(t,out version)&&version==1,""Captured live tile accepted"");
  t.Reuse();p.CaptureTileLifetimes();Check(!p.TryGetCurrentTileLifetime(t,out version),""Recapture must not adopt reused tile"");
  Check(!p.TryGetCurrentTileLifetime(new TileView(),out version),""Unrelated replacement rejected"");
  // Control: the previous execution-time capture adopts the new lifetime and animates it.
  var lateTile=new TileView();var lateEffect=Pulse(lateTile);var latePlan=new ClearPresentationPlan();latePlan.Effects.Add(lateEffect);lateTile.Reuse();latePlan.CaptureTileLifetimes();var lateBoard=new BoardController();
  new Routine(new PulseWaveEffectPlayer().Play(lateEffect,lateBoard,Context(latePlan))).Finish();new Routine(lateBoard.scheduled[0]).Finish();Check(lateBoard.boardAnimatorRef.impacts==1,""Late capture reproduces animation reaching the replacement stone"");
  t=new TileView();p=Plan(Pulse(t));t.gameObject.activeInHierarchy=false;Check(!p.TryGetCurrentTileLifetime(t,out version),""Inactive pooled tile rejected"");
  for(int i=0;i<64;i++){
   t=new TileView();var descriptor=Pulse(t);p=Plan(descriptor);var ctx=Context(p);var b=new BoardController();t.Reuse();
   new Routine(new PulseWaveEffectPlayer().Play(descriptor,b,ctx)).Finish();Check(b.scheduled.Count==0,""Queued stale pulse never schedules impact"");
   t=new TileView();descriptor=Pulse(t);p=Plan(descriptor);ctx=Context(p);b=new BoardController();int clears=0;ctx.ClearTileNow=_=>clears++;
   new Routine(new PulseWaveEffectPlayer().Play(descriptor,b,ctx)).Finish();Check(b.scheduled.Count==1,""Valid pulse scheduled"");
   var r=new Routine(b.scheduled[0]);r.Step();t.Reuse();r.Finish();Check(clears==0&&b.boardAnimatorRef.impacts==0,""Reuse during pulse delay cancels animation and clear"");
   t=new TileView();descriptor=Pulse(t);p=Plan(descriptor);ctx=Context(p);b=new BoardController();clears=0;ctx.ClearTileNow=_=>clears++;
   new Routine(new PulseWaveEffectPlayer().Play(descriptor,b,ctx)).Finish();r=new Routine(b.scheduled[0]);r.Finish();Check(clears==1&&b.boardAnimatorRef.impacts==1,""Live pulse still animates and clears once"");
  }
  t=new TileView();var radial=new OverrideRadialEffectDescriptor(new[]{t},null,null,null,null);p=Plan(radial);var c=Context(p);var board=new BoardController();t.Reuse();new Routine(new OverrideRadialEffectPlayer().Play(radial,board,c)).Finish();Check(board.scheduled.Count==0,""Stale radial ignored"");
  t=new TileView();radial=new OverrideRadialEffectDescriptor(new[]{t},null,null,null,null);p=Plan(radial);c=Context(p);int hits=0;c.NotifyCellImpactNow=_=>hits++;var rr=new Routine(new OverrideRadialEffectPlayer().Play(radial,board,c));rr.Step();t.Reuse();rr.Finish();Check(hits==0,""Delayed radial cannot affect replacement"");
  var created=new TileView();var source=new TileView();var creation=new SpecialCreationFormationEffectDescriptor(created,new[]{source},null,.1f);p=Plan(creation);c=Context(p);board=new BoardController();created.Reuse();new Routine(new SpecialCreationFormationEffectPlayer().Play(creation,board,c)).Finish();Check(board.boardAnimatorRef.formations==0&&created.reveals==0,""Queued stale creation cannot hide or reveal replacement"");
  created=new TileView();source=new TileView();creation=new SpecialCreationFormationEffectDescriptor(created,new[]{source},null,.1f);p=Plan(creation);c=Context(p);board=new BoardController();board.boardAnimatorRef.onFormation=()=>created.Reuse();new Routine(new SpecialCreationFormationEffectPlayer().Play(creation,board,c)).Finish();Check(created.reveals==0&&created.RuntimeState==TileRuntimeState.Falling,""Reuse during formation prevents reveal and stale finally state reset"");
  created=new TileView();source=new TileView();creation=new SpecialCreationFormationEffectDescriptor(created,new[]{source},null,.1f);p=Plan(creation);c=Context(p);board=new BoardController();new Routine(new SpecialCreationFormationEffectPlayer().Play(creation,board,c)).Finish();Check(created.reveals==1&&created.RuntimeState==TileRuntimeState.Idle,""Live formation still reveals and releases runtime state"");
  return count;
 }
}
Checks.Run()";
var result=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" queued-presentation lifetime checks; 8 production C# files parsed. No Unity build/Play.");
