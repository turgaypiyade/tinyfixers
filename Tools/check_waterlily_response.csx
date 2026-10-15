// Run from the repo root: csi Tools/check_waterlily_response.csx
// Runs production response/flight methods with Unity doubles; no project build or device render.
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
var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Grass/GrassWaterLilyService.cs")).GetRoot();
var cls=root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
var excluded=new[]{"Init","OnEnable","Subscribe","EnsureFlight","RentPetal"};
var progress=File.ReadAllText("Assets/_Project/Scripts/Grid/Grass/WaterLilyProgress.cs");
var stubs=@"
using System;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public Vector2 normalized {get{float d=(float)Math.Sqrt(x*x+y*y);return d>0?new Vector2(x/d,y/d):new Vector2();}}
 public float sqrMagnitude=>x*x+y*y;
 public static Vector2 one=>new Vector2(1,1);public static Vector2 up=>new Vector2(0,1);public static Vector2 down=>new Vector2(0,-1);
 public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);
}
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
 public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);
 public static implicit operator Vector3(Vector2 a)=>new Vector3(a.x,a.y);
 public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
 public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
 public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
 public static float Distance(Vector3 a,Vector3 b){var v=a-b;return (float)Math.Sqrt(v.x*v.x+v.y*v.y+v.z*v.z);}
}
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public struct Quaternion {public static Quaternion identity=>default(Quaternion);public static Quaternion Euler(float x,float y,float z)=>default(Quaternion);}
public struct Color {public float r,g,b,a;public static Color white=>new Color{r=1,g=1,b=1,a=1};}
public class GameObject {public bool activeSelf=true;public bool activeInHierarchy=>activeSelf;public void SetActive(bool v){activeSelf=v;}}
public class RectTransform {
 public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation;public Vector2 sizeDelta;
 public Vector2 anchoredPosition{get=>(Vector2)localPosition;set=>localPosition=value;}
 public Vector3 position=>localPosition;public Vector3 InverseTransformPoint(Vector3 p)=>p;public Vector3 InverseTransformVector(Vector3 p)=>p;
 public void SetAsLastSibling(){}
}
public class Sprite {}
public class Image {public Sprite sprite;public Color color;public GameObject gameObject=new GameObject();public RectTransform rectTransform=new RectTransform();}
public class WaterLilyPetalImage:Image {public int mask=15;public void SetPetals(int m,bool center=true){mask=m;}}
public static class Mathf {
 public const float PI=(float)Math.PI;public static float Abs(float x)=>Math.Abs(x);public static int Abs(int x)=>Math.Abs(x);
 public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
 public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));public static float Clamp01(float x)=>Clamp(x,0,1);
 public static float Sin(float x)=>(float)Math.Sin(x);public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
}
public static class Random {public static int Range(int a,int b)=>a;public static float Range(float a,float b)=>a;}
public static class Time {public static float deltaTime=.1f;}
public enum TileSpecial {None,LineH,LineV,PatchBot,PulseCore,SystemOverride}
public enum TileType {Gear,Core,Bolt,Plate,Key}
public enum ObstacleHitContext {NormalMatch,SpecialActivation,Booster}
public class TileView {
 public int X,Y,LifetimeVersion=1;public bool IsRuntimeIdle=true,IsSpecialCreationRevealPlaying,WasDragging,IsSpecialLocked;
 public GameObject gameObject=new GameObject();public TileSpecial special;public TileType type;public bool ov;public TileType overrideType;
 public TileSpecial GetSpecial()=>special;public TileType GetTileType()=>type;
 public bool GetOverrideBaseType(out TileType t){t=overrideType;return ov;}
 public void SetSpecial(TileSpecial s,bool deferVisualUpdate=false){special=s;}public void SetType(TileType t){type=t;}public void RefreshIcon(){}
 public void SetOverrideBaseType(TileType t){overrideType=t;ov=true;}public bool IsCurrentLifetime(int v)=>v==LifetimeVersion;
}
public class FlowStub {public bool IsSettling,IsSpecialVisualInFlight;}
public class Handle:IDisposable {Action a;public Handle(Action done){a=done;}public void Dispose(){var v=a;a=null;v?.Invoke();}}
public class BoardController {
 public enum BoardJobKind {KeyFlight}
 public int Width=3,Height=3,jobs,reserved,holds,queries;public bool free=true;public TileView[,] tiles=new TileView[3,3];public FlowStub Flow=new FlowStub();
 public event Action OnBecameIdle,OnPlayerMoveResolved;
 public IDisposable BeginJob(BoardJobKind k){jobs++;return new Handle(()=>jobs--);}
 public bool IsTileFreeForDynamicObstacle(int x,int y){queries++;var t=tiles[x,y];return free&&t!=null&&t.IsRuntimeIdle&&t.GetSpecial()==TileSpecial.None&&holds==0;}
 public TileView GetTileViewAt(int x,int y)=>x>=0&&y>=0&&x<3&&y<3?tiles[x,y]:null;
 public bool IsPendingTriggeredSpecialCell(int x,int y)=>false;public bool IsReservedTileTargetCell(int x,int y)=>false;
 public void ReserveTileTargetCell(Vector2Int p){reserved++;}public void ClearReservedTileTargetCell(Vector2Int p){reserved--;}
 public IDisposable HoldCells(Vector2Int[] cells){holds++;return new Handle(()=>holds--);}
 public Vector3 GetTileWorldCenter(TileView t)=>new Vector3(t.X*100,-t.Y*100);public void SyncTileData(int x,int y){}
 public void RequestResolveAfterActionSequence(){}
}
public class ObstacleStateService {
 public event Action<int> OnWaterLilyCleared;public Func<int,ObstacleHitContext,bool?> WaterLilyHitInterceptor;public Func<int,int> WaterLilyRemainingHitsQuery;
 public bool HasObstacleAt(int x,int y)=>false;public bool IsInteractionLockedAt(int x,int y)=>false;
 public bool IsMovableObstacleAt(int x,int y)=>false;public bool IsGrassVisibleAt(int cell)=>true;
}
public static class UiVfxPool {public static int returned;public static void Return(string key,GameObject go,int cap=96){returned++;go.SetActive(false);}}
";
var tests=@"
 private RectTransform transform=new RectTransform();
 private void EnsureFlight(Lily lily){if(lily.flight==null)lily.flight=new Image();}
 private WaterLilyPetalImage RentPetal()=>new WaterLilyPetalImage();
 private Sprite SpecialSprite(Lily lily)=>frames[0];
 private static int checks;
 private static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
 private static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
 private static GrassWaterLilyService Setup(bool free=true){
  var s=new GrassWaterLilyService {board=new BoardController(),state=new ObstacleStateService(),fxRoot=new RectTransform(),tileSize=100,bound=true};
  s.board.free=free;s.board.Flow.IsSettling=true;s.board.Flow.IsSpecialVisualInFlight=true;
  s.board.tiles[2,2]=new TileView{X=2,Y=2};s.frames=new List<Sprite>{new Sprite(),new Sprite(),new Sprite(),new Sprite(),new Sprite()};
  var image=new WaterLilyPetalImage();var lily=new Lily{cell=0,stored=TileSpecial.LineH,baseType=TileType.Core,rect=image.rectTransform,image=image,flight=new Image()};
  s.flowers.Add(lily);s.byCell.Add(0,lily);return s;
 }
 private static void Tick(GrassWaterLilyService s,float seconds){for(int i=0;i<(int)(seconds/.1f);i++)s.Update();}
 public static void RunChecks(){
  var s=Setup();var l=s.flowers[0];
  Check(s.Hit(0,ObstacleHitContext.SpecialActivation)==true,""special hit consumed"");
  Check(l.outbound&&l.age==0&&l.target!=null&&s.board.jobs==1,""starts ejection immediately during active sweep"");
  Check(l.progress.RemainingPetals==4&&l.progress.HasReleased,""first hit releases without losing petal"");
  var flight=l.flight;Tick(s,.3f);Check(l.flight.gameObject.activeSelf,""special visible before sweep finishes"");
  var target=l.target;Tick(s,1f);
  Check(l.stored==TileSpecial.None&&target.GetSpecial()==TileSpecial.LineH,""lands as reusable special"");
  Check(s.board.jobs==0&&s.board.holds==0&&s.board.reserved==0,""landing releases all resources"");
  Check(s.board.Flow.IsSettling&&s.board.Flow.IsSpecialVisualInFlight,""does not alter incoming sweep gates"");

  s=Setup(false);l=s.flowers[0];s.Hit(0,ObstacleHitContext.SpecialActivation);int queries=s.board.queries;
  Tick(s,1.2f);
  Check(l.age>1&&l.launchAge<0&&l.lastFrame==0&&l.flight.gameObject.activeSelf,""opens and visibly ejects without a landing cell"");
  Check(l.stored==TileSpecial.LineH&&s.board.jobs==1,""unplaced special remains owned and level end waits"");
  Check(s.board.queries==queries,""no per-frame board scans while waiting"");
  var position=l.flight.rectTransform.localPosition;float scale=l.flight.rectTransform.localScale.x,age=l.age;
  s.board.free=true;s.board.Flow.IsSettling=s.board.Flow.IsSpecialVisualInFlight=false;s.BoardIdle();s.Update();
  Check(l.target!=null&&l.age>age&&l.launchAge>=age,""idle retry assigns target without restarting animation"");
  Check(Near(l.from.y,position.y)&&Near(l.launchScale,scale),""late launch keeps hovering position and scale"");
  Tick(s,1f);Check(l.stored==TileSpecial.None&&s.board.jobs==0,""late landing completes once"");

  s=Setup();l=s.flowers[0];l.outbound=false;l.age=.1f;l.job=s.board.BeginJob(BoardController.BoardJobKind.KeyFlight);
  s.Hit(0,ObstacleHitContext.SpecialActivation);
  Check(l.outbound&&Near(l.age,0)&&s.board.jobs==1,""inbound hit transfers job without leak"");
  s.Update();float runningAge=l.age;target=l.target;s.Hit(0,ObstacleHitContext.SpecialActivation);
  Check(l.target==target&&l.age==runningAge&&s.board.jobs==1,""second hit does not restart or duplicate outbound flight"");
  Check(l.progress.IsDead&&l.progress.RemainingPetals==3,""second hit drops a petal"");
  Check(Near(s.petals[0].image.rectTransform.sizeDelta.x,179.2f),""detached petal is 60 percent larger"");
  Check(Near(l.rect.localScale.x,1)&&l.image.color.a==1,""remaining flower size and color unchanged"");
  s.OnDisable();Check(s.board.jobs==0&&s.board.holds==0&&s.board.reserved==0&&s.petalCount==0,""teardown cancels flight and returns petals"");

  s=Setup();l=s.flowers[0];s.Hit(0,ObstacleHitContext.SpecialActivation);target=l.target;target.LifetimeVersion++;
  Tick(s,1.2f);Check(l.stored==TileSpecial.LineH&&s.board.jobs==1&&s.board.holds==0,""replaced landing target preserves special and end hold"");
  s.board.Flow.IsSettling=s.board.Flow.IsSpecialVisualInFlight=false;s.Update();Tick(s,1.2f);
  Check(l.stored==TileSpecial.None&&s.board.jobs==0,""invalid target retries without duplicate jobs"");

  s=Setup(false);l=s.flowers[0];s.Hit(0,ObstacleHitContext.SpecialActivation);Tick(s,.4f);s.OnDisable();
  Check(s.board.jobs==0&&s.board.holds==0&&l.flight==null,""waiting ejection tears down without late rental"");
  s=Setup();l=s.flowers[0];Check(s.Hit(0,ObstacleHitContext.NormalMatch)==false&&l.age<0&&s.board.jobs==0,""normal hits cannot start release"");
  Console.WriteLine(""PASS: ""+checks+"" water-lily impact, immediate/late landing, repeat-hit, large-petal and teardown checks."");
 }
";
// UI construction and sprite lookup use doubles; response, animation and cleanup methods are production code.
var members=string.Join("\n",cls.Members.Where(m=>!(m is MethodDeclarationSyntax md && (excluded.Contains(md.Identifier.Text)||md.Identifier.Text=="SpecialSprite"))).Select(m=>m.ToFullString())).Replace("UnityEngine.Random","Random");
var source=stubs+progress+"\npublic class GrassWaterLilyService {\n"+members+tests+"\n}\nGrassWaterLilyService.RunChecks();";
var script=CSharpScript.Create(source,ScriptOptions.Default);
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Take(15).Select(d=>d.ToString())));
script.RunAsync().GetAwaiter().GetResult();
