// Run: csi Tools/check_ancient_seal_collection.csx (production methods, no Unity/project build).
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

var path = "Assets/_Project/Scripts/Grid/Board/Obstacles/AncientSealService.cs";
var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
if (errors.Length > 0) throw new Exception(string.Join("\n", errors.Select(e => e.ToString())));
var names = new[] { "SelectCollectionTarget", "CanCollectAt", "Arrive", "ReleaseFlight", "CancelFlights" };
var methods = string.Join("\n", tree.DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString().Replace("private ", "public ")));
var state = tree.DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Single(c => c.Identifier.ValueText == "SealState").ToFullString().Replace("private sealed", "public sealed");
var harness = @"
using System;
using System.Collections.Generic;
using System.Linq;
public enum ObstacleId { None, AncientSeal }
public static class Mathf { public static int Max(int a,int b) => Math.Max(a,b); }
public class BoardController {
 public int Width=9, resolves;
 public ObstacleStateService ObstacleStateService=new ObstacleStateService();
 public void RequestResolveAfterActionSequence(){resolves++;}
}
public class ObstacleStateService {
 public HashSet<int> present=new HashSet<int>(),buried=new HashSet<int>();
 public List<string> events=new List<string>();
 public bool IsBuriedAnywhere(ObstacleId id,int origin)=>buried.Contains(origin);
 public ObstacleId GetObstacleIdAt(int x,int y)=>present.Contains(y*9+x)?ObstacleId.AncientSeal:ObstacleId.None;
 public void RevealStampedBeneathByOverOrigin(int origin){present.Remove(origin);events.Add(""reveal"");}
 public void NotifyAncientSealBroken(int origin){events.Add(""goal"");}
}
public class Disposable : IDisposable {public int count;public void Dispose(){count++;}}
public class Root {public object gameObject=new object();}
public class Service {
 public const int LockCount=3;
 " + state + @"
 public class CollectionFlight {
  public int origin,color;public SealState state;public bool arrived;
  public Disposable job=new Disposable(),fx=new Disposable();
 }
 public BoardController board=new BoardController();
 public Dictionary<int,SealState> seals=new Dictionary<int,SealState>();
 public int[] focusedOrigin={-1,-1,-1};
 public HashSet<CollectionFlight> flights=new HashSet<CollectionFlight>();
 public Dictionary<long,object> gatherBatches=new Dictionary<long,object>();
 public int gatherFrame=-1,holds;
 public Root flightRoot;
 public event Action<int,int,int,int> OnCollected;
 public event Action<int,int> OnLockClosed;
 public event Action<int> OnSealBroken;
 public void BeginBreakHold(int origin,SealState s){holds++;}
 public void StopAllCoroutines(){}
 public void Destroy(object go){}
 public SealState Add(int origin,int count,int color=0){
  var s=new SealState{startColor=color};
  for(int i=0;i<3;i++)s.remaining[i]=s.total[i]=count;
  seals[origin]=s;board.ObstacleStateService.present.Add(origin);return s;
 }
 public CollectionFlight Reserve(int color){
  int origin=SelectCollectionTarget(color,board.ObstacleStateService);
  if(origin<0)return null;
  var s=seals[origin];s.inFlight[color]++;
  var f=new CollectionFlight{origin=origin,color=color,state=s};flights.Add(f);return f;
 }
 " + methods + @"
}
public static class Checks {
 static int checks;
 static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
 public static int Run(){
  var s=new Service();s.Add(30,3);s.Add(10,3);s.Add(20,3);
  var sent=Enumerable.Range(0,9).Select(_=>s.Reserve(0)).ToArray();
  Check(sent.Select(f=>f.origin).SequenceEqual(new[]{10,10,10,20,20,20,30,30,30}),""Fill each counter before the next; independent of registration order"");
  Check(s.Reserve(0)==null,""Fully reserved counters cannot overfill"");
  Check(s.seals.Values.All(v=>v.remaining[0]==3&&v.inFlight[0]==3),""Flights reserve slots without changing displayed counters"");
  int collected=0,closed=0;s.OnCollected+=(o,c,r,t)=>collected++;s.OnLockClosed+=(o,c)=>closed++;
  foreach(var f in sent.Reverse()){s.Arrive(f);s.Arrive(f);s.ReleaseFlight(f);s.ReleaseFlight(f);}
  Check(collected==9&&closed==3,""Out-of-order arrivals credit each stone and close each lock once"");
  Check(s.seals.Values.All(v=>v.active==1&&v.inFlight[0]==0&&v.remaining[0]==0),""Arrival advances the active color"");
  Check(sent.All(f=>f.fx.count==1&&f.job.count==1)&&s.flights.Count==0,""Flights dispose exactly once"");

  s=new Service();var lower=s.Add(10,3,1);s.Add(30,3);
  Check(s.Reserve(0).origin==30,""Skip a seal waiting for another color"");
  lower.startColor=0;
  Check(s.Reserve(0).origin==30,""A newly ready lower-origin seal cannot steal an unfinished counter"");
  Check(s.Reserve(0).origin==30&&s.Reserve(0).origin==10,""Move on only after all needed slots are reserved"");
  s.Add(50,2,2);
  Check(s.Reserve(2).origin==50&&s.Reserve(0).origin==10,""Colors have independent focused targets"");

  s=new Service();s.Add(10,3);s.Add(20,3);var obstacles=s.board.ObstacleStateService;
  obstacles.buried.Add(10);
  Check(s.Reserve(0).origin==20,""Covered seals receive no stones"");
  obstacles.buried.Remove(10);
  Check(s.Reserve(0).origin==20,""Revealing a seal preserves the current focus"");
  obstacles.buried.Add(20);
  Check(s.Reserve(0).origin==10,""A newly covered focus yields to an eligible seal"");
  obstacles.present.Remove(10);
  Check(s.Reserve(0)==null,""Removed or covered seals cannot be targets"");

  s=new Service();var a=s.Add(10,2);s.Add(20,2);
  var cancelled=s.Reserve(0);s.ReleaseFlight(cancelled);
  Check(a.remaining[0]==2&&a.inFlight[0]==0,""Cancelled flight refunds only its reservation"");
  Check(s.Reserve(0).origin==10,""Cancelled reservation is available on the same counter"");
  s.Reserve(0);s.Reserve(0);var active=s.flights.ToArray();
  s.gatherBatches[1]=new object();s.gatherFrame=123;s.flightRoot=new Root();s.CancelFlights();
  Check(s.seals.Values.All(v=>v.inFlight.All(n=>n==0)),""Teardown releases all outstanding reservations"");
  Check(active.All(f=>f.job.count==1&&f.fx.count==1)&&s.flights.Count==0,""Teardown disposes effects and board jobs"");
  Check(s.focusedOrigin.All(o=>o==-1)&&s.gatherBatches.Count==0&&s.gatherFrame==-1&&s.flightRoot==null,""Teardown resets focus and gather batches"");
  Check(s.Reserve(0).origin==10,""Re-enable begins with the first unfinished seal"");

  s=new Service();a=s.Add(10,1);var b=s.Add(20,1);
  for(int color=0;color<3;color++){
   var first=s.Reserve(color);Check(first.origin==10,""Immediate-arrival first seal"");s.Arrive(first);s.ReleaseFlight(first);
   var second=s.Reserve(color);Check(second.origin==20,""Immediate-arrival second seal"");s.Arrive(second);s.ReleaseFlight(second);
  }
  Check(a.active==3&&b.active==3&&s.holds==2&&s.board.resolves==2,""All locks complete and normal break flow resumes"");
  Check(s.board.ObstacleStateService.events.SequenceEqual(new[]{""reveal"",""goal"",""reveal"",""goal""}),""Reveal hidden content before reporting each goal"");
  Check(s.Reserve(0)==null&&s.Reserve(1)==null&&s.Reserve(2)==null,""Broken seals cannot collect"");
  return checks;
 }
}
Checks.Run()";
var options = ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly);
var count = CSharpScript.EvaluateAsync<int>(harness, options).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + count + " AncientSeal sequential collection checks; production source syntax valid");
