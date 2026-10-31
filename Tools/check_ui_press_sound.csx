// Production press component/classification with UI/audio doubles; no Unity build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
string component=File.ReadAllText("Assets/_Project/Scripts/UI/UiClickSound.cs");
string global=File.ReadAllText("Assets/_Project/Scripts/UI/UiClickSounds.cs");
var tree=CSharpSyntaxTree.ParseText(global).GetRoot();
string classify="public class UiClickSounds {"+string.Join("\n",tree.DescendantNodes().OfType<MethodDeclarationSyntax>()
 .Where(m=>new[]{"Classify","AutoClassify"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()))
 +string.Join("\n",tree.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(n=>n.Declaration.Variables.Any(v=>new[]{"BackWords","ConfirmWords","OkNames"}.Contains(v.Identifier.ValueText))).Select(n=>n.ToFullString()))+"}";
foreach(var p in new[]{"Assets/_Project/Scripts/UI/UiClickSound.cs","Assets/_Project/Scripts/UI/UiClickSounds.cs","Assets/_Project/Scripts/UI/PreLevelSpecialPopupController.cs"}){
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(d=>d.ToString())));
}
var harness=@"
using System;
public class DisallowMultipleComponent:Attribute {}public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
public interface IPointerDownHandler {void OnPointerDown(PointerEventData e);}
public class PointerEventData {public enum InputButton {Left,Right,Middle}public InputButton button;}
public class AudioClip {}
public class GameObject {public string name=""ContinueButton"";}
public class MonoBehaviour {public bool isActiveAndEnabled=true;public Selectable targetStub;public T GetComponent<T>() where T:class=>targetStub as T;}
public class Selectable {public GameObject gameObject=new GameObject();public bool enabled=true;public UiClickSound custom;
 public bool IsInteractable()=>enabled;public bool TryGetComponent<T>(out T t) where T:class{t=custom as T;return t!=null;}public T GetComponentInParent<T>() where T:class=>null;
}
public class PreLevelSpecialPopupController {public bool HasCustomClickSound(Selectable s)=>false;}
public class BoosterSlotView {}
public static class GameEventSfx {public enum UiSound {Tap,Confirm,Back,Denied}public static bool enabled=true;public static int plays;public static AudioClip clip;public static UiSound last;
 public static void PlayUiClip(AudioClip c){if(enabled){plays++;clip=c;}}public static void PlayUi(UiSound s){if(enabled){plays++;last=s;}}
}
/* COMPONENT */
/* CLASSIFY */
int checks=0;Action<bool,string> check=(ok,m)=>{checks++;if(!ok)throw new Exception(m);};
var target=new Selectable();var sound=new UiClickSound{mode=UiClickSound.Mode.Confirm,playOnPress=true,targetStub=target};target.custom=sound;
var down=new PointerEventData();sound.OnPointerDown(down);
check(GameEventSfx.plays==1&&GameEventSfx.last==GameEventSfx.UiSound.Confirm,""press plays immediately before any click/release handler"");
check(UiClickSounds.Classify(target)==null,""global release handler cannot replay a component-owned press"");
sound.clipOverride=new AudioClip();sound.OnPointerDown(down);check(GameEventSfx.plays==2&&GameEventSfx.clip==sound.clipOverride,""custom clip uses the central UI clip API"");
GameEventSfx.enabled=false;sound.OnPointerDown(down);check(GameEventSfx.plays==2,""sound setting gates press playback"");GameEventSfx.enabled=true;
target.enabled=false;sound.OnPointerDown(down);check(GameEventSfx.plays==2,""disabled button is silent"");target.enabled=true;
sound.OnPointerDown(new PointerEventData{button=PointerEventData.InputButton.Right});check(GameEventSfx.plays==2,""right click is silent"");
sound.mode=UiClickSound.Mode.None;sound.OnPointerDown(down);check(GameEventSfx.plays==2,""explicit None remains silent even with a clip override"");
sound.mode=UiClickSound.Mode.Auto;sound.clipOverride=null;sound.OnPointerDown(down);check(GameEventSfx.plays==3&&GameEventSfx.last==GameEventSfx.UiSound.Confirm,""Auto retains name-based confirmation classification"");
sound.playOnPress=false;sound.OnPointerDown(down);check(GameEventSfx.plays==3&&UiClickSounds.Classify(target)==GameEventSfx.UiSound.Confirm,""existing release-based buttons retain their behavior"");
sound.playOnPress=true;sound.isActiveAndEnabled=false;sound.OnPointerDown(down);check(GameEventSfx.plays==3,""inactive component does not play"");
Console.WriteLine(""PASS: ""+checks+"" production press timing, duplicate prevention, clip routing, disabled/None and settings checks. UI doubles; no Unity build."");
".Replace("/* COMPONENT */",Regex.Replace(component,@"(?m)^using [^\r\n]+;\r?$", ""))
 .Replace("/* CLASSIFY */",classify);
await CSharpScript.RunAsync(harness,ScriptOptions.Default);
