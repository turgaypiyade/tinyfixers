// Mono csi: execute the production throw and volley iterators with presentation/board doubles.
// No Unity/project build. Sprite appearance and the final hand alignment need Play-mode review.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

foreach (var pair in new[] { new[] { "Badger", "BadgerThrow" }, new[] { "Hyena", "HyenaThrows" } })
{
    var meta = File.ReadAllText("Assets/_Project/Art/UI/RoboCharacters/" + pair[0] + "/" + pair[1] + "/" + pair[1] + ".png.meta");
    var asset = File.ReadAllText("Assets/_Project/Settings/" + pair[0] + "DuelCharacter.asset");
    var frames = asset.Substring(asset.IndexOf("  throwFrames:\n", StringComparison.Ordinal));
    var guid = Regex.Match(meta, @"(?m)^guid: (\w+)").Groups[1].Value;
    var ids = Regex.Matches(meta, @"      internalID: (-?\d+)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
    var refs = Regex.Matches(frames, @"sprite: \{fileID: (-?\d+), guid: (\w+), type: 3\}").Cast<Match>().ToArray();
    if (refs.Length != 12 || !ids.SequenceEqual(refs.Select(m => m.Groups[1].Value)) || refs.Any(m => m.Groups[2].Value != guid))
        throw new Exception(pair[0] + " throw frames do not reference the supplied sheet in order");
}
Console.WriteLine("PASS: both character profiles reference all 12 sliced frames in order");

string Extract(string file, string method) => CSharpSyntaxTree.ParseText(File.ReadAllText(file))
    .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == method).ToFullString();
var viewFile = "Assets/_Project/Scripts/Grid/Board/BossDuelCharacterView.cs";
var hasAnimation = CSharpSyntaxTree.ParseText(File.ReadAllText(viewFile)).GetRoot().DescendantNodes()
    .OfType<PropertyDeclarationSyntax>().Single(p => p.Identifier.ValueText == "HasThrowAnimation").ToFullString();
var harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
public static class Time { public static float deltaTime=.01f; }
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 one=>new Vector2(1,1);
 public static Vector2 operator*(Vector2 v,float f)=>new Vector2(v.x*f,v.y*f);
 public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
}
public struct Vector2Int { public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;} }
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
 public static Vector3 up=>new Vector3(0,1);public static Vector3 right=>new Vector3(1,0);
 public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);
 public static Vector3 operator*(Vector3 v,float f)=>new Vector3(v.x*f,v.y*f,v.z*f);
 public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
 public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
}
public struct Quaternion { public static Quaternion Euler(float x,float y,float z)=>new Quaternion(); }
public static class Mathf {
 public const float PI=(float)Math.PI;
 public static float Sin(float v)=>(float)Math.Sin(v);
 public static float Clamp(float v,float a,float b)=>Math.Max(a,Math.Min(b,v));
 public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(b,v));
 public static int Min(int a,int b)=>Math.Min(a,b);
 public static float Clamp01(float v)=>Clamp(v,0,1);
 public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
}
public class Rect { public float width=100,height=100; }
public class RectTransform {
 public Rect rect=new Rect();public Vector2 pivot=new Vector2(.5f,0),sizeDelta;
 public Vector3 localPosition,position;public Quaternion localRotation;public float scaleX=1;
 public GameObject gameObject;public void SetParent(RectTransform root,bool world){}
 public Vector3 TransformPoint(Vector3 p)=>position+new Vector3(p.x*scaleX,p.y,p.z);
 public Vector3 InverseTransformPoint(Vector3 p)=>p-position;
 public Vector3 TransformVector(Vector3 p)=>p;
 public Vector3 InverseTransformVector(Vector3 p)=>p;
}
public class Image {public object sprite;public bool preserveAspect,raycastTarget;public RectTransform rectTransform=new RectTransform();}
public class CanvasRenderer {}
public class GameObject {
 public static List<GameObject> made=new List<GameObject>();public bool active,destroyed;public string name;
 public RectTransform transform;public Image image=new Image();
 public GameObject(string name,params Type[] types){this.name=name;transform=new RectTransform{gameObject=this};made.Add(this);}
 public T GetComponent<T>() where T:class=>image as T;
 public void SetActive(bool b){active=b;}
}
public static class Object { public static void Destroy(GameObject go){go.destroyed=true;} }
public class Profile {
 public class Pose {public object sprite=new object();public int index;}
 public class Frame {public Pose pose=new Pose();public Vector2 handPoint;}
 public Pose idle=new Pose{index=-1},victory=new Pose{index=-2};
 public Frame[] throwFrames=Enumerable.Range(0,12).Select(i=>new Frame{pose=new Pose{index=i},handPoint=new Vector2(.5f+i*.02f,.4f)}).ToArray();
 public float throwFramesPerSecond=16,throwIdleLeadIn=.08f;public int throwReleaseFrame=6;
}
public class BossDuelCharacterView {
 public Image body=new Image();public Profile profile=new Profile();public bool attacking,finished,victoryPending,isActiveAndEnabled=true;
 public int poseVersion,restPoses,lastIndex;public List<int> shown=new List<int>();
 public float HeldObstacleWorldSize=>23;
 public void Show(Profile.Pose pose){lastIndex=pose.index;shown.Add(lastIndex);}
 public void ShowRestPose(){restPoses++;lastIndex=-1;}
 " + hasAnimation + Extract(viewFile, "ThrowObstacle") + @"
}
public enum ObstacleId { Oil,plastic }
public enum TileSpecial { None,LineH }
public class TileView { public enum TileVisualLayout { Centered } }
public class Tile {
 public bool converted;public TileSpecial special;public TileSpecial GetSpecial()=>special;
 public void SetUseFullCellIcon(bool b){}public void SetMovableObstacleTile(bool b){converted=b;}
 public void SetFullCellMovableSprite(object s){}public void SetVisualLayout(TileView.TileVisualLayout l){}
 public void SetMovableObstacleSprite(object s){}public void ApplyTileSize(float size){}
}
public class Def {public object sprite=new object(),fullCellSprite;public bool IsMovableObstacle=true;public object GetPreviewSprite()=>sprite;}
public class Library {public Def def=new Def();public Def Get(ObstacleId id)=>def;}
public class LevelData {public Library obstacleLibrary=new Library();}
public class Service {
 public int spawned;public bool HasObstacleAt(int x,int y)=>false;
 public bool TrySpawnSingleCellObstacleAt(int x,int y,ObstacleId id){spawned++;return true;}
}
public class BoardController {
 public LevelData ActiveLevelData=new LevelData();public RectTransform TilesRoot=new RectTransform(),transform=new RectTransform();
 public Tile[,] Tiles={{new Tile(),new Tile()},{new Tile(),new Tile()}};public float TileSize=100;
 public Service ObstacleStateService=new Service();public int created;
 public Vector3 GetCellWorldCenterPosition(int x,int y)=>new Vector3(x*100,y*100);
 public void RaiseObstacleCreatedDynamic(int x,int y){created++;}
}
public static class BossDuelController {public static void MatchParentLayer(RectTransform r){} }
public static class Pressure {
 " + Extract("Assets/_Project/Scripts/Grid/Board/BossDuelObstaclePressure.cs", "Throw") + @"
}
public static class Checks {
 static int count;static void Check(bool b,string label){count++;if(!b)throw new Exception(label);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
 static int Run(IEnumerator e,Action<int> tick=null){int n=0;try{while(e.MoveNext()){if(++n>500)throw new Exception(""Iterator stalled"");tick?.Invoke(n);}}finally{(e as IDisposable)?.Dispose();}return n;}
 static List<Vector2Int> Targets()=>new List<Vector2Int>{new Vector2Int(0,0),new Vector2Int(1,1)};
 static bool Clean()=>GameObject.made.All(g=>g.destroyed);
 public static int All(){
  var v=new BossDuelCharacterView();int releases=0,tracks=0;Vector3 hand=new Vector3();
  v.body.rectTransform.position=new Vector3(200,50);v.body.rectTransform.scaleX=-1;
  Run(v.ThrowObstacle(p=>{tracks++;hand=p;},()=>{releases++;Check(v.lastIndex==6,""Release occurs on opening-hand frame"");Check(Near(hand.x,188)&&Near(hand.y,90),""Hand uses sprite coordinates and mirrored body transform"");},()=>false));
  Check(releases==1&&tracks>1&&!v.attacking&&v.restPoses==1,""Single release, tracked preparation, rest after recovery"");
  Check(Enumerable.Range(0,12).All(i=>v.shown.Contains(i)),""Every authored frame plays at ordinary frame rate"");
  v=new BossDuelCharacterView();releases=0;bool cancel=false;
  Run(v.ThrowObstacle(p=>{},()=>releases++,()=>cancel),n=>cancel=n==5);
  Check(releases==0&&!v.attacking&&v.restPoses==1,""Cancelled anticipation restores idle without release"");
  v=new BossDuelCharacterView();releases=0;Time.deltaTime=.5f;
  Run(v.ThrowObstacle(p=>{},()=>releases++,()=>false));Time.deltaTime=.01f;
  Check(releases==1,""Low frame rate cannot skip or duplicate release"");
  v=new BossDuelCharacterView();var anim=v.ThrowObstacle(p=>{},()=>{},()=>false);anim.MoveNext();v.poseVersion++;v.attacking=false;
  Run(anim);Check(v.restPoses==0,""Old throw cannot overwrite a new wave pose"");
  v=new BossDuelCharacterView();Run(v.ThrowObstacle(p=>{},()=>v.victoryPending=true,()=>false));
  Check(v.finished&&!v.attacking&&v.lastIndex==-2,""Pending victory is shown after recovery"");

  GameObject.made.Clear();var board=new BoardController();v=new BossDuelCharacterView();
  int releaseSounds=0;
  bool sawHeld=false,sawRelease=false,sawConcurrentRecovery=false;
  Run(Pressure.Throw(board,new RectTransform(),new RectTransform(),Targets(),ObstacleId.plastic,v,
   onRelease:()=>{releaseSounds++;Check(v.lastIndex==6&&board.created==0,""Throw audio begins on hand release, before placement"");}),n=>{
   var props=GameObject.made;Check(board.created==0,""Board mutates only after flight completes"");
   Check(props.All(p=>p.image.sprite==board.ActiveLevelData.obstacleLibrary.def.sprite),""Held and flying prop use actual obstacle sprite"");
   if(props.Count==2&&props[0].active&&!props[1].active){sawHeld=true;Check(Near(props[0].transform.sizeDelta.x,23),""Held prop is character-sized"");}
   if(props.Count==2&&props[1].active&&!sawRelease){sawRelease=true;Check(Near(props[0].transform.localPosition.x,12)&&Near(props[1].transform.localPosition.x,12),""Entire volley starts exactly at release hand"");}
   if(v.lastIndex>6&&v.attacking&&props[1].active)sawConcurrentRecovery=true;
  });
  Check(sawHeld&&sawRelease&&sawConcurrentRecovery,""Preparation, release and overlapping follow-through are visible"");
  Check(releaseSounds==1,""Multi-obstacle volley plays throw audio once"");
  Check(board.created==2&&board.Tiles[0,0].converted&&board.Tiles[1,1].converted&&Clean()&&!v.attacking,""Volley places requested obstacles and cleans up"");
  foreach(bool afterRelease in new[]{false,true}){
   GameObject.made.Clear();board=new BoardController();v=new BossDuelCharacterView();cancel=false;releaseSounds=0;
   Run(Pressure.Throw(board,null,null,Targets(),ObstacleId.Oil,v,()=>cancel,()=>releaseSounds++),n=>cancel=afterRelease?GameObject.made[1].active:n==2);
   Check(releaseSounds==(afterRelease?1:0),""Cancellation only plays audio if the obstacle already left the hand"");
   Check(board.created==0&&Clean()&&!v.attacking,""Cancellation before/after release cleans props and pose without placement"");
  }
  GameObject.made.Clear();board=new BoardController();v=new BossDuelCharacterView();
  var volley=Pressure.Throw(board,null,null,Targets(),ObstacleId.Oil,v);volley.MoveNext();((IDisposable)volley).Dispose();
  Check(Clean()&&!v.attacking&&board.created==0,""Disposing volley also disposes nested character animation"");
  GameObject.made.Clear();board=new BoardController();v=new BossDuelCharacterView{attacking=true};
  Run(Pressure.Throw(board,null,null,Targets(),ObstacleId.Oil,v));
  Check(board.created==0&&Clean()&&v.attacking,""Busy character cannot launch a second action"");
  GameObject.made.Clear();board=new BoardController();v=new BossDuelCharacterView();v.profile.throwFrames[0].pose.sprite=null;
  releaseSounds=0;
  Run(Pressure.Throw(board,null,null,Targets(),ObstacleId.Oil,v,onRelease:()=>releaseSounds++));
  Check(releaseSounds==1,""Fallback flight also plays throw audio once"");
  Check(board.created==2&&Clean()&&v.shown.Count==0,""Incomplete artwork falls back to existing projectile flight"");
  return count;
 }
}
Checks.All()";
var result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default.AddReferences(
    typeof(System.Collections.Generic.Queue<>).Assembly, typeof(Enumerable).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " throw-frame, hand-release, volley, cancellation and cleanup assertions");
