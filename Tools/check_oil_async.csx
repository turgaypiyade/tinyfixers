// csi Tools/check_oil_async.csx — production actions, flight lifetime and input gates; no Unity build.
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
SyntaxNode Read(string p)=>CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetRoot();
string Methods(SyntaxNode r,params string[] names)=>string.Join("\n",r.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var animator=Read("Assets/_Project/Scripts/Grid/Board/OilSpreadAnimator.cs");
var board=Read("Assets/_Project/Scripts/Grid/Board/BoardController.cs");
var cls=animator.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
var members=string.Join("\n",cls.Members.Where(m=>!(m is MethodDeclarationSyntax md && new[]{"Configure","GetOrCreateImage"}.Contains(md.Identifier.ValueText))).Select(m=>m.ToFullString()));
var action=Read("Assets/_Project/Scripts/Grid/Board/Actions/OilSpreadAction.cs").DescendantNodes().OfType<ClassDeclarationSyntax>().First().ToFullString();
var pair=Read("Assets/_Project/Scripts/Grid/Board/Obstacles/OilSpreadService.cs").DescendantNodes().OfType<StructDeclarationSyntax>().First().ToFullString();
var stubs=@"
using System;
using System.Collections;
using System.Collections.Generic;
public class SerializeField:Attribute{}public class Min:Attribute{public Min(float v){}}public class Range:Attribute{public Range(float a,float b){}}
public class Sprite{}public class Texture{}
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public static bool operator==(Vector2Int a,Vector2Int b)=>a.x==b.x&&a.y==b.y;public static bool operator!=(Vector2Int a,Vector2Int b)=>!(a==b);public override bool Equals(object o)=>o is Vector2Int p&&p==this;public override int GetHashCode()=>x*397^y;}
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 LerpUnclamped(Vector2 a,Vector2 b,float k)=>new Vector2(a.x+(b.x-a.x)*k,a.y+(b.y-a.y)*k);}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new Vector3(1,1,1);public static Vector3 operator*(Vector3 a,float k)=>new Vector3(a.x*k,a.y*k,a.z*k);}
public struct Color {public float a;public static Color white=>new Color{a=1};}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Clamp01(float v)=>Math.Max(0,Math.Min(1,v));public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);}
public static class Time {public static float deltaTime=.05f;}
public class RectTransform {public Vector2 anchoredPosition;public Vector3 localScale;}
public class GameObject {public string name=""OilFx"";public bool active;public int GetInstanceID()=>7;public void SetActive(bool v){active=v;}}
public class Image {public GameObject gameObject=new GameObject();public Sprite sprite;public float fillAmount;public Color color=Color.white;}
public class MonoBehaviour {public bool isActiveAndEnabled=true;public static int created;protected GameObject Instantiate(GameObject prefab,object parent){created++;return new GameObject();}}
public static class GameDiagnostics {public static bool CriticalErrors=>false;}
public static class Debug {public static void LogWarning(string s){}}
public static class UiVfxPool {public static readonly Stack<GameObject> pool=new Stack<GameObject>();public static int returns;public static GameObject TryRent(string k,object p,string n)=>pool.Count>0?pool.Pop():null;public static void Return(string k,GameObject go,int cap){go.SetActive(false);pool.Push(go);returns++;}}
public class Handle:IDisposable {Action end;public Handle(Action a){end=a;}public void Dispose(){var e=end;end=null;e?.Invoke();}}
public class FlowStub {public bool HasUnlocalizedClear;public int MoveClearCount;public bool IsSpecialVisualInFlight;public BoardFlowScheduler.ActivityKind lastActivity;public int Count(BoardFlowScheduler.ActivityKind k)=>0;public IDisposable BeginNonBlocking(BoardFlowScheduler.ActivityKind k){lastActivity=k;return new Handle(()=>{});}}
public class BoardFlowScheduler {public enum ActivityKind{ObstacleSpread,Flight,Presentation,SpecialSweep,ComboStep,Fall}}
public class ActionSequencer {public bool AllActionsSettled=true;}
public abstract class BoardAction {public abstract IEnumerator ExecuteVisuals(ActionSequencer s);}
public class ObstacleStateService {
 public readonly HashSet<Vector2Int> oil=new HashSet<Vector2Int>();public int reads;
 public bool IsOilAt(int x,int y){reads++;return oil.Contains(new Vector2Int(x,y));}
 public bool TryAddOilAt(int x,int y)=>oil.Add(new Vector2Int(x,y));
}
public enum BoardState {Idle,Locked}
public class BoardController {
 /* JOB TYPES */
 public bool isActiveAndEnabled=true,useFlowActivities=true;public FlowStub Flow=new FlowStub(),flowScheduler=new FlowStub();
 public int RemainingMoves=10,refreshes,created;public int jobs=>ActiveBackgroundJobs;public BoardState CurrentState;
 private int _jobEpoch;private readonly int[] _jobCounts=new int[BoardJobKindCount];
 public int SpreadingObstacles=>_jobCounts[(int)BoardJobKind.ObstacleSpread];
 public ActionSequencer actionSequencer=new ActionSequencer();public object TilesRoot=new object();public OilSpreadAnimator animator;public bool free=true;
 public ObstacleStateService ObstacleStateService=new ObstacleStateService();
 /* JOB METHODS */
 public bool IsTileFreeForDynamicObstacle(int x,int y)=>free;
 public void RaiseObstacleCreatedDynamic(int x,int y){created++;}
 public T GetComponent<T>() where T:class=>animator as T;
 public void RefreshOilOverlays(){refreshes++;}
 /* GATES */
 public bool InputBlocked()=>IsDynamicInputBlockedByBoardFlow();
 public IEnumerator WaitCommit()=>WaitForDynamicInputCommit();
}
";
var extra=@"
 // UI geometry uses doubles; production PlaySpread/Update/Tick/Release/CancelSpreads stay intact.
 private void Configure(Flight f,OilSpreadPair p){
  f.cell=p.Target;f.age=0;f.growTime=.3f;f.revealTime=.4f;f.frameTime=.1f;f.fadeTime=.05f;
  f.finalSprite=new Sprite();f.frames.Clear();for(int i=0;i<4;i++)f.frames.Add(new Sprite());
  f.bridge=new Image();f.target=new Image();f.target.gameObject.active=false;f.blend=new Image();f.drop=new Image();f.dropRect=new RectTransform();
  f.dropStart=new Vector2(0,10);f.dropEnd=new Vector2(100,0);
 }
 static int checks;
 static void Check(bool ok,string s){checks++;if(!ok)throw new Exception(s);}
 static OilSpreadAnimator Setup(){var a=new OilSpreadAnimator();a.board=new BoardController();a.board.animator=a;a.spreadFxPrefab=new GameObject();return a;}
 static List<OilSpreadPair> Pair(int x=1)=>new List<OilSpreadPair>{new OilSpreadPair(new Vector2Int(0,0),new Vector2Int(x,0))};
 static void Start(OilSpreadAnimator a,int x=1){a.board.ObstacleStateService.oil.Add(new Vector2Int(x,0));a.PlaySpread(Pair(x));}
 static void Steps(OilSpreadAnimator a,int count){for(int i=0;i<count;i++)a.Update();}
 public static void RunChecks(){
  var a=Setup();var action=new OilSpreadAction(a.board,Pair());var run=action.ExecuteVisuals(null);
  Check(!run.MoveNext(),""action commits without yielding for visual"");
  Check(a.board.ObstacleStateService.IsOilAt(1,0)&&a.board.created==1,""target locked and dynamic goal notified before input resumes"");
  Check(a.board.Flow.lastActivity==BoardFlowScheduler.ActivityKind.Presentation,""Oil uses nonblocking presentation, not legacy spread activity"");
  Check(a.board.jobs==1&&a.board.BlockingBackgroundJobs==0&&a.board.SpreadingObstacles==0&&a.active.Count==1,""flight has a level-end hold"");
  Check(!a.board.InputBlocked()&&!a.board.WaitCommit().MoveNext(),""flight does not block regional input or next move commit"");
  Check(a.IsAnimatingTarget(new Vector2Int(1,0))&&!a.IsAnimatingTarget(new Vector2Int(2,0)),""overlay suppression is target-local"");
  int refresh=a.board.refreshes;Steps(a,3);Check(a.active.Count==1&&a.board.refreshes==refresh,""flight frames do not rebuild board overlays"");
  a.PlaySpread(Pair());Check(a.active.Count==1&&a.board.jobs==1,""duplicate target has no extra flight or job"");
  var state=a.active[0];var view=state.root;Steps(a,20);
  Check(a.board.jobs==0&&a.active.Count==0&&!a.IsAnimatingTarget(new Vector2Int(1,0)),""completion releases hold and overlay suppression"");
  Check(a.board.ObstacleStateService.IsOilAt(1,0),""completion preserves committed gameplay Oil"");
  int allocations=MonoBehaviour.created;Start(a,2);
  Check(MonoBehaviour.created==allocations&&ReferenceEquals(a.active[0].root,view)&&ReferenceEquals(a.active[0],state),""repeat spread reuses pooled view and flight buffer"");
  a.board.ObstacleStateService.oil.Remove(new Vector2Int(2,0));a.Update();
  Check(a.active.Count==0&&a.board.jobs==0&&!a.board.ObstacleStateService.IsOilAt(2,0),""special hit cancels flight without recreating Oil"");
  Start(a);Start(a,2);Check(a.board.jobs==2,""overlapping moves have independent lifetime holds"");
  a.OnDisable();Check(a.board.jobs==0&&a.active.Count==0,""disable returns all flights and jobs"");
  int returns=UiVfxPool.returns;a.CancelSpreads();Check(UiVfxPool.returns==returns&&a.board.jobs==0,""repeated cancellation does not double-return/decrement"");
  Start(a);a.board.isActiveAndEnabled=false;a.Update();Check(a.board.jobs==0&&a.active.Count==0,""board disable cancels even if animator stays enabled"");
  a=Setup();a.board.free=false;run=new OilSpreadAction(a.board,Pair()).ExecuteVisuals(null);Check(!run.MoveNext()&&a.active.Count==0&&!a.board.ObstacleStateService.IsOilAt(1,0),""stale moving/special target is rejected before commit"");
  a=Setup();a.spreadFxPrefab=null;run=new OilSpreadAction(a.board,Pair()).ExecuteVisuals(null);
  Check(!run.MoveNext()&&a.board.jobs==0&&a.board.ObstacleStateService.IsOilAt(1,0),""missing visual has no leaked hold and keeps valid Oil"");
  a=Setup();a.isActiveAndEnabled=false;Start(a);Check(a.board.jobs==0&&a.active.Count==0,""disabled animator cannot acquire a late job"");
  a=Setup();a.board.BeginJob(BoardController.BoardJobKind.ObstacleSpread);Check(a.board.InputBlocked(),""legacy barrel/water spread input policy stays unchanged"");
  Console.WriteLine(""PASS: ""+checks+"" Oil async, target lock, impact cancellation, reuse and teardown checks."");
 }
";
var jobType=board.DescendantNodes().OfType<EnumDeclarationSyntax>().Single(n=>n.Identifier.ValueText=="BoardJobKind").ToFullString();
var jobHandle=board.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(n=>n.Identifier.ValueText=="BoardJobHandle").ToFullString();
var jobCount=board.DescendantNodes().OfType<FieldDeclarationSyntax>().Single(n=>n.Declaration.Variables.Any(v=>v.Identifier.ValueText=="BoardJobKindCount")).ToFullString();
var jobProperties=string.Join("\n",board.DescendantNodes().OfType<PropertyDeclarationSyntax>().Where(n=>new[]{"ActiveBackgroundJobs","BlockingBackgroundJobs"}.Contains(n.Identifier.ValueText)).Select(n=>n.ToFullString()));
stubs=stubs.Replace("/* JOB TYPES */",jobType+jobHandle+jobCount).Replace("/* JOB METHODS */",Methods(board,"BeginJob")+jobProperties);
stubs=stubs.Replace("/* GATES */",Methods(board,"IsDynamicInputBlockedByBoardFlow","WaitForDynamicInputCommit","BeginFlowActivityForJob"));
var source=stubs+pair+action+"\npublic class OilSpreadAnimator:MonoBehaviour {\n"+members+extra+"\n}\nOilSpreadAnimator.RunChecks();";
var script=CSharpScript.Create(source,ScriptOptions.Default.WithReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Take(15).Select(d=>d.ToString())));
script.RunAsync().GetAwaiter().GetResult();
// Integration assertions: data committed by the action, no sequencer wait in Oil's settle branch.
var resolve=board.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="ResolveBoard").ToFullString();
int begin=resolve.IndexOf("var spreadTargets = oilSpreadService.CalculateSpread");
int end=resolve.IndexOf("// Barrel mud",begin);
string oil=resolve.Substring(begin,end-begin);
if(oil.Contains("actionSequencer.Enqueue")||oil.Contains("yield return")||!oil.Contains("oilSpreadResolvedThisMove = true"))throw new Exception("Oil integration must commit once without sequencer waits");
Console.WriteLine("PASS: Oil settle integration is once per move and does not wait for visuals.");
