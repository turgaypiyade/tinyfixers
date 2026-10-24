// Production eligibility/add/clear/board-entry methods with doubles, not Unity or board/cascade simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
string grid="Assets/_Project/Scripts/Grid/";
string Methods(string file,params string[] names)=>string.Join("\n",CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var serviceTree=CSharpSyntaxTree.ParseText(File.ReadAllText(grid+"SpreadingGel/SpreadingGelOverlayService.cs"));
var levelTree=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Core/LevelData.cs"));
var channel=levelTree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>().Single(t=>t.Identifier.ValueText=="WaterChannelEntry").ToFullString();
foreach(var file in new[]{grid+"SpreadingGel/SpreadingGelOverlayService.cs",grid+"GridSpawner.cs"}) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var caller=CSharpSyntaxTree.ParseText(File.ReadAllText(grid+"GridSpawner.cs")).GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single(i=>i.Expression.ToString()=="spreadingGelOverlayService.Init");
if(caller.ArgumentList.Arguments.Count!=5||caller.ArgumentList.Arguments[4].Expression.ToString()!="resolvedLevel")throw new Exception("Spawner must supply the current level's channel paths before seed drawing.");
string source=@"
using System;using System.Collections.Generic;
public class TooltipAttribute:Attribute {public TooltipAttribute(string text){}}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static int Abs(int n)=>Math.Abs(n);}
public enum CellType {Empty,Normal}
public class RectTransform {}
public class MudCellView {public object gameObject=new object();public void SetVisible(bool v){}public void Clear(){}}
public class RawImage {public object gameObject=new object();}
public class LevelData {public int width,height;public int[] cells,obstacles;public WaterChannelEntry[] waterChannels;public LevelData(int w,int h){width=w;height=h;cells=new int[w*h];obstacles=new int[w*h];for(int i=0;i<cells.Length;i++)cells[i]=1;}}
/* CHANNEL */
public class SpreadingGelOverlayService {
 readonly Dictionary<int,MudCellView> viewsByCellIndex=new Dictionary<int,MudCellView>();
 readonly HashSet<int> sealedCellIndices=new HashSet<int>(),hiddenCells=new HashSet<int>(),channelCellIndices=new HashSet<int>();
 readonly Dictionary<int,RawImage> cornerPatches=new Dictionary<int,RawImage>();
 int gridWidth,gridHeight,tileSize;RectTransform overlayRoot;object pendingBorderRefresh;
 public event Action OnGelChanged;public int spawns,animations,refreshes,destroys;
 public int Count=>viewsByCellIndex.Count;public int RevealedCount=>Count-sealedCellIndices.Count;
 MudCellView SpawnCell(int x,int y){spawns++;return new MudCellView();}void AnimateSpawn(MudCellView v){animations++;}void QueueRefreshAllBorders(){refreshes++;}
 void StopCoroutine(object c){}void Destroy(object go){destroys++;}
 /* SERVICE */
}
public class TileView {public int X,Y;public bool GelContaminated=true;public static implicit operator bool(TileView t)=>t!=null;public static bool operator!(TileView t)=>t==null;}
public class Board {
 public SpreadingGelOverlayService spreadingGelService;int width=4,height=4;bool gelSpreadActiveThisMove=true;
 readonly HashSet<TileView> gelCleanMatchTiles=new HashSet<TileView>();public bool BoardHasGel=>spreadingGelService!=null&&spreadingGelService.Count>0;
 bool IsMaskHole(int x,int y)=>false;public void Created(TileView t){PaintGelUnderCreatedSpecial(t);}
 /* BOARD */
}
int checks=0;Action<bool,string> check=(ok,m)=>{checks++;if(!ok)throw new Exception(m);};
var l=new LevelData(4,4);l.waterChannels=new[]{new WaterChannelEntry{pathCellIndices=new[]{0,4,8,12}}};
var gel=new SpreadingGelOverlayService();int changes=0;gel.OnGelChanged+=()=>changes++;
gel.Init(4,4,100,new RectTransform(),l);var board=new Board{spreadingGelService=gel};
check(gel.AddGelImmediate(3,3),""ordinary authored seed remains supported"");
int spawns=gel.spawns,animations=gel.animations,refreshes=gel.refreshes,events=changes;
// Same persistent path is excluded before opening, after logical obstacle removal,
// and after completion (channel art remains; no current obstacle ID is required).
for(int phase=0;phase<3;phase++) {
 Array.Clear(l.obstacles,0,l.obstacles.Length);
 for(int y=0;y<4;y++) {
  check(!gel.AddGel(0,y)&&!gel.AddGelImmediate(0,y),""runtime and seed additions reject every channel cell including the depot"");
  var t=new TileView{X=0,Y=y};board.TryPaintGelForClearedTile(t);board.PaintGelAt(0,y);board.PaintGelForBoosterCell(0,y);board.Created(t);
  check(!gel.IsGelAt(0,y)&&!gel.IsSpreadSourceAt(0,y),""clear/combo, launcher, booster and created-special entry points cannot coat channels"");
 }
 check(gel.spawns==spawns&&gel.animations==animations&&gel.refreshes==refreshes&&changes==events&&gel.Count==1&&gel.RevealedCount==1,
  ""rejected additions create no visual/fade/border work and do not advance coverage"");
}
check(gel.AddGel(1,1),""normal neighboring tile still receives spreading gel"");
gel.SealCell(1,1,true);check(!gel.IsSpreadSourceAt(1,1)&&gel.RevealedCount==1,""ordinary buried seed stays sealed"");
gel.UnsealCell(1,1);check(gel.IsSpreadSourceAt(1,1)&&gel.RevealedCount==2,""ordinary cover reveal still works"");
gel.ClearAll();check(gel.Count==0&&!gel.AddGel(0,1),""visual clear retains the current board's channel exclusion"");
check(!gel.AddGel(-1,0)&&!gel.AddGel(4,0),""out-of-bounds behavior unchanged"");
// Reused service gets a new board's path, never the previous board's exclusions.
l=new LevelData(5,3);l.waterChannels=new[]{new WaterChannelEntry{pathCellIndices=new[]{4,9,14}}};
gel.Init(5,3,100,new RectTransform(),l);gel.ClearAll();
check(gel.AddGel(0,1)&&!gel.AddGel(4,1),""reinitialization replaces the old channel map at new dimensions"");
gel.Init(5,3,100,new RectTransform(),new LevelData(5,3));gel.ClearAll();check(gel.AddGel(4,1),""no-channel level is not blocked by stale membership"");
l=new LevelData(4,4);l.waterChannels=new[]{new WaterChannelEntry{pathCellIndices=new[]{5,9,13}},new WaterChannelEntry{pathCellIndices=new[]{2,6,10,14}}};
gel.Init(4,4,100,new RectTransform(),l);gel.ClearAll();check(!gel.AddGel(1,1)&&!gel.AddGel(2,2)&&gel.AddGel(3,2),""multiple channel paths are excluded while nearby ordinary cells remain paintable"");
l.waterChannels=new[]{new WaterChannelEntry{pathCellIndices=new[]{0,5}}};gel.Init(4,4,100,new RectTransform(),l);gel.ClearAll();
check(gel.AddGel(0,0),""invalid paths skipped by channel runtime cannot reserve arbitrary gel cells"");
gel.Init(4,4,100,new RectTransform());gel.ClearAll();check(gel.AddGel(0,1),""legacy initialization clears prior exclusion state"");
Console.WriteLine(""PASS: ""+checks+"" production gel/channel path, entry-point, no-spawn, coverage and service-reuse checks. No Unity Play/build."");
".Replace("/* CHANNEL */",channel)
 .Replace("/* SERVICE */",Methods(grid+"SpreadingGel/SpreadingGelOverlayService.cs","Init","AddGel","AddGelImmediate","AddGelInternal","CellIndex","IsGelAt","IsSealedAt","IsSpreadSourceAt","SealCell","UnsealCell","ClearAll"))
 .Replace("/* BOARD */",Methods(grid+"Board/BoardController.cs","SpreadGelToCell","PaintGelAt","PaintGelForBoosterCell","TryPaintGelForClearedTile","ShouldPaintGelOnClear","PaintGelUnderCreatedSpecial"));
await CSharpScript.RunAsync(source,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
