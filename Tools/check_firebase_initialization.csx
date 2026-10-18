// Real FirebaseAuthService with completed SDK task doubles; no Unity/build/network calls.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var cls=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Core/Backend/FirebaseAuthService.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
var service=cls.ToFullString().Replace("FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(", "Extensions.ContinueWithOnMainThread(FirebaseApp.CheckAndFixDependenciesAsync(), ").Replace("auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(", "Extensions.ContinueWithOnMainThread(auth.SignInAnonymouslyAsync(), ");
var stub=@"
using System;using System.Threading.Tasks;using System.Reflection;
public enum RuntimeInitializeLoadType {BeforeSceneLoad}
public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
public static class RuntimeSimulationSession {public static bool IsActive;}
public static class GameDiagnostics {public static bool Backend=true;}
public static class Debug {public static int errors;public static void Log(object s){}public static void LogError(object s){errors++;}}
public enum DependencyStatus {Available,UnavailableOther}
public class FirebaseApp {
 public static Task<DependencyStatus> next;public static int checks,creates;public static bool fail;
 public static Task<DependencyStatus> CheckAndFixDependenciesAsync(){checks++;return next;}
 public static FirebaseApp DefaultInstance {get{creates++;if(fail)throw new Exception(""app creation"");return new FirebaseApp();}}
}
public class FirebaseUser {public string UserId=""test-uid"";}
public class Credential {public FirebaseUser User=new FirebaseUser();}
public class FirebaseAuth {
 public static int gets;public FirebaseUser CurrentUser;
 public static FirebaseAuth GetAuth(FirebaseApp app){if(app==null)throw new Exception(""missing explicit app"");gets++;return new FirebaseAuth();}
 public Task<Credential> SignInAnonymouslyAsync()=>Task.FromResult(new Credential());
}
public static class Extensions {public static void ContinueWithOnMainThread<T>(Task<T> task,Action<Task<T>> action){action(task);}}
";
var tests=@"
int count=0;Action<bool,string> check=(ok,why)=>{count++;if(!ok)throw new Exception(why);};
Action reset=()=>{
 foreach(string n in new[]{""app"",""auth""})typeof(FirebaseAuthService).GetField(n,BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);
 typeof(FirebaseAuthService).GetField(""initStarted"",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);
 typeof(FirebaseAuthService).GetProperty(""IsReady"").SetValue(null,false);
 FirebaseApp.checks=FirebaseApp.creates=FirebaseAuth.gets=0;FirebaseApp.fail=false;
};
reset();RuntimeSimulationSession.IsActive=true;FirebaseAuthService.Initialize();check(FirebaseApp.checks==0,""simulation never initializes SDK"");RuntimeSimulationSession.IsActive=false;
foreach(string mode in new[]{""cancel"",""fault"",""unavailable"",""appfail"",""success""}){
 reset();
 if(mode==""cancel""){var tcs=new TaskCompletionSource<DependencyStatus>();tcs.SetCanceled();FirebaseApp.next=tcs.Task;}
 else if(mode==""fault"")FirebaseApp.next=Task.FromException<DependencyStatus>(new Exception(""dependency failure""));
 else FirebaseApp.next=Task.FromResult(mode==""unavailable""?DependencyStatus.UnavailableOther:DependencyStatus.Available);
 FirebaseApp.fail=mode==""appfail"";FirebaseAuthService.Initialize();
 if(mode==""success""){
  check(FirebaseAuthService.IsReady&&FirebaseApp.creates==1&&FirebaseAuth.gets==1&&FirebaseAuthService.UserId==""test-uid"",""default app precedes auth and persists readiness"");
  FirebaseAuthService.Initialize();check(FirebaseApp.checks==1,""duplicate initialization is suppressed"");
 }else{
  check(!FirebaseAuthService.IsReady&&FirebaseAuth.gets==0,""failure does not enter auth: ""+mode);
  FirebaseApp.next=Task.FromResult(DependencyStatus.Available);FirebaseApp.fail=false;FirebaseAuthService.Initialize();
  check(FirebaseAuthService.IsReady&&FirebaseApp.checks==2,""failed setup can be retried: ""+mode);
 }
}
check(Debug.errors==4,""all failure diagnostics safely read errors without faulted task.Result"");
Console.WriteLine(""PASS: ""+count+"" production Firebase setup/fault/cancellation/retry checks. Native iOS SDK behavior not simulated."");
";
await CSharpScript.RunAsync(stub+service+tests);
