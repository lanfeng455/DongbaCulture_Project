using UnityEngine;using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    Being fatherHuntTarget;float fatherSwingAge=-1,fatherNextSwing,invitationAge;bool fatherSwingHit;int fatherAIFacing;Text boarMarker;
    bool UpdateFatherHuntAI(float dt){
        fatherAIFacing=0;if(fatherIntroActive)return true;if(journeyStage!=JourneyStage.Hunting)return journeyStage==JourneyStage.Invitation||journeyStage==JourneyStage.BoarPass;
        if(Vector3.Distance(Feet,storyFatherRoot.position)>9||(Feet.x-RiverX(Feet.z))*(storyFatherRoot.position.x-RiverX(storyFatherRoot.position.z))<0){fatherHuntTarget=null;return false;}
        if(fatherHuntTarget==null||fatherHuntTarget.health<=0||fatherHuntTarget.migrationState!=0||Vector3.Distance(fatherHuntTarget.root.position,Feet)>9){fatherHuntTarget=null;float nearest=7;foreach(var a in animals){float d=Vector3.Distance(a.root.position,storyFatherRoot.position);if(a.health>0&&a.age==2&&a.migrationState==0&&clock>=a.protectedUntil&&d<nearest&&(a.root.position.x-RiverX(a.root.position.z))*(Feet.x-RiverX(Feet.z))>0){nearest=d;fatherHuntTarget=a;}}}
        if(fatherHuntTarget==null)return false;Vector3 delta=fatherHuntTarget.root.position-storyFatherRoot.position;fatherAIFacing=delta.x<0?-1:1;
        if(delta.magnitude>1.4f)MoveStoryFatherToward(fatherHuntTarget.root.position,2.8f,dt);
        else if(fatherSwingAge<0&&clock>=fatherNextSwing){fatherSwingAge=0;fatherSwingHit=false;fatherNextSwing=clock+1;}
        if(fatherSwingAge>=0){fatherSwingAge+=dt;if(fatherSwingAge>=.25f&&!fatherSwingHit){fatherSwingHit=true;if(delta.magnitude<1.9f)ApplyFatherLightHit(fatherHuntTarget);}if(fatherSwingAge>.6f)fatherSwingAge=-1;}
        storyFatherImage.sortingOrder=100-Mathf.RoundToInt(storyFatherRoot.position.z*10);return true;
    }
    void ApplyFatherLightHit(Being target){if(target==null||target.health<=0)return;target.fatherHalfHits++;target.angry=true;if(target.fatherHalfHits%2==0)ApplyEcologyHit(target);if(target.health<=0){target.root.gameObject.SetActive(false);fatherHuntTarget=null;}}
    void UpdateFatherKnifeAnimation(){if(storyFatherKnifeImage==null)return;if(fatherSwingAge>=0&&fatherHuntTarget==null){fatherSwingAge+=Time.deltaTime;if(fatherSwingAge>.6f)fatherSwingAge=-1;}storyFatherKnifeImage.enabled=StoryModeEnabled&&!fatherIntroActive&&journeyStage==JourneyStage.Hunting&&fatherSwingAge>=0;float angle=fatherSwingAge>=0?Mathf.Sin(fatherSwingAge/.6f*Mathf.PI)*-95:0;storyFatherKnifeImage.transform.localRotation=Quaternion.Euler(0,0,angle);}
    void DrawBoarTargetMarker(){
        EnsureQuietBoarMarker();
        if(boarMarker==null)return;
        bool show=StoryModeEnabled&&journeyStage==JourneyStage.Boar&&storyBoar!=null&&storyBoar.health>0&&storyBoar.root.gameObject.activeInHierarchy;
        Vector3 v=Vector3.zero;
        if(show){
            Vector3 feet=cam.WorldToViewportPoint(storyBoar.root.position);
            v=cam.WorldToViewportPoint(storyBoar.root.position+cam.transform.up*2.3f);
            // A target label is not an off-screen waypoint. Never pin it to a screen edge.
            show=feet.z>0&&feet.x>0&&feet.x<1&&feet.y>0&&feet.y<1&&v.z>0&&v.x>.06f&&v.x<.94f&&v.y>.07f&&v.y<.93f;
        }
        uiBoarPlate.SetActive(show);if(!show)return;
        var rect=(RectTransform)uiBoarPlate.transform;rect.anchorMin=rect.anchorMax=new Vector2(v.x,v.y);rect.anchoredPosition=Vector2.zero;
        boarMarker.text="GREAT BOAR\n"+storyBoar.health+" / 6";
    }
}}
