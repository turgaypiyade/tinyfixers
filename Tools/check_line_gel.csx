// Production line/cross sweep methods with delayed-clear doubles; no Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
string root="Assets/_Project/Scripts/Grid/Board/";
string Methods(string text,params string[] names)=>string.Join("\n",CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
string runner=File.ReadAllText(root+"Actions/SpecialChainRunner.cs");
string sweeps=Methods(runner,"SweepLine","SweepCross","SweepLinesSimultaneously","IsProtectedCell");
string fixture="Tools/Fixtures/LineGel/Before.cs.txt";
if(Args.Contains("--capture-before")){
 Directory.CreateDirectory(Path.GetDirectoryName(fixture));File.WriteAllText(fixture,"class Before {\n"+sweeps+"\n}\n");
 Console.WriteLine("Captured before-change line sweeps.");return;
}
foreach(var file in new[]{root+"Actions/SpecialChainRunner.cs",root+"Resolver/SpecialResolver.cs"}){
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(d=>d.ToString())));
}
string before=Methods(File.ReadAllText(fixture),"SweepLine","SweepCross","SweepLinesSimultaneously","IsProtectedCell");
string gel=Methods(File.ReadAllText(root+"BoardController.cs"),"IsGelSpreadSourceTile","NoteGelSpreadOrigin","MarkGelContaminated","ShouldPaintGelOnClear","ResetGelSpreadForNewMove","SpreadGelToCell","TryPaintGelForClearedTile","NoteGelSpreadParticipants");
string harness=@"
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public override int GetHashCode()=>x*31+y;public override bool Equals(object o)=>o is Vector2Int v&&x==v.x&&y==v.y;}
public enum TileSpecial {None,LineH,LineV,PulseCore}
public class TileView {public int X,Y;public TileSpecial special;public bool GelContaminated,Caged;public TileSpecial GetSpecial()=>special;public static implicit operator bool(TileView t)=>t!=null;public static bool operator!(TileView t)=>t==null;}
public class Gel {public HashSet<Vector2Int> cells=new HashSet<Vector2Int>(),sealedCells=new HashSet<Vector2Int>();public bool IsSpreadSourceAt(int x,int y)=>cells.Contains(new Vector2Int(x,y))&&!sealedCells.Contains(new Vector2Int(x,y));public void AddGel(int x,int y)=>cells.Add(new Vector2Int(x,y));}
public class Obstacles {public HashSet<Vector2Int> movable=new HashSet<Vector2Int>();public bool IsMovableObstacleAt(int x,int y)=>movable.Contains(new Vector2Int(x,y));}
public class BoardController {
 public static BoardController Current;public int Width=7,Height=7;int width=7,height=7;
 public TileView[,] Tiles=new TileView[7,7];public Gel spreadingGelService=new Gel();public Obstacles ObstacleStateService=new Obstacles();
 public HashSet<Vector2Int> holes=new HashSet<Vector2Int>();public bool ResetBeforeClear;bool gelSpreadActiveThisMove;
 readonly HashSet<TileView> gelCarriedBySwap=new HashSet<TileView>(),gelCleanMatchTiles=new HashSet<TileView>();
 public bool IsMaskHole(int x,int y)=>holes.Contains(new Vector2Int(x,y));
 public TileView At(int x,int y)=>Tiles[x,y];
 public IEnumerable<TileView> All(){foreach(var t in Tiles)if(t!=null)yield return t;}
 /* GEL */
}
public static class SpecialUtils {public static bool CanAffectCell(BoardController b,int x,int y)=>!b.IsMaskHole(x,y);}
public class ActionSequencer {}
public enum ClearAnimationMode {LightningStrike}
public struct LightningLineStrike {public Vector2Int originCell;public bool isHorizontal;public LightningLineStrike(Vector2Int c,bool h){originCell=c;isHorizontal=h;}}
public class MatchClearAction {
 public HashSet<TileView> targets;readonly List<Action> arrivals=new List<Action>();
 public MatchClearAction(IEnumerable<TileView> targets,bool doShake,ClearAnimationMode animationMode,HashSet<Vector2Int> affectedCells,bool includeAdjacentOverTileBlockerDamage,List<TileView> lightningVisualTargets,List<LightningLineStrike> lightningLineStrikes,bool isSpecialPhase){this.targets=new HashSet<TileView>(targets);}
 public void AddArrivalTrigger(Vector2Int cell,Action callback){arrivals.Add(callback);}
 public IEnumerator ExecuteVisuals(ActionSequencer sequencer){
  var b=BoardController.Current;if(b.ResetBeforeClear)b.ResetGelSpreadForNewMove();
  foreach(var t in targets)b.TryPaintGelForClearedTile(t);foreach(var cb in arrivals)cb();yield break;
 }
}
public class Runner {
 readonly BoardController board;readonly List<TileView> consumeTiles;readonly HashSet<Vector2Int> protectedCells=new HashSet<Vector2Int>();
 readonly List<Vector2Int> simultaneousLineCells;readonly List<LightningLineStrike> virtualLineSweeps;
 readonly int crossHalf;readonly bool gelFromSource;public List<TileView> Arrivals=new List<TileView>();
 public Runner(BoardController b,List<TileView> sources,bool carry,int half,List<Vector2Int> real,List<LightningLineStrike> virtuals){board=b;consumeTiles=sources;gelFromSource=carry;crossHalf=half;simultaneousLineCells=real;virtualLineSweeps=virtuals;}
 bool IsCaged(TileView t)=>t.Caged;void AnchorQueued(TileView t,int x,int y){}void LaunchArrivalSubChain(TileView t){Arrivals.Add(t);}
 IEnumerator RunGravityWithOverlap(bool hasNext){yield break;}
 /* SWEEPS */
 public IEnumerator Run(string mode,bool h,TileView source){var seen=new HashSet<TileView>();
  if(mode==""single"")return SweepLine(new ActionSequencer(),source,source.X,source.Y,h,new Queue<TileView>(),seen);
  if(mode==""cross"")return SweepCross(new ActionSequencer(),new Vector2Int(3,3),seen);
  return SweepLinesSimultaneously(new ActionSequencer(),seen,mode==""real"");
 }
}
public class Routine {readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();public Routine(IEnumerator e){stack.Push(e);}public void Finish(){for(int n=0;n<1000&&stack.Count>0;n++){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();(e as IDisposable)?.Dispose();continue;}if(e.Current is IEnumerator c)stack.Push(c);}if(stack.Count>0)throw new Exception(""stalled"");}}
public static class Checks {
 public static int Run(bool fixedCode){int checks=0;Action<bool,string> check=(ok,m)=>{checks++;if(!ok)throw new Exception(m);};
  foreach(string mode in new[]{""single"",""cross"",""real"",""virtual""})foreach(bool horizontal in new[]{false,true})foreach(int source in new[]{0,1,2,3,4,5,6})foreach(bool delayed in new[]{false,true}){
   var b=new BoardController{ResetBeforeClear=delayed};BoardController.Current=b;
   for(int x=0;x<7;x++)for(int y=0;y<7;y++)b.Tiles[x,y]=new TileView{X=x,Y=y};
   // Targets previously classified as a clean cascade must still inherit an explicitly contaminated hit.
   b.NoteGelSpreadParticipants(b.All());
   var a=b.At(3,3);var partner=b.At(4,3);a.special=horizontal?TileSpecial.LineH:TileSpecial.LineV;partner.special=TileSpecial.LineH;
   if(source==1)a.GelContaminated=true;if(source==2)partner.GelContaminated=true;
   if(source==3||source==4)b.spreadingGelService.cells.Add(new Vector2Int(3,3));
   if(source==4)b.spreadingGelService.sealedCells.Add(new Vector2Int(3,3));
   // Crossing gel alone does not make a dry source contagious.
   if(source==5)b.spreadingGelService.cells.Add(new Vector2Int(horizontal?5:3,horizontal?3:5));
   b.NoteGelSpreadOrigin(a);b.NoteGelSpreadOrigin(partner);
   bool comboCarries=b.IsGelSpreadSourceTile(a)||b.IsGelSpreadSourceTile(partner);
   bool carry=mode==""real""?false:mode==""single""?source==2:comboCarries;
   // Source was contaminated when the combo was decided, but its view has since
   // been released/reused before a carried payload arrives.
   if(source==6)carry=true;
   // Real implant lines test their own source cells, independently of a root combo flag.
   var lineCell=new Vector2Int(a.X,a.Y);
   var real=new List<Vector2Int>{lineCell};var virtuals=new List<LightningLineStrike>{new LightningLineStrike(lineCell,horizontal)};
   var arrival=b.At(horizontal?5:3,horizontal?3:5);arrival.special=TileSpecial.PulseCore;
   var hole=b.At(horizontal?0:3,horizontal?3:0);b.holes.Add(new Vector2Int(hole.X,hole.Y));
   var movable=b.At(horizontal?1:3,horizontal?3:1);b.ObstacleStateService.movable.Add(new Vector2Int(movable.X,movable.Y));
   var caged=b.At(horizontal?6:3,horizontal?3:6);caged.Caged=true;
   var action=new Runner(b,new List<TileView>{a,partner},carry,0,real,virtuals);new Routine(action.Run(mode,horizontal,a)).Finish();
   bool wet=source==1||source==2||source==3||source==6;
   if(mode==""single""||mode==""real"")wet=source==1||source==3||(source==2&&mode==""single"");
   if(source==6)wet=true;
   bool explicitMarks=wet&&(fixedCode||(mode==""single""&&(source==1||source==3)));
   var target=b.At(horizontal?2:3,horizontal?3:2);
   check(target.GelContaminated==explicitMarks,""line/cross/combo explicitly captures contaminated targets: ""+mode+"" source=""+source);
   check(b.spreadingGelService.cells.Contains(new Vector2Int(target.X,target.Y))==explicitMarks,""delayed clear and clean-match exemptions preserve gel: ""+mode);
   check(arrival.GelContaminated==explicitMarks,""arrival specials inherit the emitting source's gel: ""+mode);
   check(!hole.GelContaminated&&!movable.GelContaminated&&!caged.GelContaminated,""holes, movable obstacle-owned and caged targets retain exclusions"");
   check(!b.At(0,6).GelContaminated,""unhit tiles remain untouched"");
  }
  if(fixedCode)foreach(bool reverse in new[]{false,true}){
   var b=new BoardController{ResetBeforeClear=true};BoardController.Current=b;
   for(int x=0;x<7;x++)for(int y=0;y<7;y++)b.Tiles[x,y]=new TileView{X=x,Y=y};
   b.NoteGelSpreadParticipants(b.All());
   var dry=b.At(3,1);dry.special=TileSpecial.LineH;
   var wet=b.At(3,3);wet.special=TileSpecial.LineV;wet.GelContaminated=true;
   var lines=new List<Vector2Int>{new Vector2Int(dry.X,dry.Y),new Vector2Int(wet.X,wet.Y)};if(reverse)lines.Reverse();
   var action=new Runner(b,null,false,0,lines,null);new Routine(action.Run(""real"",true,dry)).Finish();
   check(b.At(3,1).GelContaminated&&b.At(3,2).GelContaminated,""wet strike contaminates an intersection even if dry strike collected it first"");
   check(!b.At(2,1).GelContaminated&&!b.At(4,1).GelContaminated,""simultaneous source snapshots prevent order-dependent spread into a dry line's other targets"");
  }
  return checks;
 }
}
Checks.Run(/* FIXED */)
".Replace("/* GEL */",gel);
var options=ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly);
int oldChecks=await CSharpScript.EvaluateAsync<int>(harness.Replace("/* SWEEPS */",before).Replace("/* FIXED */","false"),options);
Console.WriteLine("PASS: "+oldChecks+" before-change checks reproduce the missing combo/payload target contamination.");
int newChecks=await CSharpScript.EvaluateAsync<int>(harness.Replace("/* SWEEPS */",sweeps).Replace("/* FIXED */","true"),options);
Console.WriteLine("PASS: "+newChecks+" current production line/cross/implant/payload source, delayed-clear, arrival and exclusion checks.");
