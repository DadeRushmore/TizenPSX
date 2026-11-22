"""Original procedural placeholder artwork. GPL-2.0. No external assets/fonts."""
from pathlib import Path
import struct,zlib
def png(path,w,h,accent=(67,207,211)):
 def chunk(k,v):return struct.pack('>I',len(v))+k+v+struct.pack('>I',zlib.crc32(k+v)&0xffffffff)
 rows=bytearray()
 for y in range(h):
  rows.append(0)
  for x in range(w):
   base=(14+int(10*y/h),18+int(12*y/h),34+int(24*y/h))
   # Original geometric portal; no console shape, game character or trademark.
   border=(int(w*.68)<x<int(w*.91) and int(h*.22)<y<int(h*.78) and
           (x<int(w*.70) or x>int(w*.89) or y<int(h*.25) or y>int(h*.75)))
   bar=int(w*.72)<x<int(w*.87) and any(abs(y-h*t)<max(1,h*.012) for t in (.4,.5,.6))
   rows.extend(accent if border or bar else base)
 data=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(bytes(rows),9))+chunk(b'IEND',b'')
 Path(path).parent.mkdir(parents=True,exist_ok=True);Path(path).write_bytes(data)
if __name__=='__main__':
 import json
 for item in json.loads(Path('public-art.json').read_text()):png(item['path'],item['width'],item['height'],tuple(item['accent']))
