"""Build this frozen frontend using supplied signing credentials, never bundled keys."""
from pathlib import Path
import json, os, shutil, subprocess
root=Path(__file__).resolve().parent
meta=json.loads((root/'PUBLIC-SOURCE.json').read_text())
dotnet=shutil.which('dotnet')
if not dotnet: raise SystemExit('Install .NET SDK 3.1.426 and add dotnet to PATH.')
required=('AuthorPath','AuthorPass','DistributorPath','DistributorPass')
if not all(os.environ.get(k) for k in required):
 raise SystemExit('Set AuthorPath, AuthorPass, DistributorPath and DistributorPass to your own Tizen signing credentials.')
subprocess.run([dotnet,'build',meta['project'],'-c','Release','--nologo'],cwd=root,check=True)
