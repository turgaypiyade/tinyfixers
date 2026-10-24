// csi Tools/check_override_sorting.csx — executes production Canvas scopes with UI doubles; no Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;
var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/TileView.cs")).GetRoot();
var names=new[]{"BeginOverrideActivationSorting","EndOverrideActivationSorting","BeginSpecialCreationSorting","EndSpecialCreationSorting","UpdateCreationSortingOrder","UpdateOverrideActivationSorting","UpdateCreationPropellerSorting","RefreshPropellerMasking","RestIconCanvas","OnDisable","PrepareForRelease"};
var methods=string.Join("\n",root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var fields=new[]{"creationSortingCanvas","creationPropellerCanvas","overrideActivationCanvas","overrideSortingGraphics","creationSortingUsers","overrideActivationSortingUsers"};
var declarations=string.Join("\n",root.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f=>f.Declaration.Variables.Any(v=>fields.Contains(v.Identifier.Text))).Select(f=>f.ToFullString()));
var code=@"
using System;using System.Collections.Generic;
public class Component {
 public GameObject gameObject;public Transform transform=>gameObject.transform;
 public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
 public bool TryGetComponent<T>(out T c) where T:Component{c=GetComponent<T>();return c!=null;}
 public T GetComponentInParent<T>() where T:Component=>transform.GetComponentInParent<T>();
 public void GetComponentsInChildren<T>(bool inactive,List<T> result) where T:Component{result.Clear();gameObject.Collect(result);}
}
public class GameObject {
 public static int added;public Transform transform;public List<Component> components=new List<Component>();
 public bool activeSelf=true;public void SetActive(bool value){activeSelf=value;}
 public GameObject(Transform parent=null){transform=new Transform{gameObject=this,parent=parent};if(parent!=null)parent.children.Add(transform);}
 public T AddComponent<T>() where T:Component,new(){added++;var c=new T{gameObject=this};components.Add(c);return c;}
 public T GetComponent<T>() where T:Component{foreach(var c in components)if(c is T)return (T)c;return null;}
 public void Collect<T>(List<T> result) where T:Component{foreach(var c in components)if(c is T)result.Add((T)c);foreach(var child in transform.children)child.gameObject.Collect(result);}
}
public class Transform {public GameObject gameObject;public Transform parent;public List<Transform> children=new List<Transform>();public T GetComponentInParent<T>() where T:Component{for(var p=this;p!=null;p=p.parent){var c=p.gameObject.GetComponent<T>();if(c!=null)return c;}return null;}}
public class Canvas:Component {public bool enabled=true,overrideSorting;public int sortingLayerID,sortingOrder;}
public class GraphicRaycaster:Component {public bool ignoreReversedGraphics;public int blockingObjects,blockingMask;}
public class MaskableGraphic:Component {public int masks,dirty;public void RecalculateMasking(){masks++;}public void SetMaterialDirty(){dirty++;}}
public class Image:MaskableGraphic {}
public class PatchBotPropellerView:Component {}
public class PulseFuseSparkleView:Component {}
public class OverrideSpecialView:Component {}
public enum TileSpecial {None,PatchBot,SystemOverride}
public class Model {public TileSpecial special;public void SetSpecial(TileSpecial v){special=v;}}
public class TileView {
 public PulseFuseSparkleView fuseSparkleView;
 public Image iconImage;public PatchBotPropellerView propellerView;public OverrideSpecialView overrideSpecialView;public Model model=new Model();
 int LifetimeVersion,activeMoveToken,lastFallGeneration;bool IsPlannedToMoveThisFallPass,revealUsesCreationSorting,IsSpecialLocked,CageGrace,GelContaminated;
 public bool revealActive;
 void StopSpecialCreationReveal(){if(!revealActive)return;revealActive=false;EndSpecialCreationSorting();}
 void StopLineIdleSpin(){}void ResetVisualState(){}void StopAllCoroutines(){}void CancelActiveSettle(){}void CleanupOverrideMarkHalo(){}void ClearMovableObstaclePresentation(){}
 /* FIELDS */
 /* METHODS */
 static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static TileView Make(Transform parent){var tile=new GameObject(parent);return new TileView{iconImage=new GameObject(tile.transform).AddComponent<Image>(),propellerView=new GameObject(tile.transform).AddComponent<PatchBotPropellerView>(),overrideSpecialView=new GameObject(tile.transform).AddComponent<OverrideSpecialView>()};}
 static Canvas C(Component c)=>c.GetComponent<Canvas>();
 public static void Run(){
 foreach(int order in new[]{0,25,300})foreach(int layer in new[]{0,7}){
  var board=new GameObject();var bc=board.AddComponent<Canvas>();bc.sortingOrder=order;bc.sortingLayerID=layer;
  var ray=board.AddComponent<GraphicRaycaster>();ray.ignoreReversedGraphics=true;ray.blockingObjects=2;ray.blockingMask=31;
  var source=Make(board.transform);source.model.special=TileSpecial.SystemOverride;
  var target=Make(board.transform);target.model.special=TileSpecial.PatchBot;new GameObject(target.propellerView.transform).AddComponent<Image>();
  var top=new GameObject(source.overrideSpecialView.transform).AddComponent<Image>();
  var bottom=new GameObject(source.overrideSpecialView.transform).AddComponent<Image>();
  target.BeginSpecialCreationSorting();source.BeginOverrideActivationSorting();
  var sc=C(source.iconImage);var ov=C(source.overrideSpecialView);var tc=C(target.iconImage);var pc=C(target.propellerView);
  Check(sc.overrideSorting&&ov.overrideSorting&&sc.sortingOrder>pc.sortingOrder&&ov.sortingOrder>sc.sortingOrder,""source and all turn visuals beat target creation/propeller"");
  Check(sc.sortingLayerID==layer&&ov.sortingLayerID==layer&&tc.sortingLayerID==layer,""same board sorting layer"");
  Check(top.masks>0&&bottom.dirty>0,""both turn images refreshed when nearest Canvas changes"");
  var localRay=source.iconImage.GetComponent<GraphicRaycaster>();Check(localRay!=null&&localRay.blockingMask==31&&localRay.blockingObjects==2&&localRay.ignoreReversedGraphics,""icon input remains registered with equivalent raycaster"");
  var charge=new GameObject(source.overrideSpecialView.transform).AddComponent<MaskableGraphic>();
  Check(ReferenceEquals(charge.GetComponentInParent<Canvas>(),ov),""late rented lightning inherits source foreground Canvas"");
  source.EndOverrideActivationSorting();source.EndOverrideActivationSorting();
  Check(!sc.overrideSorting&&!ov.overrideSorting&&sc.enabled&&ov.enabled,""foreground released once; enabled canvases keep resting images visible"");
  Check(charge.masks>0&&charge.dirty>0,""lightning masking refreshes on sorting restore"");
  Check(tc.overrideSorting&&pc.overrideSorting,""source cleanup leaves target animation alone"");
  target.EndSpecialCreationSorting();Check(!tc.overrideSorting&&!pc.overrideSorting,""target propeller restores as before"");
  int added=GameObject.added;var buffer=source.overrideSortingGraphics;
  for(int i=0;i<50;i++){source.BeginOverrideActivationSorting();source.EndOverrideActivationSorting();}
  Check(GameObject.added==added&&ReferenceEquals(buffer,source.overrideSortingGraphics)&&buffer.Count==0,""repeated activation reuses canvases/raycaster/buffer and releases graphic references"");
  // Reveal can end before or after fanout. Its own scope must survive/downgrade independently.
  foreach(bool revealEndsFirst in new[]{true,false}){
   source.BeginSpecialCreationSorting();source.BeginOverrideActivationSorting();source.BeginOverrideActivationSorting();
   source.EndOverrideActivationSorting();Check(ov.overrideSorting&&sc.sortingOrder>order+101,""nested activation keeps foreground until last owner"");
   if(revealEndsFirst)source.EndSpecialCreationSorting();
   source.EndOverrideActivationSorting();
   Check(!ov.overrideSorting&&sc.overrideSorting==!revealEndsFirst,""reveal and activation scopes independent"");
   if(!revealEndsFirst){Check(sc.sortingOrder==order+100,""last activation downgrades surviving creation priority"");source.EndSpecialCreationSorting();}
  }
  source.BeginSpecialCreationSorting();source.revealActive=true;source.BeginOverrideActivationSorting();source.OnDisable();
  Check(source.creationSortingUsers==0&&source.overrideActivationSortingUsers==0&&!sc.overrideSorting&&!ov.overrideSorting,""disable cancels overlapping reveal/activation and resets sorting"");
  source.EndOverrideActivationSorting();Check(!sc.overrideSorting&&!ov.overrideSorting,""late end after disable is harmless"");
  source.BeginOverrideActivationSorting();source.BeginSpecialCreationSorting();source.PrepareForRelease();
  Check(source.creationSortingUsers==0&&source.overrideActivationSortingUsers==0&&!sc.overrideSorting&&!ov.overrideSorting,""pool release resets sorting before next lifetime"");
  source.model.special=TileSpecial.None;source.BeginSpecialCreationSorting();Check(sc.sortingOrder==order+100&&!ov.overrideSorting,""reused normal tile inherits no activation priority"");source.EndSpecialCreationSorting();
 }
 var missing=new TileView();missing.BeginOverrideActivationSorting();missing.EndOverrideActivationSorting();missing.OnDisable();Check(missing.creationSortingUsers==0&&missing.overrideActivationSortingUsers==0,""missing icon is safe"");
 Console.WriteLine(""PASS: ""+checks+"" production Override foreground, nesting, input, reuse and teardown checks."");
 }
}
TileView.Run();
";
await CSharpScript.RunAsync(code.Replace("/* FIELDS */",declarations).Replace("/* METHODS */",methods));
