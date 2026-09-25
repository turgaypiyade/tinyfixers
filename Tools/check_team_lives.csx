// csi Tools/check_team_lives.csx -- isolated production logic, no Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#r "System.Web.Extensions"
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
string Read(string path)=>File.ReadAllText("Assets/_Project/Scripts/"+path);
var files=new[]{"Core/LivesManager.cs","Core/LivesTimerService.cs","Core/Teams/TeamLifeInbox.cs","Core/Teams/SimTeamService.cs","Core/Backend/BackendServices.cs","Core/Backend/FirebaseTeamService.cs","Core/Backend/CloudSaveManifest.cs","UI/Team/TeamModels.cs","UI/Team/MockTeamService.cs","UI/Team/TeamScreenController.cs","UI/Team/TeamChatRow.cs","UI/MainMenuLivesDisplay.cs"};
foreach(var f in files){var errors=CSharpSyntaxTree.ParseText(Read(f),new CSharpParseOptions(LanguageVersion.Preview)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(f+": "+string.Join("\n",errors.Select(e=>e.ToString())));}
string Clean(string s)=>s.Replace("using System;", "").Replace("using UnityEngine;", "").Replace("using System.Collections.Generic;", "").Replace("UnityEngine.Random.Range", "Random.Range");
var harness=@"
using System;
using System.Collections.Generic;
using System.Reflection;
public static class Mathf {
 public static int Min(int a,int b)=>Math.Min(a,b);public static int Max(int a,int b)=>Math.Max(a,b);
 public static int Clamp(int n,int a,int b)=>Math.Min(b,Math.Max(a,n));
}
public static class Random { public static int Range(int a,int b)=>a; }
public static class PlayerPrefs {
 static Dictionary<string,int> ints=new Dictionary<string,int>();static Dictionary<string,string> strings=new Dictionary<string,string>();
 public static int GetInt(string k,int d=0)=>ints.TryGetValue(k,out var n)?n:d;public static void SetInt(string k,int n){ints[k]=n;}
 public static string GetString(string k,string d="""")=>strings.TryGetValue(k,out var s)?s:d;public static void SetString(string k,string s){strings[k]=s;}
 public static void Save(){} public static void Clear(){ints.Clear();strings.Clear();}
}
public static class TimedRewardService {public static bool IsLivesFree()=>false;}
public static class JsonUtility {
 public static T FromJson<T>(string s){if(string.IsNullOrEmpty(s))return default(T);return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<T>(s);}
 public static string ToJson(object s)=>new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(s);
}
"+Clean(Read("Core/LivesManager.cs"))+Clean(Read("Core/Teams/TeamLifeInbox.cs"))+@"
public static class Checks {
 static int count;
 static void Check(bool ok,string msg){count++;if(!ok)throw new Exception(msg);}
 static void Reset(int lives){PlayerPrefs.Clear();PlayerPrefs.SetInt(""lives_current"",lives);typeof(LivesManager).GetField(""_initialized"",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);LivesManager.RegenCapLives=5;LivesManager.Initialize();}
 public static int Run(){
  Reset(25);Check(LivesManager.Current==10&&PlayerPrefs.GetInt(""lives_current"")==10,""Existing over-cap saves migrate to 10"");
  LivesManager.AddLives(int.MaxValue);Check(LivesManager.Current==10,""Large reward cannot overflow full lives"");
  LivesManager.SpendLife();LivesManager.AddLives(5);Check(LivesManager.Current==10,""9 plus 5 caps at 10"");
  LivesManager.AddLives(-5);Check(LivesManager.Current==10,""Negative reward is ignored"");
  LivesManager.RegenCapLives=100;Check(LivesManager.RegenCapLives==10&&!LivesManager.TickRegen(),""Regen cannot bypass maximum"");
  Reset(0);var now=DateTime.UtcNow.AddSeconds(1);int changes=0;
  var inbox=new TeamLifeInbox(""team-a"",()=>changes++,(a,b)=>a);inbox.SetBots(4,()=>""Bot A"");
  Check(inbox.Request(now)&&inbox.IsWaiting&&!inbox.CanRequest,""Request creates one pending request"");
  Check(!inbox.Request(now),""Duplicate requests are rejected"");
  inbox.Tick(now.AddSeconds(29));Check(inbox.Pending==0&&LivesManager.Current==0,""No instant bot gifts"");
  inbox.Tick(now.AddSeconds(30));Check(inbox.Pending==1&&LivesManager.Current==0&&inbox.Replies[0].sender==""Bot A"",""Bot response is a named message awaiting acceptance"");
  string firstId=inbox.Replies[0].id;
  int events=changes;inbox.Tick(now.AddSeconds(30));Check(changes==events&&inbox.Pending==1,""Repeated tick cannot duplicate a reply"");
  int recursive=0;Action listener=()=>recursive+=inbox.Accept(firstId);LivesManager.OnLivesChanged+=listener;
  Check(inbox.Accept(firstId)==1&&recursive==0&&LivesManager.Current==1,""Per-message acceptance is safe against wallet event reentry"");LivesManager.OnLivesChanged-=listener;
  Check(inbox.Accept(firstId)==0&&inbox.Replies[0].accepted,""Claimed reply remains visible but cannot pay twice"");
  Check(inbox.Accept(""unknown-id"")==0,""An unknown message cannot grant lives"");
  inbox.Tick(now.AddMinutes(3));Check(inbox.Pending==4&&!inbox.IsWaiting&&inbox.Replies.Count==5,""Request finishes in five separate bot replies"");
  inbox.Tick(now.AddDays(30));Check(inbox.Pending==4&&inbox.Replies.Count==5,""Completed requests never start spontaneous donations"");
  var restored=new TeamLifeInbox(""team-a"",null,(a,b)=>a);Check(restored.Pending==4&&restored.Replies[0].accepted,""Reply history and acceptance survive service recreation"");
  Check(restored.Accept(firstId)==0,""Claim cannot replay after reload"");
  LivesManager.AddLives(8);Check(LivesManager.Current==9,""Precondition: reward arrives before acceptance"");
  string secondId=restored.Replies[1].id;
  Check(restored.Accept(secondId)==1&&LivesManager.Current==10&&restored.Pending==3,""Accepting one message grants exactly one life"");
  string thirdId=restored.Replies[2].id;
  Check(!restored.CanRequest&&!restored.Request(now)&&restored.Accept(thirdId)==0&&!restored.Replies[2].accepted,""10 lives blocks requests and acceptance without consuming the gift"");
  LivesManager.SpendLife();Check(restored.Accept(thirdId)==1&&LivesManager.Current==10&&restored.Pending==2,""A saved message can be accepted after spending"");
  Reset(5);now=DateTime.UtcNow.AddSeconds(1);var requestedOnly=new TeamLifeInbox(""team-b"",null,(a,b)=>a);requestedOnly.SetBots(2,()=>""Bot B"");
  requestedOnly.Tick(now.AddDays(30));Check(requestedOnly.Pending==0&&requestedOnly.Replies.Count==0,""Bots never donate before the player requests"");
  requestedOnly.Request(now);requestedOnly.Tick(now.AddDays(30));Check(requestedOnly.Pending==5,""Offline catch-up is bounded to requested replies"");
  foreach(var reply in requestedOnly.Replies)requestedOnly.Accept(reply.id);
  Check(LivesManager.Current==10&&requestedOnly.Pending==0,""Five individually claimed replies cannot exceed maximum"");
  requestedOnly.Tick(now.AddDays(31));Check(requestedOnly.Replies.Count==5,""No further messages after the request completes"");
  Reset(1);now=DateTime.UtcNow.AddSeconds(1);var humans=new TeamLifeInbox(""no-bots"",null,(a,b)=>a);humans.SetBots(0,null);humans.Request(now);humans.Tick(now.AddDays(2));Check(humans.Pending==0,""Teams without bots do not invent bot replies"");
  var nearFull=new TeamLifeInbox(""team-c"",null,(a,b)=>a);nearFull.SetBots(2,()=>""Bot C"");LivesManager.AddLives(8);nearFull.Request(now);nearFull.Tick(now.AddSeconds(31));Check(nearFull.Pending==1&&!nearFull.IsWaiting,""Request at 9 lives asks only for the free slot"");
  var different=new TeamLifeInbox(""team-d"",null,(a,b)=>a);Check(different.Pending==0&&different.Replies.Count==0,""Replies do not transfer to another team"");
  Reset(0);var legacy=new Dictionary<string,object>{{""teamId"",""legacy""},{""pending"",3},{""sender"",""Old Bot""},{""requested"",0},{""nextGiftTicks"",DateTime.UtcNow.Ticks}};
  PlayerPrefs.SetString(""team_life_inbox_v1"",JsonUtility.ToJson(legacy));
  var migrated=new TeamLifeInbox(""legacy"",null,(a,b)=>a);Check(migrated.Pending==3&&migrated.Replies[0].sender==""Old Bot"",""Old pending gifts migrate to individually claimable messages"");
  var migratedAgain=new TeamLifeInbox(""legacy"",null,(a,b)=>a);Check(migratedAgain.Pending==3,""Legacy gifts migrate only once"");
  migratedAgain.SetBots(2,()=>""Bot"");migratedAgain.Tick(DateTime.UtcNow.AddDays(2));Check(migratedAgain.Pending==3,""Legacy automatic gift timer cannot create unsolicited replies"");
  Reset(10);now=DateTime.UtcNow.AddSeconds(1);int botChanges=0;string botName=""Bot One"";
  var asks=new TeamLifeInbox(""bot-requests"",()=>botChanges++,(a,b)=>a);asks.SetBots(5,()=>botName);
  asks.Tick(now.AddSeconds(30));Check(asks.BotRequests.Count==0,""Bot requests are delayed"");
  asks.Tick(now.AddSeconds(61));Check(asks.BotRequests.Count==1&&botChanges==1&&asks.BotRequests[0].sender==botName,""Bot asks for a life without a player request, even at full wallet"");
  asks.Tick(now.AddSeconds(61));Check(asks.BotRequests.Count==1&&botChanges==1,""Repeated tick does not duplicate bot requests"");
  asks.Tick(now.AddSeconds(242));Check(asks.BotRequests.Count==1,""A bot cannot post a second unanswered request"");
  string askId=asks.BotRequests[0].id;
  Check(asks.HelpBot(askId)&&asks.BotRequests[0].helped&&LivesManager.Current==10,""Help completes the bot request without spending a wallet life"");
  Check(!asks.HelpBot(askId)&&!asks.HelpBot(""missing""),""Repeated and unknown bot help are ignored"");
  var asksRestored=new TeamLifeInbox(""bot-requests"",null,(a,b)=>a);asksRestored.SetBots(5,()=>botName);
  Check(asksRestored.BotRequests[0].helped&&!asksRestored.HelpBot(askId),""Helped state survives reopening"");
  asksRestored.Tick(now.AddSeconds(423));Check(asksRestored.BotRequests.Count==2,""A helped bot can ask again after the cooldown"");
  botName=""Bot Two"";asksRestored.Tick(now.AddSeconds(604));botName=""Bot Three"";asksRestored.Tick(now.AddSeconds(785));
  botName=""Bot Four"";asksRestored.Tick(now.AddSeconds(966));Check(asksRestored.BotRequests.Count==4,""Only three unanswered bot requests can accumulate"");
  Check(asksRestored.Pending==0&&asksRestored.Replies.Count==0,""Bot requests cannot grant unsolicited player lives"");
  Reset(3);var quiet=new TeamLifeInbox(""empty-team"",null,(a,b)=>a);quiet.SetBots(0,()=>""Nobody"");quiet.Tick(now.AddDays(10));Check(quiet.BotRequests.Count==0,""No bot requests appear in a team without bots"");
  var offlineAsks=new TeamLifeInbox(""offline-asks"",null,(a,b)=>a);offlineAsks.SetBots(5,()=>""Bot"");offlineAsks.Tick(now.AddDays(30));Check(offlineAsks.BotRequests.Count==1,""Returning after a long absence adds only one bot request"");
  return count;
 }
}
Checks.Run()";
var result=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Web.Script.Serialization.JavaScriptSerializer).Assembly)).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" team donation/life-cap checks; syntax of "+files.Length+" C# files");
