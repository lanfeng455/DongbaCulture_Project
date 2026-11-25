using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace DongbaPrototype { public partial class ObliqueForest {
    IEnumerator SampleWeatherFrames(float seconds,string state){
        float elapsed=0;int frames=0;while(elapsed<seconds){yield return null;elapsed+=Time.unscaledDeltaTime;frames++;}
        Debug.Log("Living weather "+state+" frame rate: "+(frames/elapsed).ToString("F1"));
    }
    IEnumerator VerifyLivingWeather(Action<bool,string> check){
        StartForestMode(false);tonePreviewFreezeAnimals=true;
        yield return null;yield return null;
        check(fruitMarkSprite!=null&&stormMarkSprite!=null,"Existing transparent fruit and wind assets load");
        check(fruitMarks.Count==12&&stormMarks.Count==6&&brokenCrowns.Count==6,"Weather uses fixed pools");
        var tree=plants.First(b=>b.tree);Vector3 rootBefore=tree.root.position;
        float phase=ecologyWindPhase;
        yield return new WaitForSeconds(.35f);
        check(ecologyWindPhase>phase&&Mathf.Abs(LivingWindPeriod-2.2f)<.01f,"Normal wind advances smoothly at original period");
        check(weatherFruitCount==0&&weatherWindCount==0&&stormBrokenCount==0,"Normal state has no fruit, wind glyphs or breakage");
        while(Balance<FlourishThreshold)RegisterCare();
        yield return SampleWeatherFrames(8,"flourishing");
        check(LivingWindPeriod<1.8f&&weatherLife>.8f,"Flourishing accelerates the shared wind beat gradually");
        check(weatherFruitCount>0,"Flourishing drops fruit from visible trees"); if(XRTrackingControlsCamera)CaptureSceneToTexture("Verification/pico-integration/weather-flourishing.png");
        check(tree.root.position==rootBefore,"Tree roots remain fixed during faster sway");
        check(weatherWindCount==0&&stormBrokenCount==0,"Flourishing never emits storm wind or snaps trees");
        int before=damageCount;var victim=animals.First(b=>b.health>0);ApplyEcologyHit(victim);ApplyEcologyHit(victim);
        check(damageCount==before+2,"Animal kill still costs two ecology points");
        before=damageCount;var cut=plants.First(b=>b.tree&&b.health>0);while(cut.health>0)ApplyEcologyHit(cut);
        check(damageCount==before+1,"Player felling still costs one ecology point");
        while(Balance>-14)RegisterDamage();before=damageCount;int careBefore=careCount;
        yield return SampleWeatherFrames(13,"declining");
        check(weatherStorm>.95f&&weatherWindCount>0,"Deep decline continuously sends translucent wind through the forest"); if(XRTrackingControlsCamera)CaptureSceneToTexture("Verification/pico-integration/weather-declining.png");
        check(stormBrokenCount>0&&stormBrokenCount<=StormBreakLimit,"Severe gusts break a limited number of staggered trees");
        check(damageCount==before&&careCount==careBefore,"Weather does not recursively change ecology scores");
        bool opaque=false;foreach(var mark in stormMarks)opaque|=mark.image.color.a>StormGlyphOpacity+.001f;
        check(!opaque,"World-space wind stays below the configured opacity cap");
        check(plants.Where(b=>b.windBroken).All(b=>b.health==0&&b.root.position==b.home),"Snapped trees keep anchored roots and existing replant interaction");
        bool exact=true;foreach(var entry in breakArt){var original=treeGlyphSets[entry.Key].trunk;exact&=entry.Value.lower.texture==original.texture&&entry.Value.upper.texture==original.texture&&Mathf.Abs(entry.Value.lower.rect.height+entry.Value.upper.rect.height-original.rect.height)<.01f;}
        check(exact,"Broken trunk pieces use disjoint crops of the original glyph");
        var broken=plants.FirstOrDefault(b=>b.windBroken);
        while(Balance<0)RegisterCare();yield return new WaitForSeconds(7);
        if(broken!=null){player.position=broken.home;interactionTarget=broken;int care=careCount;PerformCare();check(broken.health==3&&!broken.windBroken&&careCount==care+1,"Replanting a wind-broken stump restores the original tree");}
        check(stormMarks.All(b=>!b.root.gameObject.activeSelf),"Wind clears smoothly after ecological recovery");
        check(Mathf.Abs(LivingWindPeriod-2.2f)<.02f,"Recovery restores the normal wind frequency");
        StartForestMode(false);
        check(stormBrokenCount==0&&fruitMarks.All(b=>!b.root.gameObject.activeSelf)&&plants.All(b=>!b.windBroken),"Restart clears all weather state and restores complete glyphs");
        if(!XRTrackingControlsCamera){
            float elapsed=0;int frames=0;while(elapsed<2){yield return null;elapsed+=Time.unscaledDeltaTime;frames++;}
            Debug.Log("Living weather normal frame rate: "+(frames/elapsed).ToString("F1"));
        }
    }

    IEnumerator CheckLivingWeather(){
        string folder="Verification/living-weather";Directory.CreateDirectory(folder);
        var lines=new List<string>();bool passed=true;
        yield return new WaitForSeconds(2);DongbaInterface.Instance?.Close(false);yield return null;
        yield return VerifyLivingWeather((ok,label)=>{passed&=ok;lines.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines(folder+"/checks.txt",lines);Debug.Log(lines[lines.Count-1]);});
        Application.Quit(passed?0:1);
    }

    IEnumerator PreviewLivingWeather(){
        string folder="Verification/living-weather";Directory.CreateDirectory(folder+"/frames");
        yield return new WaitForSeconds(2);DongbaInterface.Instance?.Close(false);StartForestMode(false);yield return null;
        tonePreviewFreezeAnimals=true;player.position=ForestSpawn;FollowPlayerCamera(100);
        int savedRate=Time.captureFramerate;Time.captureFramerate=30;
        var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
        var prior=cam.targetTexture;var active=RenderTexture.active;
        try{
            for(int frame=0;frame<1110;frame++){
                if(frame==90)while(Balance<12)RegisterCare();
                if(frame==360)while(Balance>-16)RegisterDamage();
                if(frame==960)while(Balance<0)RegisterCare();
                if(frame==60||frame==330||frame==930){
                    string name=frame==60?"normal":frame==330?"flourishing":"declining";
                    DongbaLanguageCapture.Write(cam,folder+"/"+name+".png");
                }
                cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply(false,false);
                File.WriteAllBytes(folder+"/frames/"+frame.ToString("D4")+".jpg",tex.EncodeToJPG(90));
                cam.targetTexture=prior;RenderTexture.active=active;yield return null;
            }
            File.WriteAllText(folder+"/preview.txt","Normal -> flourishing -> severe decline -> recovery. 30 fps at normal speed.\nEcology counters are advanced by the capture fixture; character/camera fixed and animals held for comparison.\nFruit spawned="+weatherFruitCount+"; wind passes="+weatherWindCount+"; storm breaks="+stormBrokenCount+"\n");
        }finally{Time.captureFramerate=savedRate;tonePreviewFreezeAnimals=false;cam.targetTexture=prior;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(tex);}
        Application.Quit();
    }
}}
