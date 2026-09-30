import sys,re,json,collections,hashlib
from pathlib import Path
ROOT=Path('E:/Unity Projects/VNovelizerTest_v1.0');OUT=ROOT/'AddressablesChapterAudit_20260930';GROUPS=ROOT/'Assets/AddressableAssetsData/AssetGroups'
sys.path.insert(0,str(ROOT/'ScriptAudit_20260924'));import recheck_excel as a
books,bad=a.load('xlsx');assert not bad,bad
FIRST={'sntzm_intro','sntzm_clark3F(1)','sntzm_clark3F(2)','sntzm_david313-315(1)','sntzm_david313-315(2)'};refs=collections.defaultdict(list)
for name,rows in books.items():
 for r in rows:
  vals=[]
  if r['Background'] and r['Background'].lower() not in ['hide','none','stop','clear','black']:vals.append(('bg',r['Background']))
  if r['BGM'] and r['BGM'].lower() not in ['stop','pause','resume']:vals.append(('bgm',r['BGM']))
  for match in re.finditer(r'(?<!\w)bgfade\s*\(\s*([^,]+),',r['Command'],re.I):vals.append(('bg',match[1].strip()))
  for kind,value in vals:
   addr=('VNovelizerRes/Backgrounds/' if kind=='bg' else 'VNovelizerRes/Audio/Music/BGM/')+value
   refs[addr].append({'script':name,'ID':r['ID'],'kind':kind})
groupdata={};existing={}
for f in GROUPS.glob('*.asset'):
 s=f.read_text(encoding='utf-8-sig');m=re.search(r'(?m)^  m_SerializeEntries:.*\n',s)
 if not m:continue
 end=re.search(r'(?m)^  m_ReadOnly:',s[m.end():]).start()+m.end();body=s[m.end():end];entries=re.findall(r'(?ms)^  - m_GUID: .*?(?=^  - m_GUID: |\Z)',body)
 groupdata[f.stem]={'file':str(f),'text':s,'start':m.start(),'end':end,'entries':entries}
 for e in entries:
  guid=re.search(r'm_GUID: (\w+)',e)[1];ad=re.search(r'    m_Address: (.*)',e)[1];ad=json.loads(ad) if ad.startswith('"') else ad
  assert guid not in existing,('duplicate guid',guid)
  existing[guid]={'group':f.stem,'entry':e,'address':ad}
files=collections.defaultdict(list)
for base in [ROOT/'Assets/RemoteContent',ROOT/'Assets/Resources']:
 for f in base.rglob('*'):
  if not f.is_file() or f.suffix=='.meta':continue
  rel=f.relative_to(base).with_suffix('').as_posix()
  if not rel.startswith(('VNovelizerRes/Backgrounds/','VNovelizerRes/Audio/Music/BGM/')):continue
  meta=Path(str(f)+'.meta')
  if not meta.exists():continue
  g=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)
  if g:files[rel.lower()].append((f,g[1]))
plan=[];missing=[]
for address,locations in sorted(refs.items()):
 candidates=files.get(address.lower(),[])
 if len(candidates)!=1:missing.append({'address':address,'candidates':[str(x[0]) for x in candidates],'refs':locations});continue
 f,g=candidates[0];old=existing.get(g);chapter=1 if any(x['script'] in FIRST for x in locations) else 2
 plan.append(dict(guid=g,file=str(f),address=address,old_group=old['group'] if old else None,old_address=old['address'] if old else None,new_group=f'Chapter{chapter}-Remote',kind=locations[0]['kind'],shared=chapter==1 and any(x['script'] not in FIRST for x in locations),refs=locations))
result=dict(files=len(books),plan=plan,missing=missing)
(OUT/'plan.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print('COUNTS',collections.Counter((x['new_group'],x['kind']) for x in plan));print('OLD GROUPS',collections.Counter(x['old_group'] for x in plan));print('SHARED',sum(x['shared'] for x in plan));print('MISSING',json.dumps(missing,ensure_ascii=False));print('ADDRESS DIFFERENCES',[(x['address'],x['old_address']) for x in plan if x['old_address'] and x['address']!=x['old_address']]);print('GROUP COUNTS',{n:len(d['entries']) for n,d in groupdata.items()})

