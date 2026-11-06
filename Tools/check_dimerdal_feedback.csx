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
 static DimerdalView Gate(BoardController b,int origin,ObstacleId id=ObstacleId.DimerdalBlue1x1){b.ObstacleStateService.level.obstacles[origin]=(int)id;b.ObstacleStateService.level.obstacleOrigins[origin]=origin;b.ActiveLevelData.SetDimerdalProductionCount(origin,id,b.ActiveLevelData.goals[0].amount);var host=new GameObject().AddComponent<Image>();b.Dimerdals.Register(origin,new ObstacleDef{id=id},host);return host.gameObject.GetComponent<DimerdalView>();}
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
   v.PlayHit();v.Tick(.04f);var breakImage=v.PrepareForBreak();Check(ReferenceEquals(breakImage,frame)&&frame.color.a==1f&&frame.material==null,""break captures the real opaque colored frame, not the transparent host"");Check(!v.IsAnimating&&!flash.enabled&&!Field<Image>(v,""diamond"").enabled,""break cancels transient feedback and hides the produced gem"");Check(visual.localScale.x==1f&&visual.anchoredPosition.x==0,""break starts from unshaken geometry"");
   v.PlayHit();Call(v,""OnDisable"");Check(!v.IsAnimating&&!flash.enabled&&visual.localScale.x==1f,""disable cancels transient feedback"");
  }
  {
   var b=Board();var v=Gate(b,18);var untouched=Gate(b,20,ObstacleId.DimerdalGreen1x1);Open(b,18);Stone(b,63);
   Check(b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null),""open production hit accepted"");
   Check(b.jobs==1&&b.ObstacleStateService.retirements==0,""last in-flight product does not prematurely break source"");Tick(b);
   Check(b.ObstacleStateService.retirements==1&&b.ObstacleStateService.unlocked==1,""last landed product removes only its own source footprint"");Check(b.ObstacleStateService.GetObstacleIdAt(2,2)==ObstacleId.None&&b.ObstacleStateService.GetObstacleIdAt(4,2)!=ObstacleId.None,""other source remains on board"");
   Check(b.ObstacleStateService.DimerdalRemainingHitsQuery(20)==4&&b.ObstacleStateService.DimerdalCanProduceAtQuery(20),""untouched source keeps its own opening and production quota"");
   Check(!b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null),""removed source ignores damage"");
   int removed=b.ObstacleStateService.retirements;Tick(b);Check(b.ObstacleStateService.retirements==removed,""retired source cannot break twice"");var revealed=Gate(b,20,ObstacleId.DimerdalGreen1x1);Tick(b,1);Check(b.ObstacleStateService.retirements==removed,""late view of another source is not globally retired"");Call(b.Dimerdals,""OnDisable"");Check(Field<System.Collections.IList>(b.Dimerdals,""animated"").Count==0&&b.jobs==0,""service teardown releases animation and flight ownership"");
  }
  {
   var b=Board();var v=Gate(b,18);Open(b,18);b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null);Tick(b,45);
   Check(b.ObstacleStateService.retirements==0&&b.ObstacleStateService.DimerdalCanProduceQuery(),""timed-out flight returns quota without retiring sources"");Check(b.jobs==0,""failed final flight releases its job"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Board();var v=Gate(b,18);Open(b,18);Stone(b,63);b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null);Tick(b,4);Call(b.Dimerdals,""OnDisable"");Check(b.ObstacleStateService.retirements==0&&!v.IsAnimating&&b.jobs==0,""disable during last flight cancels feedback and does not spend failed quota"");Call(b.Dimerdals,""OnEnable"");Check(b.ObstacleStateService.DimerdalCanProduceQuery(),""reenable restores production after cancellation"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Board();var v=Gate(b,18);Open(b,18);var group=new HashSet<TileView>();foreach(int i in new[]{9,10,11}){var t=Stone(b,i);t.color=TileType.Bolt;group.Add(t);}var batch=b.Dimerdals.PrepareMatches(group,null);Check(batch!=null&&b.ObstacleStateService.retirements==0,""clear reservation alone never closes source"");b.Dimerdals.FinishMatches(batch,false);Check(b.ObstacleStateService.DimerdalCanProduceQuery()&&b.ObstacleStateService.retirements==0,""interrupted clear returns final commitment"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Board();b.ObstacleStateService.level.obstacles[63]=(int)ObstacleId.DimerdalDiamondBlue;b.Dimerdals.Build(b.ActiveLevelData);var v=Gate(b,18);Tick(b,1);Check(b.ObstacleStateService.retirements==0,""authored diamonds do not consume another source's authored production quota"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Board();Gate(b,18);Open(b,18);Stone(b,63);b.ObstacleStateService.buried.Add(18);b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null);Tick(b);Check(b.ObstacleStateService.retirements==0,""a covered source preserves its cover and waits for reveal"");b.ObstacleStateService.Reveal();Tick(b,1);Check(b.ObstacleStateService.retirements==1,""cover removal retries retirement even without a new view registration"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   foreach(int size in new[]{1,2,3}){var b=Board();var id=(ObstacleId)(84+size-1);Gate(b,18,id);for(int y=2;y<2+size;y++)for(int x=2;x<2+size;x++){int i=y*8+x;b.ActiveLevelData.obstacles[i]=(int)id;b.ActiveLevelData.obstacleOrigins[i]=18;}Open(b,18);Stone(b,63);b.ObstacleStateService.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null);Tick(b);Check(b.ObstacleStateService.unlocked==size*size&&b.ObstacleStateService.retirements==1,""all footprint cells unlock once for every cabinet size"");Call(b.Dimerdals,""OnDisable"");}
  }
  Console.WriteLine(""PASS: ""+checks+"" actual Dimerdal feedback/quota/retirement/failed-flight/reveal/teardown checks."");
 }
}
";
// Execute the actual layer-clearing method too. Only unrelated layer stores/helpers
// are fixtures; production retirement, footprint loop and event order stay intact.
var stateRoot=parse("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs");
var actualClear=stateRoot.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="ClearObstacleFromLevel").ToFullString().Replace("private void ClearObstacleFromLevel(","private void ActualClear(");
var fixture=@"
 public struct Beneath {public ObstacleId Id;public int Origin,Remaining;}
 public int[] remainingHitsByOrigin=Enumerable.Repeat(4,64).ToArray();
 public Dictionary<int,Beneath> _underTileBeneathMovable=new Dictionary<int,Beneath>(),layers=new Dictionary<int,Beneath>();
 public Dictionary<(ObstacleId,int),int> wallRemainingHits=new Dictionary<(ObstacleId,int),int>();
 public Dictionary<int,int> _chestColorStates=new Dictionary<int,int>(),_batteryHitsByOrigin=new Dictionary<int,int>(),_overrideBatteryHitsByOrigin=new Dictionary<int,int>(),_overrideBatteryProgressByOrigin=new Dictionary<int,int>(),_wardrobeItemCounts=new Dictionary<int,int>();
 public event Action<int> OnCellUnlocked;public Action<int,ObstacleId> restored;
 public ObstacleStateService(){OnCellUnlocked+=i=>unlocked++;}
 bool IsWallFamily(ObstacleId id)=>false;bool RemoveStampedObstacleBeneath(int cell,ObstacleId id,int origin)=>false;
 bool TryPopStampedBeneathLayer(int cell,out Beneath beneath){if(!layers.TryGetValue(cell,out beneath))return false;layers.Remove(cell);return true;}
 void RestoreCellObstacle(int cell,ObstacleId id,int origin){level.obstacles[cell]=(int)id;level.obstacleOrigins[cell]=origin;if(id==ObstacleId.None)OnCellUnlocked?.Invoke(cell);}
 void ShedGrassFlowerIfAny(int origin){}void CompleteWaterLily(int origin){}
 void ReinitRestoredBeneathOrigin(int origin,ObstacleId id,int remaining){if(remaining>=0)remainingHitsByOrigin[origin]=remaining;restored?.Invoke(origin,id);}
";
var stub=source.IndexOf(" private void ClearObstacleFromLevel(");var stubEnd=source.IndexOf("\n",stub);
var feedbackSource=source.Substring(0,stub)+" private void ClearObstacleFromLevel(int origin,ObstacleId id){retirements++;ActualClear(origin,id);}"+source.Substring(stubEnd);
feedbackSource=feedbackSource.Replace("public class ObstacleStateService {","public class ObstacleStateService {"+fixture+actualClear);
var layerChecks=@"
public static class LayerChecks {
 public static void Run(){
  int checks=0;
  foreach(int size in new[]{1,2,3}){
   var s=new ObstacleStateService{level=new LevelData()};var id=(ObstacleId)(84+size-1);int origin=18;int destroyed=0,atDestruction=-1;
   for(int y=2;y<2+size;y++)for(int x=2;x<2+size;x++){int i=y*8+x;s.level.obstacles[i]=(int)id;s.level.obstacleOrigins[i]=origin;}
   s.layers[origin]=new ObstacleStateService.Beneath{Id=ObstacleId.Mud,Origin=origin,Remaining=2};
   s.OnObstacleDestroyed+=(o,d)=>{destroyed++;atDestruction=(int)s.GetObstacleIdAt(2,2);};
   if(!s.TryRetireDimerdal(origin,id)||s.unlocked!=size*size-1||destroyed!=1||atDestruction!=(int)ObstacleId.Mud||s.remainingHitsByOrigin[origin]!=2)throw new Exception(""Actual clear failed footprint/layer/event preservation"");checks++;
   if(s.TryRetireDimerdal(origin,id)||destroyed!=1)throw new Exception(""Double retirement accepted"");checks++;
  }
  {
   var s=new ObstacleStateService{level=new LevelData{goals=new[]{new LevelGoalDefinition{targetType=LevelGoalTargetType.Collectible,collectibleId=CollectibleId.Diamond,amount=1}}}};var b=new BoardController{ObstacleStateService=s};s.board=b;
   s.level.SetDimerdalProductionCount(18,(ObstacleId)84,1);s.level.SetDimerdalProductionCount(18,(ObstacleId)87,1);
   s.level.obstacles[18]=84;s.level.obstacleOrigins[18]=18;s.layers[18]=new ObstacleStateService.Beneath{Id=(ObstacleId)87,Origin=18,Remaining=4};
   DimerdalService.Ensure(b).Build(s.level);b.Dimerdals.Register(18,new ObstacleDef{id=(ObstacleId)84},new GameObject().AddComponent<Image>());
   s.restored=(origin,id)=>b.Dimerdals.Register(origin,new ObstacleDef{id=id},new GameObject().AddComponent<Image>());
   var update=typeof(DimerdalService).GetMethod(""Update"",BindingFlags.NonPublic|BindingFlags.Instance);
   foreach(var id in new[]{(ObstacleId)84,(ObstacleId)87}){
    for(int i=0;i<4;i++)s.DimerdalHitInterceptor(18,ObstacleHitContext.NormalMatch,DimerdalRules.MatchColor(id));
    b.tiles[7,7]=new TileView{X=7,Y=7,color=TileType.Core};s.DimerdalHitInterceptor(18,ObstacleHitContext.SpecialActivation,null);for(int i=0;i<20;i++)update.Invoke(b.Dimerdals,null);
   }
   if(s.retirements!=2||s.level.obstacles[18]!=0)throw new Exception(""Reentrant beneath-source registration was lost or double-retired"");checks++;
   typeof(DimerdalService).GetMethod(""OnDisable"",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(b.Dimerdals,null);
  }
  Console.WriteLine(""PASS: ""+checks+"" actual ClearObstacleFromLevel layered footprint/event-order checks."");
 }
}
";
await CSharpScript.RunAsync(feedbackSource+feedbackChecks+layerChecks+"\nFeedbackChecks.Run();LayerChecks.Run();",ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
