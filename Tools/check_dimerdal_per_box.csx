// Actual data authoring and production methods with Unity doubles; no project build or board simulation.
#load "check_dimerdal.csx"
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var erase=parse("Assets/_Project/Scripts/Editor/LevelDataEditor.cs").DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="RemoveDimerdalAtCell").ToFullString();
var targetBranch=parse("Assets/_Project/Scripts/Grid/Board/Obstacles/ObstacleStateService.cs").DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="GetActiveMeaningfulHitsAt").DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>().Single(i=>i.Condition.ToString()=="DimerdalRules.IsShutter(id)").ToFullString();
var checkedSource=source.Replace("public class ObstacleStateService {","public class ObstacleStateService {public int DimerdalMeaningfulAtForCheck(int idx){var id=(ObstacleId)level.obstacles[idx];"+targetBranch+"return 0;}");
var perBox=@"
public static class PerBoxChecks {
 static int checks;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Call(object o,string method)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 static void Tick(BoardController b,int n=20){for(int i=0;i<n;i++){Time.frameCount++;Call(b.Dimerdals,""Update"");}}
 static void Stamp(LevelData l,int o,ObstacleId id,int size=1){for(int y=o/8;y<o/8+size;y++)for(int x=o%8;x<o%8+size;x++){l.obstacles[y*8+x]=(int)id;l.obstacleOrigins[y*8+x]=o;}}
 static int Goal(LevelData l)=>l.goals.Where(g=>g!=null&&g.targetType==LevelGoalTargetType.Collectible&&g.collectibleId==CollectibleId.Diamond).Sum(g=>g.amount);
 static BoardController Setup(int a,int b,int size=1){var l=new LevelData();Stamp(l,8,(ObstacleId)(84+size-1),size);Stamp(l,12,(ObstacleId)(90+size-1),size);l.SetDimerdalProductionCount(8,(ObstacleId)(84+size-1),a);l.SetDimerdalProductionCount(12,(ObstacleId)(90+size-1),b);l.SynchronizeDimerdalGoals();var board=new BoardController{ObstacleStateService=new ObstacleStateService{level=l}};board.ObstacleStateService.board=board;DimerdalService.Ensure(board).Build(l);foreach(int o in new[]{8,12})board.Dimerdals.Register(o,l.obstacleLibrary.Get((ObstacleId)l.obstacles[o]),new GameObject().AddComponent<Image>());return board;}
 static void Open(BoardController b,int o){var id=(ObstacleId)b.ActiveLevelData.obstacles[o];for(int i=0;i<4;i++)Check(b.ObstacleStateService.DimerdalHitInterceptor(o,ObstacleHitContext.NormalMatch,DimerdalRules.MatchColor(id)),""correct opening hit"");}
 static TileView Stone(BoardController b,int cell=63){var t=new TileView{X=cell%8,Y=cell/8,color=TileType.Core};b.tiles[t.X,t.Y]=t;return t;}
 static bool Hit(BoardController b,int o)=>b.ObstacleStateService.DimerdalHitInterceptor(o,ObstacleHitContext.SpecialActivation,null);
 static void Produce(BoardController b,int o,int n){for(int i=0;i<n;i++){Stone(b);Check(Hit(b,o),""source accepts one own-quota product"");Tick(b);}}
 public static void Run(){
  {
   var l=new LevelData();Stamp(l,8,(ObstacleId)84);Stamp(l,12,(ObstacleId)90);l.SetDimerdalProductionCount(8,(ObstacleId)84,6);l.SetDimerdalProductionCount(12,(ObstacleId)90,10);
   Check(l.SynchronizeDimerdalGoals()&&Goal(l)==16,""6+10 creates one automatic goal16"");var icon=l.goals[0].iconOverride;Check(icon==l.obstacleLibrary.Get(ObstacleId.DimerdalDiamondBlue).GetGoalIconSprite(),""automatic goal retains common purple icon"");Check(!l.SynchronizeDimerdalGoals(),""repeated authoring sync is idempotent"");
   l.SetDimerdalProductionCount(8,(ObstacleId)84,8);l.SynchronizeDimerdalGoals();Check(Goal(l)==18,""editing one source updates total immediately"");
   RemoveDimerdalAtCell(l,8);l.SynchronizeDimerdalGoals();Check(Goal(l)==10&&l.dimerdals.Length==1,""erase prunes only removed source and updates goal"");
   RemoveDimerdalAtCell(l,12);l.SynchronizeDimerdalGoals();Check(l.dimerdals.Length==0&&l.goals.Length==0,""last source removal removes stale Diamond goal"");
  }
  foreach(int total in new[]{15,16}){
   var l=new LevelData{goals=new[]{new LevelGoalDefinition{targetType=LevelGoalTargetType.Collectible,collectibleId=CollectibleId.Diamond,amount=total}}};Stamp(l,8,(ObstacleId)84);Stamp(l,12,(ObstacleId)90);l.SynchronizeDimerdalGoals();Check(Goal(l)==total&&l.GetDimerdalProductionCount(8,(ObstacleId)84)==(total+1)/2&&l.GetDimerdalProductionCount(12,(ObstacleId)90)==total/2,""legacy levels migrate once without changing total"");
  }
  {
   var l=new LevelData();Stamp(l,8,(ObstacleId)86,3);Stamp(l,12,(ObstacleId)92,3);l.SetDimerdalProductionCount(8,(ObstacleId)86,6);l.SetDimerdalProductionCount(12,(ObstacleId)92,10);l.stackedObstacles=new[]{new StackedObstacleEntry{originCellIndex=12,obstacleId=(ObstacleId)92},new StackedObstacleEntry{originCellIndex=40,obstacleId=(ObstacleId)87}};l.SetDimerdalProductionCount(40,(ObstacleId)87,3);l.SynchronizeDimerdalGoals();Check(Goal(l)==19&&l.dimerdals.Length==3,""multi-cell and duplicate base/stacked sources counted once; covered source included"");
   RemoveDimerdalAtCell(l,17);l.SynchronizeDimerdalGoals();Check(Goal(l)==13&&l.obstacles[8]==0&&l.obstacles[26]==0&&l.obstacles[12]==92,""erasing a non-origin source cell removes whole footprint and preserves other source"");
   Stamp(l,63,ObstacleId.DimerdalDiamondBlue);l.SynchronizeDimerdalGoals();Check(Goal(l)==14,""initial diamonds are added to the collectible total, not subtracted from box quotas"");
  }
  foreach(int size in new[]{1,2,3})foreach(var pair in new[]{(1,1),(6,10),(8,8)}){
   var b=Setup(pair.Item1,pair.Item2,size);Check(Goal(b.ActiveLevelData)==pair.Item1+pair.Item2,""authored sum matches goal"");Open(b,8);Open(b,12);Produce(b,8,pair.Item1);
   Check(b.ObstacleStateService.retirements==1&&b.ActiveLevelData.obstacles[8]==0&&b.ActiveLevelData.obstacles[12]!=0,""first source breaks independently at its own quota"");Check(!Hit(b,8)&&b.ObstacleStateService.DimerdalCanProduceAtQuery(12),""spent source cannot spend the other source's quota"");Check(b.TopHud.collected==0,""production is not collection"");
   Produce(b,12,pair.Item2);Check(b.ObstacleStateService.retirements==2&&b.ObstacleStateService.unlocked==2*size*size,""second source breaks at its own quota and both footprints unlock exactly once"");Check(!b.ObstacleStateService.DimerdalCanProduceQuery()&&b.jobs==0,""all spent sources stop targeting and release flight jobs"");Call(b.Dimerdals,""OnDisable"");Check(b.ObstacleStateService.DimerdalCanProduceAtQuery==null,""teardown unbinds per-source target query"");
  }
  {
   var b=Setup(2,3);Open(b,8);Open(b,12);Stone(b);Check(Hit(b,8)&&Hit(b,8)&&!Hit(b,8),""concurrent flights reserve exactly this box's remaining capacity"");Check(b.ObstacleStateService.DimerdalMeaningfulAtForCheck(8)==0&&b.ObstacleStateService.DimerdalMeaningfulAtForCheck(12)==1,""actual target-query branch ignores full box while other box is still available"");Tick(b,12);Check(b.ObstacleStateService.retirements==0,""one landed plus one pending flight does not break source"");Tick(b,30);Check(b.ObstacleStateService.DimerdalCanProduceAtQuery(8)&&b.ObstacleStateService.DimerdalCanProduceAtQuery(12),""timeout returns only the failed source reservation"");Produce(b,8,1);Check(b.ObstacleStateService.retirements==1&&b.ObstacleStateService.DimerdalCanProduceAtQuery(12),""retried last product retires only its own box"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Setup(1,1);Open(b,8);Open(b,12);Stone(b);Check(Hit(b,8),""last product launched"");Tick(b,4);Call(b.Dimerdals,""OnDisable"");Check(b.jobs==0&&b.ObstacleStateService.retirements==0,""disable during last flight cancels without premature destruction"");Call(b.Dimerdals,""OnEnable"");Check(b.ObstacleStateService.DimerdalCanProduceAtQuery(8)&&b.ObstacleStateService.DimerdalCanProduceAtQuery(12),""cancel restores independent capacities"");Produce(b,8,1);Produce(b,12,1);Check(b.ObstacleStateService.retirements==2,""re-enabled boxes produce exact original quotas"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Setup(1,1);Open(b,8);Open(b,12);var group=new HashSet<TileView>();foreach(int i in new[]{0,1,2}){var t=Stone(b,i);t.color=TileType.Bolt;group.Add(t);}var batch=b.Dimerdals.PrepareMatches(group,null);Check(batch!=null&&!b.ObstacleStateService.DimerdalCanProduceAtQuery(8)&&b.ObstacleStateService.DimerdalCanProduceAtQuery(12),""normal-match reservation occupies only its own box"");b.Dimerdals.FinishMatches(batch,false);Check(b.ObstacleStateService.DimerdalCanProduceAtQuery(8),""aborted clear returns that box's reservation"");
   batch=b.Dimerdals.PrepareMatches(group,null);Call(b.Dimerdals,""OnDisable"");Call(b.Dimerdals,""OnEnable"");b.Dimerdals.FinishMatches(batch,true);Check(b.jobs==0&&b.ObstacleStateService.DimerdalCanProduceAtQuery(8),""old-epoch batch cannot spend quota after cancellation"");Call(b.Dimerdals,""OnDisable"");
  }
  {
   var b=Setup(1,1);Stone(b);Stone(b,62);var fx=new DimerdalProductionFx();int placed=0,canceled=0;fx.OnFinished=(o,ok)=>{if(ok){placed++;fx.CancelAll();}else canceled++;};
   var host=new GameObject().AddComponent<Image>();Check(fx.Launch(b,host,new Sprite(),ObstacleId.DimerdalDiamondBlue,8)&&fx.Launch(b,host,new Sprite(),ObstacleId.DimerdalDiamondYellow,12),""two independent flights launch"");for(int i=0;i<20;i++)fx.Tick(.1f);
   Check(placed==1&&canceled==1&&!fx.HasActiveFlights&&b.jobs==0,""reentrant cancel during completion returns all ghosts/jobs once and stops old tick"");Call(b.Dimerdals,""OnDisable"");
  }
  Console.WriteLine(""PASS: ""+checks+"" actual per-box authoring/automatic goals/6+10/8+8/footprint/reservation/cancel checks."");
 }
"+erase+"}\nPerBoxChecks.Run();";
await CSharpScript.RunAsync(checkedSource+perBox,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
