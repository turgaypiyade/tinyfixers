// Production reset/persistence methods with in-memory prefs and a controllable cloud transport.
// Run: csi Tools/check_progress_reset.csx (no Unity/project build or live Firebase writes).
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
string Read(string file) => File.ReadAllText("Assets/_Project/Scripts/" + file);
string Class(string file, string name) => CSharpSyntaxTree.ParseText(Read(file)).GetRoot().DescendantNodes()
    .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == name).ToFullString();
string Methods(string file, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(Read(file)).GetRoot().DescendantNodes()
    .OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
foreach (var file in new[] { "Core/Backend/CloudSaveManifest.cs", "Core/Backend/FirebaseCloudSaveService.cs",
    "Core/Events/Progress/ProgressEventService.cs", "Core/LevelCatalog.cs", "UI/MainMenuLevelButtonController.cs" })
{
    var errors = CSharpSyntaxTree.ParseText(Read(file)).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    if (errors.Length > 0) throw new Exception(string.Join("\n", errors.Select(e => e.ToString())));
}
var harness = @"
using System;
using System.Collections.Generic;
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);}
public static class Debug {public static void Log(object x){} public static void LogWarning(object x){}}
public static class PlayerPrefs {
 public static readonly Dictionary<string,object> Data=new Dictionary<string,object>();
 public static int Saves;public static void Save(){Saves++;}public static bool HasKey(string k)=>Data.ContainsKey(k);
 public static int GetInt(string k,int d=0)=>Data.TryGetValue(k,out var v)&&v is int i?i:d;
 public static string GetString(string k,string d="""")=>Data.TryGetValue(k,out var v)&&v is string s?s:d;
 public static void SetInt(string k,int v){Data[k]=v;}public static void SetString(string k,string v){Data[k]=v;}
 public static void DeleteKey(string k){Data.Remove(k);}
}
public enum DailySlotRewardType {Lives=1,Joker=10}
public enum ObstacleId {None=0,Hat=5,FutureObstacle=103}
public static class RuntimeSimulationSession {public static bool IsActive;}
public static class FirebaseAuthService {public static bool IsReady=true;}
public class ProgressEventService {public static ProgressEventService Instance=new ProgressEventService();public int Reloads;public void ReloadFromPrefs(){Reloads++;}}
public static class MusicState {public static void ReloadFromPrefs(){}}
public static class PlayerDirectoryService {public static void UpsertIfChanged(){}}
public static class FieldValue {public static readonly object ServerTimestamp=new object();}
public class SetOptions {
 public static readonly SetOptions MergeAll=new SetOptions(); public string[] Fields;
 public static SetOptions MergeFields(params string[] fields)=>new SetOptions{Fields=fields};
}
public class Snapshot {
 public bool Exists;public Dictionary<string,object> Values=new Dictionary<string,object>();
 public bool TryGetValue<T>(string k,out T value){if(Values.TryGetValue(k,out var v)&&v is T t){value=t;return true;}value=default(T);return false;}
}
public class CloudTask {
 public bool IsFaulted,IsCanceled;public Exception Exception;public Snapshot Result;
 private Action<CloudTask> continuation;
 public void ContinueWithOnMainThread(Action<CloudTask> next){continuation=next;}
 public void Complete(bool success=true){IsFaulted=!success;continuation(this);}
}
public class Document {
 public Dictionary<string,object> LastData;public SetOptions LastOptions;
 public CloudTask Fetch,Write;
 public CloudTask GetSnapshotAsync()=>Fetch=new CloudTask();
 public CloudTask SetAsync(Dictionary<string,object> data,SetOptions options){LastData=data;LastOptions=options;return Write=new CloudTask();}
}
" + Class("Core/Backend/CloudSaveManifest.cs","CloudSaveManifest")
  + Class("Core/Backend/CurrencyLedger.cs","CurrencyLedger") + @"
public static class FirebaseCloudSaveService {
 public const int SaveVersion=1;private const float FetchRetryDelay=10;
 public static bool RestoreResolved,dirty,pushInFlight;public static int lastPushedLevel;
 public static bool SkipCloudRestoreInEditor;public static Action OnRestored;
 public static Document SaveDoc=new Document();
 public static void MarkDirty(){dirty=true;}
 public static void Restore(){StartRestore();}
 public class CloudSaveBehaviour {public static CloudSaveBehaviour Instance;public void ScheduleRetry(float delay,Action action){}}
" + Methods("Core/Backend/FirebaseCloudSaveService.cs","TryResetProgress","Push","StartRestore","ReconcileCurrencyFromCloud","ToInt","ClearForceFlag") + @"
}
public static class Checks {
 static int count;
 static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 public static int Run(){
  PlayerPrefs.SetInt(""current_level"",150);PlayerPrefs.SetInt(""player_coins"",500);PlayerPrefs.SetInt(""player_total_stars"",75);
  CurrencyLedger.AdoptLocalAsBase();CurrencyLedger.OpenSyncGate();CurrencyLedger.RecordCoins(25);CurrencyLedger.RecordStars(-5);CurrencyLedger.ReconcileForPush();
  foreach(var k in new[]{""initial_stars_granted"",""first_launch_done"",""prelevel_specials_rewarded"",""booster_hammer_count"",""lives_current"",""player_avatar_id""})PlayerPrefs.SetInt(k,1);
  PlayerPrefs.SetString(""player_id"",""uid-test"");PlayerPrefs.SetString(""player_name"",""Tester"");PlayerPrefs.SetString(""settings_language"",""tr"");
  PlayerPrefs.SetString(""purchase_thanks_receipts_v1"",""paid"");PlayerPrefs.SetString(""music_owned"",""0,1"");
  PlayerPrefs.SetString(""stats_first_launch_ticks"",""100"");PlayerPrefs.SetString(""timed_reward_1"",""99999"");
  foreach(var k in new[]{""level_stars_150"",""level_score_150"",""wonder_stage_12"",""wonder_completed_count"",""wonder_current_stage"",
    ""workshop_chapter_15_stage"",""workshop_chapter_15_reward_claimed"",""bridge_wins"",""safari_pitstop"",""safari_run_seed"",""level_attempt_count"",
    ""stats_current_streak"",""stats_longest_streak"",""tutorial_seen_2"",""combo_tutorial_seen_3"",""obstacle_hint_seen_103"",""boss_duel_howto_seen_v1"",""pending_star_reward""}) PlayerPrefs.SetInt(k,8);
  PlayerPrefs.SetString(""progress_event_v1_goals"",""old goals"");PlayerPrefs.SetString(""stats_weekly_clear_ticks"",""100,200"");
  var before=CloudSaveManifest.Collect();
  PlayerPrefs.SetInt(""current_level"",5);
  Check(CloudSaveManifest.Collect().ContainsKey(""level_stars_150""),""Selecting an earlier level preserves complete level history for reset and save"");
  Check(!FirebaseCloudSaveService.TryResetProgress(200,out var error)&&PlayerPrefs.GetInt(""current_level"")==5,""Reset waits for initial cloud restore without mutation"");
  FirebaseCloudSaveService.RestoreResolved=true;FirebaseCloudSaveService.pushInFlight=true;
  Check(!FirebaseCloudSaveService.TryResetProgress(200,out error)&&PlayerPrefs.GetInt(""wonder_stage_12"")==8,""Reset refuses to race an older in-flight write"");
  FirebaseCloudSaveService.pushInFlight=false;
  Check(FirebaseCloudSaveService.TryResetProgress(200,out error)&&error==null,""Ready reset starts"");
  Check(PlayerPrefs.GetInt(""current_level"")==1&&PlayerPrefs.GetInt(""player_total_score"")==0,""Level and score restart"");
  Check(PlayerPrefs.GetInt(""player_coins"")==525&&PlayerPrefs.GetInt(""player_total_stars"")==70&&CurrencyLedger.CoinsDelta==25&&CurrencyLedger.StarsDelta==-5,""Coins, stars and outstanding currency deltas survive reset"");
  Check(PlayerPrefs.GetInt(""initial_stars_granted"")==1&&PlayerPrefs.GetInt(""first_launch_done"")==1&&PlayerPrefs.GetInt(""prelevel_specials_rewarded"")==1,""Startup grants cannot run again"");
  Check(PlayerPrefs.GetInt(""booster_hammer_count"")==1&&PlayerPrefs.GetInt(""lives_current"")==1&&PlayerPrefs.GetString(""timed_reward_1"")==""99999"",""Inventory, lives and timed rewards survive"");
  Check(PlayerPrefs.GetString(""player_id"")==""uid-test""&&PlayerPrefs.GetString(""settings_language"")==""tr""&&PlayerPrefs.GetString(""purchase_thanks_receipts_v1"")==""paid"",""Identity, settings and purchase receipts survive"");
  Check(!PlayerPrefs.HasKey(""level_stars_150"")&&!PlayerPrefs.HasKey(""level_score_150"")&&!PlayerPrefs.HasKey(""workshop_chapter_15_stage""),""Past level records and workshop tasks clear even after selecting level 5"");
  Check(!PlayerPrefs.HasKey(""wonder_stage_12"")&&!PlayerPrefs.HasKey(""wonder_completed_count"")&&PlayerPrefs.GetInt(""wonder_last_completed"")==-1&&PlayerPrefs.GetInt(""wonder_model_v2"")==2,""Wonder tasks/background reset without resurrecting legacy migration"");
  Check(!PlayerPrefs.HasKey(""progress_event_v1_goals"")&&!PlayerPrefs.HasKey(""bridge_wins"")&&!PlayerPrefs.HasKey(""safari_pitstop""),""Event progress clears"");
  Check(!PlayerPrefs.HasKey(""stats_current_streak"")&&!PlayerPrefs.HasKey(""stats_weekly_clear_ticks"")&&PlayerPrefs.GetString(""stats_first_launch_ticks"")==""100"",""Gameplay stats reset while account creation date survives"");
  Check(!PlayerPrefs.HasKey(""obstacle_hint_seen_103"")&&!PlayerPrefs.HasKey(""boss_duel_howto_seen_v1"")&&!PlayerPrefs.HasKey(""tutorial_seen_2""),""Tutorial flags including IDs beyond 64 reset"");
  Check(ProgressEventService.Instance.Reloads==1&&!PlayerPrefs.HasKey(""pending_star_reward""),""Persistent event cache reloads and stale menu reward animation clears"");
  var doc=FirebaseCloudSaveService.SaveDoc;
  var reset=(Dictionary<string,object>)doc.LastData[""data""];
  long revision=CloudSaveManifest.ProgressResetRevision;
  Check(revision>0&&PlayerPrefs.GetInt(CloudSaveManifest.PendingProgressResetKey)==1&&!reset.ContainsKey(CloudSaveManifest.PendingProgressResetKey),""Reset revision syncs, pending acknowledgement marker stays local"");
  Check(doc.LastOptions.Fields.Length==5&&doc.LastOptions.Fields[0]==""data""&&(bool)doc.LastData[""force""]==false,""Cloud write replaces complete data map, preserves unrelated document fields"");
  Check(!reset.ContainsKey(""progress_event_v1_goals"")&&!reset.ContainsKey(""wonder_stage_12"")&&(long)reset[""player_coins""]==525,""Cloud snapshot drops completed tasks while keeping the wallet"");
  doc.Write.Complete(false);
  Check(PlayerPrefs.GetInt(CloudSaveManifest.PendingProgressResetKey)==1&&FirebaseCloudSaveService.dirty,""Failed upload retains durable reset marker for retry"");
  FirebaseCloudSaveService.Push();doc.Write.Complete();
  Check(!PlayerPrefs.HasKey(CloudSaveManifest.PendingProgressResetKey)&&CurrencyLedger.CoinsDelta==0&&CurrencyLedger.TrustedCoins==525&&CurrencyLedger.TrustedStars==70,""Acknowledged retry clears marker and folds currency exactly once"");
  Check(!CloudSaveManifest.ShouldRestoreProgress(before,150,1,false,false),""Older high-level cloud save cannot undo the reset"");
  Check(!CloudSaveManifest.ShouldRestoreProgress(before,150,1,true,false),""Stale force flag cannot replay a pre-reset save"");
  PlayerPrefs.SetString(CloudSaveManifest.ProgressResetRevisionKey,""0"");PlayerPrefs.SetInt(""current_level"",300);
  PlayerPrefs.SetInt(""wonder_stage_9"",10);PlayerPrefs.SetInt(""level_stars_299"",3);PlayerPrefs.SetString(""progress_event_v1_goals"",""stale"");
  Check(CloudSaveManifest.ShouldRestoreProgress(reset,1,300,false,true),""Newer cloud reset wins against a higher local level, including editor preference"");
  CloudSaveManifest.Apply(reset);
  Check(PlayerPrefs.GetInt(""current_level"")==1&&!PlayerPrefs.HasKey(""wonder_stage_9"")&&!PlayerPrefs.HasKey(""level_stars_299"")&&!PlayerPrefs.HasKey(""progress_event_v1_goals""),""Remote reset clears missing local fields before applying snapshot"");
  Check(CloudSaveManifest.ShouldRestoreProgress(reset,20,1,false,false)&&!CloudSaveManifest.ShouldRestoreProgress(reset,20,1,false,true),""Same reset generation retains existing level/editor conflict policy"");
  CloudSaveManifest.BeginProgressReset(300);
  Check(CloudSaveManifest.ProgressResetRevision>revision,""Repeated reset increments generation"");
  FirebaseCloudSaveService.Restore();doc.Fetch.Result=new Snapshot{Exists=true,Values=new Dictionary<string,object>{{""level"",150L},{""data"",before}}};doc.Fetch.Complete();
  Check(PlayerPrefs.GetInt(""current_level"")==1&&doc.LastOptions.Fields!=null,""Restart before upload completion keeps level 1 and retries snapshot replacement"");
  doc.Write.Complete();
  return count;
 }
}
Checks.Run()";
int count=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.Dictionary<string,object>).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+count+" progress reset, wallet preservation, cloud conflict and retry checks; changed C# syntax valid.");
