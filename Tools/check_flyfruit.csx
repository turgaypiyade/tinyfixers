// Production methods with Unity doubles. No Unity/project build, gameplay or balance simulation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
Func<string,CompilationUnitSyntax> parse=p=>CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/"+p)).GetCompilationUnitRoot();
Func<string,string[],string> methods=(p,n)=>string.Join("\n",parse(p).DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>n.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var paths=new[]{"Core/LevelData.cs","Editor/LevelDataEditor.cs","Grid/GridSpawner.cs","Grid/Board/BoardController.cs","Grid/Board/Actions/PatchBotTargetCoordinator.cs","Grid/Board/Obstacles/ObstacleStateService.cs","Grid/Board/Obstacles/FlyFruitProgress.cs","Grid/Board/Obstacles/FlyEggProductionFx.cs","Grid/Board/Obstacles/FlyFruitService.cs","Grid/Board/Obstacles/FlyFruitFlyView.cs"};
foreach(var p in paths){var e=parse(p).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(e.Length>0)throw new Exception(p+"\n"+string.Join("\n",e.Select(x=>x.ToString())));}
var enums=string.Join("\n",parse("Core/LevelData.cs").DescendantNodes().OfType<EnumDeclarationSyntax>().Select(e=>e.ToFullString()))+parse("Grid/Board/Obstacles/ObstacleStateService.cs").DescendantNodes().OfType<EnumDeclarationSyntax>().Single(e=>e.Identifier.Text=="ObstacleHitContext").ToFullString()+File.ReadAllText("Assets/_Project/Scripts/Grid/TileType.cs");
var branches=string.Join("\n",parse("Grid/Board/Obstacles/ObstacleStateService.cs").DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>m.DescendantNodes().OfType<IfStatementSyntax>().Any(i=>i.Condition.ToString()=="id == ObstacleId.FlyFruit")).SelectMany(m=>m.DescendantNodes().OfType<IfStatementSyntax>()).Where(i=>i.Condition.ToString()=="id == ObstacleId.FlyFruit"||i.Condition.ToString()=="id == ObstacleId.FlyEggBox").Take(2).Select(i=>i.ToFullString()));
var production=string.Join("\n",new[]{"FlyFruitProgress","FlyEggProductionFx","FlyFruitService","FlyFruitFlyView","DimerdalRules"}.Select(n=>string.Join("\n",parse("Grid/Board/Obstacles/"+n+".cs").Members.Select(m=>m.ToFullString())))).Replace("UnityEngine.Random.Range","TestRandom.Range");
var source=File.ReadAllText("Tools/flyfruit_harness.txt").Replace("/* ENUMS */",enums)
 .Replace("/* FRUIT DATA */",methods("Core/LevelData.cs",new[]{"GetFruitDimensions","SetFruitDimensions","GetFruitOrigins","SynchronizeFruitGoals","GetObstacleFootprint"}))
 .Replace("/* COMPLETE */",methods("Grid/Board/Obstacles/ObstacleStateService.cs",new[]{"TryCompleteFlyFruitObstacle"}))
 .Replace("/* HIT BRANCHES */",branches)
 .Replace("/* BOARD METHODS */",methods("Grid/Board/BoardController.cs",new[]{"CanReplaceIdleColorTile","TryPlaceFlyEgg"}))
 .Replace("/* RESIZE */",methods("Editor/LevelDataEditor.cs",new[]{"TryStampFruit","SimulateTopLayers","RectCells","IsStackedWallId"}).Replace("private static bool","public static bool"))
 .Replace("/* PRODUCTION */",production);
var tests=@"
public static class FlyFruitChecks {
 static int checks;static void Check(bool v,string why){checks++;if(!v)throw new Exception(why);}
 static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 static void Call(object o,string m)=>o.GetType().GetMethod(m,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);
 static void Tick(BoardController b,int n=100){for(int i=0;i<n;i++)Call(b.FlyFruits,""Update"");}
 static void Stamp(LevelData l,int o,ObstacleId id,int w=1,int h=1){for(int y=o/8;y<o/8+h;y++)for(int x=o%8;x<o%8+w;x++){l.obstacles[y*8+x]=(int)id;l.obstacleOrigins[y*8+x]=o;}if(id==ObstacleId.FlyFruit)l.SetFruitDimensions(o,w,h);}
 static BoardController Setup(){var l=new LevelData();Stamp(l,0,ObstacleId.FlyEggBox,2,2);Stamp(l,40,ObstacleId.FlyFruit);Stamp(l,44,ObstacleId.FlyFruit,2,2);l.SynchronizeFruitGoals();var b=new BoardController{ObstacleStateService=new ObstacleStateService{level=l}};b.ObstacleStateService.board=b;for(int y=0;y<8;y++)for(int x=0;x<8;x++){var t=new TileView{X=x,Y=y};t.transform.anchoredPosition=new Vector2(x*100,-y*100);b.tiles[x,y]=t;}FlyFruitService.Ensure(b).Build(b,l);foreach(int o in new[]{0,40,44})b.FlyFruits.Register(o,l.obstacleLibrary.Get((ObstacleId)l.obstacles[o]),new GameObject(""view"",typeof(Image)).GetComponent<Image>());return b;}
 static FlyFruitProgress Progress(BoardController b,int o){var map=(System.Collections.IDictionary)Field(b.FlyFruits,""fruits"");return (FlyFruitProgress)Field(map[o],""progress"");}
 static void Hatch(BoardController b,int cell){b.ActiveLevelData.obstacles[cell]=0;b.ActiveLevelData.obstacleOrigins[cell]=-1;b.FlyFruits.Hatch(cell);}
 public static void Run(){
  for(int n=0;n<20;n++){
   var p=new FlyFruitProgress();Check(p.TryReserveBrood()&&p.TryReserveBrood()&&!p.TryReserveBrood(),""six bites reserve exactly two broods"");
   for(int i=0;i<5;i++)Check(!p.Arrive(),""early bite cannot finish fruit"");Check(p.Arrive()&&p.Remaining==0&&p.Reserved==0&&!p.Arrive(),""sixth bite finishes once"");
   var c=new FlyEggBoxProgress();int[]slots=new int[4];for(int i=0;i<4;i++)Check(c.TryReserve(out slots[i]),""one of four egg slots reserved"");Check(!c.TryReserve(out int extra)&&!c.Complete,""four airborne eggs do not complete box"");
   int retry=-1;Check(c.Finish(slots[0],false)&&c.Available==1&&c.TryReserve(out retry)&&retry==slots[0],""failed landing restores same source slot"");Check(c.Finish(retry,true)&&!c.Finish(retry,true),""landing callback is idempotent"");for(int i=1;i<4;i++)c.Finish(slots[i],true);Check(c.Complete&&c.Placed==4,""four successful landings complete box"");
  }
  var b=Setup();var l=b.ActiveLevelData;Check(l.goals.Length==1&&l.goals[0].obstacleId==ObstacleId.FlyFruit&&l.goals[0].amount==2,""only two fruit goals,independent of footprint"");Check(!l.SynchronizeFruitGoals(),""fruit goal sync is idempotent"");
  foreach(var ctx in new[]{ObstacleHitContext.NormalMatch,ObstacleHitContext.Booster,ObstacleHitContext.Scripted}){
   Check(!b.ObstacleStateService.ApplyNew(0,ObstacleId.FlyEggBox,ctx).Accepted,""box rejects non-special context"");Check(!b.ObstacleStateService.ApplyNew(40,ObstacleId.FlyFruit,ctx).Accepted,""fruit rejects direct damage"");
  }
  Check(!b.ObstacleStateService.ApplyNew(40,ObstacleId.FlyFruit,ObstacleHitContext.SpecialActivation).Accepted,""special cannot eat fruit directly"");
  for(int i=0;i<4;i++)Check(b.ObstacleStateService.ApplyNew(0,ObstacleId.FlyEggBox,ObstacleHitContext.SpecialActivation).Accepted,""each special launches one egg"");
  Check(!b.ObstacleStateService.ApplyNew(0,ObstacleId.FlyEggBox,ObstacleHitContext.SpecialActivation).Accepted&&b.ObstacleStateService.boxClears==0,""fifth hit denied;box waits for landings"");Tick(b);
  var eggs=Enumerable.Range(0,64).Where(i=>l.obstacles[i]==(int)ObstacleId.FlyEgg).ToArray();Check(eggs.Length==4&&b.ObstacleStateService.boxClears==1&&b.jobs==0,""four movable eggs land and source retires once"");
  foreach(int cell in eggs){var tile=b.tiles[cell%8,cell/8];Check(tile.movable&&tile.GetTileType()==TileType.Core&&tile.GetSpecial()==TileSpecial.None,""normal tile converted to movable without clear/goal credit"");}
  Hatch(b,eggs[0]);Tick(b);Check(Progress(b,40).Remaining==3&&b.ObstacleStateService.fruitGoals==0,""one egg's three flies eat half;no goal yet"");
  Hatch(b,eggs[1]);Tick(b);Check(b.ObstacleStateService.fruitGoals==1&&l.obstacles[40]==0,""second brood completes one fruit goal"");
  Hatch(b,eggs[2]);Hatch(b,eggs[3]);Tick(b);Check(b.ObstacleStateService.fruitGoals==2&&l.obstacles[44]==0&&l.obstacles[53]==0&&b.jobs==0,""concurrent broods finish2x2fruit and release all jobs"");Call(b.FlyFruits,""OnDisable"");
  b=Setup();l=b.ActiveLevelData;b.ObstacleStateService.buried.Add(40);b.ObstacleStateService.buried.Add(44);Hatch(b,60);Tick(b,20);Check(b.jobs==1&&Progress(b,40).Remaining==6,""covered fruit defers brood without losing it"");b.ObstacleStateService.buried.Clear();Tick(b);Check(Progress(b,40).Remaining==3&&b.jobs==0,""reveal resumes deferred brood"");Call(b.FlyFruits,""OnDisable"");
  foreach(int ticks in new[]{0,5,19,29,31}){
   b=Setup();b.ObstacleStateService.ApplyNew(0,ObstacleId.FlyEggBox,ObstacleHitContext.SpecialActivation);Hatch(b,60);Tick(b,ticks);var service=b.FlyFruits;Call(service,""OnDisable"");Check(b.jobs==0&&b.FlyFruits==null&&b.ObstacleStateService.FlyEggBoxHitInterceptor==null,""teardown cancels delivery/brood,unbinds callbacks and all jobs"");
  }
  b=Setup();l=b.ActiveLevelData;var normal=b.tiles[3,3];int version=normal.LifetimeVersion;normal.IsRuntimeIdle=false;Check(!b.TryPlaceFlyEgg(normal,version),""no placement in falling stone"");normal.IsRuntimeIdle=true;normal.LifetimeVersion++;Check(!b.TryPlaceFlyEgg(normal,version),""reused view lifetime rejected"");Check(b.TryPlaceFlyEgg(normal,normal.LifetimeVersion),""idle current normal stone accepts movable egg"");Call(b.FlyFruits,""OnDisable"");
  l=new LevelData();l.goals=new[]{new LevelGoalDefinition{targetType=LevelGoalTargetType.Obstacle,obstacleId=ObstacleId.FlyEggBox},new LevelGoalDefinition{targetType=LevelGoalTargetType.Obstacle,obstacleId=ObstacleId.FlyEgg}};
  Check(EditorResize.TryStampFruit(l,18,2,2)&&l.obstacles[27]==(int)ObstacleId.FlyFruit&&l.GetFruitDimensions(18).x==2,""editor places chosen2x2 footprint in one click"");
  Check(l.goals.Length==1&&l.goals[0].amount==1&&l.goals[0].obstacleId==ObstacleId.FlyFruit,""pre-sized fruit still one goal;box/egg goals removed"");
  var saved=l.obstacles.ToArray();Check(!EditorResize.TryStampFruit(l,18,1,1)&&l.GetFruitDimensions(18).x==2&&saved.SequenceEqual(l.obstacles),""clicking existing fruit does not resize or overwrite it"");
  Check(!EditorResize.TryStampFruit(l,19,2,2)&&saved.SequenceEqual(l.obstacles),""partly overlapping placement rejected atomically"");
  l.cells[20]=0;Check(!EditorResize.TryStampFruit(l,20,2,2)&&saved.SequenceEqual(l.obstacles),""hole rejected before writing"");Check(!EditorResize.TryStampFruit(l,63,2,2)&&saved.SequenceEqual(l.obstacles),""out-of-bounds rejected atomically"");
  var q=new LevelData();q.stackedObstacles=new[]{new StackedObstacleEntry{originCellIndex=27,obstacleId=ObstacleId.Grass}};
  Check(!EditorResize.TryStampFruit(q,18,2,2)&&q.obstacles.All(v=>v==0),""overlay blocks placement even away from origin"");q.stackedObstacles=null;
  q.safes=new[]{new SafeEntry{originCellIndex=27,width=2,height=2}};Check(!EditorResize.TryStampFruit(q,18,2,2),""safe footprint protected"");q.safes=null;
  q.pinnedSpecialTypes=new int[64];q.pinnedSpecialTypes[27]=1;Check(!EditorResize.TryStampFruit(q,18,2,2),""authored special protected"");q.pinnedSpecialTypes=null;
  q.waterChannels=new[]{new WaterChannelEntry{pathCellIndices=new[]{27,35}}};Check(!EditorResize.TryStampFruit(q,18,2,2),""water channel protected"");
  for(int cell=0;cell<64;cell++)if(l.obstacleOrigins[cell]==18){l.obstacles[cell]=0;l.obstacleOrigins[cell]=-1;}
  Check(l.SynchronizeFruitGoals()&&l.goals.Length==0&&l.fruits.Length==0,""erase prunes dimensions and final fruit goal"");
  b=Setup();l=b.ActiveLevelData;foreach(var tile in b.tiles)tile.IsRuntimeIdle=false;
  Check(b.ObstacleStateService.ApplyNew(0,ObstacleId.FlyEggBox,ObstacleHitContext.SpecialActivation).Accepted,""launch can wait for an idle destination"");Tick(b,80);
  Check(b.ObstacleStateService.FlyEggBoxRemainingQuery(0)==4&&b.ObstacleStateService.boxClears==0&&b.jobs==0,""timeout restores egg slot without retiring source"");Call(b.FlyFruits,""OnDisable"");
  b=Setup();l=b.ActiveLevelData;b.ObstacleStateService.ApplyNew(0,ObstacleId.FlyEggBox,ObstacleHitContext.SpecialActivation);Tick(b,8);b.tiles[2,0].IsPlannedToMoveThisFallPass=true;Tick(b);
  Check(!b.tiles[2,0].movable&&l.obstacles.Count(id=>id==(int)ObstacleId.FlyEgg)==1,""delivery reroutes if selected normal stone starts falling"");Call(b.FlyFruits,""OnDisable"");
  b=Setup();var retained=b.FlyFruits;int natives=GameObject.created;Hatch(b,60);Hatch(b,61);Tick(b);
  Check(GameObject.created==natives,""warmed hatch/flight path creates no new visual GameObjects in doubles"");Call(retained,""OnDisable"");
  b=Setup();retained=b.FlyFruits;b.cleared=id=>{if(id==ObstacleId.FlyFruit)Call(retained,""OnDisable"");};Hatch(b,60);Hatch(b,61);Tick(b,20);
  for(int i=0;i<40&&b.FlyFruits!=null;i++)Tick(b,1);
  Check(b.FlyFruits==null&&b.jobs==0,""goal callback can disable service during arrival without list mutation or leaked jobs"");
  var def=new Library().Get(ObstacleId.FlyEgg);var root=new GameObject().transform;var fly=FlyFruitFlyView.Rent(root,def,100);
  fly.Pose(new Vector2(0,0),new Vector2(1,0),.2f,1,0,true);var body=(Image)Field(fly,""body"");Check(ReferenceEquals(body.sprite,def.auxiliarySprites[0]),""rise/split use supplied front body"");
  fly.Pose(new Vector2(10,0),new Vector2(1,1),.3f,1,0,false);Check(ReferenceEquals(body.sprite,def.auxiliarySprites[3])&&fly.transform.localScale.x>0,""right flight uses profile body"");
  fly.Pose(new Vector2(-10,0),new Vector2(-1,1),.4f,1,0,false);Check(fly.transform.localScale.x<0,""left flight mirrors right profile"");fly.Return();
  Check(UiVfxPool.rented==UiVfxPool.returned,""all rented fly/egg visuals returned after teardown"");
  b=Setup();l=b.ActiveLevelData;
  l.obstacleLibrary.Get(ObstacleId.FlyEggBox).flightLoopSound=new AudioClip();l.obstacleLibrary.Get(ObstacleId.FlyEggBox).breakSound=new AudioClip();
  l.obstacleLibrary.Get(ObstacleId.FlyEgg).flightLoopSound=new AudioClip();l.obstacleLibrary.Get(ObstacleId.FlyFruit).hitSound=new AudioClip();
  for(int i=0;i<4;i++)b.ObstacleStateService.ApplyNew(0,ObstacleId.FlyEggBox,ObstacleHitContext.SpecialActivation);
  Tick(b);Check(b.Audio.calls==5,""four launches and one source break request central one-shots"");
  Hatch(b,60);Hatch(b,61);Tick(b,1);Check(b.Audio.loops==1&&b.Audio.starts==1,""concurrent broods share one central buzz lease"");
  Tick(b);Check(b.Audio.calls==11&&b.Audio.loops==0,""six bite requests and loop release after final landing"");
  Hatch(b,62);Tick(b,1);Check(b.Audio.loops==1,""new brood restarts central buzz"");Call(b.FlyFruits,""OnDisable"");Check(b.Audio.loops==0&&b.jobs==0,""teardown releases sound lease and flight job"");
  Console.WriteLine(""PASS: ""+checks+"" fly-fruit counters,actual delivery/brood/render drivers,goals,placement,audio ownership and teardown checks."");
 }
}
FlyFruitChecks.Run();
";
var script=CSharpScript.Create(source.Replace("/* TESTS */",tests),ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Take(25).Select(e=>e.ToString())));
script.RunAsync().GetAwaiter().GetResult();
