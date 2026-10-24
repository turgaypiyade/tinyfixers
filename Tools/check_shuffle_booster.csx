// Isolated production-method regression checks. No Unity build, Play, or cascade simulation.
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
string dir="Assets/_Project/Scripts/";
string[] files={"Grid/Board/BoosterService.cs","Grid/Board/BoardController.cs","Grid/Board/ShuffleWandFx.cs","UI/JokerFocusOverlayController.cs"};
foreach(var f in files){var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(dir+f)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(f+": "+string.Join("\n",errors.Select(e=>e.ToString())));}
string Methods(string path,params string[] names)=>string.Join("\n",CSharpSyntaxTree.ParseText(File.ReadAllText(dir+path)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x=>names.Contains(x.Identifier.ValueText)).Select(x=>x.ToFullString()));
var ui=Methods(files[3],"HandleJokerTap");
if(ui.Contains("if (boosterIndex == 3)") || !ui.Contains("ActivateFocusFor(boosterIndex)"))throw new Exception("Shuffle must use the shared targeting overlay");
var holes=Methods(files[3],"RefreshHolePanels");
if(!holes.Contains("hasObstacle || board.ActiveBooster == BoardController.BoosterMode.Shuffle") || !holes.Contains("proxy.enabled = acceptsTap"))throw new Exception("Hole overlay must accept shuffle confirmation and keep other modes blocked");
var harness=@"
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
public enum TileType{Red,Blue,Green,Yellow}public enum TileSpecial{None,Pulse}
public struct Vector2Int{public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);public static Vector2 operator*(Vector2 a,float f)=>new Vector2(a.x*f,a.y*f);public static Vector2 LerpUnclamped(Vector2 a,Vector2 b,float t)=>a+(b-a)*t;}
public static class Mathf{public const float PI=(float)Math.PI;public static float Clamp01(float f)=>Math.Max(0,Math.Min(1,f));public static float Max(float a,float b)=>Math.Max(a,b);public static float Sin(float f)=>(float)Math.Sin(f);public static float Cos(float f)=>(float)Math.Cos(f);}
public static class UnityEngine{public static class Random{static System.Random r=new System.Random(125);public static int Range(int a,int b)=>r.Next(a,b);}}
public class WaitForSeconds{public WaitForSeconds(float f){}}
public static class Time{public static float deltaTime=.2f;}
public class Curve{public int length;public float Evaluate(float f)=>f;}
public class Transform{public void SetAsLastSibling(){}}
public class RectTransform{public Vector2 anchoredPosition;}
public class GameObject{public bool activeInHierarchy=true;}
public class TileView{public TileType type;public TileSpecial special;public int X,Y,LifetimeVersion=1;public RectTransform RectTransform=new RectTransform();public Transform transform=new Transform();public GameObject gameObject=new GameObject();public TileType GetTileType()=>type;public TileSpecial GetSpecial()=>special;public void SetType(TileType t){type=t;}public void SetCoords(int x,int y){X=x;Y=y;}public void SnapToGrid(float s){RectTransform.anchoredPosition=new Vector2(X*s,-Y*s);}public bool IsCurrentLifetime(int v)=>v==LifetimeVersion&&gameObject.activeInHierarchy;}
public class Obstacles{public HashSet<int> movable=new HashSet<int>(),locked=new HashSet<int>(),oil=new HashSet<int>();public bool IsMovableObstacleAt(int x,int y)=>movable.Contains(y*10+x);public bool IsInteractionLockedAt(int x,int y)=>locked.Contains(y*10+x);public bool IsOilAt(int x,int y)=>oil.Contains(y*10+x);}
public class Scope:IDisposable{Action a;public Scope(Action a){this.a=a;}public void Dispose(){a();}}
public class ActionSequencer{}
public class PaintBucketJoker{public const string RejectTile=""reject"";public bool TryValidate(TileView t,out string reason){reason=null;return t!=null;}public IEnumerator Apply(TileView t){yield break;}}
public class BoardInitService{public TileType[,] next;public TileType[,] SimulateInitialTypes(int w,int h,bool[,] locked,TileType[] pool)=>(TileType[,])next.Clone();}
public class ShuffleWandFx{public int canceled;public IEnumerator Mix(float t){yield break;}public IEnumerator Exit(float t){yield return null;}public void Cancel(){canceled++;}}
public static class BoosterAccessService{public static int spends;public static bool allowed=true;public static bool CanUse(int i)=>allowed;public static void OnBoosterUsed(BoardController.BoosterMode m){spends++;}}
public class BoardIdleHintAndComboGlowController{public static void CancelHintsForBoard(BoardController b){}}
public class BoardController{
public enum BoosterMode{None,Single,Row,Column,Shuffle,PaintBucket}
public bool IsBusy=>busy>0;public bool InputLocked;public int busy,pause,resolve,repairs,ActiveBackgroundJobs,DetachedSequencerActions;public bool IsActionSequencePlaying,IsSpecialVisualInFlight;public int Width,Height;public float TileSize=100,SwapDurationWithMultiplier=.2f;public Curve SwapMoveCurve;public bool[,] Holes;public TileView[,] Tiles;public TileType[] RandomPool={TileType.Red,TileType.Blue,TileType.Green};public Obstacles ObstacleStateService=new Obstacles();public BoardInitService BoardInitService=new BoardInitService();public BoosterService boosterService;public ActionSequencer actionSequencer=new ActionSequencer();public BoosterMode activeBooster;public BoosterMode mode=>activeBooster;int width=>Width;int height=>Height;TileView[,] tiles=>Tiles;public PaintBucketJoker paintBucket;public Action<string> OnBoosterTargetRejected;public object specialResolver,cascadeLogic,lineSweepService,lightningSpawner,lineTravelPlayer;public List<IEnumerator> scheduled=new List<IEnumerator>();
public BoardController(int w,int h){Width=w;Height=h;Holes=new bool[w,h];Tiles=new TileView[w,h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){var t=new TileView{X=x,Y=y,type=(TileType)((x+y*2)%3)};t.SnapToGrid(TileSize);Tiles[x,y]=t;}BoardInitService.next=new TileType[w,h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)BoardInitService.next[x,y]=(TileType)((x+y+1)%3);boosterService=new BoosterService(this);}
public void BeginBusy(){busy++;}public void EndBusy(){busy--;}public IDisposable PauseFlowPump(){pause++;return new Scope(()=>pause--);}public IEnumerator WaitForExclusiveBoardAccess(){yield break;}public IEnumerator ResolveBoardPublic(){resolve++;yield break;}public void StartCoroutine(IEnumerator e){scheduled.Add(e);}public void SyncAllTilesToGridData(){}public void RefreshAllTileObstacleVisuals(){}public void RefreshAllSortingOrders(){}public void RefreshTileObstacleVisual(TileView t){}public void SyncTileData(int x,int y){}public void RepairTileGridIntegrity(string why,bool snapPositions=false){repairs++;if(snapPositions)foreach(var t in Tiles)if(t!=null)t.SnapToGrid(TileSize);}void SetBoosterMode(BoosterMode m){activeBooster=m;}void SetSelectedTile(TileView t){}
"+Methods(files[1],"ActivateBooster","TryUseBoosterAtBoardCell","TryUseBoosterAtCell")+@"
}
public class BoosterService{
readonly BoardController board;const float ShuffleSwirlSeconds=1.1f;ShuffleWandFx shuffleWandFx;
public BoosterService(BoardController b){board=b;}
ShuffleWandFx BeginShuffleWand(){return null;}void PlayJokerSfx(string key){}
public bool IsCellBoosterAffectable(int x,int y)=>!board.Holes[x,y];public IEnumerator ApplyBoosterRoutine(params object[] args){yield break;}
"+Methods(files[0],"CanShuffleBoard","ShuffleBoardRoutine","SafeShuffleBoardRoutine","BuildFallbackShuffleSourceMap","TryBuildPermutationShuffleTypes","CompletesRunAt","HasImmediateMatchInTypes","HasPlayableSwapInTypes","SwapCreatesMatch","BuildShuffleSourceMap","IsShuffleMapStale","AnimateShufflePreview","CommitShuffleFromSourceMap","IsShuffleCellLocked","BuildSafeShuffleState","ApplyShuffledTypes")+@"
public void Fallback(bool[,] locked,Vector2Int[,] map)=>BuildFallbackShuffleSourceMap(locked,map);
public bool Stale(TileView[,] t,int[,] v)=>IsShuffleMapStale(t,v);
public bool Permute(TileType[,] t,bool[,] l,out TileType[,] r)=>TryBuildPermutationShuffleTypes(t,l,out r);
}
public class Routine:IDisposable{Stack<IEnumerator> stack=new Stack<IEnumerator>();public bool Done=>stack.Count==0;public Routine(IEnumerator e){stack.Push(e);}public void Step(){while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();(e as IDisposable)?.Dispose();continue;}if(e.Current is IEnumerator n){stack.Push(n);continue;}return;}}public void Finish(){for(int i=0;i<200&&!Done;i++)Step();if(!Done)throw new Exception(""stalled"");}public void Dispose(){while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}}
public static class Checks{
static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
public static int Run(){
// Fallback map is a bijection and every eligible stone moves, for irregular masks.
for(int n=2;n<=81;n++){var b=new BoardController(9,9);var mask=new bool[9,9];for(int i=0;i<81;i++)mask[i%9,i/9]=i>=n;var map=new Vector2Int[9,9];b.boosterService.Fallback(mask,map);var seen=new HashSet<int>();for(int i=0;i<81;i++){var src=map[i%9,i/9];int k=src.y*9+src.x;if(i<n){Check(k<n&&k!=i,""unlocked view moves to unlocked cell"");Check(seen.Add(k),""no duplicate view"");}else Check(k==i,""locked identity preserved"");}Check(seen.Count==n,""all unlocked views retained"");}
var board=new BoardController(3,3);var service=board.boosterService;Check(service.CanShuffleBoard(),""normal board eligible"");foreach(var t in board.Tiles)t.special=TileSpecial.Pulse;Check(!service.CanShuffleBoard(),""all-special board not eligible"");board.Tiles[0,0].special=TileSpecial.None;Check(!service.CanShuffleBoard(),""one eligible stone insufficient"");board.Tiles[1,0].special=TileSpecial.None;Check(service.CanShuffleBoard(),""two eligible stones sufficient"");board.ObstacleStateService.locked.Add(1);Check(!service.CanShuffleBoard(),""interaction lock preserved"");board.ObstacleStateService.locked.Clear();board.ObstacleStateService.oil.Add(1);Check(!service.CanShuffleBoard(),""oil preserved"");board.ObstacleStateService.oil.Clear();board.ObstacleStateService.movable.Add(1);Check(!service.CanShuffleBoard(),""movable obstacle preserved"");board.ObstacleStateService.movable.Clear();board.Holes[1,0]=true;Check(!service.CanShuffleBoard(),""hole preserved"");
// Two-step activation: the slot only selects; every valid board cell can confirm.
board=new BoardController(4,1);BoosterAccessService.spends=0;board.ActivateBooster(3);Check(board.scheduled.Count==0&&board.mode==BoardController.BoosterMode.Shuffle&&BoosterAccessService.spends==0,""slot selects without starting or spending"");
for(int target=0;target<4;target++){board=new BoardController(4,1);board.Holes[2,0]=true;board.ObstacleStateService.movable.Add(3);board.ActivateBooster(3);Check(board.TryUseBoosterAtBoardCell(target,0),""normal/empty/hole/obstacle tap accepted"");Check(board.scheduled.Count==1&&board.mode==BoardController.BoosterMode.None,""second tap starts exactly one shuffle and clears selection"");Check(!board.TryUseBoosterAtBoardCell(target,0)&&board.scheduled.Count==1,""pointer down/click cannot fire twice"");}
board=new BoardController(4,1);board.ActivateBooster(3);Check(!board.TryUseBoosterAtBoardCell(-1,0)&&!board.TryUseBoosterAtBoardCell(4,0)&&board.scheduled.Count==0,""outside board does not confirm"");board.busy=1;board.TryUseBoosterAtBoardCell(0,0);Check(board.scheduled.Count==0&&board.mode==BoardController.BoosterMode.Shuffle,""busy tap retains selection"");board.busy=0;board.InputLocked=true;board.TryUseBoosterAtBoardCell(0,0);Check(board.scheduled.Count==0,""popup lock honored"");board.InputLocked=false;board.ActivateBooster(-1);Check(board.mode==BoardController.BoosterMode.None&&BoosterAccessService.spends==0,""cancel does not spend"");
board=new BoardController(1,1);board.ActivateBooster(3);board.TryUseBoosterAtBoardCell(0,0);Check(board.scheduled.Count==0&&BoosterAccessService.spends==0,""ineligible confirmation does not spend"");
// Forced fallback: two colors -> three requested colors cannot be mapped by color counts.
board=new BoardController(2,1);board.Tiles[0,0].type=TileType.Red;board.Tiles[1,0].type=TileType.Red;board.BoardInitService.next[0,0]=TileType.Blue;board.BoardInitService.next[1,0]=TileType.Green;var left=board.Tiles[0,0];var right=board.Tiles[1,0];BoosterAccessService.spends=0;var run=new Routine(board.boosterService.ShuffleBoardRoutine(board.actionSequencer));run.Step();Check(BoosterAccessService.spends==1,""charge consumed once after plan"");run.Step();Check(left.RectTransform.anchoredPosition.x!=0||left.RectTransform.anchoredPosition.y!=0,""fallback visibly animates even without wand sprite"");run.Finish();Check(ReferenceEquals(board.Tiles[0,0],right)&&ReferenceEquals(board.Tiles[1,0],left),""fallback commits original views in new cells"");Check(board.Tiles[0,0].type==TileType.Blue&&board.Tiles[1,0].type==TileType.Green,""fallback colors applied after animation"");Check(board.busy==0&&board.pause==0&&board.resolve==1,""busy and pause released; normal resolve requested"");
board=new BoardController(1,1);BoosterAccessService.spends=0;new Routine(board.boosterService.ShuffleBoardRoutine(board.actionSequencer)).Finish();Check(BoosterAccessService.spends==0&&board.busy==0&&board.pause==0,""no charge or lock leak on ineligible board"");
board=new BoardController(2,1);board.ActiveBackgroundJobs=1;BoosterAccessService.spends=0;new Routine(board.boosterService.SafeShuffleBoardRoutine(board.BoardInitService,true)).Finish();Check(BoosterAccessService.spends==0&&board.pause==0&&board.busy==0,""exclusive timeout does not overwrite live work"");
// Cancellation restores layout and releases both busy scopes and flow pause.
board=new BoardController(2,1);run=new Routine(board.boosterService.ShuffleBoardRoutine(board.actionSequencer));run.Step();run.Step();run.Dispose();Check(board.busy==0&&board.pause==0,""canceled nested routine releases scopes"");Check(board.Tiles[0,0].RectTransform.anchoredPosition.x==0&&board.Tiles[1,0].RectTransform.anchoredPosition.x==100,""cancellation restores stone positions"");
// Same pooled object, new lifetime must invalidate the map (reference-only guard misses it).
board=new BoardController(2,1);var snapshot=(TileView[,])board.Tiles.Clone();var versions=new int[2,1]{{1},{1}};Check(!board.boosterService.Stale(snapshot,versions),""live map accepted"");board.Tiles[0,0].LifetimeVersion++;Check(board.boosterService.Stale(snapshot,versions),""pooled reuse invalidates unchanged reference map"");
// Unchanged playable types must not be accepted as a successful shuffle.
board=new BoardController(2,1);var same=new TileType[2,1];TileType[,] result;Check(!board.boosterService.Permute(same,new bool[2,1],out result),""identity-only permutation rejected"");
return checks;}}
Checks.Run()";
var count=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+count+" shuffle regression assertions; 4 production C# files parsed. No Unity build/Play/cascade simulation.");
