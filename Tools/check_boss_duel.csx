// Run from the repository root with Mono csi. No Unity/project build is performed.
// Production methods are parsed and executed in isolation; Unity animation and board services
// are test doubles. This checks arithmetic and coroutine ordering, not Unity Play-mode behavior.
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

var files = new[] {
    "Assets/_Project/Scripts/Grid/Board/BossDuelController.cs",
    "Assets/_Project/Scripts/Grid/Board/BossDuelCharacterView.cs",
    "Assets/_Project/Scripts/Grid/Board/BossDuelStatusGraphic.cs",
    "Assets/_Project/Scripts/Grid/Board/BossDifficulty.cs",
    "Assets/_Project/Scripts/Core/LevelData.cs",
    "Assets/_Project/Scripts/Editor/LevelDataEditor.cs",
    "Assets/_Project/Scripts/Grid/Board/BossDuelAttackVfx.cs",
    "Assets/_Project/Scripts/Grid/Board/BossDuelPowerOrbs.cs",
    "Assets/_Project/Scripts/Grid/Board/BossDuelObstaclePressure.cs",
    "Assets/_Project/Scripts/UI/LevelEndSimplePopupController.cs"
};
foreach (var file in files)
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview));
    var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    if (errors.Length > 0) throw new Exception(file + "\n" + string.Join("\n", errors.Select(e => e.ToString())));
}
Console.WriteLine("PASS: C# syntax, " + files.Length + " changed source files");

var controller = CSharpSyntaxTree.ParseText(File.ReadAllText(files[0])).GetRoot();
var methodNames = new[] { "ResolveAnimalTurn", "TickAnimalTurn", "TickEndEvalHold", "AbsorbAnimalDamage", "HandleAnimalMovesChanged", "NoteEndEvalProgress" };
var methods = string.Join("\n", controller.DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => methodNames.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));

var harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
 public static class Time { public static float unscaledTime; }
 public static class Debug { public static void LogWarning(string message) {} }
 public class Sprite {}
 public struct Color { public Color(float r,float g,float b,float a=1f) {} public static Color white => new Color(); }
 public static class PlayerPrefs { public static int GetInt(string key,int fallback) => fallback; }
 public static class Mathf {
  public static int Max(int a,int b)=>Math.Max(a,b); public static float Max(float a,float b)=>Math.Max(a,b);
  public static int Min(int a,int b)=>Math.Min(a,b); public static int RoundToInt(float x)=>(int)Math.Round(x);
  public static int Clamp(int x,int lo,int hi)=>Math.Max(lo,Math.Min(x,hi));
 }
public class BossDuelCharacterProfile {}
public enum ObstacleId { EnemyShieldPickup }
public class ObstacleDef { public Sprite GetPreviewSprite()=>null; }
public class ObstacleLibrary { public ObstacleDef Get(ObstacleId id)=>null; }
public class BossWaveDef {
 public float hpWeight=1,attackInterval; public int attackDamageBase,attackDamageGrowth=-1,oilCount=-1,oilEveryMoves=-1;
 public Sprite bodySprite,defeatedSprite; public Color bodyTint; public BossDuelCharacterProfile characterProfile;
}
public class LevelData {
 public BossWaveDef[] bossWaves; public int bossWaveCount,enemyAttackBaseDamage=10,enemyAttackDamageGrowth,bossAttackOilCount,bossAttackEveryMoves=3;
 public float enemyAttackInterval=2; public ObstacleLibrary obstacleLibrary;
}
public class Board {
 public int RemainingMoves=3,Holds,Failed; public bool IsExplicitlyLocked;
 public Flow Flow=new Flow(); public void BeginBossStrikeDrain(){Holds++;}
 public void EndBossStrikeDrain(){Holds--;if(Holds<0)throw new Exception(""Unbalanced hold"");}
 public void RequestLevelFail(){Failed++;}
}
public class Flow { public bool IsDuelMoveSettling; }
public class Orbs { public int InFlight; }
public class Hud { public bool AreAllGoalsCompleted; }
public class View { public int resets; public void ResetForWave(){resets++;} }
public class Duel {
 public Board board=new Board(); public Hud topHud=new Hud(); public Orbs powerOrbs=new Orbs();
 public View playerCharacterView=new View();
 public bool animalDuel=true,bossModeActive=true,isActiveAndEnabled=true,animalTurnActive,playerAttackActive,enemyMeleeActive,
   playerMoveOpen,waveTransitionActive,playerDefeated,outOfMovesDazed,endEvalHoldActive,laserFiring,over;
 public int accumulatedPower=30,waveIndex,enemyHp=90,playerHp=100,playerProtection,enemyProtection,
   defeatAnimationsActive,bonusStrikePool,boltsInFlight,counters,attacks,killMode;
 public float moveSettledTime,endEvalHoldStartTime; public bool endEvalHoldWarned;
 public Queue<int> strikeQueue=new Queue<int>();
 public object playerShieldBubble,enemyShieldBubble,playerShieldColor,enemyShieldColor,playerRobot,
   enemyRobot,playerBodyImage,playerDefeatedSprite,playerArmA,playerArmB;
 public IEnumerator started;
 public List<string> log=new List<string>();
 public bool IsOver()=>over || playerHp<=0 || !bossModeActive;
 public void StartCoroutine(IEnumerator routine){started=routine;}
 public IEnumerator AnimalPlayerStrike(){
   if(board.Holds!=1)throw new Exception(""Player attack missing hold"");
   attacks++;log.Add(""player-impact"");accumulatedPower=0;
   if(killMode==1){waveTransitionActive=true;enemyHp=0;}
   if(killMode==2){over=true;topHud.AreAllGoalsCompleted=true;bossModeActive=false;}
   yield return null;log.Add(""player-return"");
 }
 public IEnumerator EnemyAttack(){
   if(board.Holds!=1 || !animalTurnActive)throw new Exception(""Counter missing hold"");
   counters++;log.Add(""counter-impact"");
   if(playerHp<=10){playerHp=0;playerDefeated=true;bossModeActive=false;}
   yield return null;log.Add(""counter-return"");
 }
 public IEnumerator PlayDefeat(params object[] unused){log.Add(""daze"");yield return null;}
 public void PlayShieldAbsorb(params object[] unused){}
 public void ShowToast(string text,float duration){}
 public string LocFormat(string key,string fallback,params object[] args)=>string.Format(fallback,args);
 public void RefreshProtectionLabels(){}
 public IEnumerator Turn()=>ResolveAnimalTurn();
 public void Tick(float dt)=>TickAnimalTurn(dt);
 public void Hold()=>TickEndEvalHold();
 public void Progress()=>NoteEndEvalProgress();
 public int Hit(int damage,bool toPlayer)=>AbsorbAnimalDamage(damage,toPlayer);
 public void AddMoves(int n){board.RemainingMoves+=n;HandleAnimalMovesChanged(board.RemainingMoves);}
" + methods + @"
}
public static class Checks {
 static int assertions;
 static void Check(bool condition,string name){assertions++;if(!condition)throw new Exception(name);}
 static void Run(Duel d){
   var stack=new Stack<IEnumerator>();stack.Push(d.Turn());int ticks=0;
   while(stack.Count>0){
     if(++ticks>200)throw new Exception(""Stuck coroutine"");
     var current=stack.Peek();
     if(!current.MoveNext()){(current as IDisposable)?.Dispose();stack.Pop();continue;}
     if(current.Current is IEnumerator nested){stack.Push(nested);continue;}
     Check(d.board.Holds==1,""Hold must span all yields"");
     if(d.waveTransitionActive){d.waveTransitionActive=false;d.waveIndex++;d.enemyHp=120;}
   }
   Check(d.board.Holds==0,""Turn releases its level-end hold"");
 }
 public static int Test(){
   foreach(bool side in new[]{true,false})for(int shield=0;shield<=30;shield++)for(int dmg=0;dmg<=60;dmg++){
     var d=new Duel{playerProtection=shield,enemyProtection=shield};
     int hpLoss=d.Hit(dmg,side),remaining=side?d.playerProtection:d.enemyProtection;
     Check(hpLoss==Math.Max(0,dmg-shield),""Shield HP damage"");
     Check(remaining==Math.Max(0,shield-dmg),""Unused shield persists"");
     Check(hpLoss+shield-remaining==dmg,""Damage conserved"");
     Check((side?d.enemyProtection:d.playerProtection)==shield,""Other side unchanged"");
   }
   var progressing=new Duel{endEvalHoldStartTime=0,endEvalHoldWarned=true};
   Time.unscaledTime=25;progressing.Progress();
   Check(progressing.endEvalHoldStartTime==25 && !progressing.endEvalHoldWarned,""Real duel progress resets the stall diagnostic"");
   var survive=new Duel();Run(survive);
   Check(string.Join("","",survive.log)==""player-impact,player-return,counter-impact,counter-return"",""One ordered counter"");
   Check(survive.board.RemainingMoves==3,""No move refund or second consumption"");
   var wave=new Duel{killMode=1,playerProtection=12};Run(wave);
   Check(wave.counters==0 && wave.waveIndex==1 && wave.playerProtection==12 && wave.playerHp==100,""Wave kill: no counter, heal, shield loss"");
   var win=new Duel{killMode=2};win.board.RemainingMoves=0;Run(win);
   Check(win.counters==0 && win.board.Failed==0 && !win.outOfMovesDazed,""Last move final kill wins"");
   var exhausted=new Duel();exhausted.board.RemainingMoves=0;Run(exhausted);
   Check(exhausted.counters==1 && exhausted.outOfMovesDazed,""Last nonlethal move counters then dazes"");
   exhausted.AddMoves(5);Check(!exhausted.outOfMovesDazed && exhausted.playerCharacterView.resets==1,""Extra moves restore pose"");
   var midwave=new Duel{killMode=1};midwave.board.RemainingMoves=0;Run(midwave);
   Check(midwave.counters==0 && midwave.outOfMovesDazed,""Last move intermediate kill still exhausts moves"");
   var dead=new Duel{playerHp=10};Run(dead);
   Check(dead.board.Failed==1 && dead.log[dead.log.Count-1]==""counter-return"",""HP death fails after return"");
   var zero=new Duel{accumulatedPower=0};Run(zero);
   Check(zero.attacks==0 && zero.counters==1,""Zero-power move still counters and releases"");
   var idle=new Duel{playerMoveOpen=false};idle.Tick(20f);
   Check(idle.started==null && idle.counters==0,""Idle never attacks"");
   idle.playerMoveOpen=true;idle.board.Flow.IsDuelMoveSettling=true;idle.Tick(20f);
   Check(idle.started==null,""Cascades must settle first"");
   idle.board.Flow.IsDuelMoveSettling=false;idle.Tick(0.09f);
   Check(idle.started!=null,""Settled move starts turn"");
   for(int total=1;total<=500;total++)for(int count=1;count<=6;count++){
     var level=new LevelData{bossWaves=new BossWaveDef[count]};
     for(int i=0;i<count;i++)level.bossWaves[i]=new BossWaveDef{hpWeight=i==0?100000:1};
     var waves=BossDifficulty.BuildWaves(level,total);
     int sum=0;foreach(var w in waves){Check(w.hp>0,""Positive wave HP"");sum+=w.hp;}
     Check(sum==total && waves.Length<=3,""Exact goal total, max three waves"");
   }
   var example=new LevelData{bossWaves=new[]{new BossWaveDef{hpWeight=60,attackDamageBase=8},new BossWaveDef{hpWeight=90,attackDamageBase=10},new BossWaveDef{hpWeight=120,attackDamageBase=12}}};
   var roster=BossDifficulty.BuildWaves(example,270);
   Check(roster[0].hp==60 && roster[1].hp==90 && roster[2].hp==120,""Approved HP example"");
   foreach(int power in new[]{20,30,40}){
     int hp=100,counters=0,turns=0;foreach(var w in roster){int attacks=(w.hp+power-1)/power;turns+=attacks;counters+=attacks-1;hp-=(attacks-1)*w.attackDamageBase;}
     if(power==30)Check(hp==36 && turns==9 && counters==6,""Approved 30-tile encounter"");
     if(power==20)Check(hp==-16,""20-tile encounter needs protection"");
   }
   return assertions;
 }
}
" + File.ReadAllText(files[3]).Replace("using UnityEngine;", "") + "\nChecks.Test()";
var result = CSharpScript.EvaluateAsync<int>(harness, ScriptOptions.Default
    .AddReferences(typeof(System.Collections.Generic.Queue<>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: " + result + " isolated C# assertions (damage, turn order, moves, wave HP)");
