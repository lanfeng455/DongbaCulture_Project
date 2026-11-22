using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    bool BoarPaperCoversView(){
        var ground=GameObject.Find("Stationary paper ground").GetComponent<Renderer>().bounds;
        var plane=new Plane(Vector3.up,Vector3.zero);
        for(int i=0;i<4;i++){
            var ray=cam.ViewportPointToRay(new Vector3(i%2,i/2,0));
            if(!plane.Raycast(ray,out float distance))return false;
            var p=ray.GetPoint(distance);
            if(p.x<ground.min.x-.01f||p.x>ground.max.x+.01f||p.z<ground.min.z-.01f||p.z>ground.max.z+.01f)return false;
        }
        return true;
    }
    IEnumerator CheckBoarBoundary(){
        const string folder="Verification/boar-boundary";Directory.CreateDirectory(folder);
        bool record=Array.IndexOf(Environment.GetCommandLineArgs(),"--record-boar-boundary")>=0;
        if(record){Directory.CreateDirectory(folder+"/frames");Time.captureFramerate=24;}
        string report=folder+(record?"/record-checks.txt":"/checks.txt");File.WriteAllText(report,"");bool passed=true;
        Action<string,bool> check=(name,ok)=>{passed&=ok;File.AppendAllText(report,name+"="+ok+"\n");};
        yield return new WaitForSeconds(1);SetStoryModeEnabled(true);
        var sizes=new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1200),new Vector2Int(2560,1080)};
        var spots=new[]{new Vector3(-44.8f,0,0),new Vector3(-44.8f,0,20.5f),new Vector3(-44.8f,0,-20.5f),ForestSpawn,new Vector3(44,0,0)};
        int frame=0;
        for(int s=0;s<(record?1:sizes.Length);s++){
            Screen.SetResolution(sizes[s].x,sizes[s].y,false);yield return new WaitForSeconds(.25f);cam.ResetAspect();
            for(int c=0;c<(record?1:spots.Length);c++){
                string label=sizes[s].x+"x"+sizes[s].y+" position "+c;
                RestartRun();fatherIntroActive=false;ResetForestSubtitles();
                player.position=spots[c];lastDryPosition=Feet;storyFatherRoot.position=Feet+Vector3.right*1.5f;
                FollowPlayerCamera(100);yield return new WaitForSeconds(.3f);
                // Use the real ecology threshold trigger, not a replacement cutscene.
                while(Balance>StoryCaptureBalanceThreshold)RegisterDamage();
                yield return null;
                yield return new WaitForEndOfFrame();
                check(label+" threshold starts hidden boar",journeyStage==JourneyStage.BoarPass&&!storyBoar.root.gameObject.activeSelf);
                float age=0,lastX=boarPassStart.x;bool paper=true,onMap=true,east=true,entered=false,jumped=false,hidden=true,revealChecked=false;
                while(journeyStage==JourneyStage.BoarPass&&age<12){
                    age+=Time.deltaTime;paper&=BoarPaperCoversView();
                    float half=storyBoar.renderer.sprite.bounds.size.x*storyBoar.art.lossyScale.x*.5f;
                    onMap&=storyBoar.root.position.x-half>=-ForestHalfWidth-.01f&&storyBoar.root.position.x+half<=ForestHalfWidth+.01f;
                    if(!boarPassMoving)hidden&=!storyBoar.root.gameObject.activeSelf;
                    else{
                        var right=cam.WorldToViewportPoint(storyBoar.renderer.bounds.max);
                        if(!revealChecked){check(label+" reveal begins outside view",right.x<0);revealChecked=true;}
                        east&=storyBoar.root.position.x>=lastX-.001f;lastX=storyBoar.root.position.x;
                        var v=cam.WorldToViewportPoint(storyBoar.renderer.bounds.center);
                        if(v.x>.12f&&v.x<.88f&&v.y>0&&v.y<1){
                            if(!entered&&s==0){CaptureSceneToTexture(folder+"/view-"+c+".png",1280,720);}
                            entered=true;
                        }
                        jumped|=storyBoar.art.localPosition.y>.4f;
                    }
                    if(record)CaptureSceneToTexture(folder+"/frames/"+(frame++).ToString("D4")+".png",1280,720);
                    yield return new WaitForEndOfFrame();
                }
                check(label+" paper covers every frame",paper);check(label+" entire boar stays on map",onMap);
                check(label+" hidden while camera settles",hidden);check(label+" visible west to east with river jump",entered&&east&&jumped);
                check(label+" reaches east clearing",boarPassComplete&&Vector3.Distance(storyBoar.root.position,BoarClearing)<.1f);
                File.AppendAllText(report,"Duration "+label+"="+age.ToString("F2")+" seconds\n");
                if(record){for(int n=0;n<24;n++){CaptureSceneToTexture(folder+"/frames/"+(frame++).ToString("D4")+".png",1280,720);yield return new WaitForEndOfFrame();}}
            }
        }
        RestartRun();check("Restart removes boar and restores opening",storyBoar==null&&fatherIntroActive&&journeyStage==JourneyStage.Hunting);
        Time.captureFramerate=0;File.AppendAllText(report,"Recorded frames="+frame+"\n");Application.Quit(passed?0:1);
    }
}}
