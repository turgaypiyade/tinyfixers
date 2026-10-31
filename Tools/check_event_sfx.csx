// Isolated central EventSfx lifecycle/ownership tests; no Unity or project build.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
foreach(var file in new[]{"Assets/_Project/Scripts/Audio/EventSfx.cs","Assets/_Project/Scripts/Audio/EventSfxLibrary.cs","Assets/_Project/Scripts/Events/GiftFestival/GiftFestivalScreen.cs"}){
 var errors=CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.Preview)).GetDiagnostics().Where(x=>x.Severity==DiagnosticSeverity.Error).ToArray();
 if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(x=>x.ToString())));
}
string production=Regex.Replace(File.ReadAllText("Assets/_Project/Scripts/Audio/EventSfx.cs"),@"(?m)^using [^\r\n]+;\r?$","")
 .Replace("UnityEngine.Object","FakeObject").Replace("UnityEngine.Random","FakeRandom");
string harness=@"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
public class AudioClip {public float length=1;}
public class AudioMixerGroup {}
public class AudioSource {
 public static List<AudioSource> All=new List<AudioSource>();
 public AudioClip clip;public float volume,pitch,time,spatialBlend;public bool isPlaying,loop,mute,playOnAwake;
 public AudioMixerGroup outputAudioMixerGroup;public void Play(){isPlaying=true;}public void Stop(){isPlaying=false;}
}
public class Coroutine {public IEnumerator Routine;}
public class MonoBehaviour {
 public static MonoBehaviour Last;public readonly List<Coroutine> Pending=new List<Coroutine>();
 public Coroutine StartCoroutine(IEnumerator r){var c=new Coroutine{Routine=r};Pending.Add(c);r.MoveNext();return c;}
 public void StopAllCoroutines(){foreach(var c in Pending)(c.Routine as IDisposable)?.Dispose();Pending.Clear();}
}
public class GameObject {
 public GameObject(string name){}
 public T AddComponent<T>(){
  var x=(T)Activator.CreateInstance(typeof(T),true);
  if(x is AudioSource source)AudioSource.All.Add(source);
  if(x is MonoBehaviour b){MonoBehaviour.Last=b;Life(b,""OnEnable"");}return x;
 }
 public static void Life(object x,string method)=>x.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(x,null);
}
public static class FakeObject {public static void DontDestroyOnLoad(object x){}}
public static class FakeRandom {public static int Range(int a,int b)=>a;public static float Range(float a,float b)=>(a+b)*.5f;}
public static class Time {public static float unscaledDeltaTime=.02f;}
public static class Mathf {
 public static float Clamp01(float n)=>Math.Max(0,Math.Min(1,n));public static float Clamp(float n,float a,float b)=>Math.Max(a,Math.Min(b,n));public static float Max(float a,float b)=>Math.Max(a,b);
}
public static class GameSettings {
 public static bool SoundEnabled=true;public static event Action<bool> OnSoundChanged;
 public static void Set(bool value){SoundEnabled=value;OnSoundChanged?.Invoke(value);}
 public static int Subscribers=>OnSoundChanged?.GetInvocationList().Length??0;
}
public class EventSfxCue {public AudioClip[] clips={new AudioClip()};public float volume=.8f,pitchJitter,maxDuration;}
public class EventSfxLibrary {
 public const string ResourcePath=""Audio/EventSfxLibrary"";
 public float botVolumeScale=.35f,ambientVolume=.35f;public AudioClip ambientLoop=new AudioClip();
 public EventSfxCue giftFestivalIntro=new EventSfxCue(),giftFestivalThrow=new EventSfxCue(),giftFestivalBatHit=new EventSfxCue(),giftFestivalBallFlight=new EventSfxCue(),giftFestivalGlassBreak=new EventSfxCue();
}
public class GameEventSfxConfig {public AudioMixerGroup sfxGroup=new AudioMixerGroup();}
public static class Resources {
 public static EventSfxLibrary Lib=new EventSfxLibrary();public static GameEventSfxConfig Config=new GameEventSfxConfig();
 public static T Load<T>(string path)where T:class=>typeof(T)==typeof(EventSfxLibrary)?Lib as T:Config as T;
}
"+production+@"
public static class Checks {
 static int count;static void Check(bool ok,string msg){count++;if(!ok)throw new Exception(msg);}
 public static int Run(){
  GameSettings.Set(false);var muted=EventSfx.PlayTracked(x=>x.giftFestivalIntro);
  Check(AudioSource.All.Count==0,""Sound off gates new playback without allocating a pool"");EventSfx.Stop(muted);
  GameSettings.Set(true);var first=EventSfx.PlayTracked(x=>x.giftFestivalIntro);
  Check(AudioSource.All.Count==9&&GameSettings.Subscribers==1,""One shared eight-voice pool plus ambient and one settings binding"");
  foreach(var source in AudioSource.All)Check(source.outputAudioMixerGroup==Resources.Config.sfxGroup,""Every voice routes through the central SFX mixer group"");
  var second=EventSfx.PlayTracked(x=>x.giftFestivalThrow);Check(AudioSource.All.Count==9,""Repeated playback reuses sources"");
  EventSfx.Stop(first);Check(!AudioSource.All[0].isPlaying&&AudioSource.All[0].clip==null&&AudioSource.All[1].isPlaying,""Scoped cancellation stops only its owned voice and clears the clip"");
  var replacement=EventSfx.PlayTracked(x=>x.giftFestivalBatHit);EventSfx.Stop(first);
  Check(AudioSource.All[0].isPlaying,""Stale ownership token cannot stop a reused source"");
  EventSfx.StartAmbient();Check(AudioSource.All[8].isPlaying,""Ambient uses the shared loop source"");
  var limited=new EventSfxCue{maxDuration=.2f};EventSfx.Play(x=>limited);
  Check(MonoBehaviour.Last.Pending.Count==1,""Legacy duration-limited cues still schedule a fade"");
  GameSettings.Set(false);
  for(int i=0;i<8;i++)Check(!AudioSource.All[i].isPlaying&&AudioSource.All[i].mute&&AudioSource.All[i].clip==null,""Settings off immediately stops and mutes every active transient"");
  Check(MonoBehaviour.Last.Pending.Count==0,""Settings off cancels pending fade callbacks"");
  Check(AudioSource.All[8].mute,""Settings off immediately mutes active ambient"");
  EventSfx.PlayTracked(x=>x.giftFestivalGlassBreak);Check(AudioSource.All.Count==9,""Muted cues do not create fallback sources"");
  GameSettings.Set(true);
  Check(!AudioSource.All[0].isPlaying&&AudioSource.All[8].isPlaying&&!AudioSource.All[8].mute,""Unmute restores ambient but never replays stale one-shots"");
  var next=EventSfx.PlayTracked(x=>x.giftFestivalBallFlight,pitch:1.5f);
  Check(AudioSource.All[0].isPlaying&&AudioSource.All[0].pitch==1.5f&&!AudioSource.All[0].mute,""Fresh playback respects pitch and newly enabled sound"");
  EventSfx.Stop(replacement);Check(AudioSource.All[0].isPlaying,""A handle invalidated by mute cannot stop a later voice"");
  EventSfx.Stop(next);Check(!AudioSource.All[0].isPlaying,""Screen-close cancellation stops flight immediately"");
  var runner=MonoBehaviour.Last;GameObject.Life(runner,""OnDisable"");
  Check(GameSettings.Subscribers==0&&!AudioSource.All[8].isPlaying,""Teardown unsubscribes settings and stops ambient"");
  GameObject.Life(runner,""OnDestroy"");
  Check(typeof(EventSfx).GetField(""s_pool"",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)==null,""Teardown releases static source references"");
  return count;
 }
}
Checks.Run()";
int checks=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default).GetAwaiter().GetResult();
Console.WriteLine("PASS: "+checks+" central SFX pool, mixer routing, mute, ownership and teardown checks.");
