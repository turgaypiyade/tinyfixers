// Production idle-pump and gravity query logic; standalone allocation comparison, no Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var flow=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BoardFlowPump.cs")).GetRoot();
string Member(string name)=>flow.DescendantNodes().OfType<MemberDeclarationSyntax>().Single(m=>m is MethodDeclarationSyntax md&&md.Identifier.ValueText==name||m is PropertyDeclarationSyntax p&&p.Identifier.ValueText==name).ToFullString();
var cascade=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/CascadeLogic.cs")).GetRoot();
var query=cascade.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="HasAnyResolvableEmptyPlayableCell").ToFullString();
// Baseline preserves the former method exactly; compare under identical board data.
var oldQuery=query.Replace("var slotYs = resolvableSlotBuffer;\n                slotYs.Clear();","List<int> slotYs = null;").Replace("slotYs.Add(y);","(slotYs ??= new List<int>()).Add(y);").Replace("if (slotYs.Count > 0)","if (slotYs != null)");
var stubs=@"
using System;using System.Collections.Generic;
public class BoardAction {}
public class Motion {public int LandedCount;}
public class Matcher {public int ChangeVersion;}
public class Gravity {
 public bool IsGravityBusy;public int plans;public List<BoardAction> actions=new List<BoardAction>();
 public List<BoardAction> CalculateCascades(){plans++;return actions;}
}
public class BoardController {
 public enum BoardJobKind {DetachedFall}
 public bool IsBusy,IsActionSequencePlaying,OpenCell,PendingMatch,StartClear,Plan;
 public int ActiveBackgroundJobs,ClearedCellCount,FlowJobsInFlight,notes,sorts;
 public object Tiles=new object();public Gravity CascadeLogic=new Gravity();public Matcher MatchFinder=new Matcher();public Motion FallMotion=new Motion();
 public void StartFlowAction(BoardAction action,BoardJobKind kind){ActiveBackgroundJobs++;}
 public void RefreshAllSortingOrders(){sorts++;}public void NoteFlowPumpWork(){notes++;}
}
public class QueryBoard {
 public int Width=4,Height=8;public bool[,] blocked=new bool[4,8],holes=new bool[4,8];public object[,] Tiles=new object[4,8];
}
public static class Mathf {public static int Min(int a,int b)=>Math.Min(a,b);}
";
var pump=@"
public class PumpProbe {
 public BoardController board;public int scans,openQueries;
 private int quietVersion,quietClears,quietLandings;private bool hasQuietSnapshot,hasPendingMatches;
 public PumpProbe(BoardController board){this.board=board;}
 private bool HasOpenCell(){openQueries++;return board.OpenCell;}
 private bool ShouldPlanGravity()=>board.Plan;
 private bool ClearSettledGroups(){scans++;hasPendingMatches=board.PendingMatch;return board.StartClear;}
"+Member("CanSleep")+Member("Tick")+"\n}";
var queryBase=@"
 public QueryBoard board;private readonly List<int> resolvableSlotBuffer=new List<int>(16);
 private bool IsGravityBlockedCell(int x,int y)=>board.blocked[x,y];
 private bool IsTileSlotCell(int x,int y)=>!board.holes[x,y]&&!board.blocked[x,y];
 private bool IsSegmentConnectedToSpawnEdge(int x,int top){for(int y=0;y<top;y++)if(board.blocked[x,y])return false;return true;}
";
var probes="public class NewQueryProbe {"+queryBase+query+"}\npublic class OldQueryProbe {"+queryBase+oldQuery+"}";
var tests=@"
int checks=0;Action<bool,string> check=(ok,name)=>{checks++;if(!ok)throw new Exception(name);};
var b=new BoardController();var pump=new PumpProbe(b);pump.Tick();
for(int i=0;i<10000;i++)pump.Tick();
check(pump.scans==1&&pump.openQueries==1,""ten thousand unchanged idle frames perform one initial scan"");
b.MatchFinder.ChangeVersion++;pump.Tick();check(pump.scans==2,""data/obstacle mutation wakes pump"");
b.ClearedCellCount++;pump.Tick();check(pump.scans==3,""hold release or cell clear wakes pump"");
b.FallMotion.LandedCount++;pump.Tick();check(pump.scans==4,""landing event wakes pump"");
b.IsBusy=true;pump.Tick();pump.Tick();check(pump.scans==6,""busy work keeps continuous fallback alive"");b.IsBusy=false;
b.ActiveBackgroundJobs=1;pump.Tick();pump.Tick();check(pump.scans==8,""async jobs prevent idle sleep"");b.ActiveBackgroundJobs=0;
b.MatchFinder.ChangeVersion++;b.PendingMatch=true;pump.Tick();pump.Tick();
check(pump.scans==10,""unsettled/rest-delayed matches keep being evaluated"");
b.PendingMatch=false;pump.Tick();pump.Tick();check(pump.scans==11,""matching resumes sleep after becoming quiet"");
b.OpenCell=true;b.Plan=false;b.ClearedCellCount++;pump.Tick();pump.Tick();
check(pump.scans==12,""unfillable pocket sleeps until an actual change"");
b.MatchFinder.ChangeVersion++;b.Plan=true;b.CascadeLogic.actions.Add(new BoardAction());pump.Tick();
check(b.CascadeLogic.plans==1&&b.notes==1&&b.sorts==1,""woken gravity starts existing flow work"");
var qb=new QueryBoard();var current=new NewQueryProbe{board=qb};var previous=new OldQueryProbe{board=qb};var random=new Random(714);
for(int test=0;test<500;test++){
 for(int x=0;x<qb.Width;x++)for(int y=0;y<qb.Height;y++){
  qb.blocked[x,y]=random.Next(5)==0;qb.holes[x,y]=random.Next(6)==0;qb.Tiles[x,y]=random.Next(3)==0?null:new object();
 }
 check(current.HasAnyResolvableEmptyPlayableCell()==previous.HasAnyResolvableEmptyPlayableCell(),""gravity scratch reuse preserves segmented/hole/blocker query"");
}
for(int x=0;x<qb.Width;x++)for(int y=0;y<qb.Height;y++){qb.blocked[x,y]=false;qb.holes[x,y]=false;qb.Tiles[x,y]=new object();}
for(int i=0;i<50;i++){current.HasAnyResolvableEmptyPlayableCell();previous.HasAnyResolvableEmptyPlayableCell();}
long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++)previous.HasAnyResolvableEmptyPlayableCell();long oldBytes=GC.GetAllocatedBytesForCurrentThread()-start;
start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++)current.HasAnyResolvableEmptyPlayableCell();long newBytes=GC.GetAllocatedBytesForCurrentThread()-start;
check(oldBytes>0&&newBytes==0,""warmed synchronous gravity query avoids managed allocation"");
Console.WriteLine(""PASS: ""+checks+"" idle/wakeup/gravity checks; standalone 2,000-query allocation baseline=""+oldBytes+"" B, changed=""+newBytes+"" B. This is not a Unity/device profiler measurement."");
";
await CSharpScript.RunAsync(stubs+pump+probes+tests,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
