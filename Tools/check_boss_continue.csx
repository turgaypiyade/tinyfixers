// Run with Mono csi from the repo root. Isolated production methods; no project build.
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

var paths = new[] {
    "Assets/_Project/Scripts/Grid/Board/BossDuelController.cs",
    "Assets/_Project/Scripts/Grid/Board/BoardController.cs",
    "Assets/_Project/Scripts/UI/LevelEndSimplePopupController.cs",
    "Assets/_Project/Scripts/VFX/ScreenCrackFx.cs",
    "Assets/_Project/Scripts/VFX/ScreenCrackLineGraphic.cs"
};
var roots = paths.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p))).ToArray();
foreach (var tree in roots)
    foreach (var error in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
        throw new Exception(error.ToString());
string Methods(int index, params string[] names) => string.Join("\n", roots[index].GetRoot()
    .DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
var popup = Methods(2, "PerformContinue");
if (!popup.Contains("board.ContinueWithExtraMoves(currentOfferAmount)"))
    throw new Exception("The shared paid/rewarded continue must notify the duel.");
var controller = File.ReadAllText(paths[0]);
if (!controller.Contains("board.OnLevelContinued += HandleLevelContinued;") ||
    !controller.Contains("board.OnLevelContinued -= HandleLevelContinued;"))
    throw new Exception("Continue event subscription must follow the duel lifecycle.");

var harness = @"
using System;
using System.Collections;
public static class Time { public static float deltaTime = .02f; }
public class View { public int resets; public void ResetForWave(){resets++;} }
public class Bar { public int current; public void Init(int max){current=max;} }
public class Board {
 public int RemainingMoves;
 public event Action<int> OnMovesChanged;
 public event Action OnLevelContinued;
" + Methods(1, "AddMoves", "ContinueWithExtraMoves") + @"
}
public class Duel {
 public Board board = new Board();
 public View playerCharacterView = new View(), enemyCharacterView = new View();
 public Bar playerHpBar = new Bar();
 public int playerHp=30, playerMaxHp=125, enemyHp=42, waveIndex=1, playerProtection=7, enemyProtection=13;
 public int accumulatedPower, attackingPower, starts, ticks, refreshes;
 public bool isActiveAndEnabled=true, bossModeActive=true, playerDefeated, enemyDefeated,
   outOfMovesDazed, winCelebrationPlayed, waveTransitionActive, playerMoveOpen;
 public IEnumerator loop;
 public Duel(){board.OnMovesChanged+=HandleAnimalMovesChanged;board.OnLevelContinued+=HandleLevelContinued;}
 public bool IsOver()=>playerHp<=0 || !bossModeActive || enemyDefeated;
 public void StartCoroutine(IEnumerator routine){starts++;loop=routine;}
 public void TickEndEvalHold(){} public void RefreshPowerLabel(){}
 public void TickAnimalTurn(float dt){ticks++;}
 public void RefreshProtectionLabels(){refreshes++;}
" + Methods(0, "HandleAnimalMovesChanged", "HandleLevelContinued", "BattleLoop") + @"
}
public static class Checks {
 static int count;
 static void Check(bool condition,string message){count++;if(!condition)throw new Exception(message);}
 public static int Run(){
  var dead=new Duel{playerHp=0,playerDefeated=true,bossModeActive=false,winCelebrationPlayed=true};
  dead.board.AddMoves(5);
  Check(dead.playerHp==0 && dead.starts==0,""Ordinary bonus moves must not revive"");
  dead.board.ContinueWithExtraMoves(5);
  Check(dead.playerHp==125 && dead.playerHpBar.current==125,""Continue fills health and HUD"");
  Check(!dead.playerDefeated && dead.bossModeActive && !dead.winCelebrationPlayed,""Defeat and enemy victory reset"");
  Check(dead.playerCharacterView.resets==1 && dead.enemyCharacterView.resets==1,""Both fighters leave terminal poses"");
  Check(dead.starts==1 && dead.loop.MoveNext() && dead.ticks==1,""Combat loop runs again"");
  Check(dead.enemyHp==42 && dead.waveIndex==1 && dead.playerProtection==7 && dead.enemyProtection==13,""Opponent, wave and protection preserved"");
  dead.playerHp=0;dead.playerDefeated=true;dead.bossModeActive=false;
  Check(!dead.loop.MoveNext(),""Second defeat stops the resumed loop"");
  dead.board.ContinueWithExtraMoves(10);
  Check(dead.playerHp==125 && dead.starts==2 && dead.loop.MoveNext(),""Repeated continuation revives again"");
  var exhausted=new Duel{outOfMovesDazed=true};exhausted.board.ContinueWithExtraMoves(5);
  Check(exhausted.playerHp==125 && !exhausted.outOfMovesDazed && exhausted.starts==0,""Move exhaustion heals without a duplicate battle loop"");
  var bonus=new Duel();bonus.board.AddMoves(3);
  Check(bonus.playerHp==30,""Bonus moves during battle do not heal"");
  var won=new Duel{enemyDefeated=true,bossModeActive=false};won.board.ContinueWithExtraMoves(5);
  Check(won.playerHp==125 && !won.bossModeActive && won.starts==0 && won.enemyCharacterView.resets==0,""Final defeated boss stays defeated"");
  var invalid=new Duel();invalid.board.ContinueWithExtraMoves(0);
  Check(invalid.playerHp==30 && invalid.board.RemainingMoves==0,""Invalid move offers do nothing"");
  var disabled=new Duel{isActiveAndEnabled=false};disabled.board.ContinueWithExtraMoves(5);
  Check(disabled.playerHp==30 && disabled.starts==0,""Disabled duel cannot resume"");
  return count;
 }
}
Checks.Run()";
var result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " continuation checks; C# syntax for all five changed source files.");
