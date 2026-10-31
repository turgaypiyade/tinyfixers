// Real GridSpawner destruction routing, including the event that previously erased the source first.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var before=Environment.GetCommandLineArgs().Contains("--before");
var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/GridSpawner.cs")).GetRoot();
var methods=string.Join("\n",root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"HandleObstacleDestroyed","TryPlayRhodesFinalBreak"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
if(before)methods=File.ReadAllText("Tools/Fixtures/RhodesFinalEventOrder/HandleObstacleDestroyed-before.cs.txt");
var harness=@"
using System;using System.Collections;using System.Collections.Generic;
public enum ObstacleId {Safe,AncientSeal,EnergyContainer,OverrideBatteryBox,TarpStatue,Grass,RhodesStatue,Other}
public class RectTransform {}
public class GameObject {public bool active=true,destroyed;public void SetActive(bool a){active=a;}}
public class Image {public GameObject gameObject=new GameObject();public RectTransform rectTransform=new RectTransform();}
public class BoardController {public static bool IsGelLauncher(ObstacleId id)=>false;}
public class WaterLily {public bool TryPlayGrassBreak(int i,Image image)=>false;}
public static class GelLauncherFx {public static void HandOverBody(int i,Image image){}}
public static class TarpStatueService {public static bool TryPlayFinale(BoardController b,int i,Image image)=>false;}
public static class RhodesStatueView {
 public static int halves;public static bool available=true;
 public static bool TryPlayFinalBreak(BoardController b,Image image){if(!available)return false;if(image.gameObject.destroyed||!image.gameObject.active)throw new Exception(""source already removed"");halves+=2;return true;}
}
public class Probe {
 public Dictionary<int,Image> obstacleViewsByOrigin=new Dictionary<int,Image>();public Dictionary<int,ObstacleId> obstacleDefsByOrigin=new Dictionary<int,ObstacleId>();
 Dictionary<int,object> _overrideBatteryBoxViews=new Dictionary<int,object>(),_pendingObbDetonationViews=new Dictionary<int,object>(),_chestViews=new Dictionary<int,object>(),_batteryBoxViews=new Dictionary<int,object>();
 WaterLily waterLilyService;BoardController board=new BoardController();public int resets;
 bool IsTrackedObstacleViewFor(int i,ObstacleId id)=>obstacleDefsByOrigin.TryGetValue(i,out var actual)&&actual==id;
 void RemoveSafeCellClickProxiesForOrigin(int i){}void RemoveAncientSealCellClickProxiesForOrigin(int i){}
 void ApplyUnderTileCellBgTint(){}void StopObstacleHitShake(int i,RectTransform r){resets++;}
 IEnumerator DestroyGelBodyIfUnclaimed(int i,Image image){yield break;}void StartCoroutine(IEnumerator e){}
 void Destroy(GameObject g){g.destroyed=true;g.active=false;}
 /* METHODS */
 public void DestroyEvent(int i,ObstacleId id)=>HandleObstacleDestroyed(i,id);
 public Image Add(int i,ObstacleId id){var image=new Image();obstacleViewsByOrigin[i]=image;obstacleDefsByOrigin[i]=id;return image;}
 /* VISUAL FIRST */
}
public static class Tests {
 static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 public static void Run(){
  var p=new Probe();var image=p.Add(7,ObstacleId.RhodesStatue);RhodesStatueView.halves=0;p.DestroyEvent(7,ObstacleId.RhodesStatue);
  Check(RhodesStatueView.halves==2,""primary destruction event must create halves BEFORE removing source"");
  Check(image.gameObject.destroyed&&!p.obstacleViewsByOrigin.ContainsKey(7),""handoff retires original and tracking"");
  p.DestroyEvent(7,ObstacleId.RhodesStatue);Check(RhodesStatueView.halves==2,""repeated destruction cannot duplicate halves"");
  p=new Probe();image=p.Add(7,ObstacleId.Other);p.DestroyEvent(7,ObstacleId.RhodesStatue);Check(!image.gameObject.destroyed&&RhodesStatueView.halves==2,""stale obstacle ID cannot retire a replacement view"");
  p=new Probe();image=p.Add(7,ObstacleId.RhodesStatue);RhodesStatueView.available=false;p.DestroyEvent(7,ObstacleId.RhodesStatue);Check(image.gameObject.destroyed&&!p.obstacleViewsByOrigin.ContainsKey(7),""unavailable VFX still cleans source without tracking leak"");RhodesStatueView.available=true;
  /* VISUAL TEST */
  Console.WriteLine(""PASS: ""+checks+"" production Rhodes final-event routing checks"");
 }
}
".Replace("/* METHODS */",methods)
 .Replace("/* VISUAL FIRST */",before?"":"public void VisualFirst(int i){if(!TryPlayRhodesFinalBreak(i,ObstacleId.RhodesStatue))throw new Exception(\"visual first failed\");obstacleViewsByOrigin.Remove(i);obstacleDefsByOrigin.Remove(i);}")
 .Replace("/* VISUAL TEST */",before?"":@"
  p=new Probe();image=p.Add(9,ObstacleId.RhodesStatue);int halves=RhodesStatueView.halves;p.VisualFirst(9);p.DestroyEvent(9,ObstacleId.RhodesStatue);Check(RhodesStatueView.halves==halves+2,""visual-first then destruction still emits exactly two halves"");
 ");
CSharpScript.RunAsync(harness+"\nTests.Run();",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly)).GetAwaiter().GetResult();
