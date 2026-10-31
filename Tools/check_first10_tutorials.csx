// Installed API binding + prescribed one-swap shape/selection checks. No Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#r "System.Web.Extensions"
using System;using System.IO;using System.Linq;using System.Collections.Generic;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;using Microsoft.CodeAnalysis.CSharp.Scripting;
string folder="Assets/_Project/Scripts/";
string[] files={"Tutorial/ScriptedTutorial.cs"};
var refs=new List<MetadataReference>{MetadataReference.CreateFromFile(typeof(object).Assembly.Location),MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),MetadataReference.CreateFromFile(typeof(System.Net.WebClient).Assembly.Location)};
string unity="/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine";
refs.AddRange(Directory.GetFiles(unity,"*.dll").Select(p=>MetadataReference.CreateFromFile(p)));
foreach(var f in new[]{"Assembly-CSharp.dll","UnityEngine.UI.dll","Unity.TextMeshPro.dll"})refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Library/ScriptAssemblies/"+f)));
refs.Add(MetadataReference.CreateFromFile("/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Facades/netstandard.dll"));
var compilation=CSharpCompilation.Create("ScriptedTutorialApiCheck",files.Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText(folder+f),path:f)),refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var errors=compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
Console.WriteLine("PASS: changed tutorial runner binds to installed Unity/game APIs; no assembly emitted.");

var matchTree=CSharpSyntaxTree.ParseText(File.ReadAllText(folder+"Grid/Board/MatchFinder.cs")).GetRoot();
string[] matchNames={"IsNormalMatchable","EnsureRunCache","EnsureCacheArrays","RebuildRunCache","FlushRunCache","GetRunLengths","IsHigherPriorityRunAt","SquareOverlapsHigherPriorityRun","Has2x2At","DecideSpecialAt"};
string matchMethods=string.Join("\n",matchTree.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>matchNames.Contains(m.Identifier.Text)&&(m.Identifier.Text!="IsNormalMatchable"||m.ParameterList.Parameters[0].Type.ToString()=="TileData")).Select(m=>m.ToFullString()));
var tutorialTree=CSharpSyntaxTree.ParseText(File.ReadAllText(folder+"Tutorial/ScriptedTutorial.cs")).GetRoot();
string[] tutorialNames={"TryResolve","IsActivationSpecial","IsPlayableMove","TileAt","FocusCells","AddMatchesAround","AddRun","Is"};
string tutorialMethods=string.Join("\n",tutorialTree.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>tutorialNames.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
string tileSpecial=CSharpSyntaxTree.ParseText(File.ReadAllText(folder+"Grid/TileModel.cs")).GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().Single(e=>e.Identifier.Text=="TileSpecial").ToFullString();
string stubs=@"
using System;using System.IO;using System.Linq;using System.Collections.Generic;
public struct Vector2Int : IEquatable<Vector2Int> {
 public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}
 public bool Equals(Vector2Int v)=>x==v.x&&y==v.y;public override bool Equals(object v)=>v is Vector2Int p&&Equals(p);
 public override int GetHashCode()=>x*397^y;
}
public static class Mathf {public static int Abs(int n)=>Math.Abs(n);}
public class TileData {public int X,Y;public TileType Type;public TileSpecial Special;}
public class TileView {public int X,Y;public TileType Type;public TileSpecial Special;public bool IsSpecialLocked;public TileSpecial GetSpecial()=>Special;public TileType GetTileType()=>Type;}
public class ObstacleState {public bool[,] Locked;public bool IsMovableObstacleAt(int x,int y)=>false;public bool IsInteractionLockedAt(int x,int y)=>Locked[x,y];public bool IsOilAt(int x,int y)=>false;}
public class BoardController {
 public int Width,Height;public bool[,] Holes;public TileData[,] GridData;public TileView[,] Tiles;public ObstacleState ObstacleStateService;
 public BoardController(int w,int h){Width=w;Height=h;Holes=new bool[w,h];GridData=new TileData[w,h];Tiles=new TileView[w,h];ObstacleStateService=new ObstacleState{Locked=new bool[w,h]};}
}
public struct TutorialStep {public TutorialId id;public int fromCell,toCell;}
public class Fixture {public int level,width,height,expected;public int[] mask,top,tiles,specials;public TutorialStep[] steps;}
public class MatchProbe {
 readonly BoardController board;int[,] _hRunCache,_vRunCache;int _cacheW,_cacheH;bool _cacheValid;
 public MatchProbe(BoardController board){this.board=board;}
 /* MATCH METHODS */
}
public class TutorialProbe {
 readonly BoardController board;readonly int width;readonly HashSet<Vector2Int> focus=new HashSet<Vector2Int>();
 public TutorialProbe(BoardController board){this.board=board;width=board.Width;}
 public bool Resolve(TutorialStep s,out TileView a,out TileView b)=>TryResolve(s,out a,out b);
 public bool Playable(TileView a,TileView b)=>IsPlayableMove(a,b);
 /* TUTORIAL METHODS */
}
BoardController Load(Fixture f){
 var b=new BoardController(f.width,f.height);
 for(int i=0;i<f.mask.Length;i++){
  int x=i%f.width,y=i/f.width;bool free=f.mask[i]==1&&(f.top[i]==0||f.top[i]==25);
  b.Holes[x,y]=!free;if(!free)continue;
  b.GridData[x,y]=new TileData{X=x,Y=y,Type=(TileType)(f.tiles[i]-1),Special=(TileSpecial)f.specials[i]};
  b.Tiles[x,y]=new TileView{X=x,Y=y,Type=(TileType)(f.tiles[i]-1),Special=(TileSpecial)f.specials[i]};
 }
 return b;
}
void PrescribedSwap(BoardController b,TileView a,TileView z){
 int ax=a.X,ay=a.Y,zx=z.X,zy=z.Y;
 var dataA=b.GridData[ax,ay];var dataZ=b.GridData[zx,zy];
 b.Tiles[ax,ay]=z;b.Tiles[zx,zy]=a;z.X=ax;z.Y=ay;a.X=zx;a.Y=zy;
 b.GridData[ax,ay]=dataZ;b.GridData[zx,zy]=dataA;dataZ.X=ax;dataZ.Y=ay;dataA.X=zx;dataA.Y=zy;
}
int count=0;void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
var fixtures=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Fixture[]>(File.ReadAllText(""Docs/LevelDesign/Batches/ProLevels-V1-First10-Tutorials/tutorial-fixtures.json""));
foreach(var f in fixtures){
 var b=Load(f);var runner=new TutorialProbe(b);
 Check(runner.Resolve(f.steps[0],out var a,out var z),""First step resolves: ""+f.level);
 Check(runner.Playable(a,z),""First move really makes a legal match/combo: ""+f.level);
 if(f.level==9){Check(a.Special==TileSpecial.LineH&&z.Special==TileSpecial.LineV,""Both taught rockets already present"");continue;}
 bool horizontal=a.Y==z.Y;PrescribedSwap(b,a,z);
 var matcher=new MatchProbe(b);var result=matcher.DecideSpecialAt(a.X,a.Y,horizontal);
 Check((int)result==f.expected,""Actual MatchFinder creates expected special: ""+f.level+"" got ""+result);
 if(f.level==1){var lengths=matcher.GetRunLengths(a.X,a.Y);Check(Math.Max(lengths.hLen,lengths.vLen)==3,""Basic match is three, not a free special"");continue;}
 a.Special=result;b.GridData[a.X,a.Y].Special=result;
 Check(runner.Resolve(f.steps[1],out var activate,out var partner)&&activate.Special==result&&partner.Special==TileSpecial.None,""Activation selects the special just taught: ""+f.level);
}
foreach(var pair in new[]{(TutorialId.LineHActivated,TileSpecial.LineV),(TutorialId.PulseCoreActivated,TileSpecial.PulseCore),(TutorialId.PatchBotActivated,TileSpecial.PatchBot),(TutorialId.OverrideActivated,TileSpecial.SystemOverride)}){
 var f=new Fixture{width=3,height=3,mask=Enumerable.Repeat(1,9).ToArray(),top=new int[9],tiles=Enumerable.Repeat(1,9).ToArray(),specials=new int[9]};
 var b=Load(f);b.Tiles[0,0].Special=pair.Item2==TileSpecial.PatchBot?TileSpecial.PulseCore:TileSpecial.PatchBot;b.Tiles[1,1].Special=pair.Item2;
 var runner=new TutorialProbe(b);var step=new TutorialStep{id=pair.Item1,fromCell=-1,toCell=-1};
 Check(runner.Resolve(step,out var a,out var z)&&a==b.Tiles[1,1],""Earlier cascade special must not hijack ""+pair.Item1);
 b.Tiles[1,1].IsSpecialLocked=true;Check(!runner.Resolve(step,out a,out z),""Locked taught special is not forced"");
 b.Tiles[1,1].IsSpecialLocked=false;b.Tiles[1,1].Special=TileSpecial.None;
 Check(!runner.Resolve(step,out a,out z),""Missing taught special fails safely instead of teaching another type"");
 step.fromCell=0;step.toCell=1;Check(!runner.Resolve(step,out a,out z),""Explicit wrong-type activation rejected"");
}
{
 var b=Load(new Fixture{width=3,height=3,mask=Enumerable.Repeat(1,9).ToArray(),top=new int[9],tiles=Enumerable.Repeat(1,9).ToArray(),specials=new int[9]});
 var runner=new TutorialProbe(b);var s=new TutorialStep{id=TutorialId.SpecialCombo,fromCell=0,toCell=1};b.Tiles[0,0].Special=TileSpecial.LineH;
 Check(!runner.Resolve(s,out var a,out var z),""One special + normal cannot masquerade as a combo lesson"");
 b.Tiles[1,0].Special=TileSpecial.LineV;Check(runner.Resolve(s,out a,out z)&&runner.Playable(a,z),""Adjacent valid pair accepted"");
 b.ObstacleStateService.Locked[1,0]=true;Check(!runner.Resolve(s,out a,out z),""Interaction-locked pair not forced"");
 Check(!runner.Playable(b.Tiles[0,0],b.Tiles[2,2]),""Nonadjacent move rejected"");
}
Console.WriteLine(""PASS: ""+count+"" production tutorial selection and MatchFinder shape checks; no Unity, gravity, cascades or bot run."");
";
string code=stubs.Replace("/* MATCH METHODS */",matchMethods).Replace("/* TUTORIAL METHODS */",tutorialMethods);
int insertion=code.IndexOf("public struct Vector2Int");
code=code.Insert(insertion,File.ReadAllText(folder+"Tutorial/TutorialId.cs")+"\n"+File.ReadAllText(folder+"Grid/TileType.cs")+"\n"+tileSpecial+"\n");
await CSharpScript.RunAsync(code,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly,typeof(System.Web.Script.Serialization.JavaScriptSerializer).Assembly));
