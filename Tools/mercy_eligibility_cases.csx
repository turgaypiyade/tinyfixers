// Runs against production mercy, wallet and inventory sources in check_mercy_retention.csx.
Action resetMercy=()=>{
 PlayerPrefs.Data.Clear();
 set(typeof(LevelAttemptStats),"s_activeLevel",-1);
 set(typeof(LevelAttemptStats),"s_struggleRecorded",false);
 set(typeof(LivesManager),"_initialized",false);
 set(typeof(LevelAssist),"foreground",false);
 set(typeof(LevelAssist),"attemptEligible",false);
 set(typeof(LevelAssist),"lastCheckpoint",0L);
 set(typeof(LevelTelemetryStore),"state",null);
 CurrentLevel.Global=126;RuntimeSimulationSession.IsActive=false;
 TimedRewardService.Free=false;Debug.isDebugBuild=false;
};
Action loseLife=()=>{
 LevelAttemptStats.BeginAttempt(126,20);LivesManager.AddLives(1);
 check(LivesManager.SpendLife(),"actual debit succeeds");
 LevelAttemptStats.RecordGiveUp(126);
};
var returnTime=new DateTime(2026,10,16,12,0,0,DateTimeKind.Utc);
Action<TimeSpan> returnAfter=gap=>{
 PlayerPrefs.SetString("mercy_last_active_utc_ticks",(returnTime-gap).Ticks.ToString());
 LevelAssist.ResumeActivity(returnTime);
};
resetMercy();
for(int i=0;i<16;i++)loseLife();
check(LevelAssist.TierFor(126)==0,"non monetized losses alone never enable mercy");
foreach(bool payer in new[]{false,true}){
 resetMercy();
 if(payer)LevelAssist.RecordPurchaseCompleted();else LevelAssist.RecordRewardedAdCompleted();
 for(int i=0;i<15;i++){
  check(LevelAssist.TierFor(126)==(i>=12?2:i>=9?1:0),"9/12/15 actual-life tier boundary");
  loseLife();
 }
 LevelAttemptStats.BeginAttempt(126,20);
 check(LevelAssist.RefillBias==0.45f,"fifteen debits reach capped tier");
 int reads=PlayerPrefs.Reads;
 for(int i=0;i<1000;i++)check(LevelAssist.RefillBias==0.45f,"cached refill tier");
 check(PlayerPrefs.Reads==reads,"active refill hot path does not read preferences");
 LivesManager.AddLives(1);LivesManager.SpendLife();LivesManager.RefundFailureLife();
 check(LevelAttemptStats.LivesSpentOn(126)==15,"continue refund cancels the same failure debit");
 LevelAttemptStats.RecordWin(126);
 check(LevelAttemptStats.LivesSpentOn(126)==0&&LevelAssist.TierFor(126)==0,"win resets debit eligibility");
}
resetMercy();LevelAssist.RecordRewardedAdCompleted();LevelAttemptStats.BeginAttempt(126,20);
TimedRewardService.Free=true;
check(LivesManager.SpendLife()&&LevelAttemptStats.LivesSpentOn(126)==0,"free lives do not count");
TimedRewardService.Free=false;
for(int i=0;i<10;i++)LivesManager.SpendLife();
int spentLives=LevelAttemptStats.LivesSpentOn(126);
check(!LivesManager.SpendLife()&&LevelAttemptStats.LivesSpentOn(126)==spentLives,"empty balance cannot add effort");
resetMercy();returnAfter(TimeSpan.FromDays(3)-TimeSpan.FromTicks(1));LevelAttemptStats.BeginAttempt(126,20);
check(LevelAssist.RefillBias==0&&!WelcomeBackReward.HasPending,"less than 72 hours gives neither help nor gift");
resetMercy();LevelAssist.ResumeActivity(returnTime);LevelAttemptStats.BeginAttempt(126,20);
check(LevelAssist.RefillBias==0&&!WelcomeBackReward.HasPending,"first install is not a returning player");
resetMercy();returnAfter(TimeSpan.FromDays(3));
check(WelcomeBackReward.HasPending,"exactly 72 hours queues gift");
CurrentLevel.Global=140;LevelAttemptStats.BeginAttempt(140,20);
check(LevelAssist.RefillBias==0.15f,"return assistance binds to restored level");
LevelAttemptStats.RecordGiveUp(140);LevelAttemptStats.BeginAttempt(140,20);
check(LevelAssist.RefillBias==0.15f,"return help persists across retries");
check(WelcomeBackReward.GrantPending(),"return gift delivered");
check(PlayerWallet.Coins==500&&BoosterInventory.GetCount(BoardController.BoosterMode.Single)==1
 &&BoosterInventory.GetCount(BoardController.BoosterMode.Shuffle)==1
 &&PreLevelSpecialInventory.GetCount(TileSpecial.LineH)==11
 &&PreLevelSpecialInventory.GetCount(TileSpecial.PulseCore)==11,"five-part bundle adds to existing inventory defaults");
check(!WelcomeBackReward.GrantPending()&&PlayerWallet.Coins==500,"receipt prevents repeat delivery");
LevelAssist.SuspendActivity(returnTime);LevelAssist.ResumeActivity(returnTime.AddHours(1));
check(!WelcomeBackReward.HasPending,"short reopen does not queue a second gift");
LevelAssist.SuspendActivity(returnTime.AddHours(1));LevelAssist.ResumeActivity(returnTime.AddDays(4));
check(WelcomeBackReward.HasPending&&WelcomeBackReward.GrantPending()&&PlayerWallet.Coins==1000,"a later qualifying absence earns another gift");
LevelAttemptStats.RecordWin(140);LevelAttemptStats.BeginAttempt(141,20);
check(LevelAssist.RefillBias==0,"return assistance ends at win");
resetMercy();returnAfter(TimeSpan.FromDays(3));LevelAttemptStats.BeginAttempt(126,20);
LevelAssist.RecordRewardedAdCompleted();
check(LevelAssist.RefillBias==0,"earning first ad while on return help enforces nine-life condition");
resetMercy();LevelAssist.RecordPurchaseCompleted();returnAfter(TimeSpan.FromDays(5));LevelAttemptStats.BeginAttempt(126,20);
check(LevelAssist.RefillBias==0&&WelcomeBackReward.HasPending,"returning payer still needs nine lives but receives gift");
resetMercy();PlayerPrefs.SetString("iap_processed_transactions","legacy-transaction");
check(LevelAssist.HasAdOrPurchase,"legacy completed store transactions recognized");
resetMercy();PlayerPrefs.SetInt("ad_continue_used_level",125);
check(LevelAssist.HasAdOrPurchase,"legacy rewarded continue recognized");
resetMercy();PlayerPrefs.SetString("purchase_thanks_receipts_v1","dev-simulation");
check(!LevelAssist.HasAdOrPurchase,"dev thank-you receipt is not proof of payment");
resetMercy();LevelAssist.RecordRewardedAdCompleted();
CloudSaveManifest.Apply(new Dictionary<string,object>{{"mercy_has_rewarded_ad",0L}});
check(LevelAssist.HasAdOrPurchase,"stale cloud cannot forget completed monetization");
LevelAttemptStats.BeginAttempt(126,20);LivesManager.SpendLife();
var mercyCloud=CloudSaveManifest.Collect();
check(mercyCloud.ContainsKey("mercy_has_rewarded_ad")&&mercyCloud.ContainsKey("level_attempt_lives_spent")
 &&!mercyCloud.ContainsKey("mercy_last_active_utc_ticks")&&!mercyCloud.ContainsKey("welcome_back_reward_pending_ticks"),"cloud includes effort/evidence, excludes device clock/token");
resetMercy();RuntimeSimulationSession.IsActive=true;
LevelAssist.ResumeActivity(returnTime);WelcomeBackReward.Queue(returnTime.Ticks);
LevelAssist.RecordPurchaseCompleted();
check(PlayerPrefs.Data.Count==0&&!WelcomeBackReward.GrantPending(),"simulation cannot queue or grant retention rewards");
resetMercy();LevelAssist.ResumeActivity(returnTime);LevelAssist.TickActivity(returnTime.AddDays(4));
LevelAssist.SuspendActivity(returnTime.AddDays(4));LevelAssist.ResumeActivity(returnTime.AddDays(4).AddMinutes(1));
check(!WelcomeBackReward.HasPending,"long foreground session does not become false inactivity");
Console.WriteLine("PASS: "+checks+" retention/mercy/reward checks (including 2,000 cached refill reads).");
