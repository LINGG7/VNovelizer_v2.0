import sys,json,collections,hashlib
from pathlib import Path
ROOT=Path('E:/Unity Projects/VNovelizerTest_v1.0');OUT=ROOT/'ScriptAudit_20260928_BGM'
sys.path.insert(0,str(ROOT/'ScriptAudit_20260924'))
import recheck_excel as a
books,bad=a.load('xlsx'); assert not bad,bad
music=sorted({r['BGM'] for rs in books.values() for r in rs if r['BGM'] and r['BGM'].lower() not in ['stop','pause','resume']})
bit={m:1<<i for i,m in enumerate(music)};maps={n:{r['ID']:i for i,r in enumerate(rs)} for n,rs in books.items()}
todo=[('sntzm_intro',0,frozenset(),0,())];seen=set();first={};reached=set();errors=set();ends=set()
while todo:
 n,i,tags,heard,path=todo.pop()
 while True:
  if i>=len(books[n]):errors.add((n,'EOF'));break
  state=(n,i,tags,heard)
  if state in seen:break
  seen.add(state)
  if len(seen)>3000000:raise RuntimeError('state cap')
  r=books[n][i];reached.add((n,r['ID']));m=r['BGM']
  if m in bit:
   if not heard&bit[m]:
    k=(m,n,r['ID'])
    if k not in first:first[k]={'music':m,'script':n,'ID':r['ID'],'row':r['row'],'sheet':r['sheet'],'sample_choices':path,'tags':set(),'common_tags':set(tags),'tag_sets':set(),'count':0}
    x=first[k];x['tags'].update(tags);x['common_tags'].intersection_update(tags);x['tag_sets'].add(tuple(sorted(tags)));x['count']+=1
   heard|=bit[m]
  if r['tag']:tags=tags|{r['tag']}
  if '达成结局' in r['Text'] or 'ToBeContinued' in r['Note']:ends.add((n,r['ID']));break
  def target(cmds):
   dest=None
   for c,args in cmds:
    if c=='jump':
     if args.strip() not in maps[n]:errors.add((n,r['ID'],'missing jump',args))
     else:dest=(n,maps[n][args.strip()])
    if c=='loadscript':
     p=[x.strip() for x in args.split(',')]
     if p[0] not in books:errors.add((n,r['ID'],'missing script',args))
     elif len(p)>1 and p[1] not in maps[p[0]]:errors.add((n,r['ID'],'missing ID',args))
     else:dest=(p[0],maps[p[0]][p[1]] if len(p)>1 else 0)
   return dest
  opts=[arg for c,arg in r['cmds'] if c=='choice']
  if opts:
   for opt in opts:
    label,cmd=opt.split('|',1);dest=target(a.commands(cmd)) or (n,i+1)
    todo.append((*dest,tags,heard,path+((n,r['ID'],label),)))
   break
  dest=target(r['cmds'])
  if dest and dest!=(n,i):n,i=dest;continue
  if r['hidden']:
   nxt=a.branch(books[n],i,tags)
   if nxt>=len(books[n]):errors.add((n,r['ID'],'unmatched branch'));break
   i=nxt
  else:i+=1
result=[]
for k,x in sorted(first.items()):
 for key in ['tags','common_tags','tag_sets']:x[key]=sorted(x[key])
 result.append(x)
unused=[{'music':r['BGM'],'script':n,'ID':r['ID'],'row':r['row']} for n,rs in books.items() for r in rs if r['BGM'] in bit and (n,r['ID']) not in reached]
res=dict(files=len(books),music_count=len(music),states=len(seen),first_positions=result,unreachable_bgm_rows=unused,errors=sorted(errors),endings=sorted(ends))
(OUT/'bgm_first_paths.json').write_text(json.dumps(res,ensure_ascii=False,indent=2),encoding='utf8')
for m in music:
 print('\nMUSIC',m)
 for x in result:
  if x['music']==m:print(x['script'],x['ID'],'row',x['row'],'COMMON',','.join(x['common_tags']))
print('SUMMARY',len(music),'tracks',len(result),'first positions',len(seen),'states','errors',errors,'unreachable',unused)
