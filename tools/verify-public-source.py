"""Verify the published inventory and reject credentials and compiled executables."""
from pathlib import Path
import hashlib, json, re
root = Path(__file__).resolve().parents[1]
inventory = json.loads((root / 'SOURCE-MANIFEST.json').read_text(encoding='utf-8'))
errors = []
for rel, expected in inventory.items():
    path = root / rel
    if not path.resolve().is_relative_to(root.resolve()) or not path.is_file():
        errors.append(rel + ': missing/invalid path'); continue
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != expected:
        errors.append(rel + ': SHA-256 mismatch')
    if data.startswith((b'\x7fELF', b'MZ', b'!<arch>\n')):
        errors.append(rel + ': compiled executable/archive')
    if re.search(rb'gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----', data):
        errors.append(rel + ': credential pattern')
if errors:
    raise SystemExit('\n'.join(errors))
print('PASS:', len(inventory), 'inventoried files; hashes, paths, executable signatures and credential patterns checked')
