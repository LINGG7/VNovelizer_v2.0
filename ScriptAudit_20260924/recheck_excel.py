import csv, json, re, collections, openpyxl
from pathlib import Path
ROOT = Path('E:/Unity Projects/VNovelizerTest_v1.0')
RES = ROOT / 'Assets/Resources/VNovelizerRes'
OUT = ROOT / 'ScriptAudit_20260924'

def noteval(s, k):
    m = re.search(re.escape(k) + '=([^;]*)', s, re.I)
    return m.group(1).strip() if m else ''

def commands(s):
    out = []
    start = 0
    depth = 0
    for i, c in enumerate(s):
        if c == '(':
            depth += 1
        elif c == ')':
            depth -= 1
        elif c == '&' and depth == 0:
            out.append(s[start:i].strip())
            start = i + 1
    out.append(s[start:].strip())
    result = []
    for x in out:
        if not x:
            continue
        m = re.fullmatch('(\\w+)\\((.*)\\)', x, re.S)
        result.append((m[1].lower(), m[2]) if m else ('INVALID', x))
    return result

def load(kind):
    books = {}
    bad = []
    for f in sorted((RES / ('VNScripts' if kind == 'csv' else 'ExcelVNScripts')).glob('*.' + kind)):
        if kind == 'csv':
            raw = list(csv.reader(f.open(encoding='utf-8-sig')))
            sheet = ''
        else:
            
            if f.name.startswith('~$'): continue
            w = openpyxl.load_workbook(f, read_only=True, data_only=False)
            s = w.worksheets[0]
            sheet = s.title
            raw = list(s.values)
        rows = []
        for n, v in enumerate(raw[1:], 2):
            if not any((x is not None and x != '' for x in v)):
                continue
            if len(v) < 12:
                bad.append((f.name, n, 'short row'))
                continue
            v = [str(x).strip() if x is not None else '' for x in v]
            r = dict(zip(['ID', 'Speaker', 'HeadProfile', 'CharLeft', 'CharMid', 'CharRight', 'Text', 'Background', 'BGM', 'Voice', 'Command', 'Note'], v))
            r['row'] = n
            r['sheet'] = sheet
            r['level'] = int(noteval(r['Note'], '层级')) if noteval(r['Note'], '层级').isdigit() else None
            r['tag'] = noteval(r['Note'], 'tag').strip('"\'')
            r['hidden'] = 'type:exeBC' in r['Note']
            r['cmds'] = commands(r['Command'])
            r['where'] = None
            if noteval(r['Note'], 'wherelist'):
                try:
                    r['where'] = json.loads(noteval(r['Note'], 'wherelist'))
                except:
                    bad.append((f.name, n, 'bad wherelist'))
            rows.append(r)
        books[f.stem] = rows
    return (books, bad)

def static(books):
    issues = []
    counts = collections.Counter()
    tags = set()
    needed = set()
    for name, rows in books.items():
        ids = collections.Counter((r['ID'] for r in rows))
        for k, n in ids.items():
            if not k or n > 1:
                issues.append([name, k, 'duplicate/empty ID', n])

        def inspect(cmds, r):
            for cmd, args in cmds:
                counts[cmd] += 1
                if cmd == 'choice':
                    if '|' not in args:
                        issues.append([name, r['ID'], 'choice lacks action', args])
                    else:
                        inspect(commands(args.split('|', 1)[1]), r)
                elif cmd == 'parallel':
                    inspect(commands(args.replace(';', '&')), r)
                elif cmd == 'jump' and args.strip() not in ids:
                    issues.append([name, r['ID'], 'missing jump ID', args, r['row']])
                elif cmd == 'loadscript':
                    parts = [v.strip() for v in args.split(',')]
                    target = parts[0]
                    if target not in books:
                        issues.append([name, r['ID'], 'missing script', args, r['row']])
                    elif len(parts) > 1 and parts[1] not in {v['ID'] for v in books[target]}:
                        issues.append([name, r['ID'], 'missing load ID', args, r['row']])
                elif cmd == 'INVALID':
                    issues.append([name, r['ID'], 'invalid command', args])
        for r in rows:
            inspect(r['cmds'], r)
            if r['tag']:
                tags.add(r['tag'])
            if r['where']:
                w = r['where']
                for d in w.get('wheredatas', []):
                    cs = d.get('conditions', [])
                    ts = d.get('targets', [])
                    if w.get('wheretype') not in ['1', '2'] or not cs or (not ts) or (cs[0].get('valuetype') != '4') or (str(ts[0].get('valuedata')).lower() not in ['0', '1', 'true', 'false']) or (d.get('way', '=') not in ['=', '==', '!=', '<>', '≠']):
                        issues.append([name, r['ID'], 'unsupported condition', d])
                    if cs:
                        needed.add(cs[0].get('valuedata'))
    return dict(issues=issues, counts=dict(counts), undefined_tags=sorted(needed - tags))

def normalized(r):
    return (r['ID'], r['Command'], r['Note'])

def match(w, tags):
    vals = []
    for d in w['wheredatas']:
        actual = d['conditions'][0]['valuedata'].strip() in tags
        expected = str(d['targets'][0]['valuedata']).lower() in ['1', 'true']
        vals.append(actual == expected if d.get('way', '=') in ['=', '=='] else actual != expected)
    return all(vals) if w['wheretype'] == '2' else any(vals)

def branch(rows, i, tags):
    level = rows[i]['level']
    if level is None:
        return i + 1
    children = []
    same = None
    for j in range(i + 1, len(rows)):
        lv = rows[j]['level']
        if lv is None:
            continue
        if lv <= level:
            if lv == level:
                same = j
            break
        if lv == level + 1:
            children.append(j)
    fallback = None
    for j in children:
        w = rows[j]['where']
        if w:
            if match(w, tags):
                return j
        elif fallback is None:
            fallback = j
    if fallback is not None:
        return fallback
    if same is not None:
        return same
    return len(rows)

def simulate(books, label):
    maps = {n: {r['ID']: i for i, r in enumerate(rs)} for n, rs in books.items()}
    todo = [('sntzm_intro', 0, frozenset(), [], [])]
    seen = set()
    visited = set()
    issues = {}
    ends = {}
    cycles = {}
    steps = 0

    def record(dest, key, data):
        if key not in dest:
            dest[key] = data
    while todo:
        name, i, tags, path, trace = todo.pop()
        local = {}
        while True:
            if i >= len(books[name]):
                prev = books[name][-1]
                record(issues, (name, prev['ID'], 'eof'), dict(script=name, ID=prev['ID'], row=prev['row'], type='unexpected_eof', tags=sorted(tags), choices=path, trace=trace[-15:]))
                break
            state = (name, i, tags)
            if state in local:
                record(cycles, (name, books[name][i]['ID']), dict(script=name, ID=books[name][i]['ID'], choices=path, tags=sorted(tags), cycle=trace[local[state]:]))
                break
            if state in seen:
                break
            local[state] = len(trace)
            seen.add(state)
            steps += 1
            if steps > 2000000:
                raise Exception('state limit')
            r = books[name][i]
            visited.add((name, r['ID']))
            trace = trace + [name + ':' + r['ID']]
            if r['tag']:
                tags = tags | {r['tag']}
            if '达成结局' in r['Text'] or 'ToBeContinued' in r['Note']:
                record(ends, (name, r['ID']), dict(script=name, ID=r['ID'], text=r['Text'], choices=path))
                break
            opts = [a for c, a in r['cmds'] if c == 'choice']

            def target(cmds):
                dest = None
                for c, a in cmds:
                    if c == 'jump':
                        if a.strip() in maps[name]:
                            dest = (name, maps[name][a.strip()])
                        else:
                            record(issues, (name, r['ID'], 'bad_jump'), dict(script=name, ID=r['ID'], row=r['row'], type='missing_jump_target', target=a, choices=path, tags=sorted(tags), trace=trace[-15:]))
                    elif c == 'loadscript':
                        pts = [v.strip() for v in a.split(',')]
                        n = pts[0]
                        tid = pts[1] if len(pts) > 1 else None
                        if n not in books:
                            record(issues, (name, r['ID'], 'missing_script'), dict(script=name, ID=r['ID'], row=r['row'], type='missing_script', target=a, choices=path, tags=sorted(tags), trace=trace[-15:]))
                        else:
                            if tid and tid not in maps[n]:
                                record(issues, (name, r['ID'], 'bad_load_id'), dict(script=name, ID=r['ID'], row=r['row'], type='missing_load_target', target=a, choices=path, tags=sorted(tags), trace=trace[-15:]))
                            dest = (n, maps[n].get(tid, 0))
                return dest
            if opts:
                for opt in opts:
                    text, action = opt.split('|', 1)
                    dest = target(commands(action)) or (name, i + 1)
                    todo.append((*dest, tags, path + [name + ':' + r['ID'] + ' → ' + text], trace))
                break
            dest = target(r['cmds'])
            if dest and dest != (name, i):
                name, i = dest
                continue
            if r['hidden']:
                nxt = branch(books[name], i, tags)
                if nxt == len(books[name]):
                    record(issues, (name, r['ID'], 'branch_end'), dict(script=name, ID=r['ID'], row=r['row'], type='no_matching_branch', tags=sorted(tags), choices=path, trace=trace[-15:]))
                    break
                i = nxt
            else:
                i += 1
    out = dict(states=len(seen), visited_rows=len(visited), visited_scripts=len({n for n, i in visited}), issues=list(issues.values()), cycles=list(cycles.values()), endings=list(ends.values()), unvisited_scripts=sorted(set(books) - {n for n, i in visited}))
    (OUT / (label + '_simulation.json')).write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding='utf8')
    print('SIM', label, json.dumps(out, ensure_ascii=False, indent=2))
    return out
if __name__ == '__main__':
 import contextlib,io,hashlib
 OUT=ROOT/'ScriptAudit_20260924'/'recheck'
 OUT.mkdir(exist_ok=True)
 books,bad=load('xlsx')
 static_result=static(books)
 for n,rs in books.items():
  for r in rs:
   if r['Command'].count('(')!=r['Command'].count(')'):static_result['issues'].append([n,r['ID'],'unbalanced parentheses',r['Command'],r['row']])
 (OUT/'static.json').write_text(json.dumps(dict(files=len(books),nodes=sum(map(len,books.values())),parse_errors=bad,checks=static_result),ensure_ascii=False,indent=2),encoding='utf8')
 print('STATIC',json.dumps(static_result,ensure_ascii=False),flush=True)
 with contextlib.redirect_stdout(io.StringIO()): sim=simulate(books,'excel')
 print('SIM SUMMARY',json.dumps({k:v for k,v in sim.items() if k not in ['issues','cycles','endings']},ensure_ascii=False))
 for r in sim['issues']:print('ISSUE',json.dumps(r,ensure_ascii=False))
 for r in sim['cycles']:print('CYCLE',json.dumps(r,ensure_ascii=False))
 for r in sim['endings']:print('ENDING',r['script'],r['ID'],r['text'])
 inv=[dict(file=f.name,sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in sorted((RES/'ExcelVNScripts').glob('*.xlsx')) if not f.name.startswith('~$')]
 (OUT/'inventory.json').write_text(json.dumps(inv,ensure_ascii=False,indent=2),encoding='utf8')
