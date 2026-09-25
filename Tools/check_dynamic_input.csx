// csi Tools/check_dynamic_input.csx — production methods with small Unity doubles; no project build.
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
 .OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var boardMethods=Methods("Assets/_Project/Scripts/Grid/Board/BoardController.cs", "AreDynamicMatchTilesStable");
var tileMethods=Methods("Assets/_Project/Scripts/Grid/TileView.cs", "OwnsDragPosition", "OnEndDrag");
var hold=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BoardFlowPump.cs"))
 .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.ValueText=="CellHold").ToFullString();
var harness=@"
using System;
using System.Collections;
using System.Collections.Generic;
public struct Vector2Int {public int x,y; public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public class PointerEventData {}
public class TileView {
 public bool dragAccepted,dragConsumedSwap,IsRuntimeIdle=true;
 public int dragLifetime=1,dragMoveToken=1,dragX,dragY,X,Y,lifetime=1,token=1,snaps;
 public BoardController board;
 public static implicit operator bool(TileView t)=>t!=null;
 public static bool operator!(TileView t)=>t==null;
 public bool IsCurrentLifetime(int v)=>v==lifetime;
 public bool IsMoveTokenCurrent(int v)=>v==token;
 public void SnapToGrid(int size){snaps++;}
 public void StartCoroutine(IEnumerator e){}
 private IEnumerator ResetWasDragging(){yield break;}
 /* TILE */
}
public class Motion {public HashSet<TileView> pending=new HashSet<TileView>();public bool HasPendingMotion(TileView t)=>pending.Contains(t);}
public class BoardController {
 public TileView[,] tiles=new TileView[5,5];public int TileSize=100,cellHoldEpoch;
 public Motion fallMotion=new Motion();
 public Dictionary<Vector2Int,int> cellHolds=new Dictionary<Vector2Int,int>();
 public HashSet<Vector2Int> pendingTriggeredSpecialCells=new HashSet<Vector2Int>();
 public HashSet<Vector2Int> reserved=new HashSet<Vector2Int>();
 public TileView GetTileViewAt(int x,int y)=>tiles[x,y];
 public bool IsReservedTileTargetCell(int x,int y)=>reserved.Contains(new Vector2Int(x,y));
 public void ReleaseHeldCell(Vector2Int cell,int epoch){if(epoch!=cellHoldEpoch)return;if(--cellHolds[cell]==0)cellHolds.Remove(cell);}
 internal void ForgetClearReleasedHold(CellHold hold){}
 /* BOARD */
 public bool Valid(TileView[] group,CellHold hold)=>AreDynamicMatchTilesStable(group,hold);
}
public static class Checks {
 static int count;static void Check(bool ok,string msg){count++;if(!ok)throw new Exception(msg);}
 static TileView Tile(BoardController b,int x,int y){var t=new TileView{board=b,X=x,Y=y,dragX=x,dragY=y};b.tiles[x,y]=t;return t;}
 public static int Run(){
  var b=new BoardController();var a=Tile(b,1,1);var partner=Tile(b,1,2);var c=Tile(b,2,1);var d=Tile(b,3,1);
  var group=new[]{a,c,d};var ac=new Vector2Int(1,1);var pc=new Vector2Int(1,2);var cc=new Vector2Int(2,1);
  b.cellHolds[ac]=1;b.cellHolds[pc]=1;
  var own=new CellHold(b,new HashSet<Vector2Int>{ac,pc},b.cellHoldEpoch);
  Check(b.Valid(group,own),""A settled match must validate while its own swap hold is active"");
  Check(b.cellHolds.Count==2,""Validation must preserve swap holds until clear starts"");
  Check(!b.Valid(group,null),""Another transaction cannot borrow the swap hold"");
  b.cellHolds[ac]++;
  Check(!b.Valid(group,own),""A second owner of the endpoint still blocks validation"");b.cellHolds[ac]--;
  b.cellHolds[cc]=1;
  Check(!b.Valid(group,own),""An unrelated held match contributor is rejected"");b.cellHolds.Remove(cc);
  b.pendingTriggeredSpecialCells.Add(ac);
  Check(!b.Valid(group,own),""The swap cannot bypass a pending special anchor"");b.pendingTriggeredSpecialCells.Clear();
  b.fallMotion.pending.Add(c);
  Check(!b.Valid(group,own),""Queued/active fall contributors are not settled"");b.fallMotion.pending.Clear();
  c.IsRuntimeIdle=false;Check(!b.Valid(group,own),""Clearing/swapping contributors are not settled"");c.IsRuntimeIdle=true;
  b.reserved.Add(cc);Check(!b.Valid(group,own),""A reserved contributor is rejected"");b.reserved.Clear();
  b.tiles[2,1]=new TileView();Check(!b.Valid(group,own),""Replaced views cannot validate a match"");b.tiles[2,1]=c;
  b.cellHoldEpoch++;Check(!b.Valid(group,own),""An old board epoch cannot exempt current holds"");b.cellHoldEpoch--;
  own.Dispose();Check(b.Valid(group,null)&&b.cellHolds.Count==0,""Released holds leave settled matches available"");
  b.cellHolds[ac]=1;Check(!b.Valid(group,own),""A disposed hold cannot exempt a later owner"");b.cellHolds.Clear();
  // Unity delivers EndDrag even if BeginDrag returned before accepting the gesture.
  a.OnEndDrag(new PointerEventData());Check(a.snaps==0,""Rejected drag cannot snap a falling stone to its future grid cell"");
  a.dragAccepted=true;a.OnEndDrag(new PointerEventData());Check(a.snaps==1&&!a.dragAccepted,""Accepted unfinished drag returns its own idle tile to the grid"");
  a.dragAccepted=true;a.token++;a.OnEndDrag(new PointerEventData());Check(a.snaps==1,""New movement owner prevents an old drag from snapping"");a.token--;
  a.dragAccepted=true;a.lifetime++;a.OnEndDrag(new PointerEventData());Check(a.snaps==1,""Pool reuse prevents a stale pointer release from snapping"");a.lifetime--;
  a.dragAccepted=true;a.IsRuntimeIdle=false;a.OnEndDrag(new PointerEventData());Check(a.snaps==1,""A planned fall takes precedence over pointer release"");a.IsRuntimeIdle=true;
  a.dragAccepted=true;a.Y++;a.OnEndDrag(new PointerEventData());Check(a.snaps==1,""Replanned coordinates invalidate drag ownership"");a.Y--;
  a.dragAccepted=true;b.tiles[1,1]=null;a.OnEndDrag(new PointerEventData());Check(a.snaps==1,""A detached tile cannot be snapped by pointer release"");b.tiles[1,1]=a;
  a.dragAccepted=true;a.dragConsumedSwap=true;a.OnEndDrag(new PointerEventData());Check(a.snaps==1,""A consumed swap is never snapped by EndDrag"");
  return count;
 }
}
";
harness=harness.Replace("/* TILE */",tileMethods).Replace("/* BOARD */",boardMethods);
// Keep the harness's public signatures compatible with the production internal hold class.
harness=harness.Replace("public bool Valid(","internal bool Valid(");
var result=CSharpScript.EvaluateAsync<int>(harness+hold+"\nChecks.Run()",
 ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" dynamic match and drag ownership checks");
foreach(var path in new[]{"Assets/_Project/Scripts/Grid/Board/BoardFlowPump.cs","Assets/_Project/Scripts/Grid/Board/BossDuelObstaclePressure.cs"}){
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(path+": "+string.Join("\n",errors.Select(e=>e.ToString())));
}
Console.WriteLine("PASS: flow pump and boss pressure syntax (not a Unity compilation)");
