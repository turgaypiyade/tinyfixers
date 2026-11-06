// Isolated production audio ownership checks. No Unity, playback, build or gameplay test.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;using System.IO;using System.Linq;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;using Microsoft.CodeAnalysis.CSharp.Scripting;using Microsoft.CodeAnalysis.Scripting;
var board=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Audio/BoardAudioDirector.cs")).GetCompilationUnitRoot();
var wanted=new[]{"StartLoopClip","Update","OnDisable","ApplySoundEnabled","PlayOneShotClip"};
var parts=string.Join("\n",board.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>wanted.Contains(m.Identifier.Text)).Select(m=>m.ToFullString()));
var lease=board.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.Text=="ClipLoop").ToFullString();
var eventSource=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Audio/EventSfx.cs")).GetCompilationUnitRoot();
var source=@"
using System;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using UnityEngine;
namespace UnityEngine {
 public class Object {public static void DontDestroyOnLoad(object o){}}
 public class MonoBehaviour:Object {public bool isActiveAndEnabled=true;public object StartCoroutine(IEnumerator e)=>null;public void StopAllCoroutines(){}}
 public class AudioClip {public float length=1;}
 public class AudioSource {public bool isPlaying,loop,mute,playOnAwake;public float volume,pitch,time,spatialBlend;public object outputAudioMixerGroup;public AudioClip clip;public int plays;public void Stop(){isPlaying=false;}public void Play(){isPlaying=true;plays++;}public void PlayOneShot(AudioClip c,float v){plays++;isPlaying=true;}}
 public class GameObject {public static int sources;public GameObject(string n){}public T AddComponent<T>() {var v=Activator.CreateInstance(typeof(T),true);if(v is AudioSource)sources++;typeof(T).GetMethod(""OnEnable"",BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(v,null);return(T)v;}}
 public static class Resources {public static T Load<T>(string p)where T:new()=>new T();}
 public static class Random {public static int Range(int a,int b)=>a;public static float Range(float a,float b)=>a;}
 public static class Time {public static float unscaledDeltaTime=.02f,unscaledTime;}
 public static class Mathf {public static float Clamp01(float x)=>Clamp(x,0,1);public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));public static float Max(float a,float b)=>Math.Max(a,b);}
}
public static class GameSettings {public static bool SoundEnabled=true;public static event Action<bool>OnSoundChanged;public static void Set(bool v){SoundEnabled=v;OnSoundChanged?.Invoke(v);}}
public class EventSfxCue {public AudioClip[]clips={new AudioClip()};public float volume=1,pitchJitter,maxDuration;}
public class EventSfxLibrary {public const string ResourcePath=""Audio"";public float botVolumeScale=.35f,ambientVolume=.3f;public AudioClip ambientLoop;}
public class GameEventSfxConfig {public object sfxGroup=new object();}
public static class ComboBehaviorEvents {public static event Action ComboTriggered;}
public static class SystemOverrideBehaviorEvents {public static event Action OverrideFanoutStarted;}
/* EVENTS */
public class BoardAudioDirector:MonoBehaviour {
 readonly List<ClipLoop>clipLoops=new List<ClipLoop>(2);AudioSource transientSource=new AudioSource(),accentSource;
 readonly Dictionary<AudioClip,float>lastClipTimes=new Dictionary<AudioClip,float>();const float SameClipWindow=.06f;
 void HandleComboTriggered(){}void HandleOverrideFanoutStarted(){}
 public BoardAudioDirector(){GameSettings.OnSoundChanged+=ApplySoundEnabled;}
 public void Tick()=>Update();public void Disable(){OnDisable();isActiveAndEnabled=false;}
 /* LEASE */
 /* METHODS */
}
public static class Checks {
 static int n;static void Check(bool v,string why){n++;if(!v)throw new Exception(why);}
 public static void Run(){
 var b=new BoardAudioDirector();var clip=new AudioClip();var loop=b.StartLoopClip(clip,.75f);var pool=(AudioSource[])typeof(EventSfx).GetField(""s_pool"",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
 Check(pool.Count(s=>s.isPlaying&&s.loop)==1,""one central loop voice"");int allocated=GameObject.sources;
 for(int i=0;i<30;i++)EventSfx.PlayTrackedClip(new AudioClip(),.5f,false);
 Check(pool.Count(s=>s.isPlaying&&s.loop)==1&&GameObject.sources==allocated,""one-shots reuse pool and preserve live loop"");
 GameSettings.Set(false);Check(pool.All(s=>!s.isPlaying&&s.mute),""sound OFF immediately stops central voices"");b.Tick();Check(pool.All(s=>!s.isPlaying),""OFF gates new playback"");
 GameSettings.Set(true);b.Tick();Check(pool.Count(s=>s.isPlaying&&s.loop)==1,""ON resumes only live loop intent"");
 GameSettings.Set(false);loop.Dispose();GameSettings.Set(true);b.Tick();Check(pool.All(s=>!s.isPlaying),""disposed while muted does not restart"");
 var first=EventSfx.PlayTrackedClip(clip,1,true);EventSfx.Stop(first);var second=EventSfx.PlayTrackedClip(clip,1,true);EventSfx.Stop(first);Check(EventSfx.IsPlaying(second),""stale lease cannot stop reused source"");EventSfx.Stop(second);
 var handles=new EventSfx.Handle[8];for(int i=0;i<8;i++)handles[i]=EventSfx.PlayTrackedClip(clip,1,true);var blocked=b.StartLoopClip(clip);b.Tick();Check(pool.Count(s=>s.isPlaying&&s.loop)==8,""saturated loops are protected"");
 EventSfx.Stop(handles[0]);b.Tick();Check(pool.Count(s=>s.isPlaying&&s.loop)==8,""waiting lease retries when voice becomes free"");
 b.Disable();foreach(var h in handles)EventSfx.Stop(h);Check(pool.All(s=>!s.isPlaying),""teardown releases owned loop"");
 Check(b.StartLoopClip(clip)==null,""disabled board rejects new loop"");Check(GameObject.sources==allocated,""no additional AudioSource allocation"");
 Console.WriteLine(""PASS: ""+n+"" central pool/settings/ownership checks; no playback."");
 }
}
Checks.Run();
";
source=source.Replace("/* EVENTS */",string.Join("\n",eventSource.Members.Select(m=>m.ToFullString()))).Replace("/* LEASE */",lease).Replace("/* METHODS */",parts)
 .Replace("using UnityEngine;", "").Replace("namespace UnityEngine {", "").Replace("}\npublic static class GameSettings", "public static class GameSettings").Replace("UnityEngine.", "");
var script=CSharpScript.Create(source,ScriptOptions.Default.AddReferences(typeof(Enumerable).Assembly));var errors=script.Compile().Where(x=>x.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(x=>x.ToString())));script.RunAsync().GetAwaiter().GetResult();
