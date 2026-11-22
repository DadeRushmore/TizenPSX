"""Rebuild the native PS1 core AND managed Tizen app after controller ABI changes."""
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

app = Path(__file__).resolve().parents[1]
root = app.parent
core_build = root / 'pcsx-rearmed-native' / 'tizen-build'
native = core_build / 'libpcsx_tizen.so'
resource = app / 'res' / 'libpcsx_tizen.so'
project = app / 'Player.csproj'

print('Building patched ARM32 PS1 core...')
subprocess.run([sys.executable, str(core_build / 'build-core.py')], check=True, cwd=core_build.parent)
if not native.is_file():
    raise SystemExit('ARM32 native build did not produce libpcsx_tizen.so')
shutil.copy2(native, resource)
digest = hashlib.sha256(resource.read_bytes()).hexdigest()
(app / 'res' / 'core.sha256').write_text(digest + '\n')
provenance_file = app / 'provenance.json'
provenance = json.loads(provenance_file.read_text())
provenance['pcsxCoreSHA256'] = digest
provenance_file.write_text(json.dumps(provenance, indent=2) + '\n')
print('Core SHA256 updated:', digest)
print('Building Tizen .NET application...')
subprocess.run(['dotnet', 'restore', str(project)], check=True, cwd=app)
subprocess.run(['dotnet', 'build', str(project), '-c', 'Release', '--no-restore'], check=True, cwd=app)
print('Build complete. Re-sign the new TPK with your usual signer before TV installation.')
