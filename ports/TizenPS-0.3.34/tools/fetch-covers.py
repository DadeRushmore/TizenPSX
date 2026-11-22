"""Download the explicitly matched original covers; no ROMs or BIOS downloads."""
from pathlib import Path
from urllib.parse import quote
import urllib.request,json,hashlib,concurrent.futures,shutil
ROOT=Path(__file__).resolve().parents[1]
BASE='https://thumbnails.libretro.com/Sony%20-%20PlayStation/Named_Boxarts/'
def fetch(g):
    url=BASE+quote(g['coverFile'])
    dst=ROOT/'preview/assets/covers'/(g['id']+'.png');dst.parent.mkdir(parents=True,exist_ok=True)
    if not dst.exists():
        data=urllib.request.urlopen(url,timeout=40).read()
        if not data.startswith(b'\x89PNG\r\n\x1a\n'):raise ValueError('Not a PNG: '+url)
        dst.write_bytes(data)
    data=dst.read_bytes()
    native=ROOT/'roelofs-psx-native/res/covers'/dst.name
    native.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dst,native)
    print(g['title'],flush=True)
    return dict(id=g['id'],source=url,region=g['artRegion'],sha256=hashlib.sha256(data).hexdigest(),bytes=len(data))
if __name__=='__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        result=list(pool.map(fetch,json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'))))
    (ROOT/'artwork-sources.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print('Verified',len(result),'original cover files')
