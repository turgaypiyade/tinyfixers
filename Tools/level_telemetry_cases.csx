// Executed by check_mercy_retention.csx with actual production store/projection sources.
RuntimeSimulationSession.IsActive=false;
Debug.isDebugBuild=false;
PlayerPrefs.DeleteKey("level_telemetry_outbox_v2");
set(typeof(LevelTelemetryStore),"state",null);
LevelTelemetryStore.Begin(200,1,0,0,0,24);
LevelTelemetryStore.Struggle();LevelTelemetryStore.Struggle();
LevelTelemetryStore.Continue(5,900,false);
LevelTelemetryStore.Checkpoint(20);LevelTelemetryStore.Checkpoint(20);
LevelTelemetryStore.End(false,0,25);
LevelTelemetryStore.Begin(200,2,1,1,0,24);
LevelTelemetryStore.Checkpoint(15);
// A restart recreates the store from PlayerPrefs and closes the previous board as interrupted.
set(typeof(LevelTelemetryStore),"state",null);
LevelTelemetryStore.RecoverInterrupted();LevelTelemetryStore.RecoverInterrupted();
LevelTelemetryStore.Begin(200,3,1,1,1,24);
LevelTelemetryStore.Continue(5,0,true);
LevelTelemetryStore.End(true,4,30);LevelTelemetryStore.End(true,4,30);
var b=LevelTelemetryStore.Next();
check(b.attempts==3 && b.wins==1 && b.giveUps==1 && b.interruptedAttempts==1,"compacted attempts distinguish give-up from interruption");
check(b.struggles==1 && b.playSeconds==70,"struggle and checkpoint deduplication");
check(b.continues==2 && b.extraMoves==10 && b.coinsSpent==900 && b.adContinues==1,"continue source and actual costs");
check(b.firstWinAttempt==3 && b.winGiveUpsBefore==1 && b.winContinues==1 && b.winAssistTier==1,"first-win context preserved");
check(b.sequence==1 && !b.isDevelopment,"first immutable production payload");
check(LevelTelemetryStore.BindOwner("alice") && !LevelTelemetryStore.BindOwner("bob"),"pending stats cannot be sent to another account");
string payload=JsonUtility.ToJson(b);
LevelTelemetryStore.Begin(201,1,0,0,0,20);
check(JsonUtility.ToJson(LevelTelemetryStore.Next())==payload,"new gameplay cannot change in-flight payload");
set(typeof(LevelTelemetryStore),"state",null);
check(JsonUtility.ToJson(LevelTelemetryStore.Next())==payload,"ambiguous upload survives restart with same sequence and data");
LevelTelemetryStore.Acknowledge(999);
check(LevelTelemetryStore.Next().sequence==1,"wrong ack cannot discard pending data");
var empty=new Dictionary<string,object>();
var projected=LevelTelemetryProjection.Level(b,empty);
check((bool)projected["trackingFromAttemptOne"] && (int)projected["firstWinAttempt"]==3,"eligible cohort and third-attempt win");
check(!(bool)projected["firstWinWithoutContinueOrMercy"],"assisted continuation win is distinguishable");
var summary=LevelTelemetryProjection.Summary(b,empty);
check((long)summary["attemptsStarted"]==3 && (long)summary["levelsGivenUp"]==1,"summary increments agree with level");
// Model server checkpoint transaction: apply once, repeat same frozen payload after lost ack.
long committed=0;
Action<LevelTelemetryBatch> deliver=delta=>{
 if(committed>=delta.sequence)return;
 foreach(var kv in LevelTelemetryProjection.Level(delta,projected))projected[kv.Key]=kv.Value;
 committed=delta.sequence;
};
projected.Clear();deliver(b);deliver(b);
check((long)projected["attempts"]==3 && (long)projected["wins"]==1,"sequence guard prevents duplicate increments");
LevelTelemetryStore.Acknowledge(b.sequence);
check(LevelTelemetryStore.Next().sequence==2 && LevelTelemetryStore.Next().level==201,"ack advances only committed payload");
var replay=new LevelTelemetryBatch{level=200,attempts=1,wins=1,firstObservedAttempt=1,firstWinAttempt=1,winAssistTier=0,maxAssistTier=0};
var replayData=LevelTelemetryProjection.Level(replay,projected);
check(!replayData.ContainsKey("firstWinAttempt") && (long)replayData["maxAssistTier"]==1,"replay cannot rewrite first win or lower maximum assistance");
var legacy=new Dictionary<string,object>{{"attempts",5L},{"wins",1L},{"wonOnAttempt",5L}};
check(!(bool)LevelTelemetryProjection.Level(replay,legacy)["trackingFromAttemptOne"],"legacy data excluded from accurate cohort");
check(!LevelTelemetryProjection.Level(replay,legacy).ContainsKey("firstWinAttempt"),"legacy win is not replaced by replay");
var midLevel=new LevelTelemetryBatch{level=202,attempts=1,firstObservedAttempt=6,highestAttempt=6};
check(!(bool)LevelTelemetryProjection.Level(midLevel,empty)["trackingFromAttemptOne"],"mid-level rollout excluded from denominator");
check(LevelTelemetryProjection.WinBucket(1)=="1" && LevelTelemetryProjection.WinBucket(2)=="2"
 && LevelTelemetryProjection.WinBucket(7)=="6_10" && LevelTelemetryProjection.WinBucket(15)=="11_plus","win buckets");
// Offline repeated attempts coalesce instead of generating one record per attempt.
PlayerPrefs.DeleteKey("level_telemetry_outbox_v2");set(typeof(LevelTelemetryStore),"state",null);
for(int i=1;i<=500;i++){LevelTelemetryStore.Begin(300,i,i-1,i-1,3,20);LevelTelemetryStore.Struggle();LevelTelemetryStore.End(false,0,5);}
var compact=LevelTelemetryStore.Next();
check(compact.attempts==500 && compact.giveUps==500,"500 offline attempts retain exact counters");
check(PlayerPrefs.GetString("level_telemetry_outbox_v2").Length<4000,"offline size is one level summary, not 500 events");
Console.WriteLine("PASS: "+checks+" combined retention/telemetry checks.");
