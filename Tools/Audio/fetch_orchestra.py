"""Download a pinned CC0 VSCO 2 CE subset into ignored local source storage."""
from pathlib import Path
import hashlib
import json
import urllib.request
import urllib.parse
from concurrent.futures import ThreadPoolExecutor

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT/'Artifacts/Audio/OrchestraSource'
REV = '440300901dfe9275fd84e0b7763af1f8443ae62e'
tree_path = ROOT/'Artifacts/Audio/vsco-tree.json'
if not tree_path.exists():
    tree_path.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen('https://api.github.com/repos/sgossner/VSCO-2-CE/git/trees/'+REV+'?recursive=1', timeout=60) as response:
        tree_path.write_bytes(response.read())
tree = json.loads(tree_path.read_text())
assert tree['sha'] == REV
groups = {
    'trombone': ('Brass/Tenor Trombone/sus/', ['_v2_1.wav']),
    'horn_short': ('Brass/F Horn/stac/', ['_v2_rr1.wav', '_v2_rr2.wav']),
    'horn': ('Brass/F Horn/sus/', ['_v2_1.wav']),
    'strings': ('Strings/Violin Section/susVib/', ['_v2.wav']),
    'cello': ('Strings/Cello Section/susvib/', ['_v1_1.wav']),
    'short': ('Strings/Cello Section/spic/', ['_v1_RR1.wav', '_v1_RR2.wav']),
    'harp': ('Strings/Harp/', ['_mf.wav']),
    'drum': ('Percussion/', ['Snare2-HitSN_v7_rr1_Sum.wav', 'Snare2-HitSN_v7_rr2_Sum.wav', 'BDrumNewhit_v4_rr1_Sum.wav', 'BDrumNewhit_v4_rr2_Sum.wav',
                           'Timpani1_Hit_v3_rr1_Sum.wav', 'Timpani1_Hit_v3_rr2_Sum.wav']),
}
selected = []
for item in tree['tree']:
    p = item['path']
    for group, (prefix, endings) in groups.items():
        if p.startswith(prefix) and any(p.endswith(s) for s in endings):
            selected.append(dict(group=group, path=p, git_sha=item['sha']))

def fetch(item):
    target = DEST/item['path']
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists():
        url = 'https://raw.githubusercontent.com/sgossner/VSCO-2-CE/'+REV+'/'+urllib.parse.quote(item['path'])
        with urllib.request.urlopen(url, timeout=60) as response:
            target.write_bytes(response.read())
    data = target.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest() == item['git_sha']
    return dict(item, sha256=hashlib.sha256(data).hexdigest())

with ThreadPoolExecutor(max_workers=4) as pool:
    manifest = list(pool.map(fetch, selected))
(DEST/'manifest.json').write_text(json.dumps(manifest, indent=2))
print('Verified samples:', len(manifest))
