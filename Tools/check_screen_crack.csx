// Run with Mono csi. Executes the production taper on an Image-shaped mesh; no Unity build.
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#r "Microsoft.CodeAnalysis.Scripting"
using System;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

string source = File.ReadAllText("Assets/_Project/Scripts/VFX/ScreenCrackLineGraphic.cs");
string fx = File.ReadAllText("Assets/_Project/Scripts/VFX/ScreenCrackFx.cs");
if (!fx.Contains("var layer = CreateImage(name, crackRoot, null, color);") ||
    !source.Contains("BaseMeshEffect"))
    throw new Exception("Crack layers must retain the standard Image rendering path.");

string harness = @"
using System;
using System.Collections.Generic;
 public class RequireComponent : Attribute { public RequireComponent(Type t){} }
 public struct Vector3 { public float x,y,z; public Vector3(float x,float y){this.x=x;this.y=y;z=0;} }
 public struct Rect {
  public float xMin,width,height;
  public Vector3 center => new Vector3(xMin+width/2,0);
 }
 public static class Mathf {
  public static float Max(float a,float b)=>Math.Max(a,b);
  public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));
  public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
 }
 public struct UIVertex { public Vector3 position; public int color,uv; }
 public class Graphic {
  public Rect rect; public int dirty;
  public void SetVerticesDirty(){dirty++;}
  public Rect GetPixelAdjustedRect()=>rect;
 }
 public class Image : Graphic {}
 public abstract class BaseMeshEffect {
  public Graphic graphic; public bool active=true;
  protected bool IsActive()=>active;
  public abstract void ModifyMesh(VertexHelper vh);
 }
 public class VertexHelper {
  public List<UIVertex> vertices=new List<UIVertex>();
  public int currentVertCount=>vertices.Count;
  public void PopulateUIVertex(ref UIVertex v,int i){v=vertices[i];}
  public void SetUIVertex(UIVertex v,int i){vertices[i]=v;}
 }
" + source.Replace("using UnityEngine;", "").Replace("using UnityEngine.UI;", "") + @"
public static class Checks {
 static int count;
 static void Check(bool condition,string message){count++;if(!condition)throw new Exception(message);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
 public static int Run(){
  foreach(float length in new[]{0f,25f,50f,100f}){
   var image=new Image{rect=new Rect{xMin=0,width=length,height=8}};
   var taper=new ScreenCrackLineGraphic{graphic=image};taper.SetWidths(8,.6f,100);
   var mesh=new VertexHelper();
   foreach(var pos in new[]{new Vector3(0,-4),new Vector3(0,4),new Vector3(length,4),new Vector3(length,-4)})
    mesh.vertices.Add(new UIVertex{position=pos,color=123,uv=456});
   taper.ModifyMesh(mesh);
   Check(mesh.currentVertCount==4,""Taper must retain the Image mesh"");
   Check(image.dirty==1,""Setting taper requests an Image rebuild"");
   Check(Near(mesh.vertices[0].position.y,-4)&&Near(mesh.vertices[1].position.y,4),""Root stays thick"");
   float tip=length==0 ? 4 : (8+(.6f-8)*length/100)/2;
   Check(Near(mesh.vertices[2].position.y,tip)&&Near(mesh.vertices[3].position.y,-tip),""Tip narrows continuously during growth"");
   foreach(var v in mesh.vertices)Check(v.color==123&&v.uv==456,""Image color and UVs survive"");
   float before=mesh.vertices[2].position.y;taper.active=false;taper.ModifyMesh(mesh);
   Check(Near(before,mesh.vertices[2].position.y),""Disabled effect leaves the Image intact"");
  }
  return count;
 }
}
Checks.Run()";
int result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default
    .AddReferences(typeof(System.Collections.Generic.List<>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " crack mesh checks. Unity rendering still requires Play-mode verification.");
