using System.Collections.Generic;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    // Small presentation rhythms on top of the existing routes and ecology priorities.
    sealed class AnimalMoment {
        public int excursion;public bool travelling,looked;
        public float pauseUntil,feedingStarted=-100;
    }
    readonly Dictionary<Being,AnimalMoment> animalMoments=new Dictionary<Being,AnimalMoment>();
    readonly Dictionary<Being,float> sproutRevealStarts=new Dictionary<Being,float>();
    int feedingResponses;
    void ResetForestDetails(){animalMoments.Clear();sproutRevealStarts.Clear();feedingResponses=0;}
    AnimalMoment MomentFor(Being b){
        if(!animalMoments.TryGetValue(b,out var moment)){
            moment=new AnimalMoment{pauseUntil=clock+.35f+Mathf.Repeat(b.phase,1.8f)};
            animalMoments.Add(b,moment);
        }
        return moment;
    }
    bool FeedingResponseActive(Being b){
        return !b.angry&&!b.fleeing&&b.migrationState==0&&animalMoments.TryGetValue(b,out var m)&&clock-m.feedingStarted<.65f;
    }
    void BeginFeedingResponse(Being b){
        b.angry=false;b.health=2;b.fatherHalfHits=0;EndAnimalVisit(b);b.migrationState=0;
        b.fleeing=false;b.proximityFleeUntil=0;b.protectedUntil=clock+12;b.socialReadyAt=clock+1.5f;
        var moment=MomentFor(b);moment.feedingStarted=clock;moment.pauseUntil=clock+.9f;moment.travelling=false;
        b.navRefresh=clock+.15f;
        if(b.speciesIndex!=4&&Mathf.Abs(Feet.x-b.root.position.x)>.1f)b.renderer.flipX=Feet.x<b.root.position.x;
        feedingResponses++;
    }
    void ApplyFeedingPose(Being b){
        if(!FeedingResponseActive(b))return;
        float t=Mathf.Clamp01((clock-animalMoments[b].feedingStarted)/.65f);
        // Uniform, bottom-anchored breathing response, never new strokes or a distorted glyph.
        b.art.localScale*=1+.035f*Mathf.Sin(t*Mathf.PI);
    }
    float QuietAnimalSpeed(Being b){
        float speed=b.speciesIndex<=2?.48f:b.speciesIndex==3||b.speciesIndex==5?.70f:b.speciesIndex>=8?.40f:.55f;
        return speed*(.94f+Mathf.Repeat(b.phase,.12f));
    }
    void PlanQuietAnimalRoam(Being b){
        var m=MomentFor(b);b.navRefresh=clock+.2f;
        if(FeedingResponseActive(b)||clock<b.careReady)return;
        if(HasAnimalRoute(b))return;
        if(m.travelling){
            m.travelling=false;m.looked=false;
            m.pauseUntil=clock+(b.speciesIndex<=3?1.0f:1.8f)+Mathf.Repeat(b.phase+m.excursion*.73f,1.5f);
        }
        if(clock<m.pauseUntil){
            if(!m.looked&&m.pauseUntil-clock<.9f){
                Being neighbor=null;float distance=2.7f;
                foreach(var other in animals){if(other==b||other.health<=0||other.migrationState==2)continue;float d=Vector3.Distance(b.root.position,other.root.position);if(d<distance){distance=d;neighbor=other;}}
                if(neighbor!=null&&b.speciesIndex!=4&&Mathf.Abs(neighbor.root.position.x-b.root.position.x)>.35f)b.renderer.flipX=neighbor.root.position.x<b.root.position.x;
                m.looked=true;
            }
            return;
        }
        m.excursion++;
        for(int i=0;i<6;i++){
            float angle=b.phase*1.37f+(m.excursion+i)*2.39996f;
            float radius=.75f+Mathf.Repeat(b.phase+m.excursion*.61f+i*.31f,1.4f);
            var goal=b.home+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            // Waterfowl already near the river may pause along their own bank; no habitat system.
            if(i==0&&b.speciesIndex<=2&&Mathf.Abs(b.home.x-RiverX(b.home.z))<4.5f){
                goal.z=b.home.z+Mathf.Sin(angle)*1.6f;goal.x=RiverX(goal.z)+Mathf.Sign(b.home.x-RiverX(b.home.z))*2f;
            }
            if(Vector3.Distance(goal,b.root.position)<.55f||!ClearAnimalStep(goal,b))continue;
            if(PlanAnimalRoute(b,goal)){m.travelling=true;return;}
        }
        m.pauseUntil=clock+1.2f;b.navRefresh=m.pauseUntil;
    }
    void BeginSproutReveal(Being b,int order){sproutRevealStarts[b]=clock+order*.14f;b.art.localScale=b.initialScale*.08f;}
    void UpdateCarePlantMoment(Being b){
        if(!sproutRevealStarts.TryGetValue(b,out float start))return;
        float t=Mathf.Clamp01((clock-start)/.65f);
        b.art.localScale=b.initialScale*Mathf.Lerp(.08f,1,Mathf.SmoothStep(0,1,t));
        if(t>=1)sproutRevealStarts.Remove(b);
    }
}}
