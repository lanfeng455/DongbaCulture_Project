using UnityEngine;
namespace DongbaPrototype {
    // Presentation only. The motor, foot anchor, collision and action clocks own gameplay.
    // Distance drives steps: pressing into a wall cannot play a walking cycle.
    public sealed class PaperActorMotion {
        public struct Pose {public float lift,roll; public Vector3 scale;}
        readonly float idlePhase;
        public PaperActorMotion(float phase=0){idlePhase=phase;Reset();}
        float clock,step,moving,lean,care=-1,hurt=-1,speech=-1,landing=-1;
        float reactionDirection=1;
        Vector3 lastPosition; bool positioned,airborne;
        public Pose Current {get;private set;}
        public float MovingWeight=>moving;
        public void Reset(){clock=step=moving=lean=0;care=hurt=speech=landing=-1;positioned=airborne=false;Current=new Pose{scale=Vector3.one};}
        public void Care(){care=0;}
        public void Hurt(float away){hurt=0;reactionDirection=away<0?-1:1;care=-1;}
        public void Speak(){speech=0;}
        static float Pulse(float age,float duration){if(age<0||age>=duration)return 0;float s=Mathf.Sin(age/duration*Mathf.PI);return s*s;}
        static void Age(ref float age,float dt,float duration){if(age>=0){age+=dt;if(age>duration)age=-1;}}
        public Pose Tick(float dt,Vector3 position,int facing,float jumpAge,float attackAge,float strength=1){
            dt=Mathf.Clamp(dt,0,.05f);clock+=dt;
            Vector3 delta=position-lastPosition;delta.y=0;
            if(!positioned||delta.sqrMagnitude>1){delta=Vector3.zero;positioned=true;}
            lastPosition=position;
            float speed=dt>.00001f?delta.magnitude/dt:0;
            bool inAir=jumpAge>=0;
            if(airborne&&!inAir)landing=0;
            airborne=inAir;
            float weight=1-Mathf.Exp(-dt*13);
            moving=Mathf.Lerp(moving,inAir?0:Mathf.Clamp01(speed/3.2f),weight);
            lean=Mathf.Lerp(lean,Mathf.Clamp(delta.x/Mathf.Max(dt,.0001f)/4,-1,1),weight);
            if(!inAir)step+=delta.magnitude*3f;
            Age(ref care,dt,.68f);Age(ref hurt,dt,.42f);Age(ref speech,dt,.62f);Age(ref landing,dt,.30f);
            float attack=attackAge<0?0:attackAge<.16f?3.7f*Mathf.SmoothStep(0,1,attackAge/.16f):attackAge<.33f?Mathf.Lerp(3.7f,-5.6f,Mathf.SmoothStep(0,1,(attackAge-.16f)/.17f)):Mathf.Lerp(-5.6f,0,Mathf.SmoothStep(0,1,(attackAge-.33f)/.29f));
            float land=Pulse(landing,.30f),bow=attackAge>=0?0:Pulse(care,.68f),flinch=Pulse(hurt,.42f),nod=Pulse(speech,.62f);
            float action=attackAge>=0?.25f:1;
            float stretch=inAir?.025f*Mathf.Sin(Mathf.Clamp01(jumpAge/.85f)*Mathf.PI):0;
            float sy=1+.006f*Mathf.Sin(clock*3.15f+idlePhase)*(1-moving)+stretch-.055f*land-.025f*bow-.025f*flinch;
            Current=new Pose{
                lift=.045f*Mathf.Abs(Mathf.Sin(step))*moving*action,
                roll=(-1.3f*lean+1.8f*Mathf.Sin(step)*moving)*action+facing*(attack-5.2f*bow-1.5f*nod)+6*reactionDirection*flinch,
                scale=new Vector3(1+.025f*land+.01f*bow,sy,1)
            };
            var p=Current;p.lift*=strength;p.roll*=strength;p.scale=Vector3.LerpUnclamped(Vector3.one,p.scale,strength);Current=p;return p;
        }
    }
}
