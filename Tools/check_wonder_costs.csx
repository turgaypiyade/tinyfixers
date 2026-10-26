// Run with csi Tools/check_wonder_costs.csx. Exercises production catalog code without a Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#r "Microsoft.CodeAnalysis.Scripting"
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

var paths = Directory.GetFiles("Assets/_Project/Settings/Wonders", "Wonder_mis*.asset").OrderBy(p => p).ToArray();
var authoredCosts = paths.Select(path =>
{
    var yaml = File.ReadAllText(path);
    var hex = Regex.Match(yaml, @"(?m)^  starCosts: ([0-9a-f]+)$").Groups[1].Value;
    var bytes = Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
    var costs = Enumerable.Range(0, bytes.Length / 4).Select(i => BitConverter.ToInt32(bytes, i * 4)).ToArray();
    if (costs.Length != Regex.Matches(yaml, @"(?m)^    starCost:").Count
        || Regex.Matches(yaml, @"(?m)^    starCost: (?!0$)\d+").Count != 0)
        throw new Exception("Asset task weights changed; update fixture loading: " + path);
    return "new WonderDefinition(new[]{" + string.Join(",", costs) + "})";
});
var catalogYaml = File.ReadAllText("Assets/_Project/Settings/Wonders/WonderCatalog.asset");
foreach (var entry in new[]{"firstWonderTotalStars: 60", "secondWonderTotalStars: 75", "lastWonderTotalStars: 110"})
    if (!catalogYaml.Contains(entry)) throw new Exception("Missing production budget: " + entry);

var stubs = @"
using System;
public class ScriptableObject {}
public class Sprite {}
public class DailySlotReward {}
public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
public class TooltipAttribute : Attribute { public TooltipAttribute(string text) {} }
public class HeaderAttribute : Attribute { public HeaderAttribute(string text) {} }
public class MinAttribute : Attribute { public MinAttribute(int value) {} }
public static class Mathf {
 public static int Max(int a,int b) => Math.Max(a,b);
 public static int Clamp(int x,int a,int b) => Math.Max(a,Math.Min(b,x));
 public static float Clamp01(float x) => Math.Max(0f,Math.Min(1f,x));
 public static float Lerp(float a,float b,float t) => a+(b-a)*Clamp01(t);
 public static int RoundToInt(float x) => (int)Math.Round(x,MidpointRounding.ToEven);
}
public class WonderDefinition {
 public readonly int[] costs;
 public DailySlotReward[] chestRewards;
 public WonderDefinition(int[] costs) { this.costs=costs; }
 public int TaskCount => costs.Length;
 public int GetStarCost(int stage) => costs[stage];
}
public static class WonderOrder {
 public static int[] order;
 public static int RawIndex(WonderCatalog catalog,int slot) => order==null ? slot : order[slot];
}
";
var tests = @"
public static class Probe {
 static int checks;
 static void Check(bool ok,string why) { checks++; if(!ok) throw new Exception(why); }
 public static void Run(WonderDefinition[] definitions) {
  var cat=new WonderCatalog { wonders=definitions };
  var expected=new[]{60,75,81,87,92,98,104,110};
  Check(cat.Count==expected.Length,""production wonder count"");
  for(int slot=0;slot<cat.Count;slot++) {
   Check(cat.TotalStarsFor(slot)==expected[slot],""slot budget ""+slot);
   for(int raw=0;raw<cat.Count;raw++) {
    WonderOrder.order=new int[cat.Count];
    for(int i=0;i<cat.Count;i++) WonderOrder.order[i]=(i+raw)%cat.Count;
    int sum=0;
    for(int stage=0;stage<cat.Get(slot).TaskCount;stage++) {
     int cost=cat.GetStarCost(slot,stage);
     Check(cost>0,""production task has positive cost"");
     sum+=cost;
    }
    Check(sum==expected[slot],""exact total for every wonder in every player slot"");
   }
  }
  Check(cat.GetStarCost(-1,0)==0 && cat.GetStarCost(cat.Count,0)==0,""invalid slots"");
  Check(cat.GetStarCost(0,-1)==0 && cat.GetStarCost(0,cat.Get(0).TaskCount)==0,""invalid stages"");
  WonderOrder.order=null;
  cat.wonders=new[]{new WonderDefinition(new[]{10,1,9})};
  Check(cat.GetStarCost(0,0)==30 && cat.GetStarCost(0,1)==3 && cat.GetStarCost(0,2)==27,
        ""authored uneven task distribution is preserved without sorting"");
  cat.wonders=new[]{new WonderDefinition(new[]{1,1,1,1,1,1,1})};
  int roundedSum=0;
  for(int stage=0;stage<7;stage++) roundedSum+=cat.GetStarCost(0,stage);
  Check(roundedSum==60,""rounding retains exact budget"");
  Console.WriteLine(""PASS: ""+checks+"" catalog cost checks; all 64 wonder/slot combinations."");
 }
}
";
var production = File.ReadAllText("Assets/_Project/Scripts/UI/WonderCatalog.cs").Replace("using UnityEngine;", "");
await CSharpScript.RunAsync(stubs + production + tests + "\nProbe.Run(new[]{" + string.Join(",", authoredCosts) + "});", ScriptOptions.Default);
