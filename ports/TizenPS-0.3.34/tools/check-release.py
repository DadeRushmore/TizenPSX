"""Audit the tracked candidate tree, or all locally reachable Git history.

This is a technical release guard, not a complete legal or secret audit.
Ignored/untracked caches are excluded. Inspect release packages separately.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BLOCKED = {'.bin', '.cue', '.chd', '.pbp', '.iso', '.img', '.p12', '.pfx', '.crt', '.cer', '.pem', '.key', '.der', '.jks', '.keystore', '.tpk', '.wgt', '.mcr', '.srm'}
PATTERNS = {
    'private key': rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
    'GitHub token': rb'(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})',
    'AWS access key': rb'AKIA[A-Z0-9]{16}',
    'personal source path': rb'(?:[A-Za-z]:[\\/]+Users[\\/]+|/(?:home|Users)/[A-Za-z][^/\s]+/)',
    'private IP address': rb'(?<![\d.])(?:192\.168\.\d{1,3}\.\d{1,3}|10\.\d{1,3}\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3})(?![\d.])',
}
DEMO_SHA = 'dda2dd72dbf5e55e4cdb344d97bc1011baaf483451a2462677824e33b8ce5f82'

def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])

def inspect(path, data):
    problems = []
    p = Path(path)
    if p.suffix.lower() in BLOCKED or p.name in {'.env', 'profiles.xml', 'author-signature.xml'}:
        problems.append('excluded personal/game/signing file')
    if p.suffix.lower() == '.exe' and (p.name != 'homebrew.exe' or hashlib.sha256(data).hexdigest() != DEMO_SHA):
        problems.append('unapproved executable')
    if '/covers/' in path and p.suffix.lower() in {'.png', '.jpg', '.jpeg'}:
        problems.append('downloaded cover cache')
    if p.name in {'ps-emblem.svg', 'ps-emblem.png'}:
        problems.append('retired branding asset')
    variants = [data]
    if data.startswith((b'\xff\xfe', b'\xfe\xff')):
        variants.append(data.decode('utf-16', errors='replace').encode())
    for name, pattern in PATTERNS.items():
        candidates = variants
        # Upstream Maemo's standard device home and a test fixture, not developer paths.
        if name == 'personal source path' and path in {'pcsx-rearmed-native/maemo/main.c', 'pcsx-rearmed-native/maemo/hildon.c', 'pcsx-rearmed-native/deps/libretro-common/test/menu/test_menu_str.c'}:
            candidates = [v.replace(b'/home/' + b'user/', b'/device/') for v in variants]
        if any(re.search(pattern, value) for value in candidates):
            problems.append(name)
    return problems

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--history', action='store_true', help='scan all locally reachable Git objects; old imports should fail until history is cleaned')
    args = parser.parse_args()
    failures = []; count = 0
    if args.history:
        objects = [line.split(' ', 1) for line in git('rev-list', '--objects', '--all').decode().splitlines() if ' ' in line]
        proc = subprocess.Popen(['git', '-C', str(ROOT), 'cat-file', '--batch'], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
        for sha, path in objects:
            proc.stdin.write((sha+'\n').encode()); proc.stdin.flush()
            header = proc.stdout.readline().split(); data = proc.stdout.read(int(header[2])); proc.stdout.read(1)
            if header[1] != b'blob': continue
            count += 1
            failures.extend((path, reason) for reason in inspect(path, data))
        proc.stdin.close(); proc.wait()
    else:
        for path in git('ls-files', '-z').decode().split('\0'):
            if not path: continue
            count += 1
            file = ROOT/path
            if not file.is_file(): failures.append((path, 'tracked file missing')); continue
            failures.extend((path, reason) for reason in inspect(path, file.read_bytes()))
        actual = hashlib.sha256((ROOT/'roelofs-psx-native/res/libpcsx_tizen.so').read_bytes()).hexdigest()
        provenance = json.loads((ROOT/'roelofs-psx-native/provenance.json').read_text())
        if actual != provenance['pcsxCoreSHA256'] or actual != (ROOT/'roelofs-psx-native/res/core.sha256').read_text().strip():
            failures.append(('native core', 'integrity metadata mismatch'))
        for required in ['LICENSE', 'THIRD-PARTY-NOTICES.md', 'LICENSES/LGPL-2.1.txt', 'pcsx-rearmed-native/COPYING']:
            if not (ROOT/required).is_file(): failures.append((required, 'required notice missing'))
    for path, reason in sorted(set(failures)): print(f'FAIL {path}: {reason}')
    print(f'{"FAIL" if failures else "PASS"}: {count} {"historical objects" if args.history else "tracked files"} checked; {len(set(failures))} findings.')
    return bool(failures)

if __name__ == '__main__':
    raise SystemExit(main())
