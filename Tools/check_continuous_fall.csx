// Run: csi Tools/check_continuous_fall.csx
// Exercises production motor code with Unity doubles; no Unity/project build.
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
var motor=File.ReadAllText("Assets/_Project/Scripts/Grid/Board/TileFallMotionSystem.cs");
var harness=@"
using System;
using System.Collections.Generic;
 public struct Vector2 {
  public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
  public float sqrMagnitude=>x*x+y*y; public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
  public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
  public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
  public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude;
 }
 public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
 public static class Mathf {
  public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
  public static float Abs(float v)=>Math.Abs(v);public static int RoundToInt(float v)=>(int)Math.Round(v);
  public static float Clamp(float v,float a,float b)=>Max(a,Min(v,b));public static float Clamp01(float v)=>Clamp(v,0,1);
  public static float SmoothStep(float a,float b,float v){v=Clamp01(v);return a+(b-a)*v*v*(3-2*v);}
  public static float MoveTowards(float a,float b,float d)=>a+Math.Sign(b-a)*Min(Abs(b-a),d);
 }
 public static class Time {public static float time;}
 public static class Debug {public static void LogWarning(string s){} public static void LogException(Exception e){throw e;} }
public enum TileRuntimeState {Idle,Falling,Clearing,Swapping}
public class RectTransform {public Vector2 anchoredPosition;}
public class GameObject {public bool activeInHierarchy=true;}
public class TileView {
 public GameObject gameObject=new GameObject();
 public int X,Y,LifetimeVersion=1,token,PlannedFallGeneration=1,resets,arrivals,settles;
 public bool IsPlannedToMoveThisFallPass;
 public BoardController board=new BoardController();
 private int fallArrivedGeneration=-1;
 /* production arrival hooks */
 public TileView(){FallArrived+=t=>{arrivals++;arrived?.Invoke();};}
 public event Action<TileView> FallArrived;
 public TileRuntimeState RuntimeState;public bool IsRuntimeIdle=>RuntimeState==TileRuntimeState.Idle;
 public RectTransform RectTransform=new RectTransform();public Action arrived;
 public static implicit operator bool(TileView t)=>t!=null;public static bool operator!(TileView t)=>t==null;
 public bool IsCurrentLifetime(int v)=>v==LifetimeVersion;
 public bool IsMoveTokenCurrent(int v)=>v==token;public int ClaimMoveToken()=>++token;
 public void SetRuntimeState(TileRuntimeState s)=>RuntimeState=s;
 public void ResetFallStretch(){resets++;}public void ApplyFallStretch(float f){}
 public void SnapToGrid(int s){RectTransform.anchoredPosition=GetFallCellPosition(X,Y,s);}
 public Vector2 GetFallCellPosition(int x,int y,int s)=>new Vector2(x*s,-y*s);
 public void PlayLandingSettle(int s,float d,float a){settles++;}
}
public class BoardController {
 public bool UseContinuousFallMotion=true;public int jobs,FallGeneration=1;
 internal TileFallMotionSystem fallMotion;
 public TileView[,] Tiles=new TileView[9,9];
 public TileView GetTileViewAt(int x,int y)=>x>=0&&y>=0&&x<9&&y<9?Tiles[x,y]:null;
 /* production match readiness */
 public enum BoardJobKind {DetachedFall}
 class Job:IDisposable {BoardController b;public Job(BoardController b){this.b=b;b.jobs++;}public void Dispose(){if(b!=null){b.jobs--;b=null;}}}
 public IDisposable BeginJob(BoardJobKind k)=>new Job(this);
 public List<Routine> routines=new List<Routine>();
 public Routine StartCoroutine(System.Collections.IEnumerator e){var r=new Routine(e);routines.Add(r);r.Step();return r;}
 public void Pump(){foreach(var r in routines.ToArray())r.Step();}

 public int TileSize=100; public float FallArrivalLeadCells,v0=8,accel=16,vmax=14;
 public Dictionary<Vector2Int,int> reservations=new Dictionary<Vector2Int,int>();
 public void GetFallKinematics(out float v,out float a,out float m){v=v0;a=accel;m=vmax;}
 public void ReserveTileTargetCell(Vector2Int c){reservations.TryGetValue(c,out int n);reservations[c]=n+1;}
 public void ClearReservedTileTargetCell(Vector2Int c){if(reservations.TryGetValue(c,out int n)){if(n==1)reservations.Remove(c);else reservations[c]=n-1;}}
}
public class Routine {
 readonly Stack<System.Collections.IEnumerator> stack=new Stack<System.Collections.IEnumerator>();
 Routine waiting;public bool Done=>stack.Count==0;
 public Routine(System.Collections.IEnumerator e){stack.Push(e);}
 public void Step(){
  if(waiting!=null){if(!waiting.Done)return;waiting=null;}
  while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}
   if(e.Current is System.Collections.IEnumerator inner){stack.Push(inner);continue;}
   if(e.Current is Routine r){if(r.Done)continue;waiting=r;}return;
  }
 }
}
public class ActionSequencer {}
public abstract class BoardAction {public virtual bool Blocking=>true;public abstract System.Collections.IEnumerator ExecuteVisuals(ActionSequencer s);}
public class GateAction:BoardAction {public bool finish;public override System.Collections.IEnumerator ExecuteVisuals(ActionSequencer s){while(!finish)yield return null;}}
public static class Checks {
 static int count;static void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
 static Vector2 P(float x,float y)=>new Vector2(x*100,-y*100);
 static TileView Tile(int x,int from,int to)=>new TileView{X=x,Y=to,RectTransform=new RectTransform{anchoredPosition=P(x,from)}};
 static TileFallMotionSystem.Ticket Start(TileFallMotionSystem m,TileView t,Vector2[] path,bool spawn=false,float delay=0,bool settle=false)
  =>m.Start(t,t.LifetimeVersion,t.PlannedFallGeneration,path,new Vector2Int(t.X,t.Y),spawn,delay,settle,.1f,1);
 static void Tick(TileFallMotionSystem m,float dt){Time.time+=dt;m.Tick(dt);}
 static float At(int fps){var b=new BoardController();var m=new TileFallMotionSystem(b);var t=Tile(0,0,20);Start(m,t,new[]{P(0,0),P(0,20)});for(int i=0;i<fps;i++)Tick(m,1f/fps);return t.RectTransform.anchoredPosition.y;}
 static bool clearStarted,clearFinish;
 static System.Collections.IEnumerator Clear(){clearStarted=true;while(!clearFinish)yield return null;}
 static void CheckCoordinator(bool continuous){
  var b=new BoardController{UseContinuousFallMotion=continuous};var gate=new GateAction();var t=Tile(0,0,1);
  t.IsPlannedToMoveThisFallPass=true;t.RuntimeState=TileRuntimeState.Falling;b.Tiles[t.X,t.Y]=t;
  clearStarted=false;clearFinish=false;
  var c=new BoardVisualCoordinator(b,new ActionSequencer());
  var root=b.StartCoroutine(c.PlayFallWithOverlappedClear(new List<BoardAction>{gate},new HashSet<TileView>{t},()=>Clear()));
  Check(!clearStarted&&!root.Done,""Clear waits for match tile arrival"");
  t.RaiseFallArrivedFromMotion();b.Pump();b.Pump();
  Check(clearStarted&&!gate.finish,""Arrival starts clear before unrelated fall tail finishes"");
  clearFinish=true;for(int i=0;i<4;i++)b.Pump();
  Check(root.Done==continuous,""Only continuous mode returns for refill while fall tail remains"");
  Check(b.jobs==(continuous?1:0),""Continuous fall tail retains level-end job"");
  gate.finish=true;for(int i=0;i<5;i++)b.Pump();
  Check(root.Done&&b.jobs==0,""Fall completion disposes level-end job"");
 }
 static void CheckMissingArrival(){
  var b=new BoardController();var gate=new GateAction();var t=Tile(2,5,6);
  t.IsPlannedToMoveThisFallPass=true;b.Tiles[t.X,t.Y]=t;
  var motion=new TileFallMotionSystem(b);b.fallMotion=motion;
  var ticket=Start(motion,t,new[]{P(2,5),P(2,6)});
  clearStarted=false;clearFinish=true;
  var c=new BoardVisualCoordinator(b,new ActionSequencer());
  var root=b.StartCoroutine(c.PlayFallWithOverlappedClear(new List<BoardAction>{gate},new HashSet<TileView>{t},()=>Clear()));
  // An external position write makes the production motor release this motion.
  // It completes the ticket without FallArrived; the old coordinator waited forever.
  t.SnapToGrid(b.TileSize);Tick(motion,.01f);gate.finish=ticket.Done;
  Check(ticket.Done&&t.IsRuntimeIdle&&!t.HasArrivedForPlannedFall,""Revoked motion reproduces a settled tile with no arrival notification"");
  for(int i=0;i<8;i++)b.Pump();
  Check(root.Done&&clearStarted,""A settled tile without FallArrived must not deadlock overlap clear"");
 }
 static void CheckDiagonalFlow(){
  var b=new BoardController{v0=10,accel=0,vmax=10};var m=new TileFallMotionSystem(b);
  var lower=Tile(2,2,3);lower.X=1;
  var upper=Tile(2,1,2);upper.X=3;
  // Different destinations, same feed column and adjacent turn: lower stone leads.
  var ut=Start(m,upper,new[]{P(2,1),P(3,2)});
  var lt=Start(m,lower,new[]{P(2,2),P(1,3)});
  Tick(m,.01f);
  Check(lower.RectTransform.anchoredPosition.x<200&&upper.RectTransform.anchoredPosition.x==200,
   ""Adjacent diagonals from one stream must not start together, regardless of action order"");
  for(int i=0;i<100;i++)Tick(m,.01f);
  Check(lt.Done&&ut.Done,""Queued diagonal stones both arrive without deadlock"");
  foreach(int fps in new[]{30,60,120}){
   b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
   lower=Tile(2,2,5);lower.X=1;upper=Tile(2,1,4);upper.X=1;
   lt=Start(m,lower,new[]{P(2,2),P(1,3),P(1,5)});ut=Start(m,upper,new[]{P(2,1),P(1,2),P(1,4)});
   bool ordered=true,hasStarted=false;float gap=10000;
   for(int i=0;i<fps*2;i++){
    Tick(m,1f/fps);
    if(upper.RectTransform.anchoredPosition.x<199.99f){hasStarted=true;ordered&=lower.RectTransform.anchoredPosition.x<=100.01f;}
    if(upper.RectTransform.anchoredPosition.x<=100.01f)gap=Math.Min(gap,upper.RectTransform.anchoredPosition.y-lower.RectTransform.anchoredPosition.y);
   }
   Check(ordered&&hasStarted&&lt.Done&&ut.Done&&gap>=91.99f,""Turn ordering and destination-column spacing at FPS=""+fps);
  }
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(1,2,3);lower.X=0;upper=Tile(7,1,2);upper.X=6;
  Start(m,lower,new[]{P(1,2),P(0,3)});Start(m,upper,new[]{P(7,1),P(6,2)});Tick(m,.01f);
  Check(lower.RectTransform.anchoredPosition.x<100&&upper.RectTransform.anchoredPosition.x<700,""Unrelated turns still run in parallel"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(2,6,7);lower.X=1;upper=Tile(2,1,2);upper.X=1;
  Start(m,lower,new[]{P(2,6),P(1,7)});Start(m,upper,new[]{P(2,1),P(1,2)});Tick(m,.01f);
  Check(lower.RectTransform.anchoredPosition.x<200&&upper.RectTransform.anchoredPosition.x<200,""Distant turns in the same columns do not acquire a column-wide lock"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(2,2,3);lower.X=1;upper=Tile(2,1,2);upper.X=3;
  lt=Start(m,lower,new[]{P(2,2),P(1,3)},false,.15f);ut=Start(m,upper,new[]{P(2,1),P(3,2)});
  var vertical=Tile(8,0,4);Start(m,vertical,new[]{P(8,0),P(8,4)});Tick(m,.05f);
  Check(upper.RectTransform.anchoredPosition.x==200&&vertical.RectTransform.anchoredPosition.y<0,""Delayed leader retains turn priority without blocking unrelated gravity"");
  for(int i=0;i<100;i++)Tick(m,.01f);Check(lt.Done&&ut.Done,""Delayed diagonal queue drains"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(2,2,4);lower.X=1;upper=Tile(2,1,3);upper.X=1;
  lt=Start(m,lower,new[]{P(2,2),P(1,3),P(1,4)});
  ut=Start(m,upper,new[]{P(2,1),P(1,2),P(1,3)});
  var tail=Tile(2,0,2);var tt=Start(m,tail,new[]{P(2,0),P(2,2)});
  bool noPass=true;for(int i=0;i<150;i++){
   Tick(m,.01f);
   if(upper.RectTransform.anchoredPosition.x>199.99f)noPass&=tail.RectTransform.anchoredPosition.y-upper.RectTransform.anchoredPosition.y>=91.99f;
  }
  Check(noPass&&lt.Done&&ut.Done&&tt.Done,""Vertical follower cannot pass a stone queued at the diagonal entrance"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(2,2,4);lower.X=1;upper=Tile(2,1,3);upper.X=1;
  Start(m,lower,new[]{P(2,2),P(1,3),P(1,4)});Start(m,upper,new[]{P(2,1),P(1,2),P(1,3)});Tick(m,.02f);
  lower.Y=6;lower.PlannedFallGeneration++;Start(m,lower,new[]{P(1,4),P(1,6)});Tick(m,.02f);
  Check(upper.RectTransform.anchoredPosition.x==200,""Retargeting an active leader preserves ownership of its unfinished turn"");
  lower.LifetimeVersion++;lower.token++;lower.RectTransform.anchoredPosition=P(8,8);Tick(m,.01f);
  Check(upper.RectTransform.anchoredPosition.x<200,""Recycled leader releases diagonal ownership immediately"");
  m.Reset();upper=Tile(2,1,2);upper.X=1;Start(m,upper,new[]{P(2,1),P(1,2)});Tick(m,.01f);
  Check(upper.RectTransform.anchoredPosition.x<200,""Board reset leaves no diagonal reservation behind"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(2,2,3);lower.X=1;upper=Tile(2,1,2);upper.X=3;
  Start(m,lower,new[]{P(2,2),P(1,3)});Start(m,upper,new[]{P(2,1),P(3,2)});Tick(m,.5f);
  Check(upper.RectTransform.anchoredPosition.x==200,""A long frame cannot launch two adjacent diagonals simultaneously"");
  Tick(m,.01f);Check(upper.RectTransform.anchoredPosition.x>200,""Completed turn releases on the next frame"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(1,2,5);upper=Tile(2,1,2);upper.X=1;
  lt=Start(m,lower,new[]{P(1,2),P(1,5)},false,.15f);ut=Start(m,upper,new[]{P(2,1),P(1,2)});Tick(m,.05f);
  Check(upper.RectTransform.anchoredPosition.x==200,""Diagonal waits for physical outlet clearance, not just a logical vacancy"");
  for(int i=0;i<100;i++)Tick(m,.01f);Check(lt.Done&&ut.Done,""Outlet guard releases once the vertical leader clears it"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(3,2,6);lower.X=1;upper=Tile(3,1,5);upper.X=1;
  lt=Start(m,lower,new[]{P(3,2),P(2,3),P(1,4),P(1,6)});ut=Start(m,upper,new[]{P(3,1),P(2,2),P(1,3),P(1,5)});
  bool followedBeforeLanding=false;
  for(int i=0;i<200;i++){Tick(m,.01f);if(!lt.Done&&upper.RectTransform.anchoredPosition.x<300)followedBeforeLanding=true;}
  Check(followedBeforeLanding,""Follower uses a released turn before the leader's entire fall ends"");
  Check(lt.Done&&ut.Done&&b.reservations.Count==0,""Multiple consecutive turns drain without leaking reservations"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  lower=Tile(1,1,4);lower.X=2;upper=Tile(3,1,3);upper.X=2;
  ut=Start(m,upper,new[]{P(3,1),P(2,2),P(2,3)});lt=Start(m,lower,new[]{P(1,1),P(2,2),P(2,4)});Tick(m,.01f);
  Check(lower.RectTransform.anchoredPosition.x>100&&upper.RectTransform.anchoredPosition.x==300,""Opposite entries into one outlet get deterministic local priority"");
  for(int i=0;i<150;i++)Tick(m,.01f);Check(lt.Done&&ut.Done,""Opposite-entry merge drains without deadlock"");
 }
 static void CheckShortColumnRefill(){
  foreach(float dt in new[]{1f/30,1f/60,1f/120,1f/600}){
   var b=new BoardController{v0=20,accel=10,vmax=26};var m=new TileFallMotionSystem(b);
   var stream=new List<TileView>();var tickets=new List<TileFallMotionSystem.Ticket>();
   // Five playable rows, three cleared at the bottom. Existing stones lead;
   // refill enters two rows above the board, as in the level 27 scene settings.
   for(int target=4;target>=0;target--){
    int source=target>=3?target-3:target-4;
    var tile=Tile(3,source,target);stream.Add(tile);
    tickets.Add(Start(m,tile,new[]{P(3,source),P(3,target)},target<3));
   }
   bool gap=true,continuous=true,refilled=false;int independentArrivals=0;
   var other=Tile(5,0,4);other.arrived=()=>independentArrivals++;
   Start(m,other,new[]{P(5,0),P(5,4)});
   for(int frame=0;frame<600&&!(!m.HasWork&&refilled);frame++){
    Tick(m,dt);
    for(int i=1;i<stream.Count;i++)
     gap&=stream[i].RectTransform.anchoredPosition.y-stream[i-1].RectTransform.anchoredPosition.y>=91.99f;
    if(!refilled&&tickets[0].Done){
     // Another impact frees the bottom while the upper refill is still airborne.
     var cleared=stream[0];cleared.LifetimeVersion++;cleared.token++;stream.RemoveAt(0);tickets.Clear();
     foreach(var tile in stream){
      var before=tile.RectTransform.anchoredPosition;int oldTarget=tile.Y;tile.Y++;tile.PlannedFallGeneration++;
      m.Prepare(tile,tile.LifetimeVersion,tile.PlannedFallGeneration,new[]{P(3,oldTarget),P(3,tile.Y)},false);
      tickets.Add(Start(m,tile,new[]{P(3,oldTarget),P(3,tile.Y)}));
      continuous&=(tile.RectTransform.anchoredPosition-before).sqrMagnitude<.001f;
     }
     var refill=Tile(3,-2,0);var tail=stream[stream.Count-1];
     tickets.Add(Start(m,refill,new[]{P(3,-2),P(3,0)},true));
     gap&=refill.RectTransform.anchoredPosition.y-tail.RectTransform.anchoredPosition.y>=91.99f;
     stream.Add(refill);refilled=true;
    }
   }
   Check(refilled&&gap&&continuous,""Short-column replan preserves order, spawn clearance and every existing visual position"");
   bool done=true;foreach(var ticket in tickets)done&=ticket.Done;
   Check(done&&!m.HasWork&&b.reservations.Count==0&&independentArrivals==1,
    ""Short-column repeated refill settles without blocking the independent column or leaking work"");
  }
 }
 static void CheckLongLeaderDelay(){
  foreach(float dt in new[]{1f/15,1f/30,1f/60,1f/120,.2f}){
   var b=new BoardController{v0=10,accel=0,vmax=10};var m=new TileFallMotionSystem(b);
   var lower=Tile(0,0,5);var upper=Tile(0,-1,4);
   var lt=Start(m,lower,new[]{P(0,0),P(0,5)},false,3f);
   var ut=Start(m,upper,new[]{P(0,-1),P(0,4)});
   bool gap=true;
   for(int i=0;i<(int)(5f/dt)+1;i++){
    Tick(m,dt);gap&=upper.RectTransform.anchoredPosition.y-lower.RectTransform.anchoredPosition.y>=91.99f;
   }
   Check(gap&&lt.Done&&ut.Done&&!m.HasWork,
    ""A wait longer than two seconds preserves spacing and drains after the leader starts"");
   b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
   lower=Tile(2,2,4);lower.X=1;upper=Tile(2,1,3);upper.X=3;
   lt=Start(m,lower,new[]{P(2,2),P(1,3),P(1,4)},false,3f);
   ut=Start(m,upper,new[]{P(2,1),P(3,2),P(3,3)});
   float elapsed=0;bool waited=true;
   for(int i=0;i<(int)(5f/dt)+1;i++){
    Tick(m,dt);elapsed+=dt;
    if(elapsed<2.9f)waited&=upper.RectTransform.anchoredPosition.x==200;
   }
   Check(waited&&lt.Done&&ut.Done&&!m.HasWork,
    ""A long diagonal wait cannot disable turn ordering"");
  }
 }
 public static int Run(){
  CheckLongLeaderDelay();
  CheckShortColumnRefill();
  CheckDiagonalFlow();
  CheckMissingArrival();
  var readinessBoard=new BoardController();var readinessMotion=new TileFallMotionSystem(readinessBoard);readinessBoard.fallMotion=readinessMotion;
  var readyTile=Tile(2,6,6);readinessBoard.Tiles[2,6]=readyTile;
  Check(readinessBoard.IsTileReadyForContinuousMatch(readyTile),""A live idle on-grid tile is ready without an event"");
  readyTile.RectTransform.anchoredPosition=P(2,5);
  Check(!readinessBoard.IsTileReadyForContinuousMatch(readyTile),""Idle flag alone cannot accept an airborne tile"");
  readyTile.SnapToGrid(100);readinessMotion.Prepare(readyTile,1,1,new[]{P(2,5),P(2,6)},false);readyTile.RuntimeState=TileRuntimeState.Idle;
  Check(!readinessBoard.IsTileReadyForContinuousMatch(readyTile),""Queued motion prevents false-ready match even on-grid"");
  readinessMotion.Reset();Start(readinessMotion,readyTile,new[]{P(2,6),P(2,7)});readyTile.RuntimeState=TileRuntimeState.Idle;
  Check(!readinessBoard.IsTileReadyForContinuousMatch(readyTile),""An active motion prevents premature match clear"");
  readinessMotion.Reset();readinessBoard.Tiles[2,6]=Tile(2,6,6);
  Check(!readinessBoard.IsTileReadyForContinuousMatch(readyTile),""A replaced board tile is not a valid match participant"");
  var staleBoard=new BoardController();var staleTile=Tile(0,0,1);staleTile.RuntimeState=TileRuntimeState.Falling;
  staleBoard.Tiles[0,1]=staleTile;clearStarted=false;clearFinish=true;var staleGate=new GateAction{finish=true};
  var staleCoordinator=new BoardVisualCoordinator(staleBoard,new ActionSequencer());
  var staleWait=staleBoard.StartCoroutine(staleCoordinator.PlayFallWithOverlappedClear(new List<BoardAction>{staleGate},new HashSet<TileView>{staleTile},()=>Clear()));
  staleTile.LifetimeVersion++;for(int i=0;i<4;i++)staleBoard.Pump();
  Check(staleWait.Done&&!clearStarted,""Recycled match participants abort stale clear and release the resolver"");
  CheckCoordinator(true);CheckCoordinator(false);
  var arrivalTile=Tile(0,0,1);arrivalTile.RaiseFallArrivedFromMotion();arrivalTile.board.FallGeneration++;
  arrivalTile.RaiseFallArrivedFromMotion();Check(arrivalTile.arrivals==1,""Another column's pass cannot duplicate arrival"");
  arrivalTile.PlannedFallGeneration++;Check(!arrivalTile.HasArrivedForPlannedFall,""Retarget invalidates old arrival marker"");
  arrivalTile.RaiseFallArrivedFromMotion();Check(arrivalTile.arrivals==2,""New plan receives its own arrival"");
  var overlapBoard=new BoardController();var prior=Tile(0,0,1);prior.RuntimeState=TileRuntimeState.Falling;overlapBoard.Tiles[prior.X,prior.Y]=prior;
  var shortFall=new GateAction{finish=true};clearStarted=false;clearFinish=true;
  var overlap=new BoardVisualCoordinator(overlapBoard,new ActionSequencer());
  var waiting=overlapBoard.StartCoroutine(overlap.PlayFallWithOverlappedClear(new List<BoardAction>{shortFall},new HashSet<TileView>{prior},()=>Clear()));
  for(int i=0;i<3;i++)overlapBoard.Pump();
  Check(!clearStarted,""Current action completion cannot release a match tile owned by a prior pass"");
  prior.RaiseFallArrivedFromMotion();for(int i=0;i<3;i++)overlapBoard.Pump();
  Check(waiting.Done&&clearStarted,""Prior-pass arrival releases overlap clear"");
  float y30=At(30),y60=At(60),y120=At(120);
  Check(Math.Abs(y30-y120)<.02f&&Math.Abs(y60-y120)<.02f,""30/60/120 FPS must cover identical distance"");
  Check(Math.Abs(y30+1287.5f)<.02f,""Capped accelerated trajectory matches analytic distance"");
  var b=new BoardController();var m=new TileFallMotionSystem(b);var t=Tile(0,0,5);
  var old=Start(m,t,new[]{P(0,0),P(0,5)});Tick(m,.1f);float before=t.RectTransform.anchoredPosition.y;
  t.Y=10;t.PlannedFallGeneration++;var current=Start(m,t,new[]{P(0,5),P(0,10)});
  Check(old.Done&&!current.Done&&t.RectTransform.anchoredPosition.y==before,""Retarget preserves position and completes superseded ticket"");
  Tick(m,.1f);Check(Math.Abs(t.RectTransform.anchoredPosition.y+192)<.01,""Retarget preserves acceleration/momentum"");
  for(int i=0;i<150;i++)Tick(m,.01f);
  Check(current.Done&&t.RuntimeState==TileRuntimeState.Idle&&b.reservations.Count==0,""Retarget lands and releases all reservations"");
  b=new BoardController();m=new TileFallMotionSystem(b);t=Tile(0,0,4);t.RectTransform.anchoredPosition=P(0,.1f);
  Start(m,t,new[]{P(0,0),P(0,4)});Tick(m,.001f);
  Check(t.RectTransform.anchoredPosition.y<-10,""A settle-offset tile must never return to its logical source"");
  b=new BoardController();m=new TileFallMotionSystem(b);t=Tile(0,0,5);
  Start(m,t,new[]{P(0,0),P(0,5)},false,.015f);Tick(m,.02f);
  Check(Math.Abs(t.RectTransform.anchoredPosition.y+4.02f)<.001f,""Partial-frame delay consumes only its own time"");
  b=new BoardController();m=new TileFallMotionSystem(b);t=Tile(0,0,1);
  Start(m,t,new[]{P(0,0),P(0,1)},false,0,true);
  t.arrived=()=>{t.LifetimeVersion++;t.token++;t.RuntimeState=TileRuntimeState.Swapping;t.settles=0;};
  Tick(m,1);Check(t.RuntimeState==TileRuntimeState.Swapping&&t.settles==0,""Landing callback can recycle tile without stale writes"");
  b=new BoardController();m=new TileFallMotionSystem(b);t=Tile(0,0,5);old=Start(m,t,new[]{P(0,0),P(0,5)});
  t.LifetimeVersion++;t.token++;t.resets=0;Tick(m,.1f);
  Check(old.Done&&t.resets==0&&b.reservations.Count==0,""Old lifetime cannot reset the new tile's visual scale"");
  b=new BoardController();m=new TileFallMotionSystem(b);t=Tile(0,0,1);old=Start(m,t,new[]{P(0,0),P(0,1)});
  t.Y=3;Tick(m,1);t.token++;t.RuntimeState=TileRuntimeState.Swapping;t.RectTransform.anchoredPosition=P(2,2);Tick(m,1);
  Check(t.RectTransform.anchoredPosition.x==200&&t.RuntimeState==TileRuntimeState.Swapping,""Parked recovery cannot teleport a new movement owner"");
  b=new BoardController();m=new TileFallMotionSystem(b);t=Tile(1,-1,4);
  m.Prepare(t,1,1,new[]{P(0,-1),P(0,1),P(1,1),P(1,2)},true);
  t.PlannedFallGeneration=2;m.Prepare(t,1,2,new[]{P(1,2),P(1,4)},false);
  current=Start(m,t,new[]{P(1,2),P(1,4)});
  Check(t.RectTransform.anchoredPosition.x==0&&t.RectTransform.anchoredPosition.y==100,""Newer action retains original spawn origin"");
  Tick(m,.05f);Check(t.RectTransform.anchoredPosition.x==0,""Newer action retains obstacle route prefix"");
  var stale=m.Start(t,1,1,new[]{P(0,-1),P(1,2)},new Vector2Int(1,2),true,0,false,0,0);
  Check(stale.Done,""Late older action cannot rewind newer motion"");
  for(int i=0;i<100;i++)Tick(m,.01f);Check(current.Done,""Merged route completes"");
  b=new BoardController{v0=10,accel=0,vmax=10};m=new TileFallMotionSystem(b);
  var lower=Tile(0,0,5);var upper=Tile(0,-1,4);
  Start(m,lower,new[]{P(0,0),P(0,5)},false,.2f);var upperTicket=Start(m,upper,new[]{P(0,-1),P(0,4)});
  bool gap=true;for(int i=0;i<140;i++){Tick(m,.01f);gap&=upper.RectTransform.anchoredPosition.y-lower.RectTransform.anchoredPosition.y>=91.99f;}
  Check(gap&&upperTicket.Done,""Follower never overtakes delayed leader and resumes with zero acceleration"");
  b=new BoardController();m=new TileFallMotionSystem(b);lower=Tile(0,0,10);Start(m,lower,new[]{P(0,0),P(0,10)});Tick(m,.2f);
  upper=Tile(0,-1,9);Start(m,upper,new[]{P(0,-1),P(0,9)},true);float uy=upper.RectTransform.anchoredPosition.y,ly=lower.RectTransform.anchoredPosition.y;Tick(m,.02f);
  Check(Math.Abs((uy-upper.RectTransform.anchoredPosition.y)-(ly-lower.RectTransform.anchoredPosition.y))<.01f,""Spawn inherits flow speed even when already properly spaced"");
  m.Reset();Check(b.reservations.Count==0&&!m.IsMoving(upper),""Board reset releases motor ownership and reservations"");
  return count;
 }
}
";
var tileRoot=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/TileView.cs")).GetRoot();
var arrivalMethods=string.Join("\n",tileRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
 .Where(m=>new[]{"RaiseFallArrived","RaiseFallArrivedFromMotion"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var arrivalProperty=tileRoot.DescendantNodes().OfType<PropertyDeclarationSyntax>()
 .Single(p=>p.Identifier.ValueText=="HasArrivedForPlannedFall").ToFullString();
harness=harness.Replace("/* production arrival hooks */",arrivalProperty+arrivalMethods);
var boardRoot=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BoardController.cs")).GetRoot();
var matchReadiness=boardRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
 .Single(m=>m.Identifier.ValueText=="IsTileReadyForContinuousMatch").ToFullString();
harness=harness.Replace("/* production match readiness */",matchReadiness);

// Source usings belong at the beginning of the combined script.
motor=motor.Replace("using System.Collections.Generic;", "").Replace("using UnityEngine;", "");
var options=ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly);
var coordinator=File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BoardVisualCoordinator.cs")
 .Replace("using System;", "").Replace("using System.Collections;", "")
 .Replace("using System.Collections.Generic;", "").Replace("using UnityEngine;", "");
var result=CSharpScript.EvaluateAsync<int>("using System.Collections;\n"+harness+motor+coordinator+"\nChecks.Run()",options).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" continuous fall checks");

var changedSources=new[]{
 "Assets/_Project/Scripts/Grid/Board/TileFallMotionSystem.cs",
 "Assets/_Project/Scripts/Grid/Board/Actions/FallAction.cs",
 "Assets/_Project/Scripts/Grid/Board/CascadeLogic.cs",
 "Assets/_Project/Scripts/Grid/Board/CascadeLogic.Motion.cs",
 "Assets/_Project/Scripts/Grid/Board/BoardController.cs",
 "Assets/_Project/Scripts/Grid/Board/BoardVisualCoordinator.cs",
 "Assets/_Project/Scripts/Grid/Board/BoardAnimator.cs",
 "Assets/_Project/Scripts/Grid/TileView.cs"
};
foreach(var path in changedSources){
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics()
  .Where(d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(path+": "+string.Join("\n",errors.Select(e=>e.ToString())));
}
Console.WriteLine("PASS: syntax validation of "+changedSources.Length+" integration sources (not a Unity compilation)");
