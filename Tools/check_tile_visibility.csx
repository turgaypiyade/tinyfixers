// csi Tools/check_tile_visibility.csx [--before]
// Production visibility methods with UI doubles; no Unity project build or device rendering.
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
var before = Environment.GetCommandLineArgs().Contains("--before");
foreach (var path in new[]{"Assets/_Project/Scripts/Grid/TileView.cs", "Assets/_Project/Scripts/Grid/OverrideSpecialView.cs", "Assets/_Project/Scripts/Grid/Grass/GrassWaterLilyService.cs"}) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(path+": "+string.Join("\n",errors.Select(d=>d.ToString())));
}
string Methods(string path, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var tile = Methods("Assets/_Project/Scripts/Grid/TileView.cs", "FlashWhiteSpark", "ResetIconColorAfterDelay", "SetFlashColor", "StopIconFlash", "SetIcon");
var ov = Methods("Assets/_Project/Scripts/Grid/OverrideSpecialView.cs", "HideStaticIcon", "RestoreStaticIcon");
if(before) {
 tile = File.ReadAllText("Tools/Fixtures/TileVisibility/tile-before.cs.txt");
 ov = File.ReadAllText("Tools/Fixtures/TileVisibility/override-before.cs.txt");
}
var harness = @"
using System;using System.Collections;using System.Collections.Generic;
public struct Color {
 public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
 public static Color white=>new Color(1,1,1,1);
 public static Color Lerp(Color x,Color y,float t){t=Mathf.Clamp01(t);return new Color(x.r+(y.r-x.r)*t,x.g+(y.g-x.g)*t,x.b+(y.b-x.b)*t,x.a+(y.a-x.a)*t);}
}
public class CanvasRenderer {float a=1;public float GetAlpha()=>a;public void SetAlpha(float v){a=v;}}
public class Sprite {}
public static class GameDiagnostics {public static bool CriticalErrors=>true;}
public static class Debug {public static void LogError(string text){throw new Exception(text);}}
public class Image {public Sprite sprite;public Color color=Color.white;public CanvasRenderer canvasRenderer=new CanvasRenderer();public bool enabled=true,raycastTarget=true;public float Effective=>color.a*canvasRenderer.GetAlpha();}
public class GameObject {public bool activeInHierarchy=true;}
public static class Time {public static float deltaTime=1f/60;}
public static class Mathf {public static float Clamp01(float x)=>Math.Min(1,Math.Max(0,x));}
public class Coroutine {public IEnumerator routine;public bool stopped;}
public class Tile {
 public Image iconImage=new Image();public GameObject gameObject=new GameObject();
 Color originalFlashColor=Color.white;Coroutine flashRoutine;
 int lastAppliedTileSize;void ApplyTileSize(int size){}
 readonly List<Coroutine> routines=new List<Coroutine>();
 Coroutine StartCoroutine(IEnumerator routine){var c=new Coroutine{routine=routine};routines.Add(c);routine.MoveNext();return c;}
 void StopCoroutine(Coroutine c){c.stopped=true;}
 public void Tick(){foreach(var c in routines.ToArray())if(!c.stopped&&!c.routine.MoveNext())routines.Remove(c);}
 /* TILE */
}
public class Turn {
 public Image staticIconImage;bool staticIconHidden;float staticIconAlpha,staticIconRendererAlpha;
 /* OVERRIDE */
 public void Hide()=>HideStaticIcon();public void Stop()=>RestoreStaticIcon();
}
public static class Checks {
 static int checks,failures;
 static void Check(bool ok,string message){checks++;if(!ok){failures++;if(failures<=6)Console.WriteLine(""FAIL: ""+message);}}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
 public static void Run(){
  // Gather has hidden the real icon; reveal completes before the copper turn ends.
  var image=new Image{color=new Color(1,1,1,0)};var turn=new Turn{staticIconImage=image};turn.Hide();
  image.color=Color.white;turn.Hide();turn.Stop();
  Check(Near(image.Effective,1),""Override cannot restore the zero opacity sampled during gather"");
  image.color=new Color(1,1,1,.35f);turn.Hide();turn.Stop();
  Check(Near(image.color.a,.35f),""turn stop leaves an active external fade unchanged"");
  turn.Hide();image.color=Color.white;turn.Stop();turn.Stop();
  Check(Near(image.Effective,1)&&image.enabled&&image.raycastTarget,""swallow/disable/repeated stop restores pixels without disabling input graphic"");
  image.canvasRenderer.SetAlpha(.5f);turn.Hide();turn.Stop();
  Check(Near(image.canvasRenderer.GetAlpha(),.5f),""turn returns its renderer multiplier to the previous owner"");
  foreach(int fps in new[]{15,30,60,120}){
   Time.deltaTime=1f/fps;
   // A PatchBot arrival during a gather reads alpha=0. The surviving normal tile is revealed mid-flash.
   var tile=new Tile();tile.iconImage.color=new Color(.7f,.8f,.9f,0);tile.FlashWhiteSpark();
   tile.iconImage.color=Color.white;
   for(int i=0;i<fps;i++)tile.Tick();
   Check(Near(tile.iconImage.color.a,1),""normal tile survives flash/gather overlap at FPS ""+fps);
   tile=new Tile();tile.FlashWhiteSpark();tile.iconImage.color=new Color(1,1,1,0);
   for(int i=0;i<fps;i++)tile.Tick();
   Check(Near(tile.iconImage.color.a,0),""flash must not reveal a source still owned by clear/combo at FPS ""+fps);
   tile=new Tile();var random=new Random(166);
   for(int i=0;i<200;i++){
    if(i%5==0)tile.FlashWhiteSpark(.8f,.3f);
    var c=tile.iconImage.color;c.a=(float)random.NextDouble();tile.iconImage.color=c;tile.Tick();
    Check(Near(tile.iconImage.color.a,c.a),""restarted impact flashes never own visibility"");
   }
   /* CANCEL CHECK */
  }
  Console.WriteLine((failures==0?""PASS: "":""REGRESSIONS: "")+checks+"" checks, ""+failures+"" failures"");
  if(failures>0)throw new Exception(""Visibility regression reproduced"");
 }
}
".Replace("/* TILE */",tile).Replace("/* OVERRIDE */",ov)
 .Replace("/* CANCEL CHECK */", before ? "" : @"
   tile=new Tile();tile.iconImage.color=new Color(.1f,.2f,.3f,1);tile.FlashWhiteSpark(.5f,.5f);
   tile.SetIcon(new Sprite());
   for(int i=0;i<fps;i++)tile.Tick();
   Check(Near(tile.iconImage.color.r,1)&&Near(tile.iconImage.color.g,1)&&Near(tile.iconImage.color.b,1),""reassigned content cancels previous impact flash at FPS ""+fps);
 ");
CSharpScript.RunAsync(harness+"\nChecks.Run();").GetAwaiter().GetResult();
