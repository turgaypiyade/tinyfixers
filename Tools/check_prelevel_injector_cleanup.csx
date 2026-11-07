// Exercise the production idle-wait coroutine's unsubscribe paths; no Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var paths=new[]{"Assets/_Project/Scripts/GamePlay/PreLevelSpecialRuntimeInjector.cs","Assets/_Project/Scripts/GamePlay/StreakBooster/UfoImplantController.cs"};
foreach(var path in paths){var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(path));var errors=tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error);if(errors.Any())throw new Exception(string.Join("\n",errors));}
var root=CSharpSyntaxTree.ParseText(File.ReadAllText(paths[0])).GetRoot();
string methods=string.Join("\n",root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"WaitForReadyBoard","ReleaseReadySubscription","OnDisable","OnDestroy"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToString())).Replace("private IEnumerator WaitForReadyBoard","public IEnumerator WaitForReadyBoard").Replace("private void OnDisable","public void OnDisable").Replace("private void OnDestroy","public void OnDestroy");
var source=@"
using System;using System.Collections;
public static class Time {public static float unscaledDeltaTime=1;}
public sealed class BoardController {public bool IsBusy=true;private Action idle;public int handlers;public event Action OnBecameIdle{add{idle+=value;handlers++;}remove{idle-=value;handlers--;}}public void Idle(){IsBusy=false;idle?.Invoke();}}
public sealed class Injector {public BoardController board=new BoardController();private BoardController readyEventBoard;private Action readyEventHandler;float boardReadySafetyCap=30;private T FindFirstObjectByType<T>()where T:class=>null;
/* METHODS */
}
public static class Check {static int n;static void Assert(bool ok){n++;if(!ok)throw new Exception(""cleanup check ""+n);}
public static void Run(){
var i=new Injector();var e=i.WaitForReadyBoard();Assert(e.MoveNext()&&i.board.handlers==1);((IDisposable)e).Dispose();Assert(i.board.handlers==0);
i=new Injector();e=i.WaitForReadyBoard();e.MoveNext();i.OnDisable();Assert(i.board.handlers==0);((IDisposable)e).Dispose();i.OnDestroy();Assert(i.board.handlers==0);
i=new Injector();e=i.WaitForReadyBoard();e.MoveNext();i.board.Idle();Assert(e.MoveNext()&&i.board.handlers==0);Assert(!e.MoveNext());
i=new Injector();e=i.WaitForReadyBoard();for(int k=0;k<32&&e.MoveNext();k++){}Assert(i.board.handlers==0);
i=new Injector();i.board.IsBusy=false;e=i.WaitForReadyBoard();Assert(!e.MoveNext()&&i.board.handlers==0);
Console.WriteLine(""PASS: ""+n+"" injector cancellation/disable/idle/timeout cleanup checks; two production scripts parsed. No Unity build/run."");}}
Check.Run();
".Replace("/* METHODS */",methods);
await CSharpScript.RunAsync(source,ScriptOptions.Default);
