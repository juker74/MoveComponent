"""Validate core/vendor isolation, metadata, sample accounts and scene references."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
package=json.loads((root/'package.json').read_text())
assert package['name']=='com.gameframex.unity.adcomponent'
assert set(package['dependencies'])=={'com.gameframex.unity','com.gameframex.unity.event'}
assert (root/package['samples'][0]['path']/'Scenes/TopOnExample.unity').is_file()
assemblies={}
for p in root.rglob('*.asmdef'):
 d=json.loads(p.read_text());assert d['name'] not in assemblies,d['name'];assemblies[d['name']]=p
for folder in ['Runtime','Editor']:
 for p in (root/folder).rglob('*.asmdef'):
  d=json.loads(p.read_text());assert not any('AnyThink' in r or 'TopOn' in r for r in d.get('references',[])),p
 for p in (root/folder).rglob('*.cs'):
  assert not re.search(r'using\s+(AnyThink|Hotfix|Singular|Firebase)',p.read_text()),p
assert not list((root/'Runtime').rglob('*TopOn*'))
guids={}
for p in root.rglob('*.meta'):
 if '.git' in p.parts:continue
 m=re.search(r'^guid: (\w+)',p.read_text(),re.M)
 assert m,p
 assert m[1] not in guids,(p,guids.get(m[1]));guids[m[1]]=p
 assert Path(str(p)[:-5]).exists(),('orphan meta',p)
for folder in ['Runtime','Editor','Tests','Samples~/TopOn']:
 for p in (root/folder).rglob('*'):
  if p.suffix!='.meta':assert Path(str(p)+'.meta').exists(),('missing meta',p)
config=json.loads((root/'Samples~/TopOn/ThinkupTpnPlugin/Resources/json/2.2.0/plugin_setting_data.json').read_text())
for platform in ['china','nonchina']:
 assert config[platform]['android_admob_app_id']==config[platform]['ios_admob_app_id']==''
scene=(root/'Samples~/TopOn/Scenes/TopOnExample.unity').read_text()
assert 'GameFrameX.Ad.TopOn.Sample' in scene
for guid in re.findall(r'm_Script: \{fileID: 11500000, guid: (\w+)',scene):
 # Base/Event component GUIDs are supplied by the declared GameFrameX dependencies.
 assert guid in guids or guid in {'65f23b0027da6c6458866730fe041c31','4f8db4d7a93a0744d940b1e34e16027e'},guid
assert not re.search(r'App(?:Id|Key):[ \t]*\S',scene)
assert not re.search(r'(?:Reward|Interstitial|Splash)PlacementId:[ \t]*\S',scene)
print(f'Package valid: {len(list(root.rglob("*.cs")))} C# files, {len(assemblies)} assemblies, {len(guids)} unique GUIDs; core isolated from SDK.')
