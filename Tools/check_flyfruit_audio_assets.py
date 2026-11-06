"""Offline WAV/link/preservation checks; no sound playback or Unity operation."""
from pathlib import Path
import hashlib,json,re,wave,math,array,sys
R=Path(__file__).resolve().parents[1];D=R/'Docs/Checks/FlyFruit-Audio-2026-11-06'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
old=json.loads((D/'project-before.json').read_text())
changed=[p for p,h in old.items() if not (R/p).is_file() or sha(R/p)!=h]
allowed={'Assets/_Project/'+p for p in ['Scripts/Audio/BoardAudioDirector.cs','Scripts/Audio/EventSfx.cs','Scripts/Grid/Board/Obstacles/FlyFruitService.cs','Scripts/Grid/Board/Obstacles/FlyEggProductionFx.cs','Scripts/Editor/LevelDataEditor.cs','Settings/ObstacleLibrary.asset']}
audio_only='--audio-only' in sys.argv
if not audio_only:assert set(changed)==allowed,changed
lib=(R/'Assets/_Project/Settings/ObstacleLibrary.asset').read_text()
blocks=lambda s:{int(m[1]):m[0] for m in re.finditer(r'^  - id: (\d+)\n.*?(?=^  - id:|\Z)',s,re.M|re.S)}
a,b=blocks((D/'Before/Assets/_Project/Settings/ObstacleLibrary.asset').read_text()),blocks(lib)
assert all(a[k]==b[k] for k in a if k not in [100,101,102])
for id in [100,101,102]:
    primary=re.search(r'^    - sprite: (.*)$',b[id],re.M)[1]
    assert re.search(r'^    legacySprite: (.*)$',b[id],re.M)[1]==primary
    assert '    wardrobeItemSprites: []\n' in b[id]
settings={100:{'breakSound':'BoxBreak','flightLoopSound':'EggFlight'},101:{'breakSound':'EggBreak','flightLoopSound':'FlyBuzzLoop'},102:{'hitSound':'AppleBite'}}
results=json.loads((D/'processing.json').read_text());measured={}
for name,v in results.items():
    p=R/'Assets/_Project/Audio/SFX/FlyFruit'/(name+'.wav')
    assert sha(D/'Sources'/v['source'])==v['sourceSHA256']==sha(Path('/Users/turgayp/Downloads')/v['source'])
    with wave.open(str(p)) as w:
        assert (w.getnchannels(),w.getframerate(),w.getsampwidth())==(1,48000,2)
        x=array.array('h',w.readframes(w.getnframes()));dur=len(x)/48000
    peak=max(abs(i) for i in x)/32768;assert 0<peak<.7
    assert abs(dur-v['after']['duration'])<1/48000
    measured[name]={'seconds':round(dur,4),'peakDB':round(20*math.log10(peak),2),'loopSeamDelta':round(abs(x[0]-x[-1])/32768,6) if v['loop'] else None}
    if v['loop']:assert abs(x[0]-x[-1])/32768<.005
    else:assert x[0]==x[-1]==0
    guid=re.search(r'^guid: (\w+)$',p.with_suffix('.wav.meta').read_text(),re.M)[1]
    for id,fields in settings.items():
        for field,n in fields.items():
            if n==name:assert f'    {field}: {{fileID: 8300000, guid: {guid}, type: 3}}' in b[id]
report={'result':'PASS','changedExisting':changed,'preservedExisting':len(old)-len(changed),'preservationAsserted':not audio_only,'outsideTaskChanges':sorted(set(changed)-allowed),'allLevelFilesPreserved':None if audio_only else not any('/ProLevels/' in p for p in changed),'sourceFilesUnchanged':True,'waves':measured,'manualUnityTest':False}
(D/('audio-validation.json' if audio_only else 'validation.json')).write_text(json.dumps(report,indent=2)+'\n')
print('PASS: five WAVs, clip links, legacy sprite fallbacks and originals.'+(' Preservation audit excluded explicitly.' if audio_only else ' All levels preserved.'))
