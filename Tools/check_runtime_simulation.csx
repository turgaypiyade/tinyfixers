// Production simulator validation/report/integrity methods with doubles. No Unity build or bot run.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
string file="Assets/_Project/Scripts/Simulation/RuntimeSimulationSession.cs";
foreach(var symbols in new[]{new string[0],new[]{"UNITY_EDITOR"},new[]{"TF_SIM_BUILD"}}) {
 var syntax=CSharpSyntaxTree.ParseText(File.ReadAllText(file),CSharpParseOptions.Default.WithPreprocessorSymbols(symbols));
 var errors=syntax.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
}
var root=CSharpSyntaxTree.ParseText(File.ReadAllText(file),CSharpParseOptions.Default.WithPreprocessorSymbols("TF_SIM_BUILD")).GetRoot();
string nested=string.Join("\n",root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c=>new[]{"Config","GameResult","Report"}.Contains(c.Identifier.ValueText)).Select(c=>c.ToString()));
string methods=string.Join("\n",root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"ConfigError","BoardIntegrityError","Finish"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()))
 .Replace("private static string ConfigError","public static string ConfigError").Replace("private string BoardIntegrityError","public string BoardIntegrityError").Replace("private void Finish","public void Finish");
string resolve=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/GridSpawner.cs")).GetRoot().DescendantNodes()
 .OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="ResolveLevelData").ToString().Replace("private LevelData ResolveLevelData","public LevelData ResolveLevelData");
var source=@"
using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Text;
public enum RuntimeBotPolicy {Random,GoalAware,Greedy}
public enum TileSpecial {None,PulseCore}
public enum TileType {Red,Blue}
public class GameObject {public bool activeInHierarchy=true;}
public class TileView {
 public GameObject gameObject=new GameObject();public int X,Y;public BoardController Board;
 public TileSpecial special;public TileType type;public bool IsRuntimeIdle=true,KeepHiddenForCombo,settled=true;
 public TileSpecial GetSpecial()=>special;public TileType GetTileType()=>type;
}
public class TileData {public TileType Type;public TileSpecial Special;}
public class Transform {
 public List<TileView> children=new List<TileView>();
 public void GetComponentsInChildren(bool inactive,List<TileView> output){output.Clear();output.AddRange(children.Where(t=>inactive||t.gameObject.activeInHierarchy));}
}
public class BoardController {
 public int Width=1,Height=1;public Transform Parent=new Transform();public TileView[,] tiles=new TileView[1,1];public TileData[,] GridData=new TileData[1,1];
 public TileView GetTileViewAt(int x,int y)=>x<0||y<0||x>=Width||y>=Height?null:tiles[x,y];
 public bool IsTileReadyForContinuousMatch(TileView tile)=>tile.settled;
}
public static class Application {public static int exit;public static void Quit(int code){exit=code;}}
public class LevelData {public int moves=20;}
public class Selector {public LevelData ResolveLevelData()=>null;}
public static class CurrentLevel {public static int Global=1;}
public static class LevelRemoteTuning {public static int MovesFor(int level,int moves)=>moves;}
public class GridSpawner {
 public Selector levelRuntimeSelector;public LevelData level=new LevelData();bool ownsResolvedLevelInstance;
 LevelData ResolveSimLevelByNumber()=>null;LevelData CloneLevelDataForRuntime(LevelData level)=>level;
 /* RESOLVE */
}
public class RuntimeSimulationSession {
 public static bool IsActive;public static LevelData CurrentLevel;public static int CurrentLevelNumber;
 public Report _report=new Report();public bool saved=true,_finished;readonly List<TileView> _sceneTiles=new List<TileView>();
 bool SaveReport()=>saved;void SetStatus(string s){}
 /* NESTED */
 /* METHODS */
}
".Replace("/* NESTED */",nested).Replace("/* METHODS */",methods).Replace("/* RESOLVE */",resolve);
source+=File.ReadAllText("Assets/_Project/Scripts/Simulation/RuntimeSimulationReport.cs").Replace("using System.Collections.Generic;","").Replace("using System.Globalization;","").Replace("using System.Linq;","").Replace("using System.Text;","");
source+=@"
public static class Checks {
 static int count;static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
 static RuntimeSimulationSession.Config Config()=>new RuntimeSimulationSession.Config{levelNumbers=new[]{1,2},gamesPerLevel=3,outputPath=""report.json""};
 static BoardController Board(){var b=new BoardController();var t=new TileView{Board=b};b.tiles[0,0]=t;b.GridData[0,0]=new TileData();b.Parent.children.Add(t);return b;}
 public static void Run(){
  Check(RuntimeSimulationSession.ConfigError(Config())==null,""headless config valid"");
  Check(RuntimeSimulationSession.ConfigError(null)!=null,""missing config rejected"");
  var c=Config();c.levelNumbers=new int[0];Check(RuntimeSimulationSession.ConfigError(c)!=null,""empty run rejected"");
  c=Config();c.levelNumbers=new[]{0};Check(RuntimeSimulationSession.ConfigError(c)!=null,""invalid level rejected"");
  c=Config();c.gamesPerLevel=0;Check(RuntimeSimulationSession.ConfigError(c)!=null,""zero games rejected"");
  foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity}) {
   c=Config();c.timeScale=invalid;Check(RuntimeSimulationSession.ConfigError(c)!=null,""invalid speed rejected"");
   c=Config();c.stepTimeoutSeconds=invalid;Check(RuntimeSimulationSession.ConfigError(c)!=null,""invalid timeout rejected"");
  }
  c=Config();c.policy=(RuntimeBotPolicy)99;Check(RuntimeSimulationSession.ConfigError(c)!=null,""unknown bot rejected"");
  c=Config();c.outputPath="""";Check(RuntimeSimulationSession.ConfigError(c)!=null,""missing output rejected"");
  c=Config();c.scenePath="""";Check(RuntimeSimulationSession.ConfigError(c)!=null,""missing scene rejected"");
  c=Config();c.outputPath=""bad\0path"";Check(RuntimeSimulationSession.ConfigError(c)!=null,""invalid output rejected"");
  var session=new RuntimeSimulationSession();var b=Board();Check(session.BoardIntegrityError(b)==null,""registered settled board valid"");
  b=Board();b.tiles[0,0]=null;Check(session.BoardIntegrityError(b)!=null,""orphan data detected"");
  b=Board();b.tiles[0,0].gameObject.activeInHierarchy=false;Check(session.BoardIntegrityError(b)!=null,""inactive pooled tile cannot occupy a cell"");
  b=Board();b.tiles[0,0].X=1;Check(session.BoardIntegrityError(b)!=null,""wrong coordinates detected"");
  b=Board();b.GridData[0,0]=null;Check(session.BoardIntegrityError(b)!=null,""missing model data detected"");
  b=Board();b.GridData[0,0].Type=TileType.Blue;Check(session.BoardIntegrityError(b)!=null,""colour mismatch detected"");
  b=Board();b.GridData[0,0].Special=TileSpecial.PulseCore;Check(session.BoardIntegrityError(b)!=null,""special mismatch detected"");
  b=Board();b.tiles[0,0].KeepHiddenForCombo=true;Check(session.BoardIntegrityError(b)!=null,""leaked combo hiding detected"");
  b=Board();b.tiles[0,0].settled=false;Check(session.BoardIntegrityError(b)!=null,""lost visual position detected"");
  b=Board();var ghost=new TileView{Board=b,special=TileSpecial.PulseCore};b.Parent.children.Add(ghost);
  Check(session.BoardIntegrityError(b).Contains(""Orphan active PulseCore""),""unregistered idle pulse detected without deleting it"");
  ghost.gameObject.activeInHierarchy=false;Check(session.BoardIntegrityError(b)==null,""inactive pool reserve ignored"");
  ghost.gameObject.activeInHierarchy=true;ghost.IsRuntimeIdle=false;Check(session.BoardIntegrityError(b)==null,""offboard in-flight view is left to its job"");
  ghost.IsRuntimeIdle=true;ghost.Board=new BoardController();Check(session.BoardIntegrityError(b)==null,""other board ignored"");
  var report=new RuntimeSimulationSession.Report{config=Config()};
  Check(RuntimeSimulationReport.Summary(report).Contains(""0/6""),""headless planned count uses levelNumbers"");
  report.config.levelPaths=new[]{""level.asset""};Check(RuntimeSimulationReport.Summary(report).Contains(""0/3""),""editor asset paths take precedence"");
  session.saved=true;session.Finish(true);Check(Application.exit==0,""saved complete run exits zero"");
  session.Finish(false);Check(Application.exit==1,""incomplete run exits nonzero"");
  session.saved=false;session.Finish(true);Check(Application.exit==2,""failed report save cannot exit successfully"");
  var spawner=new GridSpawner();RuntimeSimulationSession.IsActive=true;RuntimeSimulationSession.CurrentLevelNumber=999;
  bool threw=false;try{spawner.ResolveLevelData();}catch(InvalidOperationException){threw=true;}
  Check(threw,""missing simulation level cannot silently play scene fallback"");
  var requested=new LevelData();RuntimeSimulationSession.CurrentLevel=requested;
  Check(spawner.ResolveLevelData()==requested,""selected simulation asset retained"");
  RuntimeSimulationSession.IsActive=false;Check(spawner.ResolveLevelData()==spawner.level,""normal gameplay scene fallback preserved"");
  Console.WriteLine(""PASS: ""+count+"" simulator config/integrity/report checks; retail/editor/sim branches parsed. No Unity build/run."");
 }
}
Checks.Run();
";
await CSharpScript.RunAsync(source,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
