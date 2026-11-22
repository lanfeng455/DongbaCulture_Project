using System;
using System.Collections.Generic;
using UnityEngine;

namespace DongbaPrototype {
public partial class ObliqueForest {
    [Header("Forest Foley / relative mix")]
    public bool ForestSoundEnabled=true;
    [Range(0,1)] public float ForestEffectsVolume=.7f;
    [Range(0,1)] public float ForestAmbienceVolume=.32f;
    [Range(0,1)] public float ForestFootstepVolume=.70f;
    [Range(0,1)] public float ForestInteractionVolume=.85f;
    [Range(0,1)] public float ForestImpactVolume=.90f;
    enum ForestSoundGroup { Footstep, Swing, Impact, Care, Story, Word }
    sealed class ForestVoice {
        public AudioSource source;public Vector3 point;public float gain;
        public string key;public ForestSoundGroup group;public double started;
    }
    readonly Dictionary<string,AudioClip> forestSounds=new Dictionary<string,AudioClip>();
    readonly List<ForestVoice> forestVoices=new List<ForestVoice>();
    readonly Dictionary<string,int> forestSoundEvents=new Dictionary<string,int>();
    readonly Dictionary<string,int> forestLastVariants=new Dictionary<string,int>();
    readonly System.Random forestAudioRandom=new System.Random(117);
    AudioSource forestWind,forestRiver;
    bool forestAudioReady,forestAudioTracking,fatherWasAirborne,audioGrabbed,audioCarried;
    Vector3 audioLastPlayer,audioLastFather,audioLastBoar;
    float audioPlayerDistance,audioFatherDistance,audioBoarDistance,audioNextRush,audioDuck=1;
    bool audioHadFather,audioHadBoar;
    double audioPlayerLandedAt=-10;
    bool audioBoarRushing;

    void SetupForestAudio(){
        if(forestAudioReady)return;
        foreach(var clip in Resources.LoadAll<AudioClip>("Audio/ForestSfx"))forestSounds[clip.name]=clip;
        for(int i=0;i<20;i++)forestVoices.Add(new ForestVoice{source=NewForestSource("Forest Foley "+i)});
        forestWind=NewForestSource("Quiet woodland air");forestRiver=NewForestSource("River bank water");
        forestWind.loop=forestRiver.loop=true;forestWind.priority=forestRiver.priority=180;
        forestSounds.TryGetValue("forest_bed",out var wind);forestWind.clip=wind;
        forestSounds.TryGetValue("river_bed",out var river);forestRiver.clip=river;
        forestAudioReady=true;ResetForestAudio();
    }
    AudioSource NewForestSource(string label){
        var root=new GameObject(label);root.transform.SetParent(transform,false);
        var source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;
        source.dopplerLevel=0;source.volume=0;source.reverbZoneMix=0;return source;
    }
    // The camera is high above the ground. Attenuate against its ground-plane
    // focus rather than Unity's default camera-distance rolloff.
    Vector3 ForestAudioFocus(){
        var ray=cam.ViewportPointToRay(new Vector3(.5f,.5f,0));
        if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float enter))return ray.GetPoint(enter);
        return Feet;
    }
    float ForestAudibility(Vector3 point){
        var d=point-ForestAudioFocus();d.y=0;
        return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,19,d.magnitude));
    }
    float ForestPan(Vector3 point){return Mathf.Clamp((cam.WorldToViewportPoint(point).x-.5f)*1.1f,-.65f,.65f);}
    ForestSoundGroup SoundGroup(string key){
        if(key.StartsWith("step_",StringComparison.Ordinal)||key=="boar_step"||key=="land")return ForestSoundGroup.Footstep;
        if(key.StartsWith("axe_",StringComparison.Ordinal)||key.StartsWith("knife_",StringComparison.Ordinal)||key=="jump")return ForestSoundGroup.Swing;
        if(key=="plant"||key=="grow"||key=="feed")return ForestSoundGroup.Care;
        if(key.StartsWith("shu_",StringComparison.Ordinal)||key=="boar_rush")return ForestSoundGroup.Story;
        return ForestSoundGroup.Impact;
    }
    float SoundGroupGain(ForestSoundGroup group){
        switch(group){
            case ForestSoundGroup.Footstep:return ForestFootstepVolume;
            case ForestSoundGroup.Care:return ForestInteractionVolume;
            case ForestSoundGroup.Impact:return ForestImpactVolume;
            default:return 1;
        }
    }
    int SoundGroupLimit(ForestSoundGroup group){
        switch(group){
            case ForestSoundGroup.Footstep:return 3;
            case ForestSoundGroup.Swing:return 2;
            case ForestSoundGroup.Impact:return 4;
            case ForestSoundGroup.Care:return 2;
            case ForestSoundGroup.Word:return 1;
            default:return 3;
        }
    }
    bool PlayForestSfx(string key,Vector3 point,float gain=1){
        if(!forestSounds.TryGetValue(key,out var clip))return false;
        return PlayForestClip(key,clip,point,gain,SoundGroup(key));
    }
    bool PlayForestClip(string key,AudioClip clip,Vector3 point,float gain,ForestSoundGroup group){
        if(!forestAudioReady||!ForestSoundEnabled||!isActiveAndEnabled||ForestEffectsVolume<=0||cam==null||clip==null||ForestAudibility(point)<.005f)return false;
        int active=0;ForestVoice voice=null;double now=AudioSettings.dspTime;
        // Reserve four voices for narrative accents / verified words. Frequent
        // footsteps and multi-target swings cannot drown out a story event.
        int available=group==ForestSoundGroup.Story||group==ForestSoundGroup.Word?20:16;
        for(int i=0;i<forestVoices.Count;i++){
            var v=forestVoices[i];
            if(!v.source.isPlaying){if(voice==null&&i<available)voice=v;continue;}
            if(v.group==group)active++;
            if(v.key==key&&now-v.started<.055&&Vector3.SqrMagnitude(v.point-point)<2.25f)return false;
        }
        if(voice==null||active>=SoundGroupLimit(group))return false;
        voice.point=point;voice.gain=gain;voice.key=key;voice.group=group;voice.started=now;
        voice.source.clip=clip;
        // Variations are recorded takes first; use only tiny pitch variation.
        voice.source.pitch=group==ForestSoundGroup.Word?1:.985f+(float)forestAudioRandom.NextDouble()*.03f;
        voice.source.priority=group==ForestSoundGroup.Word?40:group==ForestSoundGroup.Story?60:group==ForestSoundGroup.Footstep?150:90;
        voice.source.panStereo=ForestPan(point);voice.source.volume=ForestVoiceGain(voice);
        voice.source.Play();
        forestSoundEvents.TryGetValue(key,out int count);forestSoundEvents[key]=count+1;
        return true;
    }
    float ForestVoiceGain(ForestVoice voice){
        float duck=voice.group==ForestSoundGroup.Story||voice.group==ForestSoundGroup.Word?1:audioDuck;
        return ForestEffectsVolume*SoundGroupGain(voice.group)*voice.gain*ForestAudibility(voice.point)*duck*(1-Mathf.Clamp01(eastPaperProgress));
    }
    void PlayForestVariant(string family,int count,Vector3 point,float gain=1){
        int next=forestAudioRandom.Next(count);
        if(count>1&&forestLastVariants.TryGetValue(family,out int last)&&next==last)
            next=(last+1+forestAudioRandom.Next(count-1))%count;
        if(PlayForestSfx(family+"_"+next,point,gain))forestLastVariants[family]=next;
    }
    void PlayForestHit(Being target){
        if(target==storyBoar)PlayForestSfx(target.health<=0?"boar_down":"boar_hit",target.root.position);
        else if(target.tree){if(target.health<=0)PlayForestSfx("tree_fall",target.home);else PlayForestVariant("wood",3,target.home);}
        else if(target.animal){if(target.health<=0)PlayForestSfx("animal_down",target.root.position);else PlayForestVariant("hit",3,target.root.position);}
    }
    void PlayForestLanding(Vector3 point,bool playerLanding){
        PlayForestSfx("land",point,playerLanding?1:.65f);
        if(playerLanding){audioPlayerLandedAt=AudioSettings.dspTime;audioPlayerDistance=0;}
    }
    void ForestFootsteps(Vector3 p,ref Vector3 last,ref float distance,bool airborne,float gain){
        float delta=Vector3.Distance(last,p);last=p;
        if(airborne||delta>1.2f){distance=0;return;}
        distance+=delta;
        if(distance>=1.25f){distance%=1.25f;PlayForestVariant("step",4,p,gain);}
    }
    void UpdateForestAudio(float dt){
        if(!forestAudioReady||cam==null||player==null)return;
        if(!ForestSoundEnabled){StopForestAudio();return;}
        // Do not mistake listener pause for a finished clip and restart every loop.
        if(AudioListener.pause||dt<=0)return;
        if(!forestAudioTracking){audioLastPlayer=Feet;audioHadFather=audioHadBoar=false;forestAudioTracking=true;}
        if(!forestWind.isPlaying&&forestWind.clip!=null)forestWind.Play();
        if(!forestRiver.isPlaying&&forestRiver.clip!=null)forestRiver.Play();
        bool word=false;foreach(var v in forestVoices)if(v.group==ForestSoundGroup.Word&&v.source.isPlaying){word=true;break;}
        bool dialogue=fatherIntroActive||ManualStoryDialogueActive;
        float duckTarget=word?.32f:dialogue?.70f:1;
        audioDuck=Mathf.Lerp(audioDuck,duckTarget,1-Mathf.Exp(-dt*(duckTarget<audioDuck?9:2.5f)));
        float landscape=1-Mathf.Clamp01(eastPaperProgress),fade=1-Mathf.Exp(-dt*2);
        float air=ForestAmbienceVolume*.50f*landscape*audioDuck*Mathf.Lerp(1,.75f,Mathf.Clamp01(-ecologyVisual));
        forestWind.volume=Mathf.Lerp(forestWind.volume,air,fade);
        var focus=ForestAudioFocus();float nearRiver=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,9,Mathf.Abs(focus.x-RiverX(focus.z))));
        forestRiver.volume=Mathf.Lerp(forestRiver.volume,ForestAmbienceVolume*.72f*nearRiver*landscape*audioDuck,fade);
        forestRiver.panStereo=Mathf.Lerp(forestRiver.panStereo,ForestPan(new Vector3(RiverX(focus.z),0,focus.z)),fade);
        foreach(var v in forestVoices)if(v.source.isPlaying){
            v.source.volume=Mathf.Lerp(v.source.volume,ForestVoiceGain(v),1-Mathf.Exp(-dt*16));
            v.source.panStereo=Mathf.Lerp(v.source.panStereo,ForestPan(v.point),1-Mathf.Exp(-dt*10));
        }
        ForestFootsteps(Feet,ref audioLastPlayer,ref audioPlayerDistance,jumpAge>=0||AudioSettings.dspTime-audioPlayerLandedAt<.18,1);
        bool hasFather=storyFatherRoot!=null&&storyFatherRoot.gameObject.activeInHierarchy;
        if(hasFather){
            if(!audioHadFather){audioLastFather=storyFatherRoot.position;fatherWasAirborne=fatherJumpAge>=0;audioFatherDistance=0;}
            bool airJump=fatherJumpAge>=0;
            if(airJump&&!fatherWasAirborne)PlayForestSfx("jump",storyFatherRoot.position,.55f);
            if(!airJump&&fatherWasAirborne){PlayForestLanding(storyFatherRoot.position,false);audioFatherDistance=0;}
            fatherWasAirborne=airJump;
            ForestFootsteps(storyFatherRoot.position,ref audioLastFather,ref audioFatherDistance,airJump||captureBeatAge>=.8f,.50f);
        }audioHadFather=hasFather;
        bool hasBoar=storyBoar!=null&&storyBoar.health>0&&storyBoar.root.gameObject.activeInHierarchy;
        if(hasBoar){
            var p=storyBoar.root.position;if(!audioHadBoar){audioLastBoar=p;audioBoarDistance=0;audioBoarRushing=false;}
            float distance=Vector3.Distance(audioLastBoar,p);audioLastBoar=p;
            if(distance<1.2f)audioBoarDistance+=distance;else audioBoarDistance=0;
            if(audioBoarDistance>1.15f){audioBoarDistance%=1.15f;PlayForestSfx("boar_step",p,.8f);}
            bool rushing=journeyStage==JourneyStage.BoarPass&&distance>dt*4&&distance<1.2f;
            if(rushing&&!audioBoarRushing&&clock>=audioNextRush){PlayForestSfx("boar_rush",p,.75f);audioNextRush=clock+2;}
            audioBoarRushing=rushing;
        }else audioBoarRushing=false;
        audioHadBoar=hasBoar;
        if(journeyStage==JourneyStage.ReturnForKnife&&storyShu!=null){
            if(captureBeatAge>=.8f&&!audioGrabbed){audioGrabbed=true;PlayForestSfx("shu_grab",storyShu.position);}
            if(carryStarted&&!audioCarried){audioCarried=true;PlayForestSfx("shu_depart",storyShu.position);}
            if(carryStarted)foreach(var v in forestVoices)if(v.key=="shu_depart"&&v.source.isPlaying)v.point=storyShu.position;
        }
    }
    void StopForestAudio(){
        if(!forestAudioReady)return;
        foreach(var v in forestVoices)v.source.Stop();
        forestWind.Stop();forestRiver.Stop();forestWind.volume=forestRiver.volume=0;forestAudioTracking=false;
    }
    void ResetForestAudio(){
        StopForestAudio();audioPlayerDistance=audioFatherDistance=audioBoarDistance=0;
        audioGrabbed=audioCarried=fatherWasAirborne=audioHadFather=audioHadBoar=audioBoarRushing=false;
        audioNextRush=0;audioDuck=1;audioPlayerLandedAt=-10;forestLastVariants.Clear();
    }
    void OnDisable(){StopForestAudio();}
    void OnEnable(){if(forestAudioReady)ResetForestAudio();}
}
}
