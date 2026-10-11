// Run: csi Tools/check_ancient_seal_layout.csx (production layout method, no Unity/project build).
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

var path="Assets/_Project/Scripts/Grid/AncientSealView.cs";
var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
var layout=tree.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="SetBodySize").ToFullString();
var constants=string.Join("\n",tree.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f=>f.Declaration.Variables.Any(v=>
 new[]{"cellUnit","BorderReferenceCells","CounterWidthCells","CounterTopInsetCells","SmallTier","LargeTier","GemSize","ShadowDrop"}.Contains(v.Identifier.ValueText))).Select(f=>f.ToFullString()))
 +tree.DescendantNodes().OfType<StructDeclarationSyntax>().Single(t=>t.Identifier.ValueText=="SizeTier").ToFullString();
var harness=@"
using System;
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new Vector2();}
public struct Rect {public float width,height;public Rect(float w,float h){width=w;height=h;}}
public class RectTransform {public Vector2 anchorMin,anchorMax,pivot,sizeDelta,anchoredPosition;}
public class Sprite {public Rect rect;public Sprite(float w,float h){rect=new Rect(w,h);}}
public class Image {public Sprite sprite;public float pixelsPerUnit=1,pixelsPerUnitMultiplier;}
public class Text {public float fontSize;public RectTransform rectTransform=new RectTransform();}
public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}
public class View {
 public RectTransform body=new RectTransform(),disk=new RectTransform(),counterRoot=new RectTransform();
 public RectTransform[] gems={new RectTransform(),new RectTransform(),new RectTransform()};
 public Image bodyImage=new Image{sprite=new Sprite(1254,1254)},counterPlateImage=new Image{sprite=new Sprite(1933,814)};
 public Text counter=new Text(),counterShadow=new Text();
"+constants+layout+@"
}
public static class Checks {
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
 public static int Run(){
  var v=new View();float referenceBorder=0;
  foreach(var size in new[]{new Vector2(2,2),new Vector2(3,5),new Vector2(6,7),new Vector2(4,4),new Vector2(5,3),new Vector2(9,3),new Vector2(2,4),new Vector2(6,2)}){
   v.SetBodySize(size.x*100,size.y*100,100);
   Check(v.body.sizeDelta.x==size.x*100&&v.body.sizeDelta.y==size.y*100,""Body fills entire footprint"");
   Check(Near(v.disk.sizeDelta.x,v.disk.sizeDelta.y),""Disk remains circular"");
   float border=290/(v.bodyImage.pixelsPerUnit*v.bodyImage.pixelsPerUnitMultiplier);
   if(referenceBorder==0)referenceBorder=border;
   Check(Near(border,referenceBorder),""Corners remain fixed across footprints"");
   Check(Near(v.counterRoot.sizeDelta.y/v.counterRoot.sizeDelta.x,814f/1933f),""Counter plate retains source aspect ratio"");
   bool large=Math.Min(size.x,size.y)>=3;
   Check(Near(v.disk.sizeDelta.x,large?228.375f/105f*100:145f/105f*100),""Disk uses the standard size of its tier"");
   Check(Near(-v.disk.anchoredPosition.y,large?13.625f/105f*100:6.75f/105f*100),""Disk uses the standard offset of its tier"");
   float diskCenter=size.y*50+v.disk.anchoredPosition.y;   // gövde altından
   float plaqueBottom=size.y*100+v.counterRoot.anchoredPosition.y-v.counterRoot.sizeDelta.y*.5f;
   Check(diskCenter+v.disk.sizeDelta.y*.5f-plaqueBottom<=8f,""Plaque overlaps the disk rim by at most a few pixels (approved)"");
   Check(diskCenter-v.disk.sizeDelta.y*.5f>=0&&v.disk.sizeDelta.x<=size.x*100,""Disk stays inside the body"");
  }
  v.bodyImage.sprite=new Sprite(1024,1024);v.bodyImage.pixelsPerUnit=2;v.SetBodySize(300,500,100);
  Check(Near((290f*1024/1254)/(v.bodyImage.pixelsPerUnit*v.bodyImage.pixelsPerUnitMultiplier),referenceBorder),""Texture import and Canvas PPU changes preserve corner thickness"");
  v.SetBodySize(120,120,60);Check(Near(v.disk.sizeDelta.x,145f/105f*60),""Sizes follow cell size on smaller screens"");
  return count;
 }
}
Checks.Run()";
var count=CSharpScript.EvaluateAsync<int>(harness).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+count+" AncientSeal layout checks");
foreach(var f in new[]{path,"Assets/_Project/Scripts/Grid/GridSpawner.cs","Assets/_Project/Scripts/Core/LevelData.cs",
 "Assets/_Project/Scripts/Editor/LevelDataEditor.cs","Assets/_Project/Scripts/Grid/Wall/WallPieceView.cs"}){
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(f)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(f+": "+string.Join("\n",errors.Select(e=>e.ToString())));
}
Console.WriteLine("PASS: changed source syntax");

