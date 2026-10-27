"""Read-only checks before submitting the forest import. Run with Python 3."""
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
assets = root / 'Assets'
issues = []
guids = {}
for meta in assets.rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M)
    if match:
        guid = match.group(1)
        if guid in guids:
            issues.append(f'Duplicate GUID: {meta} / {guids[guid]}')
        guids[guid] = meta
module = assets / 'DongbaPrototype'
for item in module.rglob('*'):
    if item.name.endswith('.meta'):
        continue
    if not Path(str(item) + '.meta').exists():
        issues.append(f'Missing meta: {item.relative_to(root)}')
    if item.is_file() and item.stat().st_size >= 100 * 1024 * 1024:
        issues.append(f'File too large for normal Git: {item.relative_to(root)}')
for name in ['ObliqueForest.unity', 'ObliqueForest.cs']:
    if not (module / name).is_file():
        issues.append('Missing entry: ' + name)
for item in module.rglob('*.cs'):
    if re.search(r'[A-Z]:[/\\]', item.read_text(encoding='utf-8-sig')):
        issues.append(f'Absolute drive dependency: {item.relative_to(root)}')
print('GUID, meta, file-size and path checks:', 'FAIL' if issues else 'PASS')
for issue in issues:
    print(issue)
print('This does not replace importing and playing in Unity 6000.3.12f1.')
sys.exit(bool(issues))
