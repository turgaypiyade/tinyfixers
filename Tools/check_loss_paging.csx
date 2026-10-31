// Run: csi Tools/check_loss_paging.csx. Executes the production popup's paging/render/cancel methods.
// Unity UI stubs cover presentation variants and interrupted animations without a project build.
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

string path = "Assets/_Project/Scripts/UI/LevelEndSimplePopupController.cs";
var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview));
var errors = tree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
if (errors.Length > 0) throw new Exception(string.Join("\n", errors.Select(x => x.ToString())));
var source = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
string[] names = {"RenderLossPage", "HasNextLossPage", "BuildLossSentence", "HandleFailCloseClicked", "ShowFirstLossPage", "ShowNextLossPage", "CancelLossPaging"};
string production = string.Join("\n", source.Members.OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
production += "\n" + source.Members.OfType<PropertyDeclarationSyntax>().Single(x => x.Identifier.ValueText == "LossPageSize").ToFullString();
string harness = @"
using System;
using System.Collections;
using System.Collections.Generic;
public class Sprite {}
public struct Color {}
public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
public class GameObject {public bool activeSelf;public void SetActive(bool v){activeSelf=v;}}
public class Transform {}
public class RectTransform:Transform {public GameObject gameObject=new GameObject();public Vector2 anchoredPosition;}
public class TMP_Text {public GameObject gameObject=new GameObject();public string text="""";}
public class Image {public bool enabled;}
public class LossRowView {
 public GameObject gameObject=new GameObject();public int Value;public string Label;
 public void Set(Sprite icon,string label,int value,Sprite status){Value=value;Label=label;}
}
public static class ColorUtility {public static string ToHtmlStringRGB(Color c)=>""FFFFFF"";}
public static class Mathf {
 public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
 public static int Clamp(int n,int a,int b)=>Math.Max(a,Math.Min(n,b));
}
public class Coroutine {public IEnumerator Routine;public bool Cancelled;}
public class Popup {
 private readonly List<LossRowView> _lossRows=new List<LossRowView>();
 private List<(Sprite icon,string label,int amount,bool achieved)> _lossItems;
 private int _lossPage,lossItemsPerPage=3;
 private bool _lossPaging,_hasLosses,failSecondStage,_abandonWarning,_lossPanelHomeSet=true;
 private Vector2 _lossPanelHome=new Vector2(10,20);
 private Color lossNameColor;
 private TMP_Text lossMessageText,lossTitleText,failEventLossWarningText,failMessageText;
 private LossRowView lossRowPrefab;
 private Transform lossRowContainer;
 private GameObject lossSummaryRoot;
 private RectTransform lossSlidePanel=new RectTransform();
 private Image extraMovesIcon;
 private Sprite notAchievedIcon,achievedIcon;
 private float lossSlideDuration=.35f;
 private Coroutine _lossSlideRoutine;
 public int Allocations,Started,GivenUp;
 private readonly List<Coroutine> running=new List<Coroutine>();
 private LossRowView Instantiate(LossRowView prefab,Transform parent){Allocations++;return new LossRowView();}
 private static string LocalizedText(string key,string fallback)=>fallback;
 private static string LocalizedFormat(string key,string fallback,params object[] args)=>string.Format(fallback,args);
 private void ResumeFromAbandonWarning(){}
 private void SetExtraMovesBadgeTextVisible(bool v){}
 private void GiveUpLevel(){GivenUp++;}
 private IEnumerator SlideInFromRight(RectTransform panel,float duration){yield return null;}
 private IEnumerator SlideOutToLeft(RectTransform panel,float duration){yield return null;}
 private Coroutine StartCoroutine(IEnumerator routine){
  Started++;var c=new Coroutine{Routine=routine};running.Add(c);routine.MoveNext();return c;
 }
 private void StopCoroutine(Coroutine c){c.Cancelled=true;(c.Routine as IDisposable)?.Dispose();}
 private static void Drain(IEnumerator r){
  do{if(r.Current is IEnumerator nested)Drain(nested);}while(r.MoveNext());
 }
 private void Complete(){foreach(var c in running)if(!c.Cancelled)Drain(c.Routine);running.Clear();}
" + production + @"
 static int checks;
 static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 private void Setup(int count,int per,bool text,bool prefab,bool container){
  lossItemsPerPage=per;_lossItems=new List<(Sprite icon,string label,int amount,bool achieved)>();
  for(int i=0;i<count;i++)_lossItems.Add((null,""loss""+i,i+1,false));
  _hasLosses=count>0;
  lossMessageText=text?new TMP_Text():null;lossTitleText=new TMP_Text();failEventLossWarningText=new TMP_Text();
  lossRowPrefab=prefab?new LossRowView():null;lossRowContainer=container?new Transform():null;
 }
 public static int Run(){
  foreach(int count in new[]{0,1,2,3,4,5,6,7,10})foreach(int configured in new[]{-1,1,2,3,4,99})
  foreach(bool text in new[]{false,true})foreach(bool prefab in new[]{false,true})foreach(bool container in new[]{false,true}){
   var p=new Popup();p.Setup(count,configured,text,prefab,container);
   int seen=0,page=0;
   do{
    p.RenderLossPage(page);
    int expected=Math.Min(Math.Max(0,count-seen),Math.Max(1,Math.Min(3,configured)));
    string message=text?p.lossMessageText.text:p.failEventLossWarningText.text;
    if(prefab&&container){
     int active=0;
     foreach(var row in p._lossRows)if(row.gameObject.activeSelf){
      Check(row.Value==seen+active+1,""Visible row ordering must follow the current page"");active++;
     }
     Check(active==expected&&active<=3,""All row layouts must cap at three, including missing bubble text"");
    }
    if(text||!(prefab&&container)){
     for(int i=0;i<count;i++)Check(message.Contains(""loss""+i)==(i>=seen&&i<seen+expected),""Bubble/text fallback contains only the current page"");
    }
    seen+=expected;page++;
   }while(p.HasNextLossPage());
   Check(seen==count,""No losses disappear when optional prefab/text bindings are absent"");
   Check(p.Allocations<=3,""Page changes reuse at most three row objects"");
   int before=p.Allocations;p.RenderLossPage(0);Check(p.Allocations==before,""Reopening a page never churns row objects"");
  }
  foreach(bool bubble in new[]{false,true}){
   var p=new Popup();p.Setup(4,3,bubble,true,true);p.RenderLossPage(0);
   p.HandleFailCloseClicked();
   Check(p._lossPaging&&p.Started==1&&p.GivenUp==0,""First cancel opens the first page and locks only during its transition"");
   p.HandleFailCloseClicked();Check(p.Started==1,""Rapid second tap cannot start an overlapping first-page transition"");
   p.Complete();Check(!p._lossPaging,""First page unlocks cancel after its animation"");
   p.HandleFailCloseClicked();Check(p._lossPaging&&p.GivenUp==0,""Four losses advance to a second page, not give up"");
   p.HandleFailCloseClicked();Check(p.Started==2,""Rapid page taps cannot start duplicate animations"");
   p.Complete();Check(p._lossPage==1&&!p._lossPaging&&!p.HasNextLossPage(),""Second page shows the final loss and releases cancel"");
   p.HandleFailCloseClicked();Check(p.GivenUp==1,""Cancel works after the last page"");
   var interrupted=new Popup();interrupted.Setup(4,3,bubble,true,true);interrupted.RenderLossPage(0);
   interrupted.HandleFailCloseClicked();interrupted.CancelLossPaging();
   Check(!interrupted._lossPaging&&interrupted._lossSlideRoutine==null,""Hiding/disabling during a transition cannot leave cancel locked"");
   Check(interrupted.lossSlidePanel.anchoredPosition.x==10&&interrupted.lossSlidePanel.anchoredPosition.y==20,""Interrupted transition restores the panel home position"");
   interrupted.HandleFailCloseClicked();interrupted.Complete();interrupted.HandleFailCloseClicked();
   Check(interrupted.GivenUp==1,""Cancel remains usable after an interrupted transition"");
  }
  return checks;
 }
}
Popup.Run()";
int result=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+result+" production loss-popup paging, fallback, row reuse and cancel/teardown checks.");
