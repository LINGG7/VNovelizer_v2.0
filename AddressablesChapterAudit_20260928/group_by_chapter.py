import sys,re,json,collections,hashlib
from pathlib import Path
ROOT=Path('E:/Unity Projects/VNovelizerTest_v1.0');OUT=ROOT/'AddressablesChapterAudit_20260928';GROUPS=ROOT/'Assets/AddressableAssetsData/AssetGroups'
sys.path.insert(0,str(ROOT/'ScriptAudit_20260924'));import recheck_excel as a
books,bad=a.load('xlsx');assert not bad,bad
FIRST={'sntzm_intro','sntzm_clark3F(1)','sntzm_david313-315(1)'};refs=collections.defaultdict(list)
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

if '--apply' in sys.argv:
 import stat
 assert not missing,missing
 assert all(x['old_group'] for x in plan),'Unexpected unregistered asset'
 for name in ['Chapter1-Remote','Chapter2-Remote']:
  schema=(GROUPS/'Schemas'/(name+'_BundledAssetGroupSchema.asset')).read_text(encoding='utf-8-sig')
  assert 'm_IncludeInBuild: 1' in schema
  assert '18d5772cf6da98640a62f9079cda3fcf' in schema and '0fe0bbe9d5c0e754aacab48848c22da8' in schema
 affected={x['old_group'] for x in plan}|{x['new_group'] for x in plan}
 before={n:Path(groupdata[n]['file']).read_bytes() for n in affected}
 for n in affected:
  f=Path(groupdata[n]['file']);assert f.parent==GROUPS and f.suffix=='.asset'
  for ancestor in [f,*f.parents]:
   assert not ancestor.is_symlink(),str(ancestor)
   assert not getattr(ancestor.stat(),'st_file_attributes',0)&stat.FILE_ATTRIBUTE_REPARSE_POINT,str(ancestor)
 backup=OUT/'group_backups';backup.mkdir(exist_ok=True)
 for n,v in before.items():
  dest=backup/(n+'.asset.before')
  if dest.exists():assert dest.read_bytes()==v,'Backup already exists with a different version; stop'
  else:dest.write_bytes(v)
 selected={x['guid']:x for x in plan};updated={n:[] for n in groupdata}
 for n,d in groupdata.items():
  for entry in d['entries']:
   guid=re.search(r'm_GUID: (\w+)',entry)[1]
   if guid not in selected:updated[n].append(entry);continue
   item=selected[guid];new=entry
   new=re.sub(r'(?m)^    - chapter[12]_(?:required|optional)\n','',new)
   label='chapter1_required' if item['new_group']=='Chapter1-Remote' else 'chapter2_optional'
   if '    m_SerializedLabels: []' in new:new=new.replace('    m_SerializedLabels: []','    m_SerializedLabels:\n    - '+label)
   else:new=new.replace('    FlaggedDuringContentUpdateRestriction:','    - '+label+'\n    FlaggedDuringContentUpdateRestriction:')
   assert new.count('    - '+label+'\n')==1
   updated[item['new_group']].append(new)
 generated={}
 for n in affected:
  d=groupdata[n];entries=updated[n];section='  m_SerializeEntries:\n'+''.join(entries) if entries else '  m_SerializeEntries: []\n'
  newtext=d['text'][:d['start']]+section+d['text'][d['end']:]
  generated[n]=newtext
 # Validate before writing: all GUIDs and addresses are preserved; each selected GUID has exactly one target group.
 all_after={}
 for n,entries in updated.items():
  for entry in entries:
   g=re.search(r'm_GUID: (\w+)',entry)[1];assert g not in all_after
   addr=re.search(r'    m_Address: (.*)',entry)[1];addr=json.loads(addr) if addr.startswith('"') else addr
   all_after[g]=(n,addr,entry)
 assert set(all_after)==set(existing)
 for g,v in all_after.items():
  assert v[1]==existing[g]['address']
  if g in selected:assert v[0]==selected[g]['new_group']
  else:assert (v[0],v[2])==(existing[g]['group'],existing[g]['entry'])
 for n in affected:assert Path(groupdata[n]['file']).read_bytes()==before[n],'Concurrent file edit'
 for n,newtext in generated.items():
  raw=before[n];encoded=newtext.replace('\n','\r\n').encode('utf8') if b'\r\n' in raw else newtext.encode('utf8')
  if raw.startswith(b'\xef\xbb\xbf'):encoded=b'\xef\xbb\xbf'+encoded
  Path(groupdata[n]['file']).write_bytes(encoded)
  assert Path(groupdata[n]['file']).read_bytes()==encoded
 summary=dict(changed_groups=sorted(affected),moved=len(plan),counts={n:len(updated[n]) for n in affected},shared=sum(x['shared'] for x in plan),addresses_preserved=True,unrelated_entries_preserved=True)
 (OUT/'applied.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
 print('APPLIED',json.dumps(summary,ensure_ascii=False))
