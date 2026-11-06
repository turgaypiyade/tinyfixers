// Actual service/view/flight code with Unity doubles. No Unity build, Play or level simulation.
#load "check_dimerdal.csx"
using System;using System.Linq;using System.IO;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var feedbackChecks=@"
public static class FeedbackChecks {
 static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static void Call(object o,string method)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 static BoardController Board(int cap=1){var l=new LevelData{goals=new[]{new LevelGoalDefinition{targetType=LevelGoalTargetType.Collectible,collectibleId=CollectibleId.Diamond,amount=cap}}};var b=new BoardController{ObstacleStateService=new ObstacleStateService{level=l}};b.ObstacleStateService.board=b;DimerdalService.Ensure(b).Build(l);return b;}
 static DimerdalView Gate(BoardController b,int origin,ObstacleId id=ObstacleId.DimerdalBlue1x1){b.ObstacleStateService.level.obstacles[origin]=(int)id;b.ObstacleStateService.level.obstacleOrigins[origin]=origin;var host=new GameObject().AddComponent<Image>();b.Dimerdals.Register(origin,new ObstacleDef{id=id},host);return host.gameObject.GetComponent<DimerdalView>();}
 static void Open(BoardController b,int origin){for(int i=0;i<4;i++)Check(b.ObstacleStateService.DimerdalHitInterceptor(origin,ObstacleHitContext.NormalMatch,TileType.Bolt),""accepted opening hit"");}
 static TileView Stone(BoardController b,int i){var t=new TileView{X=i%8,Y=i/8,color=TileType.Core};b.tiles[t.X,t.Y]=t;return t;}
 static void Tick(BoardController b,int frames=30){for(int i=0;i<frames;i++){Time.frameCount++;Call(b.Dimerdals,""Update"");}}
 public static void Run(){
  {
   var host=new GameObject().AddComponent<Image>();host.rectTransform.anchoredPosition=new Vector2(31,47);
   var v=DimerdalView.Ensure(host,new ObstacleDef(),4);var frame=Field<Image>(v,""frame"");var visual=Field<RectTransform>(v,""visual"");var panel=Field<RectTransform>(v,""panel"");var flash=Field<Image>(v,""flash"");int created=GameObject.created;
   v.SetRemaining(3,true);v.PlayHit();Check(v.IsAnimating&&flash.enabled,""accepted hit starts feedback"");v.Tick(.06f);
   Check(visual.localScale.x>1f&&Math.Abs(visual.localRotation.z)>0,""hit gives visible scale and rock response"");Check(flash.color.a>0,""reused aperture flash is visible"");
   Check(host.rectTransform.anchoredPosition.x==31&&host.rectTransform.anchoredPosition.y==47,""cabinet logical host and flight origin stay fixed"");v.Tick(1f);
   Check(!v.IsAnimating&&!flash.enabled&&visual.localScale.x==1f&&visual.anchoredPosition.x==0,""feedback fully resets"");Check(panel.anchorMin.y==.25f,""one hit opens exactly one quarter"");
   for(int i=0;i<100;i++){v.PlayHit();v.Tick(.04f);v.Tick(1f);}Check(GameObject.created==created,""repeated hits allocate no GameObjects"");
   v.SetRemaining(0,false);v.SetProductionComplete(true,true);v.Tick(1f);Check(panel.anchorMin.y==0f,""exhausted cabinet fully closes"");Check(Math.Abs(frame.color.a-.45f)<.0001f,""exhausted opacity is 45 percent"");Check(frame.material!=null,""shared grayscale material assigned"");Check(!Field<Image>(v,""diamond"").enabled,""closed exhausted cabinet hides its diamond"");
   var slats=Field<Image[]>(v,""slats"");Check(slats.All(s=>ReferenceEquals(s.material,frame.material)),""all spent layers share one material"");v.PlayHit();Check(!v.IsAnimating,""spent cabinets do not respond to more hits"");
   var other=DimerdalView.Ensure(new GameObject().AddComponent<Image>(),new ObstacleDef(),0);other.SetProductionComplete(true,false);Check(ReferenceEquals(Field<Image>(other,""frame"").material,frame.material),""different cabinets reuse the same asset-backed material"");
   v.SetProductionComplete(false,false);Check(panel.anchorMin.y==1f&&frame.color.a==1f&&frame.material==null,""reopened cabinet restores remaining state and normal color"");v.PlayHit();Call(v,""OnDisable"");Check(!v.IsAnimating&&!flash.enabled&&visual.localScale.x==1f,""disable cancels transient feedback"");
  }
  {
   var b=Board();var v=Gate(b,18);var untouched=Gate(b,20,ObstacleId.DimerdalGreen1x1);Open(b,18);Stone(b,63);
   Check(b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null),""open production hit accepted"");
   Check(b.jobs==1&&!Field<bool>(b.Dimerdals,""productionComplete""),""last in-flight commitment does not prematurely close sources"");Tick(b);
   Check(Field<bool>(b.Dimerdals,""productionComplete"")&&Field<bool>(v,""completed"")&&Field<bool>(untouched,""completed""),""last landed diamond closes every shared-quota source"");
   Check(b.ObstacleStateService.DimerdalRemainingHitsQuery(20)==0&&!b.ObstacleStateService.DimerdalCanProduceQuery(),""unused closed sources also stop being meaningful targets"");
   Check(!b.ObstacleStateService.DimerdalHitInterceptor(20,ObstacleHitContext.SpecialActivation,null),""completed source ignores damage"");
   var revealed=Gate(b,20,ObstacleId.DimerdalGreen1x1);Check(Field<bool>(revealed,""completed"")&&Math.Abs(Field<Image>(revealed,""frame"").color.a-.45f)<.0001f,""late revealed source inherits exhausted state"");Call(b.Dimerdals,""OnDisable"");Check(Field<System.Collections.IList>(b.Dimerdals,""animated"").Count==0&&b.jobs==0,""service teardown releases animation and flight ownership"");
  }
  {
   var b=Board();var v=Gate(b,18);Open(b,18);b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null);Tick(b,45);
   Check(!Field<bool>(b.Dimerdals,""productionComplete"")&&!Field<bool>(v,""completed"")&&b.ObstacleStateService.DimerdalCanProduceQuery(),""timed-out flight returns quota without graying sources"");Check(b.jobs==0,""failed final flight releases its job"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Board();var v=Gate(b,18);Open(b,18);Stone(b,63);b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null);Tick(b,4);Call(b.Dimerdals,""OnDisable"");Check(!Field<bool>(v,""completed"")&&!v.IsAnimating&&b.jobs==0,""disable during last flight cancels feedback and does not spend failed quota"");Call(b.Dimerdals,""OnEnable"");Check(b.ObstacleStateService.DimerdalCanProduceQuery(),""reenable restores production after cancellation"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Board();var v=Gate(b,18);Open(b,18);var group=new HashSet<TileView>();foreach(int i in new[]{9,10,11}){var t=Stone(b,i);t.color=TileType.Bolt;group.Add(t);}var batch=b.Dimerdals.PrepareMatches(group,null);Check(batch!=null&&!Field<bool>(b.Dimerdals,""productionComplete""),""clear reservation alone never closes source"");b.Dimerdals.FinishMatches(batch,false);Check(b.ObstacleStateService.DimerdalCanProduceQuery()&&!Field<bool>(v,""completed""),""interrupted clear returns final commitment"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Board();b.ObstacleStateService.level.obstacles[63]=(int)ObstacleId.DimerdalDiamondBlue;b.Dimerdals.Build(b.ActiveLevelData);var v=Gate(b,18);Check(Field<bool>(v,""completed""),""authored diamonds already fulfilling quota close a newly revealed source"");Call(b.Dimerdals,""OnDisable"");
  }
  Console.WriteLine(""PASS: ""+checks+"" actual Dimerdal feedback/quota/failed-flight/reveal/teardown checks."");
 }
}
";
await CSharpScript.RunAsync(source+feedbackChecks+"\nFeedbackChecks.Run();",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
var shader=File.ReadAllText("Assets/_Project/Shaders/UI_DimerdalSpent.shader");
foreach(var contract in new[]{"_StencilComp","_StencilReadMask","UNITY_UI_CLIP_RECT","UNITY_UI_ALPHACLIP","UnityGet2DClipping","dot(c.rgb","c.a *= i.color.a"})if(!shader.Contains(contract))throw new Exception("Missing spent UI shader contract: "+contract);
Console.WriteLine("PASS: grayscale, vertex-alpha and UI mask/stencil shader source contracts (no GPU compilation).");
