"""Scan a candidate TPK. Public signing signatures are expected; private keys are not."""
import json
import hashlib
import re
import sys
import zipfile
from pathlib import Path
from importlib.util import spec_from_file_location, module_from_spec

spec = spec_from_file_location('release_scan', Path(__file__).with_name('check-release.py'))
scan = module_from_spec(spec); spec.loader.exec_module(scan)
with zipfile.ZipFile(sys.argv[1]) as archive:
    failures = []
    preserved = json.loads((scan.ROOT/"docs/preserved-cover-hashes.json").read_text())
    for name in archive.namelist():
        data = archive.read(name)
        if ('/covers/' in name and hashlib.sha256(data).hexdigest() != preserved.get(name)) or name.endswith(('.p12', '.pfx', '.crt', '.pem', '.key', '.pdb', '.bin', '.cue', '.chd', '.pbp', '.iso')):
            failures.append((name, 'excluded package resource'))
        for kind, pattern in scan.PATTERNS.items():
            candidate = data
            if kind == 'private IP address' and name.endswith('.deps.json'):
                # .NET assembly/file version fields are not network addresses.
                candidate = re.sub(rb'"(?:assemblyVersion|fileVersion)"\s*:\s*"[0-9.]+"', b'', data)
            if re.search(pattern, candidate): failures.append((name, kind))
    expected = (scan.ROOT/'roelofs-psx-native/res/core.sha256').read_text().strip()
    if hashlib.sha256(archive.read('res/libpcsx_tizen.so')).hexdigest() != expected:
        failures.append(('native core', 'package/source hash mismatch'))
    if hashlib.sha256(archive.read('res/homebrew.exe')).hexdigest() != scan.DEMO_SHA:
        failures.append(('homebrew', 'unapproved demo bytes'))
    for notice in ['LICENSE-PCSX.txt', 'LICENSE-glibc.txt', 'NOTICE-glibc.txt', 'LICENSE-Overscan.txt', 'LICENSE-Zig.txt']:
        if 'res/'+notice not in archive.namelist(): failures.append((notice, 'missing license notice'))
    print('Package scan:', 'FAIL' if failures else 'PASS')
    for name, reason in failures: print(name+': '+reason)
    raise SystemExit(bool(failures))
