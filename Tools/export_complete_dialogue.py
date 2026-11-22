"""Export the current spoken/narrated script. Does not modify Unity assets."""
from pathlib import Path
import csv
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Docs/Narrative/全流程台词本-中英对照.txt'
REPORT = ROOT / 'Verification/complete-dialogue-export'

def read(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')

def csv_rows(path):
    with (ROOT / path).open(encoding='utf-8-sig', newline='') as stream:
        return list(csv.DictReader(stream))

def unquote(text):
    return json.loads(text) if text.startswith('"') else text

def scalar_rows(text, first_key):
    """Read these Unity assets' scalar records (not a general YAML parser)."""
    result=[]
    for block in re.split(r'^  - '+first_key+r': ', text, flags=re.M)[1:]:
        lines=block.splitlines()
        row={first_key:unquote(lines[0].strip())}
        for line in lines[1:]:
            match=re.fullmatch(r'    (\w+): ?(.*)', line)
            if match:
                row[match[1]]=unquote(match[2].strip())
        result.append(row)
    return result

catalog=json.loads(read('Assets/DongbaPrototype/Resources/DongbaLanguages.json'))['entries']
lookup={}
for entry in catalog:
    for key in [entry['en'],entry['zh']]+entry.get('aliases',[]):
        lookup[key]=entry

def translated(source):
    assert source in lookup, 'No current bilingual mapping: '+source
    entry=lookup[source]
    return entry['en'],entry['zh']

story_source='Assets/DongbaPrototype/ObliqueForest.StoryLines.cs'
story=read(story_source)
literal=r'"(?:[^"\\]|\\.)*"'
def array(name):
    match=re.search(r'\b'+name+r'\s*=\s*\{(.*?)\};',story,re.S)
    assert match,name
    return [json.loads(x) for x in re.findall(literal,match[1])]

def const(name):
    match=re.search(r'\b'+name+r'\s*=\s*('+literal+r')\s*;',story)
    assert match,name
    return json.loads(match[1])

graph_source='Assets/DongbaPrototype/Resources/ForestCaptureGraph.asset'
graph={row['id']:row for row in scalar_rows(read(graph_source),'id')}
puzzle_source='Assets/Dialogues/Level 0.csv'
puzzle=csv_rows(puzzle_source)
intro_source='Assets/Dialogues/IntroDialogue.csv'
intro=csv_rows(intro_source)

# Check exports against the actual runtime libraries, not just a potentially stale CSV.
for source,rows in [(puzzle_source,puzzle),(intro_source,intro)]:
    asset_rows=scalar_rows(read(source.replace('.csv','.asset')),'speaker')
    assert len(rows)==len(asset_rows)
    for index,(row,asset) in enumerate(zip(rows,asset_rows)):
        assert row['Index']==str(index)
        assert row['Content']==asset['content'] and row['Speaker']==asset['speaker'],(source,index)
        assert row.get('ChineseContent','')==asset.get('chineseContent',''),(source,index,'Chinese')

sections=[]
records=[]
covered_puzzle=set()
covered_intro=set()
covered_graph=set()

def section(name,note=''):
    sections.append(dict(title=name,note=note,lines=[]))

def add(speaker,en,zh,source,key,note='',status='current'):
    assert en and zh,(key,'missing translation')
    line=dict(number=len(records)+1,speaker=speaker,en=en,zh=zh,source=source,
              key=key,note=note,status=status,section=sections[-1]['title'])
    records.append(line);sections[-1]['lines'].append(line)

def forest(line,key,note='',status='current',source=story_source):
    if ': ' in line:
        speaker,content=line.split(': ',1)
    else:
        speaker,content='',line
    en,zh=translated(content)
    add(speaker,en,zh,source,key,note,status)

def forest_array(name):
    for index,line in enumerate(array(name)):forest(line,f'{name}[{index}]')

def node(key,note=''):
    row=graph[key];assert row['kind']=='1'
    covered_graph.add(key)
    add(row['speaker'],row['en'],row['zh'],graph_source,key,note)

def puzzle_line(index,note='',status='current'):
    row=puzzle[index];assert not row['Content'].startswith('@')
    en,zh=(row['Content'],row['ChineseContent']) if row.get('ChineseContent') else translated(row['Content'])
    add(row['Speaker'],en,zh,puzzle_source,str(index),note,status)
    covered_puzzle.add(index)

section('1. 火塘旁，准备出发')
forest_array('FatherIntroLines')
section('2. 大野猪经过，决定追猎','环境下降触发大野猪从西向东经过后。')
forest_array('HuntInvitationLines')
section('3. 对付大野猪')
forest(const('BoarAidLine'),'BoarAidLine','父亲靠近协助战斗时说一次，属于条件台词。')
section('4. 猎猪后准备回家')
forest_array('HomewardLines')
section('5. 回到西岸，发现刀落下了','当前以剧情节点资产为准。叙事预览版先用动作停顿；普通版多说一句“等等”。')
node('knife_moment','仅普通版；叙事预览版不说这句。')
node('knife_line');node('knife_offer');node('knife_reply')
section('6. 父亲独自回去找刀，与署对峙','署问完后，父亲愣住，没有回答台词。署说完第二句后抓走父亲。')
node('shu_question');node('shu_accusation')
section('7. 高勒趣在火塘旁等待')
forest(const('WaitingNarration'),'WaitingNarration','旁白，配合三天三夜的明暗变化。')
section('8. 决定出发，寻找父亲')
forest_array('RealizationLines')
section('9. 向东走入卷轴','这一段目前由玩家行走、画面退去和卷轴转场表达，没有额外人物台词。')
section('10. 入山，寻找父亲、遇见狐狸')
for i in range(25):
    if not puzzle[i]['Content'].startswith('@'):puzzle_line(i)
section('11. 山间前行，记起父亲的话')
for i in [25,26,39]:puzzle_line(i)
section('12. 路被两蛇拦住','两蛇发出嘶声后分开，高勒趣再道谢。')
for i in [27,28,29,30]:puzzle_line(i)
section('13. 走到巨石前，发现还需要隔开两蛇的视线')
for i in [31,34,35,40,41,42]:puzzle_line(i)
section('14. 洞口的线索','靠近相应洞口才触发；两句顺序取决于玩家路线。')
puzzle_line(37,'高处的洞，接收白蛇。');puzzle_line(38,'低处的洞，接收黑蛇。')
section('15. 解谜完成，返回卷轴','当前没有新增通关对白。两蛇关卡完成后回到卷轴；后续营救关卡、救出父亲及父亲遇难的独立过程，目前没有已接入的完整台词。')
section('16. 最终返乡，面对被破坏的森林','下面是项目里已经实现的结尾段台词。它等待最终营救结束后调用；当前两蛇关卡通关不会直接触发这一段。')
for index,line in enumerate(array('RestorationArrivalLines')):
    forest(line,f'RestorationArrivalLines[{index}]',
           '返乡旁白。' if index==0 else '有被砍倒的树桩时使用，与下面“没有树桩”分支二选一。',
           'ending-not-connected')
restoration_source='Assets/DongbaPrototype/ObliqueForest.RestorationEnding.cs'
restoration=read(restoration_source)
for line in ["Gaolequ: It's so quiet here.","Gaolequ: I'll care for this forest."]:
    assert line in restoration
    forest(line,'no-stumps:'+line,'没有树桩时替换上面两句人物台词；返乡旁白不变。',
           'ending-not-connected',restoration_source)
section('17. 恢复自然后的收尾','两句按当前镜头情况二选一，不会连续说。')
for line,note in [("Gaolequ: You're back.",'附近有动物，且动物确实出现在镜头中。'),
                  ("Gaolequ: I'll come back tomorrow.",'镜头中暂时没有返回的动物。')]:
    assert line in story
    forest(line,'RestorationClosingLine:'+line,note,'ending-not-connected')
main_count=len(records)

section('附录 A. 两蛇台词库中保留、当前没有触发的句子')
puzzle_line(36,status='unused')
section('附录 B. 旧环境／神位实验场景保留的台词','这 8 句保存在 IntroDialogue，并用于 Environment／Template 场景；这两个场景不在当前 Windows 构建的启用场景列表里。一起列出，避免漏掉项目中现有的台词，但它们不接在上述主线后面。')
for i,row in enumerate(intro):
    en,zh=translated(row['Content'])
    add(row['Speaker'],en,zh,intro_source,str(i),status='unused-scene');covered_intro.add(i)

# Every current source line must be represented; commands and duplicate fallbacks
# are accounted for separately rather than disguised as new spoken lines.
assert covered_puzzle=={i for i,row in enumerate(puzzle) if not row['Content'].startswith('@')}
assert covered_intro==set(range(len(intro)))
assert covered_graph=={key for key,row in graph.items() if row['kind']=='1'}
assert len({(r['source'],r['key']) for r in records})==len(records)
for name in ['FatherIntroLines','HuntInvitationLines','HomewardLines','RealizationLines','RestorationArrivalLines']:
    for i,line in enumerate(array(name)):
        assert any(r['key']==f'{name}[{i}]' for r in records)
fallbacks=array('MissingKnifeLines')+array('ShuQuestionLines')+array('ShuAccusationLines')+['Father: Wait a moment...']
for line in fallbacks:
    speaker,content=line.split(': ',1)
    assert any(r['source']==graph_source and r['speaker']==speaker and r['en']==content for r in records), 'Changed fallback needs explicit export: '+line
for quoted in re.findall(literal,story):
    line=json.loads(quoted)
    if line.startswith(('Father: ','Gaolequ: ','Shu: ')):
        speaker,content=line.split(': ',1)
        assert any(r['speaker']==speaker and r['en']==content for r in records), 'Uncovered story line: '+line

def clean(text):return re.sub(r'<[^>]+>','',text)
speaker_names={'Father':'父亲','Gaolequ':'高勒趣','Gao':'高勒趣','Shu':'署','Fox':'狐狸','Snakes':'两蛇','':'旁白'}
output=['全流程台词本（当前游戏，中英对照）','2026-11-22','',
        '从火塘开场、狩猎、丢刀与抓捕、三天三夜、东行卷轴，到狐狸与两蛇解谜，再到最终返乡恢复自然。',
        '正文按剧情顺序排，条件分支都保留。括号是阅读备注，不是游戏字幕。这里只整理现有文本，没有新写或润色台词。',
        f'主线及结尾分支共 {main_count} 条；另附保留但未接入当前流程的 {len(records)-main_count} 条。不是每条都会在一次游玩中出现。',
        '可以直接修改这份文本后交回同步。修改这个 txt 不会自动修改游戏；森林代码、剧情节点和解谜 CSV 目前仍是三个不同的实际来源。','']
for part in sections:
    output.extend([part['title']])
    if part['note']:output.append('（'+part['note']+'）')
    output.append('')
    for row in part['lines']:
        output.append(f"{row['number']:02d}　{speaker_names.get(row['speaker'],row['speaker'])}")
        if row['note']:output.append('（'+row['note']+'）')
        output.extend(['中：'+clean(row['zh']),'EN: '+clean(row['en']),''])
output.extend(['核对范围','取自当前 StoryLines、RestorationEnding、ForestCaptureGraph、Level 0、IntroDialogue 和中英语言表，并核对了解谜场景的对话触发范围。剧情节点优先于代码中的同文回退副本。',
               '以 @ 开头的转身、行走、等待、隐藏是演出指令，不是说出来的台词；没有列成对白。',
               '普通探索、砍树、喂养、种植，以及无对白的镜头变化，没有擅自补入解释性字幕。菜单与按键提示不计入台词。',''])
OUT.parent.mkdir(parents=True,exist_ok=True)
OUT.write_text('\n'.join(output),encoding='utf-8-sig')
REPORT.mkdir(parents=True,exist_ok=True)
revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
manifest=dict(source_commit=revision,total=len(records),main_and_ending=main_count,
              forest=sum(r['source'] in [story_source,graph_source,restoration_source] for r in records),
              puzzle=len(covered_puzzle),unused_intro=len(covered_intro),
              nonspoken_puzzle_commands=sum(row['Content'].startswith('@') for row in puzzle),
              records=records)
(REPORT/'source-map.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
assert '\ufffd' not in OUT.read_text(encoding='utf-8-sig')
saved=OUT.read_text(encoding='utf-8-sig')
assert len(re.findall(r'^中：',saved,re.M))==len(records)
assert len(re.findall(r'^EN: ',saved,re.M))==len(records)
assert [int(n) for n in re.findall(r'^(\d{2})　',saved,re.M)]==list(range(1,len(records)+1))
print(f'Exported {main_count} main/ending + {len(records)-main_count} retained = {len(records)} bilingual lines.')
print('Runtime libraries match CSV; all dialogue graph nodes and all non-command library rows covered.')
print(OUT)
