"""Offline trim/gain/peak-control of supplied WAVs; preserve originals, no playback."""
from pathlib import Path
import subprocess,array,math,wave,json,uuid,hashlib,re
R=Path(__file__).resolve().parents[1];D=R/'Docs/Checks/FlyFruit-Audio-2026-11-06';A=R/'Assets/_Project/Audio/SFX/FlyFruit'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
guid=lambda n:uuid.uuid5(uuid.NAMESPACE_URL,'tinyfixers/flyfruit/audio/'+n).hex
def stats(x):
    return dict(duration=len(x)/48000,rmsDB=20*math.log10(max(1e-12,math.sqrt(sum(v*v for v in x)/max(1,len(x))))),peakDB=20*math.log10(max(1e-12,max(map(abs,x),default=0))))
def decode(p,filters=None):
    cmd=['ffmpeg','-v','error','-i',str(p)]
    if filters:cmd+=['-af',filters]
    raw=subprocess.check_output(cmd+['-ac','1','-ar','48000','-f','f32le','-']);x=array.array('f');x.frombytes(raw);return x
def write(p,x):
    samples=array.array('h',(round(max(-.999,min(.999,v))*32767) for v in x))
    with wave.open(str(p),'wb')as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(48000);f.writeframes(samples.tobytes())
def main():
    A.mkdir(exist_ok=True);Path(str(A)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid("folder")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    settings={'applebit':('AppleBite',-25,-7,.82),'eggbreak':('EggBreak',-22,-6,.85),'eggfly':('EggFlight',-20.5,-5,1.0),'flyflying':('FlyBuzzLoop',-19,-8,.9),'woodencrack':('BoxBreak',-20.5,-4,.9)}
    template=(R/'Assets/_Project/Audio/SFX/WaterLily_Swallow.wav.meta').read_text();results={}
    for src,(name,target,ceiling,volume)in settings.items():
        p=D/'Sources'/(src+'.wav');original=decode(p)
        # Work on a mono copy; remove only outer silence, retaining internal pauses.
        x=decode(p,'highpass=f=65,lowpass=f='+('6500'if src=='flyflying'else'10000'))
        start=0;end=len(x);window=240;gate=10**(-50/20)
        active=[i for i in range(0,len(x),window)if math.sqrt(sum(v*v for v in x[i:i+window])/max(1,len(x[i:i+window])))>gate]
        if active:start=max(0,active[0]-144);end=min(len(x),active[-1]+window+576)
        x=list(x[start:end]);loop=src=='flyflying'
        if loop:
            n=min(960,len(x)//4);x=x[n:-n]+[x[-n+i]*(1-i/(n-1))+x[i]*i/(n-1)for i in range(n)]
            rotation=min(range(min(240,len(x))),key=lambda i:abs(x[i]));x=x[rotation:]+x[:rotation]
        else:
            for i in range(min(144,len(x))):x[i]*=i/144
            for i in range(min(384,len(x))):x[-1-i]*=i/384
        gain=10**((target-stats(x)['rmsDB'])/20);limit=10**(ceiling/20)
        # Soft saturation above75% of the ceiling; constrain peaks without raising every transient.
        def control(v):
            v*=gain;a=abs(v);k=limit*.75
            return math.copysign(k+(limit-k)*math.tanh((a-k)/(limit-k)),v)if a>k else v
        y=[control(v)for v in x];out=A/(name+'.wav');write(out,y)
        meta=re.sub(r'^guid: .*$',f'guid: {guid(name)}',template,flags=re.M);(A/(name+'.wav.meta')).write_text(meta)
        results[name]=dict(source=p.name,sourceSHA256=sha(p),before=stats(original),after=stats(y),trimStartSeconds=start/48000,trimEndSeconds=(len(original)-end)/48000,loop=loop,mixVolume=volume,sourceUnchanged=True)
    lib=R/'Assets/_Project/Settings/ObstacleLibrary.asset';s=lib.read_text()
    fields={100:{'breakSound':('BoxBreak',.9),'flightLoopSound':('EggFlight',1.0)},101:{'breakSound':('EggBreak',.85),'flightLoopSound':('FlyBuzzLoop',.9)},102:{'hitSound':('AppleBite',.82)}}
    for id,values in fields.items():
        b=re.search(r'^  - id: '+str(id)+r'\n.*?(?=^  - id:|\Z)',s,re.M|re.S)[0];new=b
        for field,(name,volume)in values.items():
            new=re.sub(r'^    '+field+r': .*$',f'    {field}: {{fileID: 8300000, guid: {guid(name)}, type: 3}}',new,flags=re.M)
            new=re.sub(r'^    '+field+r'Volume: .*$',f'    {field}Volume: {volume}',new,flags=re.M)
        s=s.replace(b,new)
    lib.write_text(s);(D/'processing.json').write_text(json.dumps(results,indent=2)+'\n')
    print(json.dumps({n:{'seconds':round(v['after']['duration'],3),'rmsDB':round(v['after']['rmsDB'],1),'peakDB':round(v['after']['peakDB'],1),'mixVolume':v['mixVolume']}for n,v in results.items()},indent=2))
if __name__=='__main__':main()
