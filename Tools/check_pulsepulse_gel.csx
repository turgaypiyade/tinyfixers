// Isolated production source/target/paint methods with board doubles, not a board/cascade simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
string grid="Assets/_Project/Scripts/Grid/";
string Methods(string path,params string[] names)=>string.Join("\n",CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
string Branch(string path)=>CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes().OfType<IfStatementSyntax>().Single(n=>n.Condition.ToString()=="bothOriginallySpecial && originalSa == TileSpecial.PulseCore && originalSb == TileSpecial.PulseCore").ToFullString();
foreach(var path in new[]{grid+"Board/Resolver/SpecialResolver.cs",grid+"Board/Actions/SpecialChainRunner.cs",grid+"Board/BoardController.cs"}) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
string harness=@"
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public override int GetHashCode()=>x*100+y;public override bool Equals(object o)=>o is Vector2Int c&&c.x==x&&c.y==y;}
public static class Mathf {public static int Abs(int i)=>Math.Abs(i);public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}
public enum TileSpecial {None,PulseCore,LineH}
public class TileView {public int X,Y;public TileSpecial special;public bool GelContaminated,Caged;public TileSpecial GetSpecial()=>special;public static implicit operator bool(TileView t)=>t!=null;public static bool operator!(TileView t)=>t==null;}
public class GelService {
 public HashSet<Vector2Int> cells=new HashSet<Vector2Int>(),sealedCells=new HashSet<Vector2Int>();
 public bool IsSpreadSourceAt(int x,int y)=>cells.Contains(new Vector2Int(x,y))&&!sealedCells.Contains(new Vector2Int(x,y));
 public void AddGel(int x,int y){cells.Add(new Vector2Int(x,y));}
}
public class Impact {public void PlayPulseCoreExplosionVfxAtCell(int x,int y,int radiusCells,Action<float> onRadiusPx){}}
public class Hold:IDisposable {public void Dispose(){}}
public class BoardController {
 public int width=9,height=9;public int Width=>width;public int Height=>height;public TileView[,] Tiles=new TileView[9,9];
 public bool IsSpecialActivationPhase,ResetBeforeClear;public float PulseChainCatchOverlap;public Impact PulseCoreImpactService=new Impact();
 public GelService spreadingGelService=new GelService();bool gelSpreadActiveThisMove;
 readonly HashSet<TileView> gelCleanMatchTiles=new HashSet<TileView>(),gelCarriedBySwap=new HashSet<TileView>();
 public HashSet<Vector2Int> holes=new HashSet<Vector2Int>(),movable=new HashSet<Vector2Int>();
 public bool IsMaskHole(int x,int y)=>holes.Contains(new Vector2Int(x,y));public IDisposable HoldForFlow(IEnumerable<Vector2Int> cells)=>new Hold();
 /* GEL */
}
public class ResolutionContext {public HashSet<TileView> Affected=new HashSet<TileView>();public HashSet<Vector2Int> AffectedCells=new HashSet<Vector2Int>();}
public static class SpecialCellUtils {
 public static void MarkAffectedCell(ResolutionContext c,TileView t,BoardController b){c.AffectedCells.Add(new Vector2Int(t.X,t.Y));}
 public static void TryAddObstacleImpact(BoardController b,int x,int y,List<Vector2Int> impacts){}
 public static bool TryRouteMovableToImpact(BoardController b,int x,int y,List<Vector2Int> impacts)=>b.movable.Contains(new Vector2Int(x,y));
}
public static class SpecialUtils {public static bool CanAffectCell(BoardController b,int x,int y)=>!b.IsMaskHole(x,y);}
public class Effects {public void EmitComboTriggered(TileSpecial a,TileSpecial b,Vector2Int c){}}
public class BoardAction {}
public class ActionSequencer {}
public class Resolver {
 public BoardController board;ResolutionContext ctx=new ResolutionContext();Effects effectOrchestrator=new Effects();
 public Resolver(BoardController b){board=b;}
 List<BoardAction> ResolveOtherSpecialInline(TileView t)=>new List<BoardAction>();void TraceSpecialChain(string s,TileView a,TileView b){}
 public List<BoardAction> Resolve(TileView a,TileView b){
  board.NoteGelSpreadOrigin(a);board.NoteGelSpreadOrigin(b);
  var actions=new List<BoardAction>();var originalSa=a.GetSpecial();var originalSb=b.GetSpecial();bool bothOriginallySpecial=true;
  /* BRANCH */
  return actions;
 }
}
public class SpecialChainRunner:BoardAction {
 readonly BoardController board;readonly int areaHalf;readonly List<TileView> initialSpecials,consumeTiles;
 readonly HashSet<Vector2Int> protectedCells=new HashSet<Vector2Int>();
 public HashSet<TileView> Captured;public Dictionary<int,List<TileView>> Arrivals;
 public SpecialChainRunner(BoardController board,List<TileView> initialSpecials,int areaHalf,float overlap,Func<TileView,List<BoardAction>> resolve,List<TileView> consumeTiles=null){this.board=board;this.initialSpecials=initialSpecials;this.areaHalf=areaHalf;this.consumeTiles=consumeTiles;}
 bool IsCaged(TileView t)=>t.Caged;void AnchorQueued(TileView t,int x,int y){}
 IEnumerator RunGravityWithOverlap(bool next){yield break;}
 IEnumerator RunRadialArrivalClear(ActionSequencer s,Func<Vector2Int,int> ringOf,Func<float> radius,HashSet<TileView> clearTiles,HashSet<Vector2Int> affectedCells,List<Vector2Int> impactCells,Dictionary<int,List<TileView>> arrivalSpecials){
  Captured=clearTiles;Arrivals=arrivalSpecials;
  if(board.ResetBeforeClear)board.ResetGelSpreadForNewMove();
  foreach(var t in clearTiles)board.TryPaintGelForClearedTile(t);
  yield break;
 }
 /* PULSE */
 public IEnumerator Run()=>ExplodePulse(new ActionSequencer(),initialSpecials[0],initialSpecials[0].X,initialSpecials[0].Y,new Queue<TileView>(),new HashSet<TileView>{initialSpecials[0]});
}
public class Routine {readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();public Routine(IEnumerator e){stack.Push(e);}public void Finish(){for(int n=0;n<1000&&stack.Count>0;n++){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();(e as IDisposable)?.Dispose();continue;}if(e.Current is IEnumerator c)stack.Push(c);}if(stack.Count>0)throw new Exception(""stalled"");}}
public static class Checks {
 public static int Run(bool old){int checks=0;Action<bool,string> check=(ok,m)=>{checks++;if(!ok)throw new Exception(m);};
  // Source modes: dry, carried gel on A/B, exposed gel under A/B, sealed gel under B.
  foreach(int source in new[]{0,1,2,3,4,5})foreach(bool delayed in new[]{false,true}) {
   var b=new BoardController{ResetBeforeClear=delayed};
   for(int x=0;x<9;x++)for(int y=0;y<9;y++)b.Tiles[x,y]=new TileView{X=x,Y=y};
   var a=b.Tiles[4,4];var partner=b.Tiles[5,4];a.special=partner.special=TileSpecial.PulseCore;
   var chain=b.Tiles[7,4];chain.special=TileSpecial.LineH;
   var clean=b.Tiles[1,1];b.NoteGelSpreadParticipants(new[]{clean});
   var hole=b.Tiles[2,2];b.holes.Add(new Vector2Int(2,2));
   var movable=b.Tiles[2,3];b.movable.Add(new Vector2Int(2,3));
   var cage=b.Tiles[3,2];cage.Caged=true;
   // Gel crossed by a dry explosion is not a source for the combo.
   b.spreadingGelService.cells.Add(new Vector2Int(6,6));
   if(source==1)a.GelContaminated=true;if(source==2)partner.GelContaminated=true;
   if(source==3)b.spreadingGelService.cells.Add(new Vector2Int(a.X,a.Y));
   if(source==4||source==5)b.spreadingGelService.cells.Add(new Vector2Int(partner.X,partner.Y));
   if(source==5)b.spreadingGelService.sealedCells.Add(new Vector2Int(partner.X,partner.Y));
   var action=(SpecialChainRunner)new Resolver(b).Resolve(a,partner)[0];new Routine(action.Run()).Finish();
   bool contagious=source>=1&&source<=4;bool actualMarks=contagious&&!(old&&(source==2||source==4));
   check(clean.GelContaminated==actualMarks,""explicit target marking accepts gel from either combo source"");
   check(chain.GelContaminated==actualMarks,""arrival special inherits combo gel before its own activation"");
   check(b.spreadingGelService.cells.Contains(new Vector2Int(clean.X,clean.Y))==actualMarks,""previous clean-match exemption cannot erase an explicitly jelled pulse hit"");
   check(!hole.GelContaminated&&!movable.GelContaminated&&!cage.GelContaminated,""holes, obstacle-owned movable cells and caged targets stay outside tile marking"");
   check(!b.spreadingGelService.cells.Contains(new Vector2Int(0,0)),""unhit cells are not painted"");
   if(!contagious)check(!clean.GelContaminated&&!chain.GelContaminated,""dry/hidden-seed combo does not spread merely by crossing gel"");
   if(actualMarks)check(action.Captured.All(t=>t.GelContaminated),""every collected clear target retains gel independent of delayed scope reset"");
  }
  return checks;
 }
}
Checks.Run(/* OLD */)
".Replace("/* GEL */",Methods(grid+"Board/BoardController.cs","IsGelSpreadSourceTile","NoteGelSpreadOrigin","MarkGelContaminated","ShouldPaintGelOnClear","ResetGelSpreadForNewMove","SpreadGelToCell","TryPaintGelForClearedTile","NoteGelSpreadParticipants"))
 .Replace("/* PULSE */",Methods(grid+"Board/Actions/SpecialChainRunner.cs","ExplodePulse","IsProtectedCell","SquareCells"));
var options=ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly);
var before=await CSharpScript.EvaluateAsync<int>(harness.Replace("/* BRANCH */",Branch("Docs/Checks/PulsePulse-Gel/Before/SpecialResolver.cs")).Replace("/* OLD */","true"),options);
var after=await CSharpScript.EvaluateAsync<int>(harness.Replace("/* BRANCH */",Branch(grid+"Board/Resolver/SpecialResolver.cs")).Replace("/* OLD */","false"),options);
Console.WriteLine("PASS: "+before+" before / "+after+" after checks. Previous partner-only gel omission reproduced; both source orders, delayed scope reset, clean/dry/hidden-seed controls and chained-special contamination checked. No Unity Play/build.");
