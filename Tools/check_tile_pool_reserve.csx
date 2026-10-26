// Real board pool/Cargo handoff methods with Unity doubles. No project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var names=new[]{"WarmTilePool","AcquireTileObject","ReleaseTile","ClearTilePool","CargoExitRoutine"};
var root=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/BoardController.cs")).GetRoot();
var methods=string.Join("\n",root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString())).Replace("private IEnumerator CargoExitRoutine", "public IEnumerator CargoExitRoutine");
var stubs=@"
using System;using System.Collections;using System.Collections.Generic;
public class Transform {public Transform parent;public void SetParent(Transform p,bool world){parent=p;}}
public class RectTransform:Transform {}
public class GameObject {
 public bool activeSelf=true,destroyed;public TileView tile;public Transform transform=new Transform();
 public T GetComponent<T>() where T:class=>tile as T;public void SetActive(bool on){activeSelf=on;}
}
public class TileView {
 public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;
 public int X,Y,LifetimeVersion,prepares;public TileView(){gameObject.tile=this;}
 public void PrepareForRelease(){prepares++;LifetimeVersion++;}
 public static implicit operator bool(TileView t)=>t!=null&&!t.gameObject.destroyed;
 public static bool operator!(TileView t)=>t==null||t.gameObject.destroyed;
}
public static class Mathf {public static int Clamp(int n,int a,int b)=>Math.Max(a,Math.Min(b,n));}
public class GhostFx {
 public int captures;public IEnumerator PlayCargoExit(TileView tile,RectTransform target,float seconds){captures++;yield return null;}
}
public class PoolProbe {
 public bool useTilePool=true;public int tilePoolWarmCount=24,width=3,height=3,created,destroyed,holds,flights,releases;
 public GameObject tilePrefab=new GameObject();public Transform parent=new Transform();public GhostFx GoalFlyFx=new GhostFx();
 public TileView live,selected;public IEnumerator child;
 public Queue<TileView> _tilePool=new Queue<TileView>();public HashSet<TileView> gelCleanMatchTiles=new HashSet<TileView>();
 GameObject Instantiate(GameObject prefab,Transform parent){created++;var tile=new TileView();tile.transform.SetParent(parent,false);return tile.gameObject;}
 void Destroy(GameObject go){destroyed++;go.destroyed=true;}
 void ClearCellsReferencing(TileView tile){if(ReferenceEquals(live,tile))live=null;}
 void ReleaseHoldsVacatedAt(int x,int y){holds++;}
 void BeginGoalOrbFlight(){flights++;}void EndGoalOrbFlight(){flights--;}
 IEnumerator StartCoroutine(IEnumerator routine){child=routine;routine.MoveNext();return routine;}
 /* METHODS */
}
".Replace("/* METHODS */",methods);
var tests=@"
int checks=0;Action<bool,string> check=(ok,name)=>{checks++;if(!ok)throw new Exception(name);};
var p=new PoolProbe();p.WarmTilePool();
check(p.created==9&&p._tilePool.Count==9,""prewarm reserve is capped at board cell count"");
p.WarmTilePool();check(p.created==9,""warming twice never duplicates reserve"");
var go=p.AcquireTileObject(p.parent);var tile=go.tile;
check(go.activeSelf&&p.created==9&&p._tilePool.Count==8,""first acquire reuses prewarmed object"");
p.live=tile;p.gelCleanMatchTiles.Add(tile);p.ReleaseTile(tile);
check(p.live==null&&!go.activeSelf&&!p.gelCleanMatchTiles.Contains(tile)&&p._tilePool.Count==9,""release detaches board ref, clears per-life state and parks inactive"");
p.ReleaseTile(tile);check(p._tilePool.Count==9,""duplicate release never duplicates pool entry"");
p.ClearTilePool();check(p._tilePool.Count==0&&p.destroyed==9,""level reset destroys and forgets old reserve"");
p.useTilePool=false;p.WarmTilePool();check(p.created==9,""pool-off path does not preallocate"");
go=p.AcquireTileObject(p.parent);p.ReleaseTile(go.tile);check(p.created==10&&p.destroyed==10,""explicit pool-off mode retains old creation/release behavior"");
p=new PoolProbe();tile=new TileView();var run=p.CargoExitRoutine(tile,null);
check(run.MoveNext()&&p.GoalFlyFx.captures==1&&!tile.gameObject.activeSelf&&p._tilePool.Count==1,""Cargo ghost captures before source returns to pool"");
var same=p.AcquireTileObject(p.parent);check(object.ReferenceEquals(same,tile.gameObject),""Cargo source available during flight"");
tile.LifetimeVersion++;p.live=tile;
while(p.child.MoveNext()){}while(run.MoveNext()){}
check(p._tilePool.Count==0&&tile.gameObject.activeSelf&&object.ReferenceEquals(p.live,tile)&&p.flights==0,""old Cargo completion cannot release a reused lifetime"");
p=new PoolProbe();tile=new TileView();run=p.CargoExitRoutine(tile,null);run.MoveNext();((IDisposable)run).Dispose();
check(p._tilePool.Count==1&&p.flights==0,""interrupted flight balances hold without a second release"");
p=new PoolProbe();p.GoalFlyFx=null;tile=new TileView();run=p.CargoExitRoutine(tile,null);
check(!run.MoveNext()&&p._tilePool.Count==1&&p.flights==0,""missing VFX still releases Cargo and flight job"");
Console.WriteLine(""PASS: ""+checks+"" production tile reserve/reuse/Cargo cancellation checks; no Unity build."");
";
await CSharpScript.RunAsync(stubs+tests,ScriptOptions.Default.AddReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
