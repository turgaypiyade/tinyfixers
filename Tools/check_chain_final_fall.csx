// Isolated production-coroutine regression; no Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
var tree=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/Actions/SpecialChainRunner.cs"));
var method=tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="RunGravityWithOverlap");
var finalCall=tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.ValueText=="RunChainVisuals")
 .DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i=>i.Expression.ToString()=="RunGravityWithOverlap"&&i.ArgumentList.Arguments.Any(a=>a.NameColon?.Name.Identifier.ValueText=="waitForCompletionOn"&&a.Expression.ToString()=="sequencer"));
if(!finalCall)throw new Exception("Root chain must await its post-anchor fall with the current sequencer");
var harness=@"
using System;
using System.Collections;
using System.Collections.Generic;
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
public class WaitForSeconds {public WaitForSeconds(float seconds){}}
public class ActionSequencer {}
public class BoardAction {
 public bool released,started,completed;
 public virtual IEnumerator ExecuteVisuals(ActionSequencer s){started=true;while(!released)yield return null;completed=true;}
}
public class FallAction:BoardAction {public float GetEstimatedVisualDuration(BoardController b)=>.3f;}
public class CascadeLogic {
 public List<BoardAction> actions=new List<BoardAction>();
 public List<BoardAction> CalculateCascades()=>actions;
}
public class BoardController {
 public CascadeLogic CascadeLogic=new CascadeLogic();public List<BoardAction> detached=new List<BoardAction>();public int refreshes;
 public void StartImmediateAction(BoardAction a){detached.Add(a);}
 public void RefreshAllSortingOrders(){refreshes++;}
}
public class Chain {
 public BoardController board=new BoardController();float catchOverlap=.5f;public bool nextResolve;
 "+method.ToFullString()+@"
 public IEnumerator Final(){yield return RunGravityWithOverlap(false,new ActionSequencer());nextResolve=true;}
 public IEnumerator Intermediate()=>RunGravityWithOverlap(false);
}
public class Runner {
 Stack<IEnumerator> stack=new Stack<IEnumerator>();public Runner(IEnumerator e){stack.Push(e);}
 public bool Step(){while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();continue;}if(e.Current is IEnumerator child){stack.Push(child);continue;}return true;}return false;}
}
public static class Checks {
 static int count;static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
 public static int Run(){
  var chain=new Chain();var first=new FallAction();var second=new FallAction();chain.board.CascadeLogic.actions.Add(first);chain.board.CascadeLogic.actions.Add(second);
  var runner=new Runner(chain.Final());Check(runner.Step()&&first.started&&!chain.nextResolve,""Final refill keeps caller suspended while falling"");
  Check(chain.board.detached.Count==0,""Post-anchor refill cannot escape into a background job"");
  for(int i=0;i<3;i++)runner.Step();Check(!chain.nextResolve&&!second.started,""No estimate-based early completion or premature next action"");
  first.released=true;Check(runner.Step()&&first.completed&&second.started&&!chain.nextResolve,""All final cascade actions must finish before handoff"");
  second.released=true;Check(!runner.Step()&&second.completed&&chain.nextResolve,""Resolve resumes only after the actual final movement completes"");
  chain=new Chain();chain.board.CascadeLogic.actions.Add(new FallAction());runner=new Runner(chain.Intermediate());
  Check(!runner.Step()&&chain.board.detached.Count==1,""Intermediate chain overlap behavior is preserved"");
  chain=new Chain();runner=new Runner(chain.Final());Check(!runner.Step()&&chain.nextResolve,""Empty final cascade introduces no wait"");
  return count;
 }
}
Checks.Run()";
var result=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" final-fall handoff checks; root final-await call verified");
