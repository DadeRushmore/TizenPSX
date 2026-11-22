"""Regenerate both frontend catalogs after editing catalog.json."""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
games=json.loads((root/'catalog.json').read_text(encoding='utf-8'))
(root/'preview/catalog.js').write_text('window.TIZENPS_CATALOG = '+json.dumps(games,ensure_ascii=False,indent=2)+';\n',encoding='utf-8')
(root/'roelofs-psx-native/res/catalog.tsv').write_text('\n'.join('\t'.join([g['id'],g['title'],g['genre'],g['publisher'],g['year'],g['artRegion'],g['description'],'|'.join(g['aliases'])]) for g in games),encoding='utf-8')
print('Synchronized',len(games),'games')
