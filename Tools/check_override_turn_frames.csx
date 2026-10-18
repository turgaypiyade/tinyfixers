// csi Tools/check_override_turn_frames.csx — production animation methods with UI doubles, no Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;
var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/OverrideSpecialView.cs")).GetRoot();
var cls=root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
var omitted=new[]{"Awake","BuildChildren","MakeImage"};
var members=string.Join("\n",cls.Members.Where(n=>!(n is MethodDeclarationSyntax m&&omitted.Contains(m.Identifier.ValueText))).Select(n=>n.ToFullString()));
var stubs=@"
using System;using System.Collections;using System.Collections.Generic;using OverrideSpecialView=View;
public class SerializeField:Attribute{}public class Header:Attribute{public Header(string s){}}public class Tooltip:Attribute{public Tooltip(string s){}}public class Range:Attribute{public Range(float a,float b){}}
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>default(Vector2);public static Vector2 one=>new Vector2(1,1);
 public static Vector2 up=>new Vector2(0,1);public static Vector2 left=>new Vector2(-1,0);public static Vector2 right=>new Vector2(1,0);public float sqrMagnitude=>x*x+y*y;public Vector2 normalized=>this/Math.Max(.00001f,magnitude);public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*Mathf.Clamp01(t);public static Vector2 operator/(Vector2 a,float b)=>new Vector2(a.x/b,a.y/b);public static Vector2 operator-(Vector2 a)=>new Vector2(-a.x,-a.y);public float magnitude=>(float)Math.Sqrt(x*x+y*y);public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 public static Vector2 Scale(Vector2 a,Vector2 b)=>new Vector2(a.x*b.x,a.y*b.y);public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);
}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>default(Vector3);public static Vector3 one=>new Vector3(1,1,1);public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y,0);}
public struct Quaternion {float z;public static Quaternion identity=>Euler(0,0,0);public static Quaternion Euler(float x,float y,float z)=>new Quaternion{z=z};public static Vector3 operator*(Quaternion q,Vector3 v){float a=q.z*(float)Math.PI/180;return new Vector3(v.x*(float)Math.Cos(a)-v.y*(float)Math.Sin(a),v.x*(float)Math.Sin(a)+v.y*(float)Math.Cos(a),v.z);}}
public struct Rect {public Vector2 size;public float width=>size.x;public float height=>size.y;public Vector2 center=>Vector2.zero;}
public class RectTransform {public Vector2 sizeDelta=new Vector2(100,100),anchoredPosition;public Rect rect=>new Rect{size=sizeDelta};public Vector3 localScale=Vector3.one,localEulerAngles;public Quaternion localRotation;public void SetAsFirstSibling(){}}
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);}
public class Sprite {public string name;public Sprite(string n){name=n;}}
public class Image {
 public enum Type{Simple,Filled}public enum FillMethod{Vertical}
 public GameObject gameObject=new GameObject();public RectTransform transform=>rectTransform;public bool raycastTarget=true,enabled=true;public static int created;public Image(){created++;}public RectTransform rectTransform=new RectTransform();public Color color;public Type type;public FillMethod fillMethod;public int fillOrigin;public float fillAmount;public bool preserveAspect=true;
 Sprite s;public int writes;public Sprite sprite{get=>s;set{writes++;s=value;}}
}
public class GameObject {public RectTransform transform=new RectTransform();public int layer;public void SetActive(bool v){activeInHierarchy=v;}public bool activeInHierarchy=true;public static int created;object component;public GameObject(){created++;}public T GetComponent<T>() where T:class=>typeof(T)==typeof(RectTransform)?transform as T:component as T;public T AddComponent<T>() where T:new(){var c=new T();if(c is MaskableGraphic){var g=c as MaskableGraphic;g.gameObject=this;g.rectTransform=transform;}if(c is View)(c as View).Attach(this,transform);component=c;return c;}}
public class MaskableGraphic {public RectTransform rectTransform=new RectTransform();public GameObject gameObject;public bool raycastTarget;public int dirty;public void SetVerticesDirty(){dirty++;}public Rect GetPixelAdjustedRect()=>rectTransform.rect;protected virtual void OnPopulateMesh(VertexHelper vh){}}
public class VertexHelper {public System.Collections.Generic.List<Vector3> points=new System.Collections.Generic.List<Vector3>();public int triangles;public int currentVertCount=>points.Count;public void Clear(){points.Clear();triangles=0;}public void AddVert(Vector3 p,Color c,Vector2 uv){if(float.IsNaN(p.x)||float.IsNaN(p.y)||c.a<0||c.a>1)throw new Exception(""invalid mesh"");points.Add(p);}public void AddTriangle(int a,int b,int c){if(a<0||b<0||c<0||a>=points.Count||b>=points.Count||c>=points.Count)throw new Exception(""invalid index"");triangles++;}}
public static class UiVfxPool {static Dictionary<string,Stack<GameObject>> pools=new Dictionary<string,Stack<GameObject>>();public static int rented,returned;public static bool parentTransition;public static GameObject RentRect(string k,object p,string n){rented++;Stack<GameObject> pool;if(!pools.TryGetValue(k,out pool))pools[k]=pool=new Stack<GameObject>();var go=pool.Count>0?pool.Pop():new GameObject();go.SetActive(true);go.transform.anchoredPosition=Vector2.zero;go.transform.localScale=Vector3.one;return go;}public static void Return(string k,GameObject go,int max){if(parentTransition)throw new Exception(""reparent during parent activation"");returned++;go.SetActive(false);Stack<GameObject> pool;if(!pools.TryGetValue(k,out pool))pools[k]=pool=new Stack<GameObject>();pool.Push(go);}}
public class CanvasGroup {public float alpha;}
public enum ParticleSystemStopBehavior {StopEmitting}public enum ParticleSystemShapeType {Circle}
public class ParticleSystem {public class Main {public bool loop;public float startSpeed,startSize,startLifetime;}public class Shape {public float radius;public ParticleSystemShapeType shapeType;}public class Emission {public float rateOverTimeMultiplier;}public Main main=new Main();public Shape shape=new Shape();public Emission emission=new Emission();public void Play(){}public void Stop(bool a,ParticleSystemStopBehavior b){}public void Emit(int n){}}
public static class GameDiagnostics {public static bool CriticalErrors=>false;}
public static class Debug {public static void LogError(string s){throw new Exception(s);}}
/* LINK */
/* COMBO */
/* GRAPHIC */
public class Coroutine {public IEnumerator enumerator;public bool stopped;}
public class WaitForSeconds {public WaitForSeconds(float s){}}
public static class Time {public static float deltaTime=1f/60;}
public static class Mathf {public const float PI=(float)Math.PI;public const float Deg2Rad=PI/180f;public static float Abs(float v)=>Math.Abs(v);public static float InverseLerp(float a,float b,float v)=>Clamp01((v-a)/(b-a));public static float Sin(float v)=>(float)Math.Sin(v);public static float Cos(float v)=>(float)Math.Cos(v);public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp(float v,float a,float b)=>Math.Max(a,Math.Min(b,v));public static float Clamp01(float v)=>Clamp(v,0,1);public static float Pow(float a,float b)=>(float)Math.Pow(a,b);public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);public static float Min(float a,float b)=>Math.Min(a,b);public static int FloorToInt(float f)=>(int)Math.Floor(f);}
public class View {
 public GameObject gameObject=new GameObject();public object transform=new object();
 T GetComponent<T>() where T:class=>rt as T;
 Coroutine StartCoroutine(IEnumerator e)=>new Coroutine{enumerator=e};void StopCoroutine(Coroutine c){c.stopped=true;}
 void BuildChildren(){if(topImage==null)topImage=new Image();if(bottomImage==null)bottomImage=new Image();if(fullImage==null)fullImage=new Image();}
 /* MEMBERS */
 static int checks;
 static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
 public void Attach(GameObject go,RectTransform rect){gameObject=go;rt=rect;BuildChildren();}
 public float Phase=>turnFramePhase;public bool TopReverse=>topTurnReverse;public bool BottomReverse=>bottomTurnReverse;
 public static View Setup(){
  var v=new View{rt=new RectTransform(),topHalfSprite=new Sprite(""original top""),bottomHalfSprite=new Sprite(""original bottom""),fullSphereSprite=new Sprite(""original sphere""),spinTurns=5,spinDuration=.8f,settleDuration=.5f,settleOvershootScale=1.25f};
  v.BuildChildren();v.topTurnFrames=new Sprite[8];v.bottomTurnFrames=new Sprite[6];int[] order={1,2,4,3,5,6};
  for(int i=0;i<8;i++)v.topTurnFrames[i]=new Sprite(""T""+(i+1));for(int i=0;i<6;i++)v.bottomTurnFrames[i]=new Sprite(""B""+order[i]);
  v.RestoreOriginalHalves();return v;
 }
 public static void Run(){
  var v=Setup();v.ApplyTurnFrames(0);Check(v.topImage.sprite.name==""T1""&&v.bottomImage.sprite.name==""B1"",""both begin at neutral phase"");
  v.ApplyTurnFrames(1);Check(v.topImage.sprite.name==""T8""&&v.bottomImage.sprite.name==""B2"",""top reverses while bottom advances"");
  v.ApplyTurnFrames(2);Check(v.topImage.sprite.name==""T7""&&v.bottomImage.sprite.name==""B4"",""corrected bottom order continues"");
  v.ApplyTurnFrames(3);Check(v.bottomImage.sprite.name==""B3"",""bottom frames 3 and 4 are exchanged"");
  v.ApplyTurnFrames(6);Check(v.bottomImage.sprite.name==""B1"",""six-frame bottom cycle wraps independently"");v.ApplyTurnFrames(8);Check(v.topImage.sprite.name==""T1"",""eight-frame upper cycle wraps"");
  int writes=v.topImage.writes;v.ApplyTurnFrames(8.9f);Check(v.topImage.writes==writes,""same frame avoids redundant sprite writes"");
  Check(Near(v.topImage.rectTransform.sizeDelta.y,65.1515f)&&Near(v.bottomImage.rectTransform.sizeDelta.y,45.1356f),""trimmed frames match original half bounds"");
  v.SpinHalf(v.topImage,20,90,1);Check(Near(v.topImage.rectTransform.anchoredPosition.x,-16.9458f)&&Near(v.topImage.rectTransform.anchoredPosition.y,20.0399f),""top frame orbits original Z pivot"");
  v.RestoreOriginalHalves();v.SpinHalf(v.topImage,0,0,1);Check(v.topImage.sprite==v.topHalfSprite&&Near(v.topImage.rectTransform.sizeDelta.y,100)&&Near(v.topImage.rectTransform.anchoredPosition.y,0),""merge restores original padded half"");
  v.topTurnFrames=null;v.bottomTurnFrames=new Sprite[0];v.ApplyTurnFrames(1);Check(v.topImage.sprite==v.topHalfSprite&&v.bottomImage.sprite==v.bottomHalfSprite,""missing sequences preserve legacy sprites"");
  v.topHalfSprite=null;v.bottomHalfSprite=null;v.RestoreOriginalHalves();Check(v.topImage.type==Image.Type.Filled&&Near(v.topImage.fillAmount+v.bottomImage.fillAmount,1),""full-sprite clipping fallback preserved"");
  foreach(int fps in new[]{15,30,60,120}){
   v=Setup();Time.deltaTime=1f/fps;int objects=Image.created;v.PlayCreation();var e=v.routine.enumerator;bool spun=false,changed=false;int iterations=0;float previous=0;
   while(e.MoveNext()){
    if(++iterations>400)throw new Exception(""creation did not finish"");
    if(v.topUsesTurnFrame){spun|=Math.Abs(v.topImage.rectTransform.localEulerAngles.z)>.01f;changed|=v.topImage.sprite!=v.topTurnFrames[0];Check(v.turnFramePhase-previous<=2.001f,""low-FPS phase stays below half a turn for both sheets"");previous=v.turnFramePhase;
     Check(Near(v.topImage.rectTransform.localEulerAngles.z,-v.bottomImage.rectTransform.localEulerAngles.z),""existing opposite Z spin preserved"");}
   }
   Check(spun&&changed,""both sprite turn and original Z spin run at FPS ""+fps);
   Check(v.fullImage.sprite==v.fullSphereSprite&&Near(v.fullImage.color.a,1)&&Near(v.fullImage.rectTransform.localScale.x,1),""original full copper sphere finishes merge"");
   Check(!v.topUsesTurnFrame&&!v.bottomUsesTurnFrame&&v.idleRoutine!=null&&Image.created==objects,""idle reuses three existing Images"");
   v.PlayCreation();e=v.routine.enumerator;e.MoveNext();e.MoveNext();v.OnDisable();
   Check(v.routine==null&&v.idleRoutine==null&&!v.topUsesTurnFrame&&Near(v.topImage.color.a,0)&&Near(v.topImage.rectTransform.anchoredPosition.x,0),""disable cancels playback and clears trimmed pose"");
   v.Stop();Check(Image.created==objects&&v.topImage.sprite==v.topHalfSprite,""repeat stop/replay never creates extra Images"");
  }
  foreach(int fps in new[]{15,30,60,120}){
   v=Setup();v.turnFramesOnly=true;v.staticIconImage=new Image{color=Color.white};Time.deltaTime=1f/fps;int objects=Image.created;
   v.PlayCreation();var e=v.routine.enumerator;bool changed=false;int iterations=0;
   while(e.MoveNext()){
    if(++iterations>200)throw new Exception(""preview did not finish"");
    if(!v.topUsesTurnFrame)continue;
    changed|=v.topImage.sprite!=v.topTurnFrames[0];
    float rock=v.topImage.rectTransform.localEulerAngles.z;
    Check(Math.Abs(rock)<=9.001f&&Near(rock,v.bottomImage.rectTransform.localEulerAngles.z),""both halves share bounded rocking instead of old opposite Z spins"");
    var topOffset=Quaternion.Euler(0,0,rock)*(Vector3)Vector2.Scale(v.canvasSize,v.topTurnOffsetRatio);
    var bottomOffset=Quaternion.Euler(0,0,rock)*(Vector3)Vector2.Scale(v.canvasSize,v.bottomTurnOffsetRatio);
    Check(Near(v.topImage.rectTransform.anchoredPosition.y,topOffset.y)&&Near(v.bottomImage.rectTransform.anchoredPosition.y,bottomOffset.y),""rock preserves common pivot without old separation"");
    Check(Near(v.staticIconImage.color.a,0)&&v.staticIconImage.raycastTarget&&v.staticIconImage.enabled,""static icon cannot cover frames; input graphic remains enabled"");
    Check(Near(v.fullImage.color.a,0),""old full-sphere merge pop stays hidden"");
   }
   Check(changed&&!v.staticIconHidden&&Near(v.staticIconImage.color.a,1)&&v.idleRoutine!=null,""isolated preview animates then restores static icon and idle"");
   Check(Image.created==objects,""isolated preview creates no extra Images"");
   v.PlayCreation();e=v.routine.enumerator;e.MoveNext();e.MoveNext();v.OnDisable();
   Check(Near(v.staticIconImage.color.a,1)&&!v.staticIconHidden&&!v.IsPlaying,""interrupted preview restores icon opacity"");v.Stop();
  }
  v=Setup();Check(Near(v.TurnFrameRate(0),18)&&Near(v.TurnFrameRate(1),120),""turn accelerates from 18 to 120 sprite frames per second"");
  float rate=0;for(int i=0;i<=100;i++){float next=v.TurnFrameRate(i/100f);Check(next>=rate&&next<=120,""turn speed never slows down"");rate=next;}
  Check(Near(v.TurnFrameRate(.5f),v.TurnFrameRate(1)),""fast finish holds peak speed"");
  foreach(int fps in new[]{30,60,120}){
   v=Setup();v.turnFramesOnly=true;Time.deltaTime=1f/fps;v.PlayCreation();var e=v.routine.enumerator;
   float previous=0,early=0,late=0;int earlyFrames=0,lateFrames=0;
   while(e.MoveNext())if(v.topUsesTurnFrame){float delta=v.turnFramePhase-previous;previous=v.turnFramePhase;
    if(previous<1){early+=delta;earlyFrames++;}if(v.turnFramePhase>10){late+=delta;lateFrames++;}}
   Check(earlyFrames>0&&lateFrames>0&&late/lateFrames>early/earlyFrames*2,""rendered phase visibly accelerates at FPS ""+fps);
  }
  v=Setup();v.turnFramesOnly=true;v.staticIconImage=new Image{color=Color.white};Time.deltaTime=1f/60;
  v.BeginActivation();var active=v.routine.enumerator;float lastPhase=0;bool rockedLeft=false,rockedRight=false;
  for(int i=0;i<180;i++){Check(active.MoveNext(),""activation stays alive beyond idle duration"");
   if(!v.topUsesTurnFrame)continue;
   float z=v.topImage.rectTransform.localEulerAngles.z;rockedLeft|=z< -2;rockedRight|=z>2;
   Check(v.chargeGraphic!=null&&Near(v.staticIconImage.color.a,0),""charge and animated sphere remain visible throughout fanout"");
   if(i>90)Check(v.turnFramePhase>lastPhase,""fast turn continues while rocking at peak speed"");lastPhase=v.turnFramePhase;
  }
  Check(rockedLeft&&rockedRight,""charge rocks both directions"");
  var pooled=v.chargeGraphic;int returnsBefore=UiVfxPool.returned;UiVfxPool.parentTransition=true;v.OnDisable();UiVfxPool.parentTransition=false;Check(object.ReferenceEquals(v.chargeGraphic,pooled)&&UiVfxPool.returned==returnsBefore&&!v.activating&&Near(v.topImage.rectTransform.localEulerAngles.z,0)&&Near(v.staticIconImage.color.a,1),""disable hides retained charge without reparenting during activation"");
  v.BeginActivation();active=v.routine.enumerator;active.MoveNext();active.MoveNext();Check(object.ReferenceEquals(pooled,v.chargeGraphic),""charge mesh object is reused after cancellation"");
  int objectsBefore=GameObject.created;v.Stop();v.Stop();Check(UiVfxPool.rented==UiVfxPool.returned,""all acquired effects returned exactly once, including repeated Stop"");
  v.PlayCreation();active=v.routine.enumerator;while(active.MoveNext()){}Check(GameObject.created==objectsBefore&&UiVfxPool.rented==UiVfxPool.returned,""idle repeat reuses pooled effect and returns on completion"");
  Check(Near(v.ChargeRock(0,0,false),0)&&Math.Abs(v.ChargeRock(.8f,1,false))<.001f,""idle rocking eases in/out without end pose jump"");
  v.chargeLightning=false;v.BeginActivation();active=v.routine.enumerator;active.MoveNext();active.MoveNext();Check(v.chargeGraphic==null,""disabled lightning never acquires an effect"");v.Stop();
  var graphic=new OverrideChargeGraphic();var mesh=new VertexHelper();graphic.Populate(mesh);Check(mesh.currentVertCount==0,""pooled mesh starts empty"");
  graphic.SetCharge(.5f,1,true);int dirty=graphic.dirty;graphic.SetCharge(.51f,1,true);Check(graphic.dirty==dirty,""mesh reshaping throttled within 24 Hz tick"");
  graphic.Populate(mesh);Check(mesh.currentVertCount==252&&mesh.triangles==126,""three seven-segment layered arcs have bounded geometry"");
  for(int tick=0;tick<100;tick++){
   graphic.SetCharge(tick/24f,1,true);graphic.Populate(mesh);bool bounded=mesh.currentVertCount<=252;foreach(var p in mesh.points)bounded&=Math.Abs(p.x)<70&&Math.Abs(p.y)<70;Check(bounded,""all lightning vertices stay close to sphere silhouette"");
  }
  bool sawThin=false,sawThick=false;
  for(int sample=0;sample<30;sample++){graphic.SetCharge(sample/24f,1,true);graphic.Populate(mesh);
   for(int segment=0;segment<21;segment++){int index=segment*12;float width=new Vector2(mesh.points[index].x-mesh.points[index+1].x,mesh.points[index].y-mesh.points[index+1].y).magnitude;sawThin|=Near(width,9.5f);sawThick|=Near(width,15.2f);}}
  Check(sawThin&&sawThick,""lightning occasionally thickens selected segments without adding objects"");
  graphic.ResetCharge();graphic.SetCharge(.5f,1,true);graphic.Populate(mesh);
  float glowWidth=new Vector2(mesh.points[0].x-mesh.points[1].x,mesh.points[0].y-mesh.points[1].y).magnitude;
  float coreWidth=new Vector2(mesh.points[8].x-mesh.points[9].x,mesh.points[8].y-mesh.points[9].y).magnitude;
  Check(glowWidth>=9.499f&&glowWidth<=15.201f&&coreWidth>=1.799f&&coreWidth<=2.881f,""lightning glow and bright core have visibly increased widths"");
  graphic.SetCharge(.55f,1,true,0,9,true);graphic.Populate(mesh);
  Check(mesh.currentVertCount==380&&mesh.triangles==190,""cap light shares pooled mesh with bounded extra geometry"");
  var start=new Vector2((mesh.points[252].x+mesh.points[253].x)/2,(mesh.points[252].y+mesh.points[253].y)/2);
  var expected=Quaternion.Euler(0,0,9)*new Vector3(16.2f,41.4f,0);
  Check(Near(start.x,expected.x)&&Near(start.y,expected.y),""cap highlight follows upper sphere rocking pivot"");
  graphic.SetCharge(.6f,1,true,.5f,9,true);graphic.Populate(mesh);
  var opposite=new Vector2((mesh.points[252].x+mesh.points[253].x)/2,(mesh.points[252].y+mesh.points[253].y)/2);
  Check((opposite-start).magnitude>30,""highlight visibly orbits orange cap with turn phase"");
  graphic.SetCharge(.65f,1,true,.7f,-9,false);graphic.Populate(mesh);Check(mesh.currentVertCount==252,""missing cap frames omit highlight without affecting lightning"");
  graphic.SetCharge(.7f,1,true,.8f,9,true,.8f);graphic.Populate(mesh);
  Check(mesh.currentVertCount==860&&mesh.triangles==430,""speed trails and cap share one bounded mesh without ghost objects"");
  graphic.SetCharge(.75f,1,true,.9f,-9,true,0);graphic.Populate(mesh);Check(mesh.currentVertCount==380,""zero blur removes color trails"");
  graphic.ResetCharge();graphic.Populate(mesh);Check(mesh.currentVertCount==0,""returned effect has no stale mesh"");
  graphic.SetCharge(.8f,1,true);graphic.Populate(mesh);Check(mesh.currentVertCount==252,""pool reuse clears cap and blur state"");
  graphic.SetCharge(.85f,1,true,0,0,true,0,0);graphic.Populate(mesh);Check(mesh.currentVertCount==0,""combo charge follows zero opacity without a bright remnant"");
  graphic.SetCharge(1,1,false);graphic.Populate(mesh);Check(mesh.currentVertCount==0,""idle lightning fades completely by finish"");
  var source=Setup();var left=Setup();var right=Setup();var leftIcon=new Image{color=Color.white};var rightIcon=new Image{color=Color.white};
  Check(left.BeginComboTurn(source,leftIcon,false)&&right.BeginComboTurn(source,rightIcon,true),""combo views accept shared source asset references"");
  Check(left.TopReverse&&!left.BottomReverse&&!right.TopReverse&&right.BottomReverse,""left upper turns right; right upper turns left; both lower halves oppose their own upper"");
  left.EnsureIdle();Check(left.idleRoutine==null&&left.routine==null&&left.IsPlaying,""externally driven combo never starts an idle or extra coroutine"");
  float phase=0;for(int i=1;i<70;i++){left.TickComboTurn(i/60f,1f/60,1);right.TickComboTurn(i/60f,1f/60,1);Check(left.Phase>phase&&Near(left.Phase,right.Phase),""both combo phases advance continuously beyond lift/smash boundary"");phase=left.Phase;}
  UiVfxPool.parentTransition=true;left.OnDisable();UiVfxPool.parentTransition=false;Check(left.chargeGraphic!=null&&!left.IsPlaying,""parent disable retains combo child without a return callback"");left.Stop();right.EndComboTurn();Check(right.staticIconImage==null,""pooled combo view releases its scene icon reference"");Check(!left.IsPlaying&&!right.IsPlaying&&Near(leftIcon.color.a,1)&&UiVfxPool.rented==UiVfxPool.returned,""external combo cancellation restores both icons and returns charge graphics"");
  Console.WriteLine(""PASS: ""+checks+"" Override turn, rocking, charge mesh, pooling and teardown checks."");
 }
}
View.Run();Combo.Run();";
var graphicRoot=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/OverrideChargeGraphic.cs")).GetRoot();
var graphicClass=graphicRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First().WithAttributeLists(default(SyntaxList<AttributeListSyntax>)).ToFullString();
graphicClass=graphicClass.Insert(graphicClass.LastIndexOf('}'),"public void Populate(VertexHelper vh)=>OnPopulateMesh(vh);\n");
var comboRoot=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/VFX/OverrideComboController.cs")).GetRoot();
var comboClass=comboRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
var selected=new[]{"Co_Play","Stop","OnDisable","Cleanup","GetPreClearDuration","GetRadialWaveDuration","GetTotalDuration","IsFirstTurnRole","AcquireComboTurns","RentComboTurn","TickComboTurns","ReleaseComboTurns","ReleaseComboTurn","ReleaseComboLink","SetGlowPos","SetFlashAlpha","SetAlpha","WithAlpha","EaseInQuint","EaseInOutCubic","EaseOutQuad","Smooth01","ResolveAwayDirection"};
var comboMembers=string.Join("\n",comboClass.Members.Where(n=>n is FieldDeclarationSyntax||n is EventFieldDeclarationSyntax||n is PropertyDeclarationSyntax||n is MethodDeclarationSyntax&&selected.Contains(((MethodDeclarationSyntax)n).Identifier.ValueText)).Select(n=>n.ToFullString()));
var comboHarness=File.ReadAllText("Tools/override_combo_harness.txt");
var linkClass=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/VFX/OverrideComboLinkGraphic.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First().WithAttributeLists(default(SyntaxList<AttributeListSyntax>)).ToFullString();
linkClass=linkClass.Insert(linkClass.LastIndexOf('}'),"public void Populate(VertexHelper vh)=>OnPopulateMesh(vh);\n");
var script=CSharpScript.Create(stubs.Replace("/* MEMBERS */",members).Replace("/* GRAPHIC */",graphicClass).Replace("/* LINK */",linkClass).Replace("/* COMBO */",comboHarness.Replace("/* MEMBERS */",comboMembers)));var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Take(15).Select(d=>d.ToString())));script.RunAsync().GetAwaiter().GetResult();
// Check imported asset references as well as runtime frame order.
var prefab=File.ReadAllText("Assets/_Project/Prefabs/Grid/Tile_test.prefab");
foreach(var pair in new[]{new{Label="Top",Field="topTurnFrames"},new{Label="Bottom",Field="bottomTurnFrames"}}){
 var meta=File.ReadAllText("Assets/_Project/Art/Icons/Specials/OverrideCopper_Turn"+pair.Label+(pair.Label=="Top"?"V2":"")+".png.meta");
 var guid=Regex.Match(meta,@"guid: ([0-9a-f]{32})").Groups[1].Value;
 if(!meta.Contains("spriteMode: 2")||!meta.Contains("isReadable: 0")||!meta.Contains("enableMipMap: 0"))throw new Exception("Unsafe/missing sheet importer");
 var block=Regex.Match(prefab,@"  "+pair.Field+@":\n((?:  - .*\n)+)").Groups[1].Value;
 var ids=Regex.Matches(block,@"fileID: (\d+), guid: "+guid).Cast<Match>().Select(m=>long.Parse(m.Groups[1].Value)).ToArray();
 var expected=pair.Label=="Top"?new long[]{21300000,21300002,21300004,21300006,21300008,21300010,21300012,21300014}:new long[]{21300000,21300002,21300006,21300004,21300008,21300010};
 if(!ids.SequenceEqual(expected))throw new Exception("Invalid prefab sprite order: "+pair.Label);
 foreach(var id in ids)if(!meta.Contains("internalID: "+id+"\n"))throw new Exception("Unresolved sprite file ID");
}
Console.WriteLine("PASS: both imported sheets resolve all eight upper and six lower prefab sprite references in the intended order.");

var tile=File.ReadAllText("Assets/_Project/Scripts/Grid/TileView.cs");
if(!prefab.Contains("turnFramesOnly: 1")||!prefab.Contains("staticIconImage: {fileID: 5310917763237610214}"))throw new Exception("Frame-only preview is not wired to the tile icon");
var tileRoot=CSharpSyntaxTree.ParseText(tile).GetRoot();
var reveal=tileRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="PlaySpecialCreationReveal").ToFullString();
if(reveal.IndexOf("UsesTurnFramesOnly")>reveal.IndexOf("StartCoroutine(CoSpecialCreationRevealSimple"))throw new Exception("Generic creation reveal would still obscure isolated preview");
Console.WriteLine("PASS: frame-only prefab mode bypasses generic tile creation reveal.");

var action=File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Actions/SystemOverrideFanoutPlacementAction.cs");
var actionTree=CSharpSyntaxTree.ParseText(action).GetRoot();
var execute=actionTree.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="ExecuteVisuals");
if(!action.Contains("handle.OverrideView?.BeginActivation()")||!execute.DescendantNodes().OfType<FinallyClauseSyntax>().Any(f=>f.ToString().Contains("StopOriginLift(originLift)")&&f.ToString().Contains("StopImplantBreaths(implantBreaths)")))throw new Exception("Sustained charge not connected to fanout/cancellation cleanup");
Console.WriteLine("PASS: sustained charge connected to fanout lifetime; pooled mesh stays bounded and cancels cleanly.");

if(!prefab.Contains("turnFramesPerSecond: 120")||!prefab.Contains("turnBlurStrength: 0.8"))throw new Exception("Fast turn/blur settings not enabled");
Console.WriteLine("PASS: new eight-frame top, faster ramp and speed trails enabled on active tile prefab.");
