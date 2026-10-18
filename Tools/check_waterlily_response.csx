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
public struct Quaternion {
 public float x,y,z;public static Quaternion identity=>default(Quaternion);public static Quaternion Euler(float x,float y,float z)=>new Quaternion{x=x,y=y,z=z};
 public static Quaternion operator*(Quaternion a,Quaternion b)=>Euler(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator*(Quaternion q,Vector3 v){float a=q.z*(float)Math.PI/180;float c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(v.x*c-v.y*s,v.x*s+v.y*c,v.z);}
}
public struct Color {public float r,g,b,a;public static Color white=>new Color{r=1,g=1,b=1,a=1};}
public class GameObject {public bool activeSelf=true,destroyed;public bool activeInHierarchy=>activeSelf;public void SetActive(bool v){activeSelf=v;}}
public class RectTransform {
 public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation;public Vector2 sizeDelta,pivot;
 public Vector2 anchoredPosition{get=>(Vector2)localPosition;set=>localPosition=value;}
 public Vector3 position=>localPosition;public Vector3 InverseTransformPoint(Vector3 p)=>p;public Vector3 InverseTransformVector(Vector3 p)=>p;
 public void SetAsLastSibling(){}
}
public class Sprite {}
public class Image {public bool raycastTarget;public Sprite sprite;public Color color;public GameObject gameObject=new GameObject();public RectTransform rectTransform=new RectTransform();}
public class WaterLilyPetalImage:Image {public int mask=15;public void SetPetals(int m,bool center=true){mask=m;}}
public static class Mathf {
 public const float PI=(float)Math.PI;public static float Abs(float x)=>Math.Abs(x);public static int Abs(int x)=>Math.Abs(x);
 public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
 public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));public static float Clamp01(float x)=>Clamp(x,0,1);
 public static float Sin(float x)=>(float)Math.Sin(x);public static float Cos(float x)=>(float)Math.Cos(x);public static float Exp(float x)=>(float)Math.Exp(x);public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
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
 private bool isActiveAndEnabled=true;
 private static void Destroy(GameObject go){go.destroyed=true;}
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
  var image=new WaterLilyPetalImage{color=Color.white};var lily=new Lily{cell=0,stored=TileSpecial.LineH,baseType=TileType.Core,rect=image.rectTransform,image=image,flight=new Image()};
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
  s=Setup();l=s.flowers[0];Check(s.Hit(0,ObstacleHitContext.NormalMatch)==false&&l.age<0&&s.board.jobs==0,""normal hits cannot start release while a special is stored"");
  s=Setup();l=s.flowers[0];
  foreach(var kind in new[]{ObstacleHitContext.NormalMatch,ObstacleHitContext.Booster}){
   Check(s.Hit(0,kind)==false&&l.progress.RemainingPetals==4&&!l.progress.HasReleased&&l.age<0&&s.board.jobs==0,""stored special stays closed to normal/booster hits"");
  }
  s.Hit(0,ObstacleHitContext.SpecialActivation);target=l.target;float releaseAge=l.age;
  Check(l.progress.RemainingPetals==4&&l.progress.HasReleased&&l.outbound,""first special impact releases without losing a petal"");
  Check(s.Hit(0,ObstacleHitContext.NormalMatch)==false&&l.progress.RemainingPetals==4&&l.age==releaseAge&&l.target==target&&s.board.jobs==1,""normal hit cannot alter an unfinished special release"");
  Tick(s,1.2f);Check(l.stored==TileSpecial.None&&s.board.jobs==0,""release lands before normal damage resumes"");
  Check(s.Hit(0,ObstacleHitContext.NormalMatch)==false&&l.progress.RemainingPetals==4,""four petals still require a special after release"");
  Check(s.Hit(0,ObstacleHitContext.SpecialActivation)==true&&l.progress.RemainingPetals==3&&l.progress.IsDead&&!l.progress.CanSwallow,""special drops the first petal after release"");
  Check(s.Hit(0,ObstacleHitContext.NormalMatch)==true&&l.progress.RemainingPetals==2,""normal matches damage three or fewer petals"");s.OnDisable();
  // Full flower may swallow again after release: HasReleased must not bypass the special-only gate.
  s=Setup();l=s.flowers[0];l.progress.Hit(true);
  Check(s.Hit(0,ObstacleHitContext.NormalMatch)==false&&l.progress.RemainingPetals==4&&l.age<0,""a second swallowed special is still protected from normal matches"");
  Check(s.Hit(0,ObstacleHitContext.SpecialActivation)==true&&l.progress.RemainingPetals==3&&l.outbound,""repeat swallow opens only to special and retains prior petal progression"");s.OnDisable();
  foreach(int mode in new[]{0,1,2}){
   s=Setup();l=s.flowers[0];l.stored=TileSpecial.None;
   Check(s.Hit(0,ObstacleHitContext.Booster)==false&&l.progress.RemainingPetals==4,""booster does not newly damage empty flowers"");
   for(int i=0;i<4;i++){
    if(i==0) Check(s.Hit(0,ObstacleHitContext.NormalMatch)==false&&l.progress.RemainingPetals==4,""normal match cannot damage the initial four petals"");
    var kind=i>0&&(mode==0||mode==2&&i%2==0)?ObstacleHitContext.NormalMatch:ObstacleHitContext.SpecialActivation;
    Check(s.Hit(0,kind)==true&&l.progress.RemainingPetals==3-i,""normal/special/mixed sequence removes exactly one petal per hit"");
    Check(l.progress.IsDead&&!l.progress.CanSwallow&&s.board.jobs==0,""petal damage disables swallowing without creating flight holds"");
   }
   Check(!l.image.gameObject.activeSelf&&s.Hit(0,ObstacleHitContext.NormalMatch)==null,""four petals gone delegates next normal hit to grass"");
   Check(s.Hit(0,ObstacleHitContext.SpecialActivation)==null,""bare grass keeps its existing special-hit path"");
   s.OnDisable();Check(s.petalCount==0&&s.board.holds==0&&s.board.jobs==0,""normal petal effects return to pool on teardown"");
  }
  s=Setup();l=s.flowers[0];l.stored=TileSpecial.None;
  var grass=new RectTransform{anchoredPosition=new Vector2(-50,50),sizeDelta=new Vector2(100,100),pivot=new Vector2(0,1)};
  s.BindGrassVisual(0,grass);var grassHome=grass.anchoredPosition;
  Check(s.Hit(0,ObstacleHitContext.NormalMatch)==false&&l.progress.RemainingPetals==4,""full empty flower rejects normal damage but reacts visually"");
  Check(s.Hit(0,ObstacleHitContext.SpecialActivation)==true&&l.progress.RemainingPetals==3,""first special removes the first petal"");
  int scans=s.board.queries;s.Update();
  Check(l.swayApplied&&Math.Abs(l.swayAngle)>.1f&&s.board.jobs==0,""gentle sway starts without gameplay jobs"");
  Check(Near(l.rect.localRotation.z,grass.localRotation.z),""flower and grass sway in sync"");
  var center=(Vector3)grass.anchoredPosition+grass.localRotation*new Vector3(50,-50);
  Check(Near(center.x,0)&&Near(center.y,0),""grass top-left pivot compensated around cell centre"");
  Check(Near(l.rect.localScale.x,1)&&Near(grass.localScale.x,1)&&l.image.color.a==1,""sway never changes scale or colour"");
  float angle=l.swayAngle;s.Hit(0,ObstacleHitContext.NormalMatch);
  Check(Near(l.swayAngle,angle),""repeated hit preserves current spring angle"");
  Tick(s,1.2f);
  Check(l.swayAge<0&&!l.swayApplied&&Near(grass.anchoredPosition.x,grassHome.x)&&Near(grass.localRotation.z,0),""sway settles exactly at grass home"");
  Check(s.board.queries==scans,""sway adds no landing or board scans"");
  s.Hit(0,ObstacleHitContext.NormalMatch);s.Update();
  var replacement=new RectTransform{anchoredPosition=new Vector2(20,30),sizeDelta=new Vector2(120,120)};
  s.BindGrassVisual(0,replacement);
  Check(Near(grass.anchoredPosition.x,grassHome.x)&&Near(grass.localRotation.z,0)&&ReferenceEquals(l.grass,replacement),""grass redraw restores old visual before binding replacement"");
  s.Update();s.OnDisable();
  Check(!l.swayApplied&&l.swayAge<0&&Near(replacement.anchoredPosition.x,20)&&Near(replacement.localRotation.z,0),""disable restores both layers and cancels sway"");
  s=Setup();l=s.flowers[0];s.BindGrassVisual(0,new RectTransform());s.Hit(0,ObstacleHitContext.SpecialActivation);s.Update();
  Check(l.age>0&&l.outbound&&l.swayApplied&&s.board.jobs==1,""hit sway composes with immediate special ejection"");
  s.Cleared(0);Check(!l.swayApplied&&l.swayAge<0,""cleared lily restores grass pose"");s.OnDisable();
  s=Setup();l=s.flowers[0];l.stored=TileSpecial.None;
  for(int i=0;i<4;i++){s.Hit(0,ObstacleHitContext.SpecialActivation);Check(l.swayAge==0,""petal break retriggers bush shake"");}
  Check(l.progress.RemainingPetals==0&&s.Hit(0,ObstacleHitContext.NormalMatch)==null&&l.swayAge==0,""bare grass hit still shakes and delegates damage"");
  var finalGrass=new Image{color=Color.white};s.BindGrassVisual(0,finalGrass.rectTransform);
  Check(s.TryPlayGrassBreak(0,finalGrass),""final grass clear hands off existing image"");
  Check(s.TryPlayGrassBreak(0,finalGrass)&&s.board.jobs==0,""duplicate finale does not create a job"");
  s.Cleared(0);s.Update();
  Check(l.completed&&l.swayApplied&&!finalGrass.gameObject.destroyed,""grass shakes after gameplay clear"");
  Tick(s,.4f);Check(finalGrass.gameObject.destroyed&&l.breakingGrass==null&&l.grass==null,""short finale destroys its existing image"");
  s=Setup();l=s.flowers[0];finalGrass=new Image{color=Color.white};s.TryPlayGrassBreak(0,finalGrass);s.OnDisable();
  Check(finalGrass.gameObject.destroyed&&l.breakingGrass==null,""disable cleans up an unfinished grass finale"");
  foreach(int fps in new[]{30,60,120}){
   s=Setup();l=s.flowers[0];l.stored=TileSpecial.None;Time.deltaTime=1f/fps;s.Hit(0,ObstacleHitContext.NormalMatch);
   float peak=0,last=0;int crossings=0;
   for(int i=0;i<fps/2;i++){s.Update();float a=l.swayAngle;peak=Math.Max(peak,Math.Abs(a));if(a*last<0)crossings++;if(Math.Abs(a)>.001f)last=a;}
   Check(peak>3&&peak<=6f&&crossings>=4&&l.swayAge<0,""brief high frequency shake stays bounded at FPS ""+fps);
   s.OnDisable();
  }
  Time.deltaTime=.1f;
  foreach(var direction in new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1)}){
   s=Setup();l=s.flowers[0];l.stored=TileSpecial.None;l.lean=direction;l.swallowOffset=direction*100f;l.home=new Vector2(0,0);l.age=.14f;l.outbound=false;
   s.Update();
   float reach=l.rect.anchoredPosition.x*direction.x+l.rect.anchoredPosition.y*direction.y;
   Check(reach>89&&reach<=90.01f,""swallow visibly reaches toward all four neighbour directions"");
   Check(l.rect.localScale.x>1.17f||l.rect.localScale.y>1.17f,""swallow stretches along its target axis"");
   s.OnDisable();Check(Near(l.rect.anchoredPosition.x,0)&&Near(l.rect.anchoredPosition.y,0)&&Near(l.rect.localScale.x,1)&&Near(l.rect.localScale.y,1),""swallow cancellation restores home pose"");
  }
  foreach(var end in new[]{new Vector3(0,200),new Vector3(0,-200),new Vector3(200,0),new Vector3(-200,0),new Vector3(-200,-200),new Vector3(200,200)}){
   s=Setup();l=s.flowers[0];l.from=new Vector3(0,0);l.to=end;l.age=0;l.launchScale=.12f;s.ConfigureSpitArc(l);
   var start=SpitArcPosition(l,0);var finish=SpitArcPosition(l,1);var mid=SpitArcPosition(l,.5f);var delta=end-start;var offset=mid-start;
   float deviation=Math.Abs(offset.x*delta.y-offset.y*delta.x)/Vector3.Distance(start,end);
   Check(Vector3.Distance(start,l.spitLaunch)<.001f&&Vector3.Distance(finish,end)<.001f,""Bezier endpoints match pop and landing"");
   Check(deviation>10,""arc bends visibly for vertical, horizontal and diagonal targets"");
  }
  s=Setup();l=s.flowers[0];s.Hit(0,ObstacleHitContext.SpecialActivation);
  l.age=l.launchAge+SpitPopSeconds*.5f;s.AnimateSpit(l);var popPosition=l.flight.rectTransform.localPosition;float popScale=l.flight.rectTransform.localScale.x;
  Check(Vector3.Distance(popPosition,l.from)>1&&popScale>.5f&&popScale<SpitPeakScale,""special visibly grows while leaving mouth"");
  l.age=l.launchAge+SpitPopSeconds;s.AnimateSpit(l);var boundary=l.flight.rectTransform.localPosition;
  Check(Vector3.Distance(boundary,l.spitLaunch)<.001f&&Near(l.flight.rectTransform.localScale.x,SpitPeakScale),""pop joins arc without position or scale jump"");
  l.age=l.launchAge+SpitTravel;s.AnimateSpit(l);
  Check(Vector3.Distance(l.flight.rectTransform.localPosition,l.to)<.001f&&Near(l.flight.rectTransform.localScale.x,1),""arc lands exactly at normal size"");
  l.age=l.launchAge+SpitTravel+SpitLanding*.5f;s.AnimateSpit(l);Check(Vector3.Distance(l.flight.rectTransform.localPosition,l.to)<3&&l.flight.rectTransform.localScale.x<1.04f,""landing bounce stays small"");
  s.OnDisable();Check(s.board.jobs==0&&s.board.holds==0,""arc cancellation releases target and flight hold"");
  s=Setup(false);l=s.flowers[0];s.Hit(0,ObstacleHitContext.SpecialActivation);Tick(s,.6f);
  var hover=l.flight.rectTransform.localPosition;float hoverScale=l.flight.rectTransform.localScale.x;
  s.board.free=true;s.TryRelease(l);s.AnimateSpit(l);
  Check(l.spitPopDuration==0&&Vector3.Distance(l.flight.rectTransform.localPosition,hover)<.001f&&Near(l.flight.rectTransform.localScale.x,hoverScale),""late safe target continues hover without another pop"");s.OnDisable();
  s=Setup();l=s.flowers[0];l.stored=TileSpecial.None;
  var meal=new TileView{X=1,Y=0,special=TileSpecial.PulseCore};s.Swallow(l,meal);var mealPosition=l.flight.rectTransform.localPosition;
  Check(meal.special==TileSpecial.None&&l.stored==TileSpecial.PulseCore,""swallow transfers only the special while the normal tile remains"");
  l.age=SwallowSeconds*.5f;s.AnimateSwallow(l);
  Check(Vector3.Distance(l.flight.rectTransform.localPosition,mealPosition)<.001f&&l.rect.anchoredPosition.x>89,""stationary meal is swallowed by the approaching flower"");
  l.age=SwallowSeconds;s.AnimateSwallow(l);
  Check(l.age<0&&s.board.jobs==0&&Near(l.rect.anchoredPosition.x,0)&&!l.flight.gameObject.activeSelf,""mouth returns home after swallowing and closes its flight job"");s.OnDisable();
  foreach(int fps in new[]{30,60,120}){
   s=Setup();l=s.flowers[0];s.Hit(0,ObstacleHitContext.SpecialActivation);float previousHeight=0,previousScale=.12f;
   for(int i=0;i<=fps;i++){
    l.age=l.launchAge+SpitPopSeconds*i/fps;s.AnimateSpit(l);var pos=l.flight.rectTransform.localPosition;float size=l.flight.rectTransform.localScale.x;
    Check(Near(pos.x,l.from.x)&&pos.y>=previousHeight-.001f&&size>=previousScale-.001f,""initial lift stays vertical and grows smoothly at FPS ""+fps);
    previousHeight=pos.y;previousScale=size;
   }
   Check(Near(previousHeight-l.from.y,85)&&Near(previousScale,1.55f),""lift reaches a visible height and peak size before travel"");
   previousScale=1.55f;
   for(int i=0;i<=fps;i++){
    l.age=l.launchAge+SpitPopSeconds+(SpitTravel-SpitPopSeconds)*i/fps;s.AnimateSpit(l);float size=l.flight.rectTransform.localScale.x;
    Check(size<=previousScale+.001f&&size>=.999f,""arc shrinks monotonically to original size at FPS ""+fps);previousScale=size;
   }
   Check(Vector3.Distance(l.flight.rectTransform.localPosition,l.to)<.001f&&Near(previousScale,1),""arc reaches exact target at original size"");s.OnDisable();
  }
  Check(Near(HitSwaySeconds,.32f),""larger bush shake keeps original short duration"");
  Console.WriteLine(""PASS: ""+checks+"" water-lily impact, immediate/late landing, repeat-hit, large-petal, shared hit-sway and teardown checks."");
 }
";
// UI construction and sprite lookup use doubles; response, animation and cleanup methods are production code.
var members=string.Join("\n",cls.Members.Where(m=>!(m is MethodDeclarationSyntax md && (excluded.Contains(md.Identifier.Text)||md.Identifier.Text=="SpecialSprite"))).Select(m=>m.ToFullString())).Replace("UnityEngine.Random","Random");
var source=stubs+progress+"\npublic class GrassWaterLilyService {\n"+members+tests+"\n}\nGrassWaterLilyService.RunChecks();";
var script=CSharpScript.Create(source,ScriptOptions.Default);
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Take(15).Select(d=>d.ToString())));
script.RunAsync().GetAwaiter().GetResult();
