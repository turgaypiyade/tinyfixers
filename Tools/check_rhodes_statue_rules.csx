// Actual shared stage selection and damage-context rules. No Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var tree=(CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Core/ObstacleLibrary.cs")).GetRoot();
var stripped=tree.RemoveNodes(tree.DescendantNodes().OfType<AttributeListSyntax>(),SyntaxRemoveOptions.KeepNoTrivia);
var production=string.Join("\n",stripped.Members.Where(m=>!(m is ClassDeclarationSyntax c&&c.Identifier.ValueText=="ObstacleLibrary")).Select(m=>m.ToFullString()));
var state=(CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs")).GetRoot();
var match=state.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="DoesContextMatchRule").ToString().Replace("private bool","public static bool");
var collect=state.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="CollectCellsForOrigin").ToString().Replace("private int[]","public int[]");
var stubs=@"
using System;using System.Collections.Generic;using System.Linq;
public enum ObstacleId {None=0,Stone=1,RhodesStatue=70}
public enum TileType {Gear}
public enum ObstacleHitContext {NormalMatch,SpecialActivation,Booster,Scripted}
public class Sprite {public int Frame;}
public class AudioClip {}
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public static Vector2Int one=>new Vector2Int(1,1);}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(b,v));}
public class TestLevel {public int[] obstacles=new int[16],obstacleOrigins=Enumerable.Repeat(-1,16).ToArray();}
";
var probe="public class Probe {public TestLevel level=new TestLevel();private bool HasStampedObstacleAt(int a,ObstacleId b,int c)=>false;\n"+match+"\n"+collect+"\n}";
var resolutionRoot=(CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleResolutionService.cs")).GetRoot();
var apply=resolutionRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="ApplyDamage").ToString();
var requestRoot=(CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleDamageRequest.cs")).GetRoot();
var request=string.Join("\n",requestRoot.Members.Select(m=>m.ToFullString()));
var routing=@"
public class ObstacleStateService {
 public int remaining=4;public ObstacleId id=ObstacleId.RhodesStatue;
 public struct ObstacleHitResult {public bool didHit;}
 public ObstacleHitResult TryDamageAt(ObstacleDamageRequest r){
  bool hit=Probe.DoesContextMatchRule(r.context,ObstacleDamageSourceRule.SpecialOnly);
  if(hit)remaining--;return new ObstacleHitResult{didHit=hit};
 }
 public ObstacleId GetObstacleIdAt(int x,int y)=>id;
}
public class BoardController {public ObstacleStateService ObstacleStateService=new ObstacleStateService();}
";
var resolver="public class ResolutionProbe {public BoardController board=new BoardController();public bool forced;private bool ConsumePatchBotForcedHit(int x,int y)=>forced;private bool IsCrossContextFallbackAllowedAt(int x,int y)=>false;private void ConsumeStageTransition(ObstacleStateService.ObstacleHitResult result){}\n"+apply+"\n}";
var tests=@"
int checks=0;Action<bool,string> check=(ok,name)=>{checks++;if(!ok)throw new Exception(name);};
var def=new ObstacleDef{id=ObstacleId.RhodesStatue,hits=4,size=new Vector2Int(2,2)};
for(int i=0;i<4;i++)def.stages.Add(new StageRule{sprite=new Sprite{Frame=i},damageRule=ObstacleDamageSourceRule.SpecialOnly,behavior=ObstacleBehaviorType.OverTileBlocker,blocksCells=true,allowDiagonal=true});
for(int remaining=4;remaining>0;remaining--){
 check(def.GetSpriteForRemainingHits(remaining).Frame==4-remaining,""stage order intact to scaffold"");
 foreach(ObstacleHitContext context in Enum.GetValues(typeof(ObstacleHitContext)))
  check(Probe.DoesContextMatchRule(context,def.GetDamageRuleForRemainingHits(remaining))==(context==ObstacleHitContext.SpecialActivation),""only special activation can damage every stage"");
 check(!def.IsMovableObstacleForRemainingHits(remaining)&&def.GetBlocksCellsForRemainingHits(remaining),""fixed blocking footprint through final stage"");
}
check(def.GetSpriteForRemainingHits(0)==null,""no sprite after fourth hit"");
var p=new Probe();foreach(int i in new[]{5,6,9,10}){p.level.obstacles[i]=70;p.level.obstacleOrigins[i]=5;}
check(p.CollectCellsForOrigin(5,ObstacleId.RhodesStatue).SequenceEqual(new[]{5,6,9,10}),""all four cells share one collected footprint"");
p.level.obstacles[0]=70;p.level.obstacleOrigins[0]=0;
check(p.CollectCellsForOrigin(5,ObstacleId.RhodesStatue).Length==4,""another statue is not part of the same origin"");
check(def.GetGoalIconSprite().Frame==0,""default goal preview stays intact"");
var resolverProbe=new ResolutionProbe();
foreach(var context in new[]{ObstacleHitContext.NormalMatch,ObstacleHitContext.Booster,ObstacleHitContext.Scripted})
 check(!resolverProbe.ApplyDamage(new ObstacleDamageRequest(0,0,context)).didHit&&resolverProbe.board.ObstacleStateService.remaining==4,""real resolver cannot turn a normal/tool/scripted hit into statue damage"");
check(resolverProbe.ApplyDamage(ObstacleDamageRequest.Special(new Vector2Int(0,0))).didHit&&resolverProbe.board.ObstacleStateService.remaining==3,""special damage still passes real resolver"");
resolverProbe.forced=true;
check(resolverProbe.ApplyDamage(new ObstacleDamageRequest(0,0,ObstacleHitContext.NormalMatch)).didHit,""PatchBot forced special impact remains supported"");
resolverProbe=new ResolutionProbe();resolverProbe.board.ObstacleStateService.id=ObstacleId.Stone;
check(resolverProbe.ApplyDamage(ObstacleDamageRequest.Booster(new Vector2Int(0,0))).didHit,""existing booster fallback remains unchanged for other obstacles"");
Console.WriteLine(""PASS: ""+checks+"" shared Colossus stage/context/footprint checks (production methods, no Unity build)."");
";
await CSharpScript.RunAsync(stubs+production+probe+request+routing+resolver+tests,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
