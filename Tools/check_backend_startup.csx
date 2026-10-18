#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;
var parse=new CSharpParseOptions(LanguageVersion.Latest,preprocessorSymbols:new[]{"TF_IAP","TF_FIREBASE_ANALYTICS","UNITY_EDITOR"});
Func<string,CompilationUnitSyntax> root=p=>{var node=CSharpSyntaxTree.ParseText(File.ReadAllText(p),parse).GetRoot();return (CompilationUnitSyntax)node.ReplaceTrivia(node.DescendantTrivia(descendIntoTrivia:true).Where(t=>t.IsDirective||t.IsKind(SyntaxKind.DisabledTextTrivia)),(t,r)=>default(SyntaxTrivia));};
var analytics=root("Assets/_Project/Scripts/Core/Backend/GameAnalytics.cs").Members.Single().ToFullString();
var result=root("Assets/_Project/Scripts/Core/LevelAttemptStats.cs").Members.OfType<StructDeclarationSyntax>().Single().ToFullString();
var current=root("Assets/_Project/Scripts/Core/CurrentLevel.cs").Members.Single().ToFullString();
var iap=root("Assets/_Project/Scripts/UI/Shop/StoreIap.cs").Members.OfType<ClassDeclarationSyntax>().Single();
var names=new[]{"Bootstrap","Connect","HandleStoreConnected","HandleProductsFetched"};
var methods=string.Join("\n",iap.Members.OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var harness=@"
using System;using System.Collections.Generic;using System.Linq;using System.Reflection;using System.Threading.Tasks;
public enum RuntimeInitializeLoadType {AfterSceneLoad}
public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
public static class RuntimeSimulationSession {public static bool IsActive;}
public static class GameDiagnostics {public static bool Backend=>false;public static bool AnalyticsEvents;}
public static class Debug {public static bool isDebugBuild;public static readonly List<string> logs=new List<string>();public static void Log(object o){logs.Add(o.ToString());}public static void LogWarning(object o){throw new Exception(""disabled diagnostic evaluated"");}}
public static class PlayerPrefs {public static int level;public static int GetInt(string k,int d)=>level;}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);}
public class Parameter {public string Name;public object Value;public Parameter(string n,long v){Name=n;Value=v;}public Parameter(string n,string v){Name=n;Value=v;}}
public static class FirebaseAnalytics {
 public static string EventName;public static Parameter[] Payload;public static int sends;public static Dictionary<string,string> properties=new Dictionary<string,string>();
 public static void LogEvent(string n,Parameter[] p){EventName=n;Payload=p;sends++;}
 public static void SetUserProperty(string n,string v){properties[n]=v;}public static void SetUserId(string id){}
}
public static class FirebaseAuthService {public static string UserId;public static event Action OnReady;}
public static class LevelAttemptStats {
 public static event Action<int,int> OnAttemptStarted;public static event Action<LevelAttemptResult> OnAttemptEnded;
 public static void Start(int l,int a)=>OnAttemptStarted?.Invoke(l,a);public static void End(LevelAttemptResult r)=>OnAttemptEnded?.Invoke(r);
}
/* RESULT */
/* CURRENT */
/* ANALYTICS */
public class ShopOffer {public string StoreProductId;}
public class ShopCatalog {public static ShopCatalog Shared=new ShopCatalog();public List<ShopOffer> offers=new List<ShopOffer>();public IEnumerable<ShopOffer> RealMoneyOffers()=>offers;}
public enum ProductType {Consumable}
public class ProductDefinition {public string id;public ProductDefinition(string s,ProductType t){id=s;}}
public class Product {}
public class Failure {public string FailureReason,Message,message;}
public class StoreController {
 public event Action OnStoreConnected;public event Action<List<Product>> OnProductsFetched;
 public event Action<Failure> OnProductsFetchFailed,OnStoreDisconnected,OnPurchasesFetchFailed;
 public event Action<object> OnPurchasePending,OnPurchaseConfirmed,OnPurchaseFailed,OnPurchaseDeferred,OnPurchasesFetched;
 public int connects,fetches,purchases;public bool autoConnect=true,callbacks;public List<ProductDefinition> defs;
 public Task Connect(){connects++;callbacks=OnStoreConnected!=null&&OnStoreDisconnected!=null;if(autoConnect)Connected();return Task.CompletedTask;}
 public void Connected()=>OnStoreConnected?.Invoke();public void Disconnected()=>OnStoreDisconnected?.Invoke(new Failure());
 public void FetchProducts(List<ProductDefinition> d){fetches++;defs=d;}
 public void Products()=>OnProductsFetched?.Invoke(new List<Product>());
 public void FetchPurchases(){purchases++;}
}
public static class UnityIAPServices {public static StoreController instance=new StoreController();public static StoreController StoreController()=>instance;}
public static class StoreIap {
 private static StoreController store;private static bool productsReady;private static ShopOffer purchasingOffer;
 private static readonly Dictionary<string,ShopOffer> offersByProductId=new Dictionary<string,ShopOffer>();
 public static event Action OnPricesUpdated;public static bool IsAvailable=>store!=null&&productsReady;
 private static void HandlePurchasePending(object o){}private static void HandlePurchaseConfirmed(object o){}private static void HandlePurchaseFailed(object o){}
 /* IAP METHODS */
}
public static class Checks {
 static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Boot(Type t)=>t.GetMethod(""Bootstrap"",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
 static void Payload(string name,long level){Check(FirebaseAnalytics.EventName==name,""event name"");Check(FirebaseAnalytics.Payload.Count(p=>p.Name==""level"")==1,""one exact lowercase level"");Check(FirebaseAnalytics.Payload.Single(p=>p.Name==""level"").Value is string && (string)FirebaseAnalytics.Payload.Single(p=>p.Name==""level"").Value==level.ToString(System.Globalization.CultureInfo.InvariantCulture),""correct nonempty string level"");}
 public static void Run(){
  RuntimeSimulationSession.IsActive=true;Boot(typeof(GameAnalytics));LevelAttemptStats.Start(1,1);Check(FirebaseAnalytics.sends==0,""simulation does not subscribe or emit"");
  RuntimeSimulationSession.IsActive=false;Boot(typeof(GameAnalytics));PlayerPrefs.level=151;LevelAttemptStats.Start(151,2);Payload(""level_start"",151);
  Check((string)FirebaseAnalytics.Payload.Single(p=>p.Name==""attempt"").Value==""2"",""attempt preserved"");
  PlayerPrefs.level=152;LevelAttemptStats.End(new LevelAttemptResult(151,true,2,1,50,3,0,25));Payload(""level_end"",151);
  Check(FirebaseAnalytics.properties[""current_level""]==""152"",""progress property advances while event keeps played level"");
  Check((string)FirebaseAnalytics.Payload.Single(p=>p.Name==""success"").Value==""1"",""win result preserved"");
  LevelAttemptStats.End(new LevelAttemptResult(152,false,3,2,60,0,1,25));Payload(""level_end"",152);
  Check((string)FirebaseAnalytics.Payload.Single(p=>p.Name==""success"").Value==""0"",""failure result preserved"");
  GameAnalytics.LogEvent(""level_start"");Payload(""level_start"",152);
  GameAnalytics.LogEvent(""level_end"",null);Payload(""level_end"",152);
  GameAnalytics.LogEvent(""level_end"",(""level"",0),(""level"",-1),(""attempt"",4));Payload(""level_end"",152);Check(FirebaseAnalytics.Payload.Length==2,""invalid duplicates collapsed"");
  PlayerPrefs.level=0;GameAnalytics.LogEvent(""level_start"",null);Payload(""level_start"",1);
  GameAnalytics.LogEvent(""other"",(""score"",10));Check(FirebaseAnalytics.Payload.Length==1&&FirebaseAnalytics.Payload[0].Name==""score"",""other event schema unchanged"");

  Check(Debug.logs.Count==0,""disabled payload tracing is silent"");GameDiagnostics.AnalyticsEvents=true;
  LevelAttemptStats.End(new LevelAttemptResult(151,true,2,1,50,3,2,25,4,2,5,900,1,true));Payload(""level_end"",151);
  foreach(var key in new[]{""level"",""attempt"",""success"",""assist_tier"",""fails_before"",""struggles_before"",""struggled"",""measurement_version"",""is_development""})Check(FirebaseAnalytics.Payload.Single(p=>p.Name==key).Value is string,""dimension must be string: ""+key);
  foreach(var key in new[]{""seconds"",""moves_left"",""moves_total"",""continues"",""extra_moves"",""continue_coins"",""ad_continues""})Check(FirebaseAnalytics.Payload.Single(p=>p.Name==key).Value is long,""metric must remain numeric: ""+key);
  Check(Debug.logs.Count==1&&Debug.logs[0].Contains(""level=\""151\"" (String)"")&&Debug.logs[0].Contains(""success=\""1\"" (String)"")&&Debug.logs[0].Contains(""seconds=50 (Int64)"")&&Debug.logs[0].Contains(""Editor stub""),""payload log exposes actual types and editor limitation"");GameDiagnostics.AnalyticsEvents=false;
  int before=FirebaseAnalytics.sends;RuntimeSimulationSession.IsActive=true;LevelAttemptStats.Start(10,1);LevelAttemptStats.End(new LevelAttemptResult(10,false,1,0,5,0,0,20));GameAnalytics.LogEvent(""level_start"");Check(FirebaseAnalytics.sends==before,""all simulation paths suppressed"");
  Boot(typeof(StoreIap));Check(UnityIAPServices.instance.connects==0,""IAP disabled in simulations"");RuntimeSimulationSession.IsActive=false;
  ShopCatalog.Shared.offers.Add(new ShopOffer{StoreProductId=""coins""});ShopCatalog.Shared.offers.Add(new ShopOffer{StoreProductId=""coins""});ShopCatalog.Shared.offers.Add(new ShopOffer{StoreProductId=""bundle""});
  Boot(typeof(StoreIap));var store=UnityIAPServices.instance;Check(store.callbacks,""required callbacks subscribed before Connect"");Check(store.fetches==1&&store.defs.Count==2,""one catalog fetch, duplicate products omitted"");Check(!StoreIap.IsAvailable,""not ready before product results"");
  Boot(typeof(StoreIap));Check(store.connects==1,""bootstrap does not duplicate connection or subscriptions"");store.Products();Check(StoreIap.IsAvailable&&store.purchases==1,""products enable store and fetch pending purchases"");
  store.Disconnected();Check(!StoreIap.IsAvailable,""disconnect invalidates availability"");store.Connected();Check(store.fetches==2&&!StoreIap.IsAvailable,""reconnect refreshes products once"");
  typeof(StoreIap).GetField(""store"",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);UnityIAPServices.instance=new StoreController{autoConnect=false};Boot(typeof(StoreIap));Check(UnityIAPServices.instance.fetches==0,""failed connection cannot fetch products"");
  Console.WriteLine(""PASS: ""+checks+"" analytics payload / IAP startup assertions; production methods, stub SDK transport, no Unity launch or network writes."");
 }
}
Checks.Run();";
harness=harness.Replace("/* RESULT */",result).Replace("/* CURRENT */",current).Replace("/* ANALYTICS */",analytics).Replace("/* IAP METHODS */",methods);
harness=System.Text.RegularExpressions.Regex.Replace(harness,@"^#.*$","",System.Text.RegularExpressions.RegexOptions.Multiline);
var script=CSharpScript.Create(harness,Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(d=>d.ToString())));script.RunAsync().GetAwaiter().GetResult();
