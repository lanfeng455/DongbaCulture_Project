using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DongbaPrototype
{
    // Coordinates are world units. Rendering alone uses a fixed 1280 x 720 canvas.
    public sealed partial class ForestPrototype : MonoBehaviour
    {
        public enum Phase { Explore, Battle, Victory, Defeat }
        public Phase State { get; private set; }
        public float X { get; private set; } = 2.5f;
        public float Y { get; private set; }
        public int Hits { get; private set; }
        public int PlayerHealth { get; private set; } = 3;
        public int Falls { get; private set; }
        public bool Busy { get; private set; }
        [Header("Restrained ambient motion")]
        [Range(1.8f,2.6f)] public float MasterPeriod=2.2f;
        [Range(0,1.5f)] public float WindStrength=.85f;
        [Range(0,16)] public float TreeBreezePixels=11f;
        [Range(0,24)] public float TreeGustPixels=11f;
        [Range(0,10)] public float GrassBreezePixels=6f;
        [Range(0,16)] public float GrassGustPixels=8f;
        [Range(10,50)] public float CloudDriftRangePixels=28f;
        [Range(10,24)] public float CloudDriftPeriod=14f;
        [Range(0,.015f)] public float IdleBreath=.009f;
        public bool ShowDiagnostics=false;

        [Header("Player movement feedback")]
        [Range(20,80)] public float MoveAcceleration=46f;
        [Range(30,100)] public float MoveDeceleration=68f;
        [Range(0,5)] public float MoveLeanDegrees=3f;
        [Range(0,5)] public float StepBobPixels=3.2f;
        [Range(0,.02f)] public float StepSquash=.008f;
        [Range(.05f,.14f)] public float TurnDuration=.09f;
        [Range(0,.15f)] public float TurnCompression=.08f;

        [Header("Idle feedback")]
        [Range(1.5f,2.2f)] public float IdlePeriod=1.85f;
        [Range(.01f,.02f)] public float IdleVerticalScale=.015f;
        [Range(.005f,.01f)] public float IdleHorizontalComp=.0075f;

        [Header("Jump and landing feedback")]
        [Range(.06f,.09f)] public float JumpSquashDuration=.075f;
        [Range(.03f,.05f)] public float JumpStretch=.045f;
        [Range(.02f,.03f)] public float JumpNarrow=.025f;
        [Range(.08f,.14f)] public float LandingDuration=.12f;
        [Range(.03f,.09f)] public float LandingSquash=.065f;
        [Range(.08f,.12f)] public float CoyoteTime=.1f;
        [Range(.1f,.12f)] public float JumpBufferTime=.11f;

        [Header("Attack feedback")]
        [Range(.08f,.12f)] public float AttackWindup=.1f;
        [Range(8,20)] public float AttackLungePixels=14f;
        [Range(4,12)] public float AttackLeanDegrees=8f;
        public float FirstHitStop=.05f,SecondHitStop=.08f,ThirdHitStop=.12f;
        public float SecondHitShakePixels=2.5f,ThirdHitShakePixels=5f;

        float velocity, clock, ambientClock, impact, flash, currentMove, moveVelocity, stepPhase;
        float environmentFreeze, impactWind;
        float turnTimer, jumpVisualAge, landingTimer, landingStrength, coyoteRemaining, jumpBufferRemaining;
        float hitStopRemaining, animalHitTimer, animalKnockback, animalKnockbackVelocity;
        float cameraShakeTimer, cameraShakeDuration, cameraShakeAmplitude, defeatTimer;
        float animalWorldX=AnimalX,animalMoveVelocity,animalAttackWindup,animalAttackCooldown,animalPatrolTime;
        float playerInvulnerable,playerHitFlash,playerKnockbackVelocity,playerRespawnTimer;
        int animalFacing=-1,animalAttackDirection=-1;
        int activeAttackPower;
        bool currentAttackHit;
        int visualFacing=1;
        float fpsWindowTime, displayedFps, displayedAnimationRate;
        int fpsWindowFrames, animationWindowUpdates, fpsTestFrames;
        int facing=1, attackFacing=1;
        bool grounded = true;
        Texture2D paper, environment, playerArt, animalArt, axeArt, cloudArt;
        readonly Texture2D[] waterArt = new Texture2D[10];
        readonly Texture2D[] ambientArt = new Texture2D[6];
        readonly Rect[] ambientRects = {
            new Rect(14,156,122,366), new Rect(130,222,122,304),
            new Rect(1010,217,130,316), new Rect(251,396,53,130),
            new Rect(305,408,57,118), new Rect(785,450,50,76)
        };
        readonly Texture2D[] glyphs = new Texture2D[5];
        readonly float[] ambientPhase = new float[6];
        readonly float[] ambientAmplitude = new float[6];
        readonly float[] debugTreeDisplacement = new float[3];
        GUIStyle label, small, title;
        AudioSource sound, music;
        AudioClip[] strikes;
        AudioClip backgroundMusic;
        [Header("Audio")]
        [Range(0f,.5f)] public float MusicVolume=.18f;
        struct InkParticle { public Vector2 position,velocity; public float age,lifetime,size; public bool active; }
        readonly InkParticle[] inkParticles=new InkParticle[36];
        readonly Vector2[] axeBladeCollider=new Vector2[4];
        readonly System.Random effectRandom=new System.Random(9407);
        static readonly Color Ink = new Color(.17f,.23f,.20f);
        static readonly Color Red = new Color(.65f,.22f,.16f);
        static readonly Color PaperPanel = new Color(.94f,.88f,.75f,.78f);
        static readonly string[] Names = { "PLAYER", "GROUND", "TREE", "RIVER", "ANIMAL" };
        static readonly float[] GustStarts={3.0f,11.2f,20.8f,30.1f,40.7f,50.0f};
        static readonly Rect WaterChannel=new Rect(455,548,360,126);
        static readonly Rect[] WaterRects={
            new Rect(-13,12,84,46),new Rect(33,8,122,53),new Rect(155,8,122,57),new Rect(269,8,107,57),
            new Rect(-6,46,99,53),new Rect(71,39,141,57),new Rect(208,43,122,57),
            new Rect(17,73,137,65),new Rect(132,69,137,65),new Rect(238,73,122,72)
        };
        static readonly int[] WaterRows={0,0,0,0,1,1,1,2,2,2};
        static readonly int[] WaterColumns={0,1,2,3,0,1,2,0,1,2};
        static readonly float[] WaterAmplitudes={2.7f,3.4f,3.8f,2.7f,2.9f,4.8f,3.1f,3.0f,4.0f,3.0f};
        static readonly float[] TreeTipLocalX={90f,94f,108f};
        static readonly float[] TreeTipLocalY={24f,76f,25f};
        const float RiverLeft = 7.2f;
        const float RiverRight = 12.6f;
        const float AnimalX = 16f;
        const float MoveSpeed = 5.8f;
        const float JumpSpeed = 12.2f;
        // Exported transparent actor textures retain the original ink at its
        // original pixel scale and add transparent padding around it.
        const float PlayerTextureWidthRatio = 137f/95f;
        const float PlayerTextureHeightRatio = 143f/119f;
        const float PlayerFootPivotY = 131f/143f;
        const float ChickenTextureWidthRatio = 180f/140f;
        const float ChickenTextureHeightRatio = 146f/122f;
        const float ChickenFootPivotY = 134f/146f;
        bool automated, windPreview, fpsTest, motionDebug, actionPreview, symmetryPreview;
        string symmetryValidationId="default";
        int symmetrySuperSize=1;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            QualitySettings.vSyncCount=0;
            string[] args=Environment.GetCommandLineArgs();
            windPreview=Array.IndexOf(args,"--wind-preview")>=0;
            fpsTest=Array.IndexOf(args,"--fps-test")>=0;
            motionDebug=Array.IndexOf(args,"--motion-debug")>=0;
            actionPreview=Array.IndexOf(args,"--action-preview")>=0;
            symmetryPreview=Array.IndexOf(args,"--symmetry-preview")>=0;
            experiencePreview=Array.IndexOf(args,"--experience-preview")>=0;
            int validationIdIndex=Array.IndexOf(args,"--validation-id");
            if(validationIdIndex>=0&&validationIdIndex+1<args.Length) symmetryValidationId=args[validationIdIndex+1];
            int superSizeIndex=Array.IndexOf(args,"--capture-super-size");
            if(superSizeIndex>=0&&superSizeIndex+1<args.Length) int.TryParse(args[superSizeIndex+1],out symmetrySuperSize);
            symmetrySuperSize=Mathf.Clamp(symmetrySuperSize,1,4);
            automated=windPreview||fpsTest||actionPreview||symmetryPreview||experiencePreview||Array.IndexOf(args,"--prototype-test")>=0;
            paper = new Texture2D(256,256);
            var random = new System.Random(42);
            var pixels = new Color[256*256];
            for(int i=0;i<pixels.Length;i++) { float n=(float)random.NextDouble()*.045f; pixels[i]=new Color(.92f-n,.88f-n,.76f-n); }
            paper.SetPixels(pixels); paper.Apply();
            for(int i=0;i<5;i++) glyphs[i]=Resources.Load<Texture2D>("Glyphs/"+Names[i].ToLowerInvariant());
            environment=Resources.Load<Texture2D>("ApprovedArt/forest_environment");
            playerArt=Resources.Load<Texture2D>("ApprovedArt/player_glyph");
            animalArt=Resources.Load<Texture2D>("ApprovedArt/chicken_colored");
            axeArt=Resources.Load<Texture2D>("ApprovedArt/axe_glyph");
            string[] ambientNames={"tree_left","tree_mid_left","tree_right","plant_single_left_branch","plant_single_left_curve","plant_single_right_bank"};
            for(int i=0;i<ambientArt.Length;i++) ambientArt[i]=Resources.Load<Texture2D>("ApprovedArt/ambient_"+ambientNames[i]);
            cloudArt=Resources.Load<Texture2D>("ApprovedArt/ambient_cloud_v2");
            for(int i=0;i<waterArt.Length;i++) waterArt[i]=Resources.Load<Texture2D>($"ApprovedArt/ambient_water_{i:00}");
            var windRandom=new System.Random(7319);
            for(int i=0;i<ambientArt.Length;i++) {
                float randomPhase=(float)windRandom.NextDouble()*Mathf.PI*2;
                ambientPhase[i]=i<3?0:randomPhase;
                ambientAmplitude[i]=Mathf.Lerp(.85f,1.15f,(float)windRandom.NextDouble());
            }
            sound=gameObject.AddComponent<AudioSource>(); sound.volume=.12f;
            backgroundMusic=Resources.Load<AudioClip>("Audio/naxi_inspired_forest_bgm");
            music=gameObject.AddComponent<AudioSource>();
            music.loop=true; music.playOnAwake=false; music.spatialBlend=0; music.volume=MusicVolume;
            if(backgroundMusic!=null) { music.clip=backgroundMusic; music.Play(); }
            strikes=new AudioClip[3];
            for(int k=0;k<3;k++) {
                float[] samples=new float[16000];
                for(int i=0;i<samples.Length;i++) { float t=i/32000f; samples[i]=(Mathf.Sin(t*180*Mathf.PI*2)+(float)(random.NextDouble()*2-1)*.2f)*Mathf.Exp(-t*24)*.5f; }
                strikes[k]=AudioClip.Create("Impact "+(k+1),samples.Length,1,32000,false); strikes[k].SetData(samples,0);
            }
            if(fpsTest) StartCoroutine(MeasureFps());
            else if(windPreview) StartCoroutine(CaptureWindPreview());
            else if(actionPreview) StartCoroutine(CaptureActionPreview());
            else if(symmetryPreview) StartCoroutine(CaptureAttackSymmetry());
            else if(experiencePreview) StartCoroutine(CaptureExperiencePreview());
            else if(automated) StartCoroutine(SmokeTest());
        }

        void Update()
        {
            float dt=(windPreview||actionPreview||symmetryPreview)?1f/60f:Time.unscaledDeltaTime;
            clock+=dt;
            if(environmentFreeze>0) environmentFreeze=Mathf.Max(0,environmentFreeze-dt);
            else {
                ambientClock+=dt;
                impactWind=Mathf.MoveTowards(impactWind,0,dt*2.2f);
            }
            fpsWindowTime+=dt; fpsWindowFrames++; animationWindowUpdates++;
            if(fpsTest) fpsTestFrames++;
            if(fpsWindowTime>=.5f) {
                displayedFps=fpsWindowFrames/fpsWindowTime;
                displayedAnimationRate=animationWindowUpdates/fpsWindowTime;
                fpsWindowTime=0; fpsWindowFrames=animationWindowUpdates=0;
            }
            flash=Mathf.MoveTowards(flash,0,dt*3);
            UpdateFeedback(dt);
            UpdateExperience(dt);
            if(!automated) UpdateAnimalAI(dt);
            if(automated) return;
            var k=Keyboard.current;
            if(k==null) return;
            if(k.rKey.wasPressedThisFrame) { ResetGame(); return; }
            if(State==Phase.Defeat) return;
            float move=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);
            Tick(move,k.spaceKey.wasPressedThisFrame||k.wKey.wasPressedThisFrame||k.upArrowKey.wasPressedThisFrame,Time.deltaTime);
            if(k.jKey.wasPressedThisFrame||k.enterKey.wasPressedThisFrame) Attack();
            if(k.eKey.wasPressedThisFrame) InteractExperience();
        }

        void UpdateFeedback(float dt)
        {
            if(!Busy) {
                float previousTurn=turnTimer;
                turnTimer=Mathf.Max(0,turnTimer-dt);
                if(previousTurn>TurnDuration*.5f&&turnTimer<=TurnDuration*.5f) visualFacing=facing;
            }
            landingTimer=Mathf.Max(0,landingTimer-dt);
            hitStopRemaining=Mathf.Max(0,hitStopRemaining-dt);
            animalHitTimer=Mathf.Max(0,animalHitTimer-dt);
            cameraShakeTimer=Mathf.Max(0,cameraShakeTimer-dt);
            playerInvulnerable=Mathf.Max(0,playerInvulnerable-dt);
            playerHitFlash=Mathf.Max(0,playerHitFlash-dt);
            animalAttackCooldown=Mathf.Max(0,animalAttackCooldown-dt);
            if(!grounded) jumpVisualAge+=dt;

            animalKnockback+=animalKnockbackVelocity*dt;
            animalKnockbackVelocity*=Mathf.Exp(-dt*11f);
            if(Mathf.Abs(animalKnockbackVelocity)<1) animalKnockbackVelocity=0;
            if(Hits<3) animalKnockback=Mathf.MoveTowards(animalKnockback,0,dt*24f);

            if(defeatTimer>0) {
                defeatTimer=Mathf.Max(0,defeatTimer-dt);
                if(defeatTimer<=0&&Hits>=3) State=Phase.Victory;
            }
            if(playerRespawnTimer>0) {
                playerRespawnTimer=Mathf.Max(0,playerRespawnTimer-dt);
                if(playerRespawnTimer<=0) RespawnPlayer();
            }
            for(int i=0;i<inkParticles.Length;i++) if(inkParticles[i].active) {
                InkParticle p=inkParticles[i];
                p.age+=dt;
                if(p.age>=p.lifetime) p.active=false;
                else {
                    p.position+=p.velocity*dt;
                    p.velocity.y+=34f*dt;
                    p.velocity*=Mathf.Exp(-dt*2.8f);
                }
                inkParticles[i]=p;
            }
        }

        public void Tick(float move,bool jump,float dt)
        {
            // Combat never gates locomotion: move and jump during windup, hit and recovery.
            move=Mathf.Clamp(move,-1,1);
            if(Mathf.Abs(move)>.01f) {
                int desiredFacing=move>0?1:-1;
                if(desiredFacing!=facing) {
                    facing=desiredFacing;
                    if(!Busy) turnTimer=TurnDuration;
                }
            }
            if(jump) jumpBufferRemaining=JumpBufferTime;
            dt=Mathf.Min(dt,.1f);
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt/.01f));
            for(int i=0;i<steps;i++) {
                float step=dt/steps;
                if(grounded) coyoteRemaining=CoyoteTime;
                else coyoteRemaining=Mathf.Max(0,coyoteRemaining-step);
                jumpBufferRemaining=Mathf.Max(0,jumpBufferRemaining-step);
                if(jumpBufferRemaining>0&&(grounded||coyoteRemaining>0)) BeginJump();

                float targetSpeed=move*MoveSpeed;
                float response=Mathf.Abs(move)>.01f?MoveAcceleration:MoveDeceleration;
                moveVelocity=Mathf.MoveTowards(moveVelocity,targetSpeed,response*step);
                X=Mathf.Clamp(X+(moveVelocity+playerKnockbackVelocity)*step,.5f,19.5f);
                playerKnockbackVelocity*=Mathf.Exp(-step*8f);
                stepPhase+=Mathf.Abs(moveVelocity)*step*1.85f;
                bool bank=X<RiverLeft||X>RiverRight;
                if(!grounded||!bank) { grounded=false; velocity-=22*step; Y+=velocity*step; }
                if(Y<=0&&bank&&velocity<=0) {
                    float landingSpeed=-velocity;
                    bool wasAirborne=!grounded;
                    Y=0; velocity=0; grounded=true;
                    if(wasAirborne&&landingSpeed>1.2f) Land(landingSpeed);
                    if(jumpBufferRemaining>0) BeginJump();
                }
                if(Y< -2.5f) { Falls++; X=6.4f; Y=0; velocity=moveVelocity=0; grounded=true; visualFacing=facing=1; }
            }
            currentMove=moveVelocity/MoveSpeed;
            if(Hits<3&&State!=Phase.Defeat) State=animalProvoked&&!ExperienceAnimalFriendly&&Mathf.Abs(X-animalWorldX)<3.5f?Phase.Battle:Phase.Explore;
        }

        void UpdateAnimalAI(float dt)
        {
            if(Hits>=3||State==Phase.Victory||State==Phase.Defeat) return;
            if(UpdateFriendlyExperienceAnimal(dt)) return;
            float distance=X-animalWorldX;
            bool playerReachable=X>RiverRight&&Y<1.35f;
            bool alerted=animalProvoked&&playerReachable&&Mathf.Abs(distance)<4.8f;

            if(animalAttackWindup>0) {
                float previous=animalAttackWindup;
                animalAttackWindup=Mathf.Max(0,animalAttackWindup-dt);
                if(previous>0&&animalAttackWindup<=0&&Mathf.Abs(X-animalWorldX)<1.15f&&Y<1.25f)
                    DamagePlayer(animalAttackDirection);
                return;
            }

            if(alerted) {
                animalFacing=distance>=0?1:-1;
                if(Mathf.Abs(distance)>1.0f) {
                    animalMoveVelocity=Mathf.MoveTowards(animalMoveVelocity,animalFacing*1.65f,dt*5f);
                    animalWorldX=Mathf.Clamp(animalWorldX+animalMoveVelocity*dt,13.35f,18.4f);
                } else {
                    animalMoveVelocity=Mathf.MoveTowards(animalMoveVelocity,0,dt*8f);
                    if(animalAttackCooldown<=0) {
                        animalAttackDirection=animalFacing;
                        animalAttackWindup=.28f;
                        animalAttackCooldown=1.15f;
                    }
                }
            } else {
                animalPatrolTime+=dt;
                float patrolTarget=AnimalX+Mathf.Sin(animalPatrolTime*.72f)*.72f;
                animalFacing=patrolTarget>=animalWorldX?1:-1;
                animalMoveVelocity=Mathf.MoveTowards(animalMoveVelocity,animalFacing*.58f,dt*1.8f);
                if(Mathf.Abs(patrolTarget-animalWorldX)<.06f) animalMoveVelocity*=.4f;
                animalWorldX=Mathf.Clamp(animalWorldX+animalMoveVelocity*dt,15.15f,16.85f);
            }
        }

        void DamagePlayer(int direction)
        {
            if(playerInvulnerable>0||PlayerHealth<=0) return;
            PlayerHealth=Mathf.Max(0,PlayerHealth-1);
            playerInvulnerable=.9f;
            playerHitFlash=.3f;
            playerKnockbackVelocity=direction*4.8f;
            cameraShakeAmplitude=2.5f;
            cameraShakeDuration=cameraShakeTimer=.1f;
            SpawnInk(new Vector2(50+X*59,486-Y*59),6,.72f,false);
            if(PlayerHealth<=0) {
                State=Phase.Defeat;
                Busy=false; impact=0;
                playerRespawnTimer=1.05f;
            }
        }

        void RespawnPlayer()
        {
            PlayerHealth=3;
            X=6.4f; Y=0; velocity=moveVelocity=playerKnockbackVelocity=0;
            grounded=true; facing=visualFacing=attackFacing=1;
            playerInvulnerable=1f;
            State=Phase.Explore;
            animalWorldX=AnimalX;
            animalMoveVelocity=animalAttackWindup=0;
        }

        void BeginJump()
        {
            velocity=JumpSpeed;
            grounded=false;
            coyoteRemaining=jumpBufferRemaining=0;
            jumpVisualAge=0;
        }

        void Land(float speed)
        {
            landingTimer=LandingDuration;
            landingStrength=Mathf.Lerp(.55f,1,Mathf.InverseLerp(JumpSpeed*.65f,JumpSpeed*1.7f,speed));
            int count=Mathf.RoundToInt(Mathf.Lerp(4,8,landingStrength));
            SpawnInk(new Vector2(50+X*59,536),count,landingStrength,false);
        }

        void SpawnInk(Vector2 origin,int count,float strength,bool attackInk)
        {
            for(int n=0;n<count;n++) {
                int slot=-1;
                for(int i=0;i<inkParticles.Length;i++) if(!inkParticles[i].active) { slot=i; break; }
                if(slot<0) return;
                float side=(float)(effectRandom.NextDouble()*2-1);
                float up=(float)effectRandom.NextDouble();
                InkParticle p=new InkParticle {
                    active=true,age=0,
                    lifetime=Mathf.Lerp(.25f,.45f,(float)effectRandom.NextDouble()),
                    size=Mathf.Lerp(1.8f,3.8f,(float)effectRandom.NextDouble())*strength,
                    position=origin+new Vector2(side*5,(float)(effectRandom.NextDouble()*4-2)),
                    velocity=attackInk
                        ? new Vector2(attackFacing*Mathf.Lerp(22,58,up),Mathf.Lerp(-34,20,(float)effectRandom.NextDouble()))*strength
                        : new Vector2(side*Mathf.Lerp(16,42,up),Mathf.Lerp(-42,-18,(float)effectRandom.NextDouble()))*strength
                };
                inkParticles[slot]=p;
            }
        }

        void DrawInkParticles()
        {
            for(int i=0;i<inkParticles.Length;i++) if(inkParticles[i].active) {
                InkParticle p=inkParticles[i];
                float alpha=1-p.age/p.lifetime;
                Box(new Rect(p.position.x-p.size*.5f,p.position.y-p.size*.5f,p.size,p.size),new Color(.32f,.17f,.11f,alpha*.78f));
            }
        }

        public void Attack() { if(!Busy&&PlayerHealth>0&&State!=Phase.Defeat) StartCoroutine(Strike()); }
        IEnumerator Strike()
        {
            Busy=true;
            // Lock every part of this swing to one direction. A strike during a
            // turn completes the visual turn immediately; later movement input is
            // remembered in facing and is applied only after recovery.
            attackFacing=facing;
            visualFacing=attackFacing;
            turnTimer=0;
            activeAttackPower=Mathf.Clamp(Hits+1,1,3);
            currentAttackHit=false;
            float thrustDuration=.08f;
            float recoveryDuration=.22f;
            float duration=AttackWindup+thrustDuration+recoveryDuration;
            float t=0; bool struck=false;
            while(t<duration) {
                if(PlayerHealth<=0) break;
                if(hitStopRemaining>0) { yield return null; continue; }
                t+=Time.deltaTime;
                if(t<AttackWindup) impact=-.16f*Mathf.SmoothStep(0,1,t/AttackWindup);
                else if(t<AttackWindup+thrustDuration) impact=Mathf.SmoothStep(0,1,(t-AttackWindup)/thrustDuration);
                else impact=1-Mathf.SmoothStep(0,1,(t-AttackWindup-thrustDuration)/recoveryDuration);
                // One hit per swing. The transformed blade polygon is the attack
                // collider; there is no separate invisible forward range.
                float chopProgress=(t-AttackWindup)/thrustDuration;
                bool bladeInActiveArc=chopProgress>=.38f&&chopProgress<=1.55f;
                if(!struck&&bladeInActiveArc&&TryStrikeExperienceTree()) {
                    struck=true; currentAttackHit=true;
                }
                if(!struck&&Hits<3&&bladeInActiveArc&&AxeBladeOverlapsAnimal()) {
                    struck=true; currentAttackHit=true; Hits++; activeAttackPower=Hits; flash=.55f;
                    RecordExperienceAnimalHarm(Hits>=3);
                    float stop=Hits==1?FirstHitStop:(Hits==2?SecondHitStop:ThirdHitStop);
                    hitStopRemaining=stop; environmentFreeze=stop; impactWind=attackFacing;
                    animalHitTimer=stop+.16f;
                    animalKnockbackVelocity=attackFacing*(Hits==1?42f:(Hits==2?72f:118f));
                    SpawnInk(new Vector2(50+animalWorldX*59,486),Hits==1?4:(Hits==2?7:12),.55f+Hits*.22f,true);
                    if(Hits>=2) {
                        cameraShakeAmplitude=Hits==2?SecondHitShakePixels:ThirdHitShakePixels;
                        cameraShakeDuration=cameraShakeTimer=Hits==2?.11f:.14f;
                    }
                    if(Hits==3) defeatTimer=.46f;
                    sound.PlayOneShot(strikes[Hits-1]);
                }
                yield return null;
            }
            impact=0;
            if(PlayerHealth<=0) { Busy=false; activeAttackPower=0; currentAttackHit=false; yield break; }
            if(Hits==3) {
                while(defeatTimer>0) yield return null;
                State=Phase.Victory;
            }
            Busy=false; activeAttackPower=0; currentAttackHit=false;
            if(facing!=visualFacing) turnTimer=TurnDuration;
        }

        public void ResetGame(bool stopAnimations=true) {
            if(stopAnimations) StopAllCoroutines();
            State=Phase.Explore; X=2.5f; Y=0; velocity=currentMove=moveVelocity=0; Hits=0; Falls=0; PlayerHealth=3;
            grounded=true; Busy=false; impact=flash=0; facing=visualFacing=attackFacing=1;
            turnTimer=jumpVisualAge=landingTimer=coyoteRemaining=jumpBufferRemaining=0;
            hitStopRemaining=animalHitTimer=animalKnockback=animalKnockbackVelocity=0;
            cameraShakeTimer=cameraShakeDuration=cameraShakeAmplitude=defeatTimer=0;
            playerInvulnerable=playerHitFlash=playerKnockbackVelocity=playerRespawnTimer=0;
            animalWorldX=AnimalX; animalMoveVelocity=animalAttackWindup=animalAttackCooldown=animalPatrolTime=0;
            animalFacing=animalAttackDirection=-1;
            for(int i=0;i<inkParticles.Length;i++) inkParticles[i].active=false;
            ResetExperience();
        }

        void Box(Rect r,Color c) { GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=Color.white; }
        void Text(Rect r,string s,GUIStyle style,Color c) { var old=GUI.color; GUI.color=c; GUI.Label(r,s,style); GUI.color=old; }
        void Glyph(int id,Rect r,Color c)
        {
            if(glyphs[id]!=null) { GUI.color=c; GUI.DrawTexture(r,glyphs[id],ScaleMode.ScaleToFit); GUI.color=Color.white; return; }
            // Deliberately mechanical, unmistakable placeholders, NOT invented Dongba.
            float w=Mathf.Max(2,r.width*.035f);
            Box(new Rect(r.x,r.y,w,r.height),c); Box(new Rect(r.xMax-w,r.y,w,r.height),c);
            Box(new Rect(r.x,r.y,r.width,w),c); Box(new Rect(r.x,r.yMax-w,r.width,w),c);
            for(int i=0;i<=id;i++) Box(new Rect(r.x+r.width*.18f+i*r.width*.13f,r.y+r.height*.25f,r.width*.055f,r.height*.3f),c);
            int previousSize=small.fontSize;
            small.fontSize=r.width<80?7:11;
            small.wordWrap=false;
            Text(new Rect(r.x,r.y+r.height*.60f,r.width,Mathf.Max(16,r.height*.3f)),"PH / "+Names[id],small,c);
            small.fontSize=previousSize;
        }

        void OnGUI()
        {
            if(label==null) { label=new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleLeft,fontStyle=FontStyle.Bold}; small=new GUIStyle(label){fontSize=13,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Normal}; title=new GUIStyle(label){fontSize=28}; }
            float s=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*s)/2,(Screen.height-720*s)/2,0),Quaternion.identity,Vector3.one*s);
            if(symmetryPreview) {
                DrawSymmetryBackground();
                DrawForest();
                DrawSymmetryMarkers();
                GUI.matrix=Matrix4x4.identity;
                return;
            }
            GUI.DrawTexture(new Rect(0,0,1280,720),environment!=null?environment:paper,ScaleMode.StretchToFill);
            DrawAmbientMotion();
            DrawExperienceEnvironmentWash();
            DrawExperienceGrowth();
            DrawForest();
            DrawExperienceWorld();
            DrawInkParticles();
            DrawBattle();
            DrawExperienceHud();
            if(ShowDiagnostics||motionDebug) {
                Box(new Rect(1010,676,242,28),PaperPanel);
                Text(new Rect(1018,677,226,25),$"FPS {displayedFps:0.0}   ENV {displayedAnimationRate:0.0}/s",small,Ink);
            }
            if(motionDebug) {
                Box(new Rect(404,24,472,34),new Color(.94f,.88f,.75f,.9f));
                Text(new Rect(414,27,452,28),$"TREE TOP PX   {debugTreeDisplacement[0],6:0.0}   {debugTreeDisplacement[1],6:0.0}   {debugTreeDisplacement[2],6:0.0}",small,Red);
            }
            GUI.matrix=Matrix4x4.identity;
        }

        void DrawAmbientMotion()
        {
            if(environment==null) return;
            float masterPhase=ambientClock*Mathf.PI*2/MasterPeriod;
            float gustAmount=Mathf.Max(Mathf.Abs(SignedGust(ambientClock)),Mathf.Abs(impactWind));
            for(int i=0;i<ambientArt.Length;i++) {
                Texture2D texture=ambientArt[i];
                if(texture==null) continue;
                if(!ExperienceAmbientVisible(i)) continue;
                bool tree=i<3;
                Rect r=ExperienceAmbientRect(i,ambientRects[i]);
                float delay=tree?.12f+i*.09f:(i-3)*.055f;
                float delayedMasterPhase=masterPhase-delay*Mathf.PI*2/MasterPeriod;
                float primary=Mathf.Sin(delayedMasterPhase);
                float breeze=tree?primary:
                    primary*.7f+Mathf.Sin(delayedMasterPhase*2+ambientPhase[i])*.3f;
                float gustScale=tree
                    ? 1+gustAmount*(TreeGustPixels/Mathf.Max(1,TreeBreezePixels))
                    : 1+gustAmount*(GrassGustPixels/Mathf.Max(1,GrassBreezePixels));
                float displacement=(tree?TreeBreezePixels:GrassBreezePixels)*breeze*gustScale*WindStrength*ambientAmplitude[i]*ExperienceMotionScale;
                if(tree) displacement+=ExperienceTreeImpact(i);
                DrawBent(texture,r,displacement,tree?20:12,ExperienceAmbientTint(i));
                if(tree) {
                    debugTreeDisplacement[i]=displacement;
                    if(motionDebug) Box(new Rect(r.x+TreeTipLocalX[i]+displacement-3,r.y+TreeTipLocalY[i]-3,6,6),new Color(.9f,.08f,.05f,1));
                }
            }

            // The cloud stays near the approved composition and changes direction
            // on a much slower rhythm than the trees. It never wraps across an edge.
            if(cloudArt!=null) {
                Rect cloud=new Rect(378,154,238,79);
                float slowPhase=ambientClock*Mathf.PI*2/CloudDriftPeriod;
                float secondaryPhase=ambientClock*Mathf.PI*2/(CloudDriftPeriod*1.63f);
                float cloudOffset=CloudDriftRangePixels*(Mathf.Sin(slowPhase)*.76f+Mathf.Sin(secondaryPhase+.7f)*.24f-Mathf.Sin(.7f)*.24f);
                cloud.x+=cloudOffset;
                DrawPivoted(cloudArt,cloud,0,Color.white);
            }

            // Every visible water glyph stays near its approved position. Ordered
            // phase delays carry a restrained wave from left to right across each
            // row; no glyph travels through another or wraps around the channel.
            GUI.BeginGroup(WaterChannel);
            for(int i=0;i<WaterRects.Length;i++) {
                Texture2D waterTexture=waterArt[i];
                if(waterTexture==null) continue;
                Rect water=WaterRects[i];
                int row=WaterRows[i];
                float waterOffset=Mathf.Sin(masterPhase-WaterColumns[i]*.35f+row*.18f);
                water.x+=waterOffset*WaterAmplitudes[i]*(1+gustAmount*.65f)*ExperienceMotionScale;
                DrawPivoted(waterTexture,water,0,Color.white);
            }
            GUI.EndGroup();
        }

        void DrawSymmetryBackground()
        {
            const int cell=32;
            for(int y=0;y<720;y+=cell) for(int x=0;x<1280;x+=cell)
                Box(new Rect(x,y,cell,cell),((x/cell+y/cell)&1)==0?new Color(.92f,.92f,.92f):new Color(.76f,.76f,.76f));
            Box(new Rect(0,535,1280,2),new Color(.22f,.22f,.22f));
        }

        void DrawSymmetryMarkers()
        {
            float originalX=50+X*59;
            float lunge=Busy?attackFacing*AttackLungePixels:0;
            float footX=originalX+lunge;
            Box(new Rect(originalX-1,520,2,33),new Color(.2f,.2f,.2f,.8f));
            Box(new Rect(footX-4,532,8,8),attackFacing>0?new Color(.05f,.35f,.85f):new Color(.85f,.12f,.18f));
        }

        float SignedGust(float time)
        {
            float t=Mathf.Repeat(time,60f);
            for(int i=0;i<GustStarts.Length;i++) {
                float local=t-GustStarts[i];
                if(local<0||local>1.55f) continue;
                float envelope;
                if(local<.62f) envelope=Mathf.SmoothStep(0,1,local/.62f);
                else if(local<.82f) envelope=1;
                else envelope=Mathf.SmoothStep(1,0,(local-.82f)/.73f);
                return (i%2==0?1:-1)*envelope;
            }
            return 0;
        }

        void DrawBent(Texture2D texture,Rect rect,float topDisplacement,int slices,Color tint)
        {
            Color previousColor=GUI.color;
            GUI.color=tint;
            float sliceHeight=rect.height/slices;
            for(int i=0;i<slices;i++) {
                float bottom=i/(float)slices;
                float top=(i+1)/(float)slices;
                float middle=(bottom+top)*.5f;
                float offset=i==0?0:topDisplacement*Mathf.Pow(middle,2.15f);
                Rect destination=new Rect(rect.x+offset,rect.yMax-(i+1)*sliceHeight,rect.width,sliceHeight+.65f);
                Rect uv=new Rect(0,bottom,1,1f/slices+.002f);
                GUI.DrawTextureWithTexCoords(destination,texture,uv,true);
            }
            GUI.color=previousColor;
        }

        void DrawPivoted(Texture2D texture,Rect rect,float degrees,Color tint)
        {
            Matrix4x4 previousMatrix=GUI.matrix;
            Color previousColor=GUI.color;
            if(Mathf.Abs(degrees)>.0001f) GUI.matrix=previousMatrix*LocalRotationAround(new Vector2(rect.center.x,rect.yMax),degrees);
            GUI.color=tint;
            GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,true);
            GUI.color=previousColor;
            GUI.matrix=previousMatrix;
        }

        void DrawForest()
        {
            if(environment==null) {
                for(int i=0;i<8;i++) { float x=80+i*153; Glyph(2,new Rect(x,230+(i%3)*25,74,160-(i%3)*20),new Color(.34f,.41f,.32f,.4f)); }
                for(int i=0;i<20;i++) if(i<8||i>12) Glyph(1,new Rect(50+i*59,535,55,48),Ink);
                for(int i=0;i<5;i++) Glyph(3,new Rect(50+(8+i)*59,547+Mathf.Sin(clock*2+i)*4,55,56),new Color(.23f,.45f,.5f));
            }
            Vector2 shake=ImpactShake();
            float px=50+X*59;
            float py=536-Y*59;
            float speed01=Mathf.Clamp01(Mathf.Abs(moveVelocity)/MoveSpeed);
            float scaleX=1,scaleY=1,footLift=0,lean=0;
            bool playerIdle=speed01<.03f&&grounded&&!Busy&&landingTimer<=0;
            if(playerIdle) {
                float breath=Mathf.Sin(clock*Mathf.PI*2/IdlePeriod);
                scaleY*=1+breath*IdleVerticalScale;
                scaleX*=1-breath*IdleHorizontalComp;
            }
            if(grounded&&speed01>.02f) {
                footLift=Mathf.Abs(Mathf.Sin(stepPhase))*StepBobPixels*speed01;
                float compression=Mathf.Sin(stepPhase*2)*StepSquash*speed01;
                scaleX*=1+compression;
                scaleY*=1-compression*.65f;
                lean+=facing*MoveLeanDegrees*speed01;
            }
            if(turnTimer>0) {
                float turnProgress=1-turnTimer/TurnDuration;
                float squeeze=Mathf.Sin(turnProgress*Mathf.PI)*TurnCompression;
                scaleX*=1-squeeze;
                scaleY*=1+squeeze*.45f;
            }
            if(!grounded) {
                if(jumpVisualAge<JumpSquashDuration) {
                    float squash=Mathf.Sin(jumpVisualAge/JumpSquashDuration*Mathf.PI);
                    scaleY*=1-JumpStretch*squash;
                    scaleX*=1+JumpNarrow*squash;
                } else if(velocity>1.5f) {
                    float rise=Mathf.Clamp01(velocity/JumpSpeed);
                    scaleY*=1+JumpStretch*rise;
                    scaleX*=1-JumpNarrow*rise;
                } else if(velocity<-1.5f) {
                    float fall=Mathf.Clamp01(-velocity/JumpSpeed);
                    scaleY*=1+JumpStretch*.72f*fall;
                    scaleX*=1-JumpNarrow*.65f*fall;
                    lean+=currentMove*2.2f*fall;
                }
            }
            if(landingTimer>0) {
                float p=1-landingTimer/LandingDuration;
                float squash=Mathf.Sin(p*Mathf.PI)*LandingSquash*landingStrength;
                scaleY*=1-squash;
                scaleX*=1+squash*.72f;
            }
            float drawImpact=symmetryPreview&&Busy?1:impact;
            float lunge=Busy?drawImpact*attackFacing*AttackLungePixels:0;
            if(Busy) lean+=attackFacing*AttackLeanDegrees*drawImpact;
            if(hitStopRemaining>0&&currentAttackHit) { scaleX*=1.025f; scaleY*=.975f; }
            float playerWidth=80*PlayerTextureWidthRatio*scaleX;
            float playerHeight=96*PlayerTextureHeightRatio*scaleY;
            Vector2 playerFoot=new Vector2(px+lunge+shake.x,py-footLift+shake.y);
            Rect playerRect=RectFromFoot(playerFoot,playerWidth,playerHeight,PlayerFootPivotY);
            float playerBlink=playerInvulnerable>0?.72f+.28f*Mathf.Abs(Mathf.Sin(clock*35f)):1f;
            Color playerTint=Color.Lerp(Color.white,new Color(1f,.56f,.46f),playerHitFlash/.3f);
            playerTint.a=playerBlink;
            DrawArtPivoted(playerArt,0,playerRect,playerFoot,playerTint,visualFacing<0,lean);
            if(Busy) DrawAttackAxe(playerFoot,drawImpact,lean);

            if(!symmetryPreview&&(State!=Phase.Victory||defeatTimer>0)) {
                float recoil=flash>0?attackFacing*Mathf.Abs(Mathf.Sin(flash*18))*(4+Hits*3):0;
                float animalBreath=flash<=0?(Mathf.Sin(clock*Mathf.PI*2/2.15f+1.7f)*.5f+.5f)*IdleBreath*.8f:0;
                float attention=flash<=0?SoftPulse(Mathf.Repeat(clock+.35f,3.1f),.15f,.63f):0;
                if(animalAttackWindup>0) attention=Mathf.Max(attention,Mathf.Sin((1-animalAttackWindup/.28f)*Mathf.PI));
                float hitEnvelope=animalHitTimer>0?Mathf.Sin(Mathf.Clamp01(animalHitTimer/.2f)*Mathf.PI):0;
                float animalScaleY=1+animalBreath+attention*.008f+hitEnvelope*(.018f+Hits*.012f);
                float animalScaleX=1-hitEnvelope*(.01f+Hits*.006f);
                float animalHeight=91*ChickenTextureHeightRatio*animalScaleY;
                float animalWidth=102*ChickenTextureWidthRatio*animalScaleX;
                float animalLift=flash<=0?Mathf.Sin(clock*Mathf.PI*2/2.15f+1.2f)*.7f+attention*2.2f:0;
                float peckLunge=animalAttackWindup>0?animalAttackDirection*Mathf.Sin((1-animalAttackWindup/.28f)*Mathf.PI)*13f:0;
                Vector2 animalFoot=new Vector2(50+animalWorldX*59+recoil+attention*1.2f+peckLunge+animalKnockback+shake.x,536+animalLift+shake.y);
                Rect animalRect=RectFromFoot(animalFoot,animalWidth,animalHeight,ChickenFootPivotY);
                Color animalTint=Color.Lerp(Color.white,new Color(1f,.75f,.55f),flash);
                if(defeatTimer>0) DrawScattered(animalArt,animalRect,1-defeatTimer/.46f,animalTint);
                else DrawArtPivoted(animalArt,4,animalRect,animalFoot,animalTint,animalFacing>0,-attackFacing*hitEnvelope*(2+Hits));
            }
        }

        Vector2 ImpactShake()
        {
            if(cameraShakeTimer<=0||cameraShakeDuration<=0) return Vector2.zero;
            float fade=cameraShakeTimer/cameraShakeDuration;
            return new Vector2(Mathf.Sin(clock*107f),Mathf.Sin(clock*83f+1.4f))*cameraShakeAmplitude*fade;
        }

        Rect RectFromFoot(Vector2 foot,float width,float height,float footPivotY)
        {
            return new Rect(foot.x-width*.5f,foot.y-height*footPivotY,width,height);
        }

        void DrawAttackAxe(Vector2 playerFoot,float drawImpact,float playerLean)
        {
            if(axeArt==null) return;
            GetAttackAxePose(playerFoot,drawImpact,playerLean,out Vector2 hand,out float size,out float degrees);
            bool flip=attackFacing<0;
            // The visible end of the handle is at roughly (10.5%, 76%) in the
            // transparent source. Mirroring uses its exact reflected coordinate.
            float handlePivotX=flip?.895f:.105f;
            Rect axeRect=new Rect(hand.x-size*handlePivotX,hand.y-size*.76f,size,size);
            DrawArtPivoted(axeArt,0,axeRect,hand,Color.white,flip,degrees);
        }

        void GetAttackAxePose(Vector2 playerFoot,float drawImpact,float playerLean,out Vector2 hand,out float size,out float degrees)
        {
            size=86f+activeAttackPower*2f;
            Vector2 localHand=new Vector2(attackFacing*31f,-51f);
            Vector2 leanedHand=RotateCanvasVector(localHand,playerLean);
            hand=playerFoot+leanedHand;
            // impact moves 0 -> wind-up (-.16) -> strike (1) -> 0. This maps to a
            // readable backward lift and a smooth forward chop.
            float swing=Mathf.InverseLerp(-.16f,1f,drawImpact);
            float swingDegrees=Mathf.Lerp(-48f,38f,Mathf.SmoothStep(0,1,swing));
            degrees=playerLean+attackFacing*swingDegrees;
        }

        Vector2 RotateCanvasVector(Vector2 value,float degrees)
        {
            float radians=degrees*Mathf.Deg2Rad;
            float cosine=Mathf.Cos(radians),sine=Mathf.Sin(radians);
            return new Vector2(value.x*cosine-value.y*sine,value.x*sine+value.y*cosine);
        }

        bool AxeBladeOverlapsAnimal()
        {
            float attackLean=attackFacing*AttackLeanDegrees*impact;
            Vector2 playerFoot=new Vector2(
                50+X*59+impact*attackFacing*AttackLungePixels,
                536-Y*59);
            GetAttackAxePose(playerFoot,impact,attackLean,out Vector2 hand,out float size,out float degrees);

            // Outer contour of the cutting head in axe_glyph.png. Coordinates are
            // normalized against the transparent 256px source and transformed by
            // the same grip pivot, mirror and rotation used for drawing.
            SetBladePoint(0,.50f,.25f,hand,size,degrees);
            SetBladePoint(1,.91f,.31f,hand,size,degrees);
            SetBladePoint(2,.72f,.56f,hand,size,degrees);
            SetBladePoint(3,.50f,.64f,hand,size,degrees);

            float animalFootX=50+animalWorldX*59+animalKnockback;
            Rect animalCollider=new Rect(animalFootX-51f,444f,102f,94f);
            return ConvexPolygonOverlapsRect(axeBladeCollider,animalCollider);
        }

        void SetBladePoint(int index,float sourceX,float sourceY,Vector2 hand,float size,float degrees)
        {
            Vector2 fromGrip=new Vector2((sourceX-.105f)*size*attackFacing,(sourceY-.76f)*size);
            axeBladeCollider[index]=hand+RotateCanvasVector(fromGrip,degrees);
        }

        bool ConvexPolygonOverlapsRect(Vector2[] polygon,Rect rect)
        {
            if(!OverlapOnAxis(polygon,rect,Vector2.right)||!OverlapOnAxis(polygon,rect,Vector2.up)) return false;
            for(int i=0;i<polygon.Length;i++) {
                Vector2 edge=polygon[(i+1)%polygon.Length]-polygon[i];
                Vector2 axis=new Vector2(-edge.y,edge.x);
                if(!OverlapOnAxis(polygon,rect,axis)) return false;
            }
            return true;
        }

        bool OverlapOnAxis(Vector2[] polygon,Rect rect,Vector2 axis)
        {
            float polygonMin=Vector2.Dot(polygon[0],axis),polygonMax=polygonMin;
            for(int i=1;i<polygon.Length;i++) {
                float projection=Vector2.Dot(polygon[i],axis);
                polygonMin=Mathf.Min(polygonMin,projection); polygonMax=Mathf.Max(polygonMax,projection);
            }
            float a=Vector2.Dot(new Vector2(rect.xMin,rect.yMin),axis);
            float b=Vector2.Dot(new Vector2(rect.xMax,rect.yMin),axis);
            float c=Vector2.Dot(new Vector2(rect.xMax,rect.yMax),axis);
            float d=Vector2.Dot(new Vector2(rect.xMin,rect.yMax),axis);
            float rectMin=Mathf.Min(Mathf.Min(a,b),Mathf.Min(c,d));
            float rectMax=Mathf.Max(Mathf.Max(a,b),Mathf.Max(c,d));
            return polygonMax>=rectMin&&rectMax>=polygonMin;
        }

        void DrawArtPivoted(Texture2D texture,int fallback,Rect rect,Vector2 footPivot,Color tint,bool flip,float degrees)
        {
            Matrix4x4 previous=GUI.matrix;
            if(Mathf.Abs(degrees)>.001f) GUI.matrix=previous*LocalRotationAround(footPivot,degrees);
            DrawArt(texture,fallback,rect,tint,flip);
            GUI.matrix=previous;
        }

        Matrix4x4 LocalRotationAround(Vector2 pivot,float degrees)
        {
            return Matrix4x4.TRS(new Vector3(pivot.x,pivot.y,0),Quaternion.Euler(0,0,degrees),Vector3.one)
                *Matrix4x4.TRS(new Vector3(-pivot.x,-pivot.y,0),Quaternion.identity,Vector3.one);
        }

        void DrawScattered(Texture2D texture,Rect rect,float progress,Color tint)
        {
            if(texture==null) { Glyph(4,rect,tint); return; }
            Color old=GUI.color;
            GUI.color=new Color(tint.r,tint.g,tint.b,1-progress);
            const int slices=7;
            float height=rect.height/slices;
            for(int i=0;i<slices;i++) {
                float offset=(i-3)*progress*2.2f+attackFacing*progress*10f;
                Rect part=new Rect(rect.x+offset,rect.y+i*height-progress*Mathf.Abs(i-3)*1.2f,rect.width,height+.5f);
                Rect uv=new Rect(0,1-(i+1)/(float)slices,1,1f/slices+.002f);
                GUI.DrawTextureWithTexCoords(part,texture,uv,true);
            }
            GUI.color=old;
        }

        void DrawArt(Texture2D texture,int fallback,Rect rect,Color tint,bool flip)
        {
            if(texture==null) { Glyph(fallback,rect,tint); return; }
            var old=GUI.color; GUI.color=tint;
            GUI.DrawTextureWithTexCoords(rect,texture,flip?new Rect(1,0,-1,1):new Rect(0,0,1,1),true);
            GUI.color=old;
        }

        float SoftPulse(float time,float start,float end)
        {
            if(time<=start||time>=end) return 0;
            float x=(time-start)/(end-start);
            float s=Mathf.Sin(x*Mathf.PI);
            return s*s;
        }

        void DrawBattle()
        {
            // Health appears beside the actors only when combat makes it useful.
            // The exploration composition stays free of a permanent HUD.
            if(State==Phase.Battle||PlayerHealth<3||playerInvulnerable>0) {
                float playerCanvasX=50+X*59;
                float playerCanvasY=401-Y*59;
                DrawInkHealth(new Vector2(playerCanvasX-28,playerCanvasY),PlayerHealth,.76f);
            }
            if(State==Phase.Battle&&Hits<3) {
                float animalCanvasX=50+animalWorldX*59+animalKnockback;
                DrawInkHealth(new Vector2(animalCanvasX-30,422),3-Hits,.82f);
            }
        }

        void DrawInkHealth(Vector2 origin,int amount,float scale)
        {
            for(int i=0;i<3;i++) {
                bool active=i<amount;
                float y=origin.y+(i==1?-1.2f:(i==2?.7f:0));
                float width=(i==0?19f:(i==1?22f:18f))*scale;
                float height=(i==1?5.5f:4.5f)*scale;
                Color color=active?new Color(.61f,.20f,.14f,.92f):new Color(.38f,.29f,.22f,.16f);
                float x=origin.x+i*25f*scale;
                Box(new Rect(x,y,width,height),color);
                Box(new Rect(x+width-2f*scale,y-1f*scale,3f*scale,height+1f*scale),new Color(color.r,color.g,color.b,color.a*.58f));
                if(active) Box(new Rect(x+3f*scale,y+height,Mathf.Max(2f,4f-i)*scale,1.2f*scale),new Color(color.r,color.g,color.b,color.a*.48f));
            }
        }

        IEnumerator CaptureWindPreview()
        {
            string directory=Path.Combine(Application.dataPath,"..","wind-preview-frames");
            Directory.CreateDirectory(directory);
            foreach(string oldFrame in Directory.GetFiles(directory,"wind-*.jpg")) File.Delete(oldFrame);

            // Offline verification recorder only. Time.captureFramerate advances the
            // simulation by exactly 1/60 second for every rendered frame, so encoder
            // speed cannot turn the preview into a stepped 10 FPS animation. Normal
            // play never enters this path and performs no screenshot allocation.
            Time.captureFramerate=60;
            clock=0;
            ambientClock=0;
            displayedFps=59.71f;
            displayedAnimationRate=59.71f;
            yield return null;
            for(int i=0;i<600;i++) {
                yield return new WaitForEndOfFrame();
                Texture2D frame=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(directory,$"wind-{i:0000}.jpg"),frame.EncodeToJPG(82));
                Destroy(frame);
            }
            Time.captureFramerate=0;
            File.WriteAllText(Path.Combine(Application.dataPath,"..","wind-preview.done"),"600 unique frames / 10 seconds / 60 FPS / Unity "+Application.unityVersion);
            Application.Quit(0);
        }

        IEnumerator CaptureActionPreview()
        {
            string directory=Path.Combine(Application.dataPath,"..","action-preview-frames");
            Directory.CreateDirectory(directory);
            foreach(string oldFrame in Directory.GetFiles(directory,"action-*.jpg")) File.Delete(oldFrame);
            Time.captureFramerate=60;
            ResetGame(false);
            X=5.2f;
            const int frameCount=960;
            for(int frame=0;frame<frameCount;frame++) {
                bool jump=frame==137;
                float move=0;
                if(frame>=120&&frame<216) move=1;
                else if(frame>=216&&frame<229) move=-1;
                else if(frame>=229&&frame<247) move=1;
                else if(frame>=300&&frame<307) move=-1;
                else if(frame==372) move=1;
                Tick(move,jump,1f/60f);
                if(frame==315||frame==420||frame==558||frame==696) Attack();
                yield return new WaitForEndOfFrame();
                Texture2D frameTexture=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(directory,$"action-{frame:0000}.jpg"),frameTexture.EncodeToJPG(84));
                Destroy(frameTexture);
            }
            Time.captureFramerate=0;
            File.WriteAllText(Path.Combine(Application.dataPath,"..","action-preview.done"),"960 unique frames / 16 seconds / 60 FPS / Unity "+Application.unityVersion);
            Application.Quit(0);
        }

        IEnumerator CaptureAttackSymmetry()
        {
            string directory=Path.Combine(Application.dataPath,"..","symmetry-validation",symmetryValidationId);
            Directory.CreateDirectory(directory);
            Time.captureFramerate=60;
            X=(640f-50f)/59f; Y=0; velocity=moveVelocity=0; grounded=true; Hits=0;
            // Let Windows finish applying the requested framebuffer size before
            // capturing the first direction; otherwise the first frame can retain
            // the launcher's previous window scale on high-DPI displays.
            for(int warmup=0;warmup<12;warmup++) yield return new WaitForEndOfFrame();
            int[] directions={-1,1};
            string[] names={"left","right"};
            int capturedWidth=0,capturedHeight=0;
            for(int sample=0;sample<directions.Length;sample++) {
                int direction=directions[sample];
                X=(640f-50f)/59f; Y=0; velocity=currentMove=moveVelocity=0; grounded=true;
                Busy=false; impact=flash=0; hitStopRemaining=0; currentAttackHit=false;
                visualFacing=-direction; facing=direction; turnTimer=TurnDuration;
                Attack();
                yield return new WaitForEndOfFrame();
                Texture2D frame=ScreenCapture.CaptureScreenshotAsTexture(symmetrySuperSize);
                capturedWidth=frame.width; capturedHeight=frame.height;
                File.WriteAllBytes(Path.Combine(directory,names[sample]+".png"),frame.EncodeToPNG());
                Destroy(frame);
                while(Busy) yield return null;
            }
            Time.captureFramerate=0;
            File.WriteAllText(Path.Combine(directory,"result.txt"),$"PASS\nFramebuffer: {Screen.width}x{Screen.height}\nCapture: {capturedWidth}x{capturedHeight}\nSuper size: {symmetrySuperSize}\nFoot anchor canvas: 640,536\nLunge canvas pixels: {AttackLungePixels:0.###}\nImmediate reverse attack: tested both directions\nUnity: {Application.unityVersion}");
            Application.Quit(0);
        }

        IEnumerator MeasureFps()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            fpsTestFrames=0;
            float started=Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(8f);
            float elapsed=Time.realtimeSinceStartup-started;
            float measured=fpsTestFrames/elapsed;
            string report=$"FPS average: {measured:0.00}\nEnvironment updates/s: {measured:0.00}\nTarget FPS: {Application.targetFrameRate}\nVSync count: {QualitySettings.vSyncCount}\nUnity: {Application.unityVersion}";
            File.WriteAllText(Path.Combine(Application.dataPath,"..","fps-report.txt"),report);
            Application.Quit(0);
        }

        IEnumerator SmokeTest()
        {
            string path=Path.Combine(Application.dataPath,"..","verification.txt");
            yield return null;
            if(!Check(axeArt!=null,"Axe art missing",path)) yield break;
            if(!Check(backgroundMusic!=null&&music!=null&&music.clip==backgroundMusic&&music.loop&&music.isPlaying,"Background music missing or not playing",path)) yield break;
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"..","ambient-a.png"));
            yield return new WaitForSeconds(.8f);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"..","ambient-b.png"));
            yield return new WaitForSeconds(.2f);
            try {
                for(int i=0;i<155;i++) Tick(1,false,.01f);
                if(Falls==0) throw new Exception("River did not reset player");
                X=6.8f; Y=0; velocity=moveVelocity=0; grounded=true;
                Tick(1,true,.01f);
                for(int i=0;i<115;i++) Tick(1,false,.01f);
                if(X<=RiverRight||Y!=0||Falls!=1) throw new Exception("Jump across river failed");
                for(int i=0;i<55;i++) Tick(1,false,.01f);
                if(X<=AnimalX||moveVelocity<MoveSpeed*.95f) throw new Exception("Contact locked movement");

                X=7.15f; Y=0; velocity=0; moveVelocity=2; grounded=true; coyoteRemaining=0;
                Tick(1,false,.04f);
                if(grounded) throw new Exception("Coyote setup did not leave bank");
                Tick(1,true,.05f);
                if(velocity<=0) throw new Exception("Coyote jump failed");

                X=13; Y=.18f; velocity=-3; moveVelocity=0; grounded=false; coyoteRemaining=jumpBufferRemaining=0;
                Tick(0,true,.01f);
                for(int i=0;i<9&&velocity<=0;i++) Tick(0,false,.01f);
                if(velocity<=0) throw new Exception("Buffered landing jump failed");
            } catch(Exception e) { File.WriteAllText(path,"FAIL: "+e); Application.Quit(1); yield break; }

            // Air and ground attacks must miss outside range; turning away must also miss.
            X=12; Y=0; moveVelocity=0; facing=1; Attack();
            while(Busy) yield return null;
            if(!Check(Hits==0,"Distant swing caused damage",path)) yield break;
            X=15; moveVelocity=0; facing=visualFacing=1;
            Tick(-1,false,.01f);
            Attack();
            if(!Check(attackFacing==-1&&visualFacing==-1&&turnTimer==0,"Immediate reverse attack direction mismatch",path)) yield break;
            Tick(1,false,.01f);
            if(!Check(attackFacing==-1&&visualFacing==-1&&facing==1,"Attack direction changed during recovery",path)) yield break;
            while(Busy) yield return null;
            if(!Check(Hits==0,"Back-facing swing caused damage",path)) yield break;
            facing=1; Y=2; Attack();
            while(Busy) yield return null;
            if(!Check(Hits==0,"High swing caused damage",path)) yield break;

            // Retreat during a swing: locomotion must run even while Busy is true.
            X=13f; Y=0; velocity=moveVelocity=0; grounded=true; Attack();
            Tick(-1,true,.05f);
            if(!Check(Busy&&X<13f&&Y>0,"Attack blocked movement or jump",path)) yield break;
            while(Busy) yield return null;
            if(!Check(Hits==0,"Out-of-range moving swing caused damage",path)) yield break;

            for(int i=0;i<3;i++) {
                X=14.8f; Y=0; velocity=moveVelocity=0; grounded=true; facing=visualFacing=1;
                Attack(); Attack();
                float startX=X; bool captured=false;
                while(Busy) {
                    // All three damaging swings run through the blade collider.
                    // Jumping remains allowed, but an airborne blade only hits if
                    // its transformed polygon actually reaches the animal.
                    Tick(1,false,Mathf.Min(Time.deltaTime,.02f));
                    if(i==1&&!captured&&Hits==2) {
                        captured=true;
                        ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"..","battle.png"));
                    }
                    yield return null;
                }
                if(!Check(X>startX,"Movement locked during hit",path)) yield break;
                if(Hits!=i+1) { File.WriteAllText(path,"FAIL: Attack debounce/count"); Application.Quit(1); yield break; }
                if(i<2&&State!=Phase.Battle) { File.WriteAllText(path,"FAIL: Early victory"); Application.Quit(1); yield break; }
            }
            if(State!=Phase.Victory) { File.WriteAllText(path,"FAIL: Missing victory"); Application.Quit(1); yield break; }
            float defeatedX=X;
            for(int i=0;i<30;i++) Tick(-1,i==0,.01f);
            if(!Check(X<defeatedX&&Y>0,"Victory locked movement",path)) yield break;
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"..","victory.png"));
            yield return new WaitForSeconds(.4f);
            ResetGame(false);
            if(Hits!=0||State!=Phase.Explore||Busy) { File.WriteAllText(path,"FAIL: Restart"); Application.Quit(1); yield break; }
            float patrolStart=animalWorldX;
            for(int i=0;i<120;i++) { UpdateAnimalAI(.01f); UpdateFeedback(.01f); }
            if(!Check(Mathf.Abs(animalWorldX-patrolStart)>.08f,"Animal patrol did not move",path)) yield break;
            X=15f; Y=0; animalWorldX=16f; PlayerHealth=3;
            for(int i=0;i<180&&PlayerHealth==3;i++) { UpdateAnimalAI(.01f); UpdateFeedback(.01f); }
            if(!Check(PlayerHealth==3&&!animalProvoked,"Unprovoked animal attacked player",path)) yield break;
            animalProvoked=true;
            for(int i=0;i<180&&PlayerHealth==3;i++) { UpdateAnimalAI(.01f); UpdateFeedback(.01f); }
            if(!Check(PlayerHealth==2&&playerInvulnerable>0,"Provoked animal did not retaliate",path)) yield break;
            int healthAfterPeck=PlayerHealth;
            for(int i=0;i<20;i++) { UpdateAnimalAI(.01f); UpdateFeedback(.01f); }
            if(!Check(PlayerHealth==healthAfterPeck,"Player invulnerability did not prevent repeated damage",path)) yield break;
            ResetGame(false);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"..","forest.png"));
            yield return new WaitForSeconds(.4f);
            string success="PASS: ambient motion; movement and braking; river reset; jump across; coyote time; jump buffer; contact does not lock movement; blade-collider attacks; three-hit victory; peaceful animal patrol, retaliation after provocation, player damage and invulnerability; movement after victory; restart.\nUnity "+Application.unityVersion;
            Debug.Log("PROTOTYPE TEST "+success);
            try { File.WriteAllText(path,success); } catch(Exception e) { Debug.LogWarning("Could not write test report: "+e.Message); }
            Application.Quit(0);
        }
        bool Check(bool condition,string message,string path) {
            if(!condition) {
                Debug.LogError("PROTOTYPE TEST FAIL: "+message);
                try { File.WriteAllText(path,"FAIL: "+message); } catch(Exception e) { Debug.LogError("Could not write test report: "+e.Message); }
                Application.Quit(1);
            }
            return condition;
        }
        void OnDestroy() { if(paper) Destroy(paper); if(strikes!=null) foreach(var clip in strikes) if(clip) Destroy(clip); }
    }
}
