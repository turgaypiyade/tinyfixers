// csi Tools/check_wall_progressive_collapse.csx — production scheduler, release and FX methods; no Unity build.
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
SyntaxNode Read(string p) => CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetRoot();
string Methods(SyntaxNode root, params string[] names) => string.Join("\n", root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var wall=Read("Assets/_Project/Scripts/Grid/Wall/WallPieceView.cs");
var board=Read("Assets/_Project/Scripts/Grid/Board/BoardController.cs");
var flow=Read("Assets/_Project/Scripts/Grid/Board/BoardFlowPump.cs");
var service=Read("Assets/_Project/Scripts/Grid/Wall/WallObstacleService.cs");
foreach(var root in new[]{wall,board,flow,service}) {
 var errors=root.SyntaxTree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length!=0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var hold=flow.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.ValueText=="CellHold").ToFullString();
var constants=string.Join("\n",wall.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f=>f.Modifiers.Any(SyntaxKind.ConstKeyword)).Select(f=>f.ToFullString()));
var fxTypes=string.Join("\n",wall.DescendantNodes().Where(n=>n is EnumDeclarationSyntax||n is StructDeclarationSyntax).Select(n=>n.ToFullString()));
var harness=@"
using System;
using System.Collections;
using System.Collections.Generic;
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
}
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
}
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
public class GameObject {public bool active=true;public void SetActive(bool value){active=value;}}
public class RectTransform {public GameObject gameObject=new GameObject();public Vector3 localScale,localEulerAngles;public Vector2 anchoredPosition;}
public class Image {public RectTransform rectTransform=new RectTransform();public Color color;public object sprite;public GameObject gameObject=>rectTransform.gameObject;}
public static class UiVfxPool {
 public static readonly Stack<Image> pool=new Stack<Image>();public static readonly Dictionary<GameObject,Image> live=new Dictionary<GameObject,Image>();
 public static int created,returned;
 public static Image Rent(){Image img;if(pool.Count==0){img=new Image();created++;}else img=pool.Pop();img.gameObject.SetActive(true);img.color=new Color(1,1,1,1);live.Add(img.gameObject,img);return img;}
 public static void Return(string key,GameObject go,int max){if(!live.TryGetValue(go,out var img))throw new Exception(""Double pool return"");live.Remove(go);returned++;go.SetActive(false);pool.Push(img);}
}
public static class Time {public static float unscaledDeltaTime=.016f;}
public static class Random {static System.Random rng=new System.Random(73);public static float value=>(float)rng.NextDouble();}
public static class Mathf {
 public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Lerp(float a,float b,float t)=>a+(b-a)*t;public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));
}
public class BoardController {
 public int width=12,height=12,cellHoldEpoch,ClearedCellCount;
 public Dictionary<Vector2Int,int> cellHolds=new Dictionary<Vector2Int,int>();internal List<CellHold> clearReleasedHolds=new List<CellHold>();
 /* BOARD */
}
/* HOLD */
public class Wall {
 public sealed class CellParts {
  public int x,y; public bool cellReleased;
  public RectTransform baseGroup=new RectTransform(),concaveGroup=new RectTransform(),detailGroup=new RectTransform(),flames=new RectTransform();public object punch;
 }
 public Dictionary<int,CellParts> cells=new Dictionary<int,CellParts>();
 public float ts=100;public int width=12;public Action<int> cellVacatedCallback;public Action finishCallback;
 public bool collapsing=true;public int stops,destroys,blasts;public object gameObject=new object();
 /* CONSTANTS */
 /* FXTYPES */
 readonly List<FxParticle> effects=new List<FxParticle>();
 void StopAllCoroutines(){stops++;}void StopCoroutine(object co){stops++;}void Destroy(object go){destroys++;}
 void SpawnBigBlast(Vector2 center,float scale){blasts++;TrackFx(UiVfxPool.Rent(),FxKind.Blast,.36f,from:.3f,to:2.6f);TrackFx(UiVfxPool.Rent(),FxKind.Dust,.95f,new Vector2(10,30));}
 void SpawnDebris(Vector2 center,int count){TrackFx(UiVfxPool.Rent(),FxKind.Debris,.65f,new Vector2(20,40),spin:100);}
 /* WALL */
 public IEnumerator Run(int trigger,Action<int,bool> onBlast=null,Action onStart=null)=>CollapseRoutine(trigger,onBlast,onStart);
 public void Step()=>Update();public void Disable()=>OnDisable();public void Removed()=>OnDestroy();public void Done()=>Finish();
 public void RepeatBlast(int cell)=>BlastCell(cells[cell],cell,false,null);
 public int LiveFx=>effects.Count;
 public CellParts Add(int x,int y){var p=new CellParts{x=x,y=y};cells.Add(y*width+x,p);return p;}
}
public static class Checks {
 static int checks;static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 static void RunWave(int columns,int rows,int tx,int ty,float dt){
  Time.unscaledDeltaTime=dt;var b=new BoardController();var w=new Wall();var coords=new List<Vector2Int>();
  for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){w.Add(x,y);coords.Add(new Vector2Int(x,y));}
  var hold=b.HoldCells(coords);int jobs=1,started=0;var order=new List<int>();
  w.finishCallback=()=>{hold.Dispose();jobs--;};
  w.cellVacatedCallback=i=>{
   var p=w.cells[i];Check(!p.baseGroup.gameObject.active&&!p.concaveGroup.gameObject.active&&!p.detailGroup.gameObject.active&&!p.flames.gameObject.active,""All cell visuals hidden BEFORE release"");
   hold.Release(new Vector2Int(i%b.width,i/b.width));order.Add(i);
   foreach(var pair in w.cells)if(!pair.Value.cellReleased)Check(pair.Value.baseGroup.gameObject.active&&b.cellHolds.ContainsKey(new Vector2Int(pair.Value.x,pair.Value.y)),""Unblasted cells stay visible and held"");
  };
  int trigger=ty*b.width+tx;var routine=w.Run(trigger,(i,first)=>Check(first==(i==trigger),""Trigger blast flag follows origin""),()=>started++);
  Check(routine.MoveNext(),""Effects outlive first blast"");
  Check(order.Count==1&&order[0]==trigger&&started==1,""First cell disappears and opens immediately, before wave finishes"");
  Check(b.ClearedCellCount==1,""Opening wakes gravity in the first frame"");
  if(coords.Count>1)Check(jobs==1,""Level-end stays held for remaining cells"");
  int frame=0;bool running=true;while(running&&frame++<1000){w.Step();running=routine.MoveNext();}
  Check(!running&&frame<1000,""Wave and effects finish"");
  Check(order.Count==coords.Count&&b.cellHolds.Count==0&&jobs==0,""Every blasted cell releases once and job completes"");
  int prev=-1;foreach(int i in order){int d=Math.Abs(i%b.width-tx)+Math.Abs(i/b.width-ty);Check(d>=prev,""Blast propagates in BFS rings"");prev=d;}
  Check(w.LiveFx==0&&UiVfxPool.live.Count==0,""All FX return after natural completion"");
  int returned=UiVfxPool.returned;w.RepeatBlast(trigger);w.Removed();w.Done();Check(jobs==0&&UiVfxPool.returned==returned,""Duplicate finish/blast/destroy has no effect"");
 }
 public static int Run(){
  RunWave(3,5,1,2,.016f);RunWave(6,7,0,0,.1f);RunWave(1,1,0,0,.016f);RunWave(4,4,3,3,.033f);
  int warmed=UiVfxPool.created;RunWave(1,1,0,0,.016f);Check(UiVfxPool.created==warmed,""Repeated effects reuse pooled images"");
  var b=new BoardController();var w=new Wall();w.Add(1,1);w.Add(1,2);var hold=b.HoldCells(new[]{new Vector2Int(1,1),new Vector2Int(1,2)});int jobs=1;
  w.cellVacatedCallback=i=>hold.Release(new Vector2Int(i%12,i/12));w.finishCallback=()=>{hold.Dispose();jobs--;};
  var routine=w.Run(13);routine.MoveNext();Check(w.LiveFx>0&&b.cellHolds.Count==1,""Cancellation happens with a pending cell and live FX"");
  w.Disable();w.Removed();w.Done();Check(jobs==0&&b.cellHolds.Count==0&&UiVfxPool.live.Count==0&&w.LiveFx==0,""Disable/destroy releases pending holds and FX once"");
  Check(w.stops==1&&w.destroys==1,""Cancel stops scheduler and removes stale view"");
  var pos=new Vector2Int(2,2);var a=b.HoldCells(new[]{pos,pos});var c=b.HoldCells(new[]{pos});int before=b.ClearedCellCount;
  a.Release(pos);Check(b.cellHolds[pos]==1&&b.ClearedCellCount==before,""Another owner keeps the cell blocked"");
  a.Dispose();c.Dispose();c.Dispose();Check(!b.cellHolds.ContainsKey(pos)&&b.ClearedCellCount==before+1,""Final owner wakes gravity once"");
  var old=b.HoldCells(new[]{pos});b.cellHoldEpoch++;b.cellHolds.Clear();var current=b.HoldCells(new[]{pos});before=b.ClearedCellCount;
  old.Release(pos);old.Dispose();Check(b.cellHolds[pos]==1&&b.ClearedCellCount==before,""Old callbacks cannot unlock the new board"");current.Dispose();
  return checks;
 }
}
Checks.Run()
".Replace("/* BOARD */",Methods(board,"HoldCells","ReleaseHeldCell","ForgetClearReleasedHold"))
 .Replace("/* HOLD */",hold).Replace("/* CONSTANTS */",constants).Replace("/* FXTYPES */",fxTypes)
 .Replace("/* WALL */",Methods(wall,"CollapseRoutine","BlastCell","RingDistances","CellCenter","Finish","OnDisable","OnDestroy","TrackFx","Update","ReleaseEffect","ReleaseAllEffects"));
int count=CSharpScript.EvaluateAsync<int>(harness,Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine($"PASS: {count} assertions over real wall scheduler, per-blast release, FX reuse, cancellation and hold ownership; source syntax valid.");
