"""Offline CC0 Foley editing; see Tools/AudioSources/SOURCES.txt.
pip install -r Tools/requirements-audio.txt; python Tools/make_forest_sfx.py
No runtime synthesis and no invented Naxi speech.
"""
from pathlib import Path
import json
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt, resample_poly
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'Tools/AudioSources'
OUT=ROOT/'Assets/DongbaPrototype/Resources/Audio/ForestSfx'
REPORT=ROOT/'Verification/audio-redesign-20261122'
SR=44100
manifest=[]
def load(name,speed=1.,low=6000,high=85):
    path=SRC/name if '.' in name else SRC/'kenney'/(name+'.ogg')
    a,sr=sf.read(path,always_2d=True);a=a.mean(axis=1)
    if sr!=SR:a=resample_poly(a,SR,sr)
    if speed!=1:a=resample_poly(a,1000,round(speed*1000))
    a-=a.mean()
    return sosfilt(butter(2,[high,low],btype='bandpass',fs=SR,output='sos'),a)
def fade(a,attack=.004,release=.035):
    a=a.copy();n=min(int(SR*attack),len(a)//3);m=min(int(SR*release),len(a)//3)
    if n:a[:n]*=np.sin(np.linspace(0,np.pi/2,n))**2
    if m:a[-m:]*=np.cos(np.linspace(0,np.pi/2,m))**2
    a[0]=a[-1]=0
    return a
def cut(a,seconds):return fade(a[:int(seconds*SR)])
def mix(*layers):
    out=np.zeros(max(int(t*SR)+len(a) for a,t,g in layers))
    for a,t,g in layers:
        start=int(t*SR);out[start:start+len(a)]+=a*g
    return out
def save(name,a,rms=.07,peak=.48,loop=False,sources=()):
    a=a.astype(np.float64)
    if not loop:a=fade(a)
    a*=min(rms/max(np.sqrt(np.mean(a*a)),1e-9),peak/max(np.max(abs(a)),1e-9))
    assert np.all(np.isfinite(a)) and np.max(abs(a))<.71
    OUT.mkdir(parents=True,exist_ok=True)
    sf.write(OUT/(name+'.wav'),a,SR,subtype='PCM_16')
    manifest.append(dict(name=name,seconds=round(len(a)/SR,3),rms_db=round(20*np.log10(np.sqrt(np.mean(a*a))),2),peak_db=round(20*np.log10(max(abs(a))),2),sources=list(sources),loop=loop))
def loop_segment(name,source,rms):
    a=load(source,low=6500,high=170);n=28*SR;k=2*SR
    assert len(a)>=n+k
    w=np.linspace(0,1,k)
    # Tail -> head crossfade. Neither side of the seam dips to silence.
    a=np.r_[a[k:n],a[n:n+k]*np.cos(w*np.pi/2)+a[:k]*np.sin(w*np.pi/2)]
    save(name,a,rms=rms,peak=.35,loop=True,sources=[source])
def main():
    for i,name in enumerate(['footstep02','footstep04','footstep05','footstep09']):
        save('step_'+str(i),load(name,low=3700,high=120),.052,.29,sources=[name])
    for i,(name,speed) in enumerate([('cloth1',1.8),('cloth2',1.35),('cloth3',1.5)]):
        save('axe_'+str(i),cut(load(name,speed=speed,low=7600,high=380),.29),.052,.31,sources=[name])
    for i in range(2):
        name='knifeSlice'+('2' if i else '')
        save('knife_'+str(i),cut(load(name,speed=1.6,low=6500,high=500),.26),.041,.28,sources=[name])
    chop=load('chop',low=5800,high=140)
    for i,speed in enumerate([1.,.94,1.06]):
        c=load('chop',speed=speed,low=5200+i*500,high=130)
        tail=cut(load('creak'+str(i+1),speed=1.3,low=3000),.15)*.16
        save('wood_'+str(i),mix((c,0,1),(tail,.07,1)),.10,.57,sources=['chop','creak'+str(i+1)])
    leather=load('dropLeather',low=2700,high=100)
    for i,speed in enumerate([1.,1.09,.93]):
        save('hit_'+str(i),cut(load('dropLeather',speed=speed,low=2200+i*350),.23),.072,.44,sources=['dropLeather'])
    cloth=load('cloth4',low=4700,high=220);foot=load('footstep04',low=3100)
    save('jump',cut(cloth,.23),.031,.19,sources=['cloth4'])
    save('land',mix((foot,0,.85),(cloth,.015,.18)),.062,.38,sources=['footstep04','cloth4'])
    save('tree_fall',mix((chop,0,.65),(load('creak2',speed=.86,low=3200),.08,.65),(load('bookPlace2',speed=.85,low=1900),.66,.55),(cloth,.72,.35)),.083,.58,sources=['chop','creak2','bookPlace2','cloth4'])
    save('animal_down',mix((leather,0,.7),(cloth,.08,.35)),.061,.40,sources=['dropLeather','cloth4'])
    save('player_hurt',cut(leather,.30),.079,.48,sources=['dropLeather'])
    save('plant',mix((foot,0,.32),(load('handleSmallLeather',low=4500),.06,1),(foot,.26,.20)),.045,.29,sources=['footstep04','handleSmallLeather'])
    save('grow',load('bookFlip2',speed=.90,low=5100,high=250),.039,.25,sources=['bookFlip2'])
    save('feed',mix((load('handleSmallLeather2',low=4500),0,1),(cut(foot,.085),.18,.14)),.036,.23,sources=['handleSmallLeather2','footstep04'])
    heavy=load('footstep00',speed=.85,low=2700,high=100)
    save('boar_step',heavy,.065,.35,sources=['footstep00'])
    save('boar_rush',mix((load('cloth1',speed=.82,low=4800,high=150),0,.6),(heavy,.02,.3),(heavy,.19,.2)),.062,.38,sources=['cloth1','footstep00'])
    save('boar_hit',load('dropLeather',speed=.83,low=2300),.085,.49,sources=['dropLeather'])
    save('boar_down',mix((load('dropLeather',speed=.8,low=2200),0,.8),(heavy,.13,.3),(cloth,.21,.5)),.081,.52,sources=['dropLeather','footstep00','cloth4'])
    save('shu_grab',mix((leather,0,.45),(load('clothBelt',speed=.88,low=4400),.025,.6)),.061,.39,sources=['dropLeather','clothBelt'])
    save('shu_depart',mix((load('cloth1',speed=.66,low=5800,high=140),0,.7),(load('bookFlip1',speed=.9,low=4800,high=180),.12,.25)),.058,.35,sources=['cloth1','bookFlip1'])
    loop_segment('forest_bed','wind_excerpt.flac',.038)
    loop_segment('river_bed','river_excerpt.flac',.052)
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    REPORT.mkdir(parents=True,exist_ok=True)
    names=['step_0','step_1','axe_0','wood_0','tree_fall','plant','feed','hit_0','animal_down','shu_depart']
    before=REPORT/'before'
    for label,directory in [('before',before),('after',OUT)]:
        if not directory.exists():continue
        samples=[]
        for name in names:
            a,sr=sf.read(directory/(name+'.wav'));a=a.mean(axis=1) if a.ndim>1 else a
            gain=(.455 if label=='before' else .49) if name.startswith('step') else .7
            if label=='after':
                if name in ['plant','feed']:gain*=.85
                if name.startswith(('wood','tree_fall','hit','animal_down')):gain*=.90
            samples.extend([a*gain,np.zeros(int(.35*SR))])
        sf.write(REPORT/(label+'-sfx.wav'),np.concatenate(samples),SR,subtype='PCM_16')
    (REPORT/'sample-order.txt').write_text('脚步×2 → 挥斧 → 砍木 → 树倒 → 补种 → 喂养 → 受击 → 倒地 → 署带走\n素材对比，非实机录音；使用默认音效音量。',encoding='utf8')
    print('Exported',len(manifest),'Foley clips; maximum sample peak',max(m['peak_db'] for m in manifest),'dBFS')
if __name__=='__main__':main()
