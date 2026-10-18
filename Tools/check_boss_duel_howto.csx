// Run: csi Tools/check_boss_duel_howto.csx. Script checks only; no Unity/project build.
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

var ui = CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BossDuelHowToPlay.cs"));
var controller = CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BossDuelController.cs"));
foreach (var tree in new[] { ui, controller })
    if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
        throw new Exception(string.Join("\n", tree.GetDiagnostics()));
string Method(SyntaxTree tree, string name) => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == name).ToFullString();
var seenKey = ui.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
    .Single(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == "SeenKey")).ToFullString();
var revealDuration = ui.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
    .Single(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == "RevealDuration")).ToFullString();
var gate = ui.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>()
    .Single(p => p.Identifier.ValueText == "ShouldShow").ToFullString();
var harness = @"
using System;
using System.Collections.Generic;
public static class RuntimeSimulationSession {public static bool IsActive;}
public static class PlayerPrefs {
 public static Dictionary<string,int> Data=new Dictionary<string,int>();public static int Saves;
 public static int GetInt(string key,int fallback)=>Data.TryGetValue(key,out var n)?n:fallback;
 public static void SetInt(string key,int value)=>Data[key]=value;
 public static void Save(){Saves++;}
}
public class GameObject {public bool Active=true;public void SetActive(bool active){Active=active;}}
public class BossDuelHowToPlay {
" + seenKey + gate + revealDuration + @"
 public bool Completed {get;private set;}public float revealTime;public int Reveals;public GameObject gameObject=new GameObject();
 public void RefreshReveal(){Reveals++;}public void Tap(){OnTap();}public void Read(){revealTime=RevealDuration;}
" + Method(ui,"OnTap") + @"
}
public class Board {public bool Locked=true;public int Unlocks;public void SetInputLocked(bool locked){Locked=locked;if(!locked)Unlocks++;}}
public class Pause:IDisposable {public int Releases;public void Dispose(){Releases++;}}
public class Opening {
 public BossDuelHowToPlay howToPlay;public Pause openingFlowPause;public bool ownsOpeningInputLock;public Board board=new Board();public int Destroyed;
 public void Destroy(GameObject go){Destroyed++;go.SetActive(false);}
 public void Release(){ReleaseOpening();}
" + Method(controller,"ReleaseOpening") + @"
}
public static class Checks {
 static int n;static void Check(bool ok,string message){n++;if(!ok)throw new Exception(message);}
 public static int Run(){
  Check(BossDuelHowToPlay.ShouldShow,""First duel shows instructions"");
  RuntimeSimulationSession.IsActive=true;Check(!BossDuelHowToPlay.ShouldShow,""Simulation never waits for a tutorial tap"");RuntimeSimulationSession.IsActive=false;
  var view=new BossDuelHowToPlay();view.Tap();
  Check(!view.Completed&&view.Reveals==1&&view.gameObject.Active,""Early tap reveals the full single page without dismissing it"");
  Check(BossDuelHowToPlay.ShouldShow&&PlayerPrefs.Saves==0,""Revealing instructions does not mark them seen"");
  var aborted=new Opening{howToPlay=view,openingFlowPause=new Pause(),ownsOpeningInputLock=true};var pause=aborted.openingFlowPause;
  aborted.Release();aborted.Release();Check(pause.Releases==1&&aborted.board.Unlocks==1&&aborted.Destroyed==1,""Scene/disable cleanup releases modal, flow pause and input exactly once"");
  Check(BossDuelHowToPlay.ShouldShow,""Aborting before final confirmation shows instructions next attempt"");
  view=new BossDuelHowToPlay();view.Read();view.Tap();
  Check(view.Completed&&!view.gameObject.Active&&!BossDuelHowToPlay.ShouldShow&&PlayerPrefs.Saves==1,""One tap after the reveal marks completion and dismisses; later duels skip"");
  view.Tap();Check(PlayerPrefs.Saves==1,""Duplicate completion taps do not save twice"");
  PlayerPrefs.Data.Clear();PlayerPrefs.Saves=0;
  view=new BossDuelHowToPlay();view.Tap();view.Tap();
  Check(view.Completed&&PlayerPrefs.Saves==1,""Early reveal then confirmation starts the duel without extra pages"");
  return n;
 }
}
Checks.Run()";
int count=CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.Dictionary<string,int>).Assembly)).GetAwaiter().GetResult();
var init=Method(controller,"InitWhenLevelReady");
if (!(init.IndexOf("yield return intro.Play()") < init.IndexOf("BossDuelHowToPlay.Show")
    && init.IndexOf("BossDuelHowToPlay.Show") < init.IndexOf("board.OnTilesCleared +=")
    && init.IndexOf("board.OnTilesCleared +=") < init.IndexOf("StartCoroutine(BattleLoop())")))
    throw new Exception("Expected VS -> how-to -> gameplay subscription/battle order");
Console.WriteLine("PASS: " + count + " onboarding state/cleanup checks; syntax and intro/gameplay order verified.");
