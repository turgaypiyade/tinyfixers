// Profile-to-bridge mapping through production methods/assets; no Unity/project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;

CompilationUnitSyntax Read(string p)=>CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetCompilationUnitRoot();
var race=Read("Assets/_Project/Scripts/Events/BridgeRepair/BridgeRepairRace.cs");
var selected=new[]{"BuildContestants","ResolvePlayerCharacterIndex","Shuffled"};
var methods=string.Join("\n",race.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>selected.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var contestant=race.DescendantNodes().OfType<StructDeclarationSyntax>().Single(n=>n.Identifier.ValueText=="BridgeContestant").ToFullString();
var library=Read("Assets/_Project/Scripts/Core/AvatarLibrary.cs").Members.Single().ToFullString();
var provider=Read("Assets/_Project/Scripts/UI/PlayerAvatarProvider.cs").Members.Single().ToFullString();
var asset=File.ReadAllText("Assets/_Project/Resources/Profiles/AvatarLibrary.asset");
var entries=Regex.Matches(asset,@"  - \{fileID: (-?\d+), guid: ([a-f0-9]+), type: 3\}").Cast<Match>().ToArray();
var ids=Regex.Matches(asset,@"^  - (ram|bear|pig|rabbit|monkey)$",RegexOptions.Multiline).Cast<Match>().Select(m=>m.Groups[1].Value).ToArray();
var expectedIds=new[]{"ram","bear","pig","rabbit","monkey","ram","bear","monkey","rabbit","pig"};
var expectedSprites=new long[]{7579475653578040229,-6664296859188597269,1986064304,-761605233,136910328,-551442832,-1984518346,-1602835991,-842979251,-553701343};
if(!ids.SequenceEqual(expectedIds)||!entries.Select(m=>long.Parse(m.Groups[1].Value)).SequenceEqual(expectedSprites))throw new Exception("Profile atlas order/character identities no longer match the reviewed portraits");
var meta=File.ReadAllText("Assets/_Project/Art/UI/TopHUD/Avatar_Sheets.png.meta");
foreach(var entry in entries)if(entry.Groups[2].Value!="d549b445e4f7d4240a809d3614fea3a2"||!meta.Contains("internalID: "+entry.Groups[1].Value+"\n"))throw new Exception("Unresolved profile sprite");
var config=File.ReadAllText("Assets/_Project/Resources/Events/BridgeRepairConfig.asset");
var characterIds=Regex.Matches(config,@"^  - id: (\w+)$",RegexOptions.Multiline).Cast<Match>().Select(m=>m.Groups[1].Value).ToArray();
if(ids.Any(id=>!characterIds.Contains(id)))throw new Exception("Profile animal missing from BridgeRepairConfig");

var stubs=@"
using System;using System.Collections.Generic;using System.Linq;using System.Reflection;
public class SerializeField:Attribute{}public class Tooltip:Attribute{public Tooltip(string s){}}
public class CreateAssetMenu:Attribute{public string fileName,menuName;}
public class ScriptableObject{}
public class Sprite{public string name;public Sprite(string n){name=n;}}
public static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(v,b));}
public static class Resources{public static object asset;public static T Load<T>(string path) where T:class=>asset as T;}
public static class PlayerProfile{public static int AvatarId;public static string PlayerName=""Selected Fixer"";}
public class BridgeCharacterDef{public string id;}
public class BridgeRepairConfig{public int contestantCount=5;public List<BridgeCharacterDef> characters=new List<BridgeCharacterDef>();}
public static class BridgeRepairState{public static int RunSeed;public static string[] BotNames={""A"",""B"",""C"",""D""};}
";
var tests=@"
public static class Checks{
 static int count;
 static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
 static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
 public static void Run(){
  var expected=new[]{""ram"",""bear"",""pig"",""rabbit"",""monkey"",""ram"",""bear"",""monkey"",""rabbit"",""pig""};
  var sprites=expected.Select((id,i)=>new Sprite(id+i)).ToArray();var lib=new AvatarLibrary();Set(lib,""avatars"",sprites);Set(lib,""characterIds"",expected);Resources.asset=lib;
  var cfg=new BridgeRepairConfig{characters=new List<BridgeCharacterDef>{new BridgeCharacterDef{id=""bear""},new BridgeCharacterDef{id=""pig""},new BridgeCharacterDef{id=""rabbit""},new BridgeCharacterDef{id=""ram""},new BridgeCharacterDef{id=""monkey""}}};
  foreach(int seed in new[]{1,7,77,1000,int.MaxValue}){
   BridgeRepairState.RunSeed=seed;int[] lanes=null;
   for(int avatar=0;avatar<10;avatar++){
    PlayerProfile.AvatarId=avatar;var list=BridgeRepairRace.BuildContestants(cfg);var repeat=BridgeRepairRace.BuildContestants(cfg);
    Check(list.Count==5&&list[0].isPlayer&&list[0].slot==0,""player stays in player slot"");
    Check(cfg.characters[list[0].characterIndex].id==expected[avatar],""all profile variants select their own animal across seeds"");
    Check(ReferenceEquals(list[0].avatar,sprites[avatar])&&list[0].displayName==PlayerProfile.PlayerName,""portrait/name and bridge character share current profile"");
    Check(list.Select(c=>c.characterIndex).Distinct().Count()==5,""reserve player animal while preserving distinct bot characters"");
    Check(list.Select(c=>c.lane).Distinct().Count()==5,""each contestant retains a unique lane"");
    Check(list.Select(c=>c.characterIndex).SequenceEqual(repeat.Select(c=>c.characterIndex)),""reopening never rerolls player or bots"");
    if(lanes==null)lanes=list.Select(c=>c.lane).ToArray();
    Check(list.Select(c=>c.lane).SequenceEqual(lanes),""profile change does not move race lanes or consume extra RNG"");
   }
  }
  cfg.characters.Reverse();PlayerProfile.AvatarId=0;
  var reordered=BridgeRepairRace.BuildContestants(cfg);
  Check(cfg.characters[reordered[0].characterIndex].id==""ram"",""mapping uses stable identity instead of config list index"");
  cfg.characters[reordered[0].characterIndex].id=""RAM"";
  Check(cfg.characters[BridgeRepairRace.BuildContestants(cfg)[0].characterIndex].id==""RAM"",""character identity matching is case insensitive"");
  PlayerProfile.AvatarId=-100;Check(lib.GetCharacterId(-100)==""ram""&&ReferenceEquals(PlayerAvatarProvider.Current,sprites[0]),""low out-of-range profile matches portrait fallback"");
  PlayerProfile.AvatarId=100;Check(lib.GetCharacterId(100)==""pig""&&ReferenceEquals(PlayerAvatarProvider.Current,sprites[9]),""high out-of-range profile matches portrait fallback"");
  cfg.characters=new List<BridgeCharacterDef>{new BridgeCharacterDef{id=""bear""}};
  Check(BridgeRepairRace.BuildContestants(cfg)[0].characterIndex==0,""missing animal falls back deterministically"");
  cfg.characters=null;Check(BridgeRepairRace.BuildContestants(cfg)[0].characterIndex==0,""missing character catalog is safe"");
  Set(lib,""characterIds"",null);Check(PlayerAvatarProvider.CurrentCharacterId==string.Empty,""old/unmapped avatar library is safe"");
  Set(lib,""avatars"",null);Check(lib.GetCharacterId(0)==string.Empty&&lib.Get(0)==null,""empty avatar library is safe"");
  typeof(PlayerAvatarProvider).GetField(""_lib"",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);Resources.asset=null;
  Check(PlayerAvatarProvider.CurrentCharacterId==string.Empty&&PlayerAvatarProvider.Current==null,""missing resource preserves deterministic fallback"");
  Console.WriteLine(""PASS: ""+count+"" production profile/bridge mapping checks, all 10 variants across race seeds, uniqueness, config reorder, reopen and missing-data cases. No Unity playback."");
 }
}
Checks.Run();
";
var script=CSharpScript.Create(stubs+library+provider+contestant+"public static class BridgeRepairRace{"+methods+"}"+tests,ScriptOptions.Default.WithReferences(typeof(System.Collections.Generic.HashSet<int>).Assembly));
var errors=script.Compile().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Take(20).Select(d=>d.ToString())));
script.RunAsync().GetAwaiter().GetResult();
foreach(var path in new[]{"Assets/_Project/Scripts/Events/BridgeRepair/BridgeRepairMapScreen.cs","Assets/_Project/Scripts/Events/BridgeRepair/BridgeRepairController.cs"})
 if(!File.ReadAllText(path).Contains("BridgeRepairRace.BuildContestants("))throw new Exception("Map/victory bypass the shared profile mapping");
Console.WriteLine("PASS: authored avatar mappings resolve; race map and victory both use the shared contestant selection.");
