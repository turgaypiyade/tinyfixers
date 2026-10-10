// Run: csi Tools/check_mercy_retention.csx (standalone logic checks, no Unity build).
#r "System.Web.Extensions"
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
 "Assets/_Project/Scripts/Core/Backend/LevelTelemetryStore.cs",
 "Assets/_Project/Scripts/Core/Backend/LevelTelemetryProjection.cs",
 "Assets/_Project/Scripts/Core/LevelAttemptStats.cs",
 "Assets/_Project/Scripts/Core/LevelAssist.cs",
 "Assets/_Project/Scripts/Core/LivesManager.cs",
 "Assets/_Project/Scripts/UI/Shop/ShopPurchaseService.cs",
 "Assets/_Project/Scripts/Core/Backend/CloudSaveManifest.cs"
};
var production = string.Join("\n", paths.Select(p => {
 var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetRoot();
 return string.Join("\n", root.Members.Select(m => m.ToFullString())).Replace("UnityEngine.Object", "ObjectStub");
}));
var stubs = @"
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
public static class Application {public static string version=""test"", platform=""Editor"";}
public static class JsonUtility {
 public static string ToJson(object v)=>new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(v);
 public static T FromJson<T>(string json)=>new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<T>(json);
}
public static class PlayerPrefs {
 public static Dictionary<string, object> Data = new Dictionary<string,object>();
 public static int Saves;
 public static int GetInt(string k,int d=0)=>Data.ContainsKey(k)?(int)Data[k]:d;
 public static string GetString(string k,string d="""")=>Data.ContainsKey(k)?(string)Data[k]:d;
 public static bool HasKey(string k)=>Data.ContainsKey(k);
 public static void SetInt(string k,int v)=>Data[k]=v;
 public static void SetString(string k,string v)=>Data[k]=v;
 public static void DeleteKey(string k)=>Data.Remove(k);
 public static void Save(){Saves++;}
}
public static class Mathf {
 public static int Max(int a,int b)=>Math.Max(a,b);
 public static int Min(int a,int b)=>Math.Min(a,b);
 public static float Min(float a,float b)=>Math.Min(a,b);
 public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(b,v));
 public static int RoundToInt(float v)=>(int)Math.Round(v);
}
public class MonoBehaviour {}
public class GameObject { public GameObject(string s){} public T AddComponent<T>()=>default(T); }
public static class ObjectStub {public static void DontDestroyOnLoad(object o){} }
public static class Time {public static float unscaledDeltaTime;}
public static class RuntimeSimulationSession {public static bool IsActive;}
public static class CurrentLevel {public static int Global=126;}
public static class Debug {public static bool isDebugBuild;public static void Log(object s){} }
public enum DailySlotRewardType { Lives }
public static class TimedRewardService {public static bool Free;public static bool IsLivesFree()=>Free;}
public class ShopOffer {
 public enum PriceType {Coins, Stars, Free, RealMoney}
 public PriceType priceType;public int priceAmount;public string displayName=""test"";
}
public static class PlayerWallet {public static bool SpendCoins(int n)=>true;public static bool SpendStars(int n)=>true;}
public static class ShopState {public static void RecordPurchase(ShopOffer o){} }
public static class ShopRewardGranter {public static int Grants;public static void Grant(ShopOffer o){Grants++;} }
";
var tests = @"
int checks=0;
Action<bool,string> check=(ok,name)=>{checks++;if(!ok)throw new Exception(name);};
Action<Type,string,object> set=(type,field,v)=>type.GetField(field,BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,v);
LevelAttemptStats.BeginAttempt(126,20);
check(LevelAttemptStats.StrugglesOn(126)==0,""fresh attempt"");
LevelAttemptStats.RecordStruggle(125);
check(LevelAttemptStats.StrugglesOn(126)==0,""wrong level ignored"");
LevelAttemptStats.RecordStruggle(126);
int saved=PlayerPrefs.Saves;
LevelAttemptStats.RecordStruggle(126);
check(LevelAttemptStats.StrugglesOn(126)==1 && PlayerPrefs.Saves==saved,""continued fail counts once"");
check(LevelAttemptStats.ConsecutiveFails(126)==0 && LevelAttemptStats.HasActiveAttempt,""struggle does not end attempt or mark give-up"");
// Model process restart: persisted prefs survive, runtime attempt fields do not.
set(typeof(LevelAttemptStats),""s_activeLevel"",-1);
set(typeof(LevelAttemptStats),""s_struggleRecorded"",false);
check(LevelAttemptStats.StrugglesOn(126)==1,""fail-screen app exit preserves effort"");
LevelAttemptStats.BeginAttempt(126,20);
LevelAttemptStats.RecordGiveUp(126);
check(LevelAttemptStats.StrugglesOn(126)==1 && LevelAttemptStats.ConsecutiveFails(126)==1,""early abandon is not farmable mercy"");
for(int i=0;i<9;i++){
 LevelAttemptStats.BeginAttempt(126,20);LevelAttemptStats.RecordStruggle(126);LevelAttemptStats.RecordGiveUp(126);
}
LevelAttemptStats.BeginAttempt(126,20);
check(LevelAssist.RefillBias==0.45f,""returning after ten losses keeps capped assistance"");
LevelAttemptStats.RecordStruggle(126);
check(LevelAssist.RefillBias==0.45f,""current attempt tier stays fixed"");
LevelAttemptStats.RecordWin(126);
check(LevelAttemptStats.StrugglesOn(126)==0 && LevelAttemptStats.ConsecutiveFails(126)==0,""continued win clears mercy and fails"");
check((long)CloudSaveManifest.Collect()[""level_attempt_struggles""]==0,""win sends explicit cloud reset"");
PlayerPrefs.SetInt(""level_attempt_level"",126);PlayerPrefs.SetInt(""level_attempt_fails"",5);
PlayerPrefs.DeleteKey(""level_attempt_struggles"");
check(LevelAssist.TierFor(126)==2,""old save retains help"");
LevelAttemptStats.BeginAttempt(126,20);LevelAttemptStats.RecordGiveUp(126);
check(LevelAttemptStats.StrugglesOn(126)==5,""migration freezes old fails independently"");
LevelAttemptStats.BeginAttempt(127,20);
check(LevelAttemptStats.StrugglesOn(127)==0,""new level starts clean"");
RuntimeSimulationSession.IsActive=true;
LevelAttemptStats.RecordStruggle(127);
check(LevelAttemptStats.StrugglesOn(127)==0 && LevelAssist.RefillBias==0,""simulation cannot affect mercy"");
RuntimeSimulationSession.IsActive=false;

PlayerPrefs.SetInt(""lives_current"",9);
check(LivesManager.GrantPurchaseThanks(""store:txn-1"") && LivesManager.Current==10,""gift adds one life"");
check(!LivesManager.GrantPurchaseThanks(""store:txn-1"") && LivesManager.PendingPurchaseThanks==0,""duplicate receipt ignored"");
check(!LivesManager.GrantPurchaseThanks("" ""),""missing transaction rejected"");
check(LivesManager.GrantPurchaseThanks(""store:txn-2"") && LivesManager.Current==10 && LivesManager.PendingPurchaseThanks==1,""full lives preserves pending gift"");
set(typeof(LivesManager),""_initialized"",false);
check(!LivesManager.GrantPurchaseThanks(""store:txn-2"") && LivesManager.PendingPurchaseThanks==1,""restarted app does not duplicate gift"");
LivesManager.SpendLife();
check(LivesManager.Current==9 && LivesManager.PendingPurchaseThanks==1,""fail life debit unaffected by pending gift"");
LivesManager.AddLives(1);
check(LivesManager.Current==10 && LivesManager.PendingPurchaseThanks==1,""continue refund does not consume gift"");
LivesManager.SpendLife();LivesManager.ClaimPendingPurchaseThanks();
check(LivesManager.Current==10 && LivesManager.PendingPurchaseThanks==0,""next attempt claims gift within life cap"");
var snapshot=CloudSaveManifest.Collect();
check(snapshot.ContainsKey(""purchase_thanks_receipts_v1"") && snapshot.ContainsKey(""purchase_thanks_pending_lives""),""gift ledger and pending balance in cloud manifest"");
int receipts=0,purchases=0;bool thanks=false;
ShopPurchaseService.OnReceipt+=(offer,gift)=>{receipts++;thanks=gift;};
ShopPurchaseService.OnPurchased+=offer=>purchases++;
var real=new ShopOffer{priceType=ShopOffer.PriceType.RealMoney};
check(!ShopPurchaseService.TryPurchase(real) && receipts==0 && ShopRewardGranter.Grants==0,""production has no unverified real-money grant"");
check(ShopPurchaseService.NotifyVerifiedPurchaseFulfilled(real,""store:txn-3"") && thanks && receipts==1 && purchases==1,""verified fulfilment notifies receipt with thank-you"");
check(ShopRewardGranter.Grants==0,""post-fulfilment hook never grants main bundle twice"");
check(!ShopPurchaseService.NotifyVerifiedPurchaseFulfilled(real,""store:txn-3"") && receipts==1,""repeated verified callback has no gift or celebration"");
check(!ShopPurchaseService.NotifyVerifiedPurchaseFulfilled(new ShopOffer{priceType=ShopOffer.PriceType.Free},""store:txn-4""),""free offers cannot request paid thanks"");
ShopPurchaseService.TryPurchase(new ShopOffer{priceType=ShopOffer.PriceType.Coins});
check(!thanks && ShopRewardGranter.Grants==1,""coin purchase has no paid gift"");
Debug.isDebugBuild=true;
ShopPurchaseService.TryPurchase(real);
check(thanks && ShopRewardGranter.Grants==2,""dev simulation can preview gift"");
RuntimeSimulationSession.IsActive=true;
int pending=LivesManager.PendingPurchaseThanks;
check(!ShopPurchaseService.NotifyVerifiedPurchaseFulfilled(real,""store:sim-ignored"") && LivesManager.PendingPurchaseThanks==pending,""simulation cannot grant thanks"");
Console.WriteLine(""PASS: ""+checks+"" retention checks (production logic with Unity service stubs; no project build)."");
";
await CSharpScript.RunAsync(stubs+production+tests+File.ReadAllText("Tools/level_telemetry_cases.csx"),
 ScriptOptions.Default.AddReferences(typeof(System.Web.Script.Serialization.JavaScriptSerializer).Assembly));
foreach(var path in new[]{"Assets/_Project/Scripts/Core/Backend/GameAnalytics.cs", "Assets/_Project/Scripts/UI/Shop/ShopPurchaseFeedback.cs", "Assets/_Project/Scripts/UI/LevelEndSimplePopupController.cs", "Assets/_Project/Scripts/Grid/GridSpawner.cs"}) {
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0) throw new Exception(path+": "+string.Join(";",errors.Select(e=>e.ToString())));
}
Console.WriteLine("PASS: modified UI/spawner syntax checks.");
