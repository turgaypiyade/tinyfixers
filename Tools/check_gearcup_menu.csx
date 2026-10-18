// Real GearCup availability, timer and menu reminder methods; no Unity/network/build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
CompilationUnitSyntax Parse(string f)=>(CompilationUnitSyntax)CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Events/GearCup/"+f+".cs")).GetRoot();
var state=Parse("GearCupState");
var properties=string.Join("\n",state.DescendantNodes().OfType<PropertyDeclarationSyntax>().Where(p=>new[]{"HasJoined","End","IsUnlocked","HasPendingResult","CanShowInvitation","IsMenuAvailable"}.Contains(p.Identifier.Text)).Select(p=>p.ToFullString()));
var model=string.Join("\n",state.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c=>c.Identifier.Text=="GearCupSave"||c.Identifier.Text=="GearCupWin").Select(c=>c.ToFullString()));
var badge=Parse("GearCupPreviewEventButton");
var reminder=badge.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="TryShowReminder").ToFullString().Replace("private void", "public void");
var screen=Parse("GearCupPreviewScreen");
var screenMembers=string.Join("\n",screen.DescendantNodes().OfType<MemberDeclarationSyntax>().Where(m=>m is MethodDeclarationSyntax md&&new[]{"ShowInvitation","TryShowPendingResult"}.Contains(md.Identifier.Text)||m is PropertyDeclarationSyntax pd&&new[]{"IsInvitationVisible","PreviewTimeRemaining"}.Contains(pd.Identifier.Text)).Select(m=>m.ToFullString()));
var rules=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Events/GearCup/GearCupRules.cs")).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single().ToFullString();
var stubs=@"
using System;using System.Collections.Generic;
public enum TileType {Gear,Core,Bolt,Plate}
public class GearCupPreviewConfig {public static GearCupPreviewConfig Shared=new GearCupPreviewConfig();public bool eventEnabled=true,autoShowJoinPopup=true;public int minLevelGate=51,seasonHours=72;}
public static class CurrentLevel {public static int Global=100;}
public static class PlayerTeamState {public static bool HasTeam=true;}
public static class RuntimeSimulationSession {public static bool IsActive;}
public static class FirebaseCloudSaveService {public static bool RestoreResolved=true;}
public static class LoadingScreenManager {public static bool IsVisible;}
public static class CustomIntroLoadingManager {public static bool IsVisible;}
public static class RuntimeChoicePopup {public static bool IsShowing;}
public static class RewardChestRevealOverlay {public static bool IsShowing;}
public static class ShopPurchaseCelebration {public static bool IsShowing;}
public static class MarketNavigator {public static bool Open;public static bool IsMenuMarketOpen()=>Open;}
public class WorkshopRepairButtonTutorial {public static bool IsSeen()=>true;}
public class TutorialOverlayController {}
public class PopupEntranceAnimator {public bool IsVisible;}
public enum FindObjectsSortMode {None}
public static class Time {public static float unscaledTime;}
public static class PlayerPrefs {public static Dictionary<string,string> values=new Dictionary<string,string>();public static string GetString(string k,string d)=>values.TryGetValue(k,out var v)?v:d;public static void SetString(string k,string v){values[k]=v;}public static void Save(){}}
public class GameObject {public bool activeInHierarchy;}
public class Transform {public Transform parent;}
public class GearCupJoinPopup {
 public GameObject gameObject=new GameObject();public static GearCupJoinPopup Create(Transform parent,GearCupPreviewConfig config)=>new GearCupJoinPopup();
 public void Show(ScreenProbe s){gameObject.activeInHierarchy=true;s.invitations++;}
}
";
var source=stubs+model+rules+"\npublic static class GearCupState {public const string InvitationAskedKey=\"gearcup_join_asked_v1\";public static GearCupSave Data=new GearCupSave();"+properties+"}\n"+@"
public class ScreenProbe {
 public GearCupPreviewConfig config=GearCupPreviewConfig.Shared;public GameObject gameObject=new GameObject();public Transform transform=new Transform();private GearCupJoinPopup joinPopup;
 public int invitations,results;
 public void Open(){if(GearCupState.CanShowInvitation)ShowInvitation();}
 private void OpenEvent(){gameObject.activeInHierarchy=true;results++;}
 public void Close(){gameObject.activeInHierarchy=false;if(joinPopup!=null)joinPopup.gameObject.activeInHierarchy=false;}
"+screenMembers+"}\n"+@"
public class MenuProbe {
 public ScreenProbe screen=new ScreenProbe();private float menuReadyAt=-1f;
 public bool tutorial;public PopupEntranceAnimator[] popups=Array.Empty<PopupEntranceAnimator>();public int scans;
 private T FindFirstObjectByType<T>() where T:class,new()=>tutorial&&typeof(T)==typeof(TutorialOverlayController)?new T():null;
 private T[] FindObjectsByType<T>(FindObjectsSortMode s){scans++;return (T[])(object)popups;}
"+reminder+"}\n";
var tests=@"
int checks=0;Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
check(GearCupState.IsMenuAvailable&&GearCupState.CanShowInvitation,""fresh eligible player sees invitation badge"");
var view=new ScreenProbe();check(view.PreviewTimeRemaining>TimeSpan.Zero,""new season has live countdown"");
var old=new GearCupSave{joined=true,endTicks=DateTime.UtcNow.AddMinutes(-5).Ticks};GearCupState.Data=old;
check(!GearCupState.IsMenuAvailable&&!GearCupState.CanShowInvitation&&view.PreviewTimeRemaining==TimeSpan.Zero,""expired unsettled season logo is hidden"");
old.settledRank=2;old.coinDelivered=old.itemDelivered=true;
check(GearCupState.HasPendingResult&&!GearCupState.IsMenuAvailable,""unseen result remains deliverable without expired badge"");
old.resultPresented=true;old.outbox.Add(new GearCupWin());check(!GearCupState.IsMenuAvailable,""offline score outbox cannot be overwritten by next invitation"");
old.outbox.Clear();check(GearCupState.CanShowInvitation&&GearCupState.IsMenuAvailable&&view.PreviewTimeRemaining>TimeSpan.Zero,""completed old season opens a real new season countdown"");
old.endTicks=DateTime.UtcNow.AddMinutes(5).Ticks;check(GearCupState.IsMenuAvailable&&!GearCupState.CanShowInvitation,""live enrolled player has badge without repeat invitation"");
GearCupPreviewConfig.Shared.eventEnabled=false;check(!GearCupState.IsMenuAvailable&&!GearCupState.CanShowInvitation,""disabled event hides and never invites"");GearCupPreviewConfig.Shared.eventEnabled=true;
CurrentLevel.Global=50;check(!GearCupState.IsMenuAvailable,""level gate respected"");CurrentLevel.Global=100;GearCupState.Data=new GearCupSave();
var menu=new MenuProbe();Time.unscaledTime=0;menu.TryShowReminder();check(menu.scans==0&&menu.screen.invitations==0,""initial quiet delay performs no modal scan"");
Time.unscaledTime=4;menu.popups=new[]{new PopupEntranceAnimator{IsVisible=true}};menu.TryShowReminder();check(menu.screen.invitations==0,""other event popup blocks GearCup invitation"");
menu.popups=Array.Empty<PopupEntranceAnimator>();menu.tutorial=true;menu.TryShowReminder();check(menu.screen.invitations==0,""tutorial blocks invitation"");menu.tutorial=false;
menu.TryShowReminder();check(menu.screen.invitations==1&&menu.screen.IsInvitationVisible,""clear menu opens actual invitation method"");
menu.screen.Close();int scans=menu.scans;menu.TryShowReminder();check(menu.screen.invitations==1&&menu.scans==scans,""same cycle never nags or rescans modals"");
PlayerPrefs.values.Clear();PlayerTeamState.HasTeam=false;menu.TryShowReminder();check(menu.screen.invitations==1,""team-less player is not automatically invited"");PlayerTeamState.HasTeam=true;
LoadingScreenManager.IsVisible=true;menu.TryShowReminder();LoadingScreenManager.IsVisible=false;Time.unscaledTime=100;menu.TryShowReminder();check(menu.screen.invitations==1,""loading completion restarts quiet delay"");
Time.unscaledTime=104;menu.TryShowReminder();check(menu.screen.invitations==2,""next eligible window can show again"");menu.screen.Close();
GearCupState.Data=new GearCupSave{joined=true,endTicks=DateTime.UtcNow.AddMinutes(-1).Ticks,settledRank=1,coinDelivered=true,itemDelivered=true};
menu.TryShowReminder();check(menu.screen.results==1&&menu.screen.gameObject.activeInHierarchy,""hidden expired badge does not strand result presentation"");
Console.WriteLine(""PASS: ""+checks+"" GearCup live/expired/reward/outbox/timer/reminder/modal checks. UI/native transport not tested."");
";
await CSharpScript.RunAsync(source+tests,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
