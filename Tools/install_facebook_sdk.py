"""Install only the official Meta Unity SDK Assets; no Unity/build invocation or credentials."""
from pathlib import Path, PurePosixPath
import io,json,tarfile,urllib.request,zipfile,hashlib
ROOT=Path(__file__).resolve().parents[1]
TAG='sdk-version-19.0.0'
info=json.load(urllib.request.urlopen('https://api.github.com/repos/meta/facebook-sdk-for-unity/releases/tags/'+TAG,timeout=30))
asset=next(a for a in info['assets'] if a['name']=='facebook-unity-sdk-19.0.0.zip')
raw=urllib.request.urlopen(asset['browser_download_url'],timeout=120).read()
digest=hashlib.sha256(raw).hexdigest()
if asset.get('digest'):assert asset['digest']=='sha256:'+digest,'Release digest mismatch'
z=zipfile.ZipFile(io.BytesIO(raw))
packages=[n for n in z.namelist() if n.endswith('.unitypackage')]
print('Official SDK archive:',len(raw),'bytes; packages:',packages)
assert len(packages)==1
package=tarfile.open(fileobj=io.BytesIO(z.read(packages[0])),mode='r:gz')
entries={m.name:m for m in package.getmembers() if m.isfile()}
written=[]
for name,member in entries.items():
 if not name.endswith('/pathname'):continue
 path=package.extractfile(member).read().decode().strip()
 parts=PurePosixPath(path).parts
 assert parts[0]=='Assets' and '..' not in parts,path
 # Samples and docs are not required by the game.
 if not path.startswith('Assets/FacebookSDK/'):continue
 if '/Examples/' in path or '/Example/' in path:continue
 prefix=name.rsplit('/',1)[0]
 for suffix,tail in [('asset',''),('asset.meta','.meta')]:
  item=entries.get(prefix+'/'+suffix)
  if item is None:continue
  target=ROOT/(path+tail)
  payload=package.extractfile(item).read()
  if target.exists():assert target.read_bytes()==payload,'Refusing to overwrite existing '+str(target)
  else:
   target.parent.mkdir(parents=True,exist_ok=True)
   target.write_bytes(payload);written.append(str(target.relative_to(ROOT)))
report={'source':asset['browser_download_url'],'sha256':digest,'version':'19.0.0','files':written}
out=ROOT/'Docs/Checks/Account-Linking/facebook-sdk-install.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print('Installed',len(written),'official SDK files; no Unity/build or network writes.')
