using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    [Header("Paper character motion")]
    [Range(.12f,.4f)] public float CharacterTurnSeconds=.26f;
    [Range(0,1.5f)] public float CharacterMotionStrength=1;
    readonly GlyphTurnMotion playerTurn=new GlyphTurnMotion(),fatherTurn=new GlyphTurnMotion();
    readonly PaperActorMotion playerMotion=new PaperActorMotion(),fatherMotion=new PaperActorMotion(1.7f);
    Vector3 playerUnturnedScale,fatherUnturnedScale,playerPosePosition,fatherPosePosition;
    Quaternion playerPoseRotation,fatherPoseRotation;
    bool playerTurnApplied,fatherTurnApplied;
    // Undo the exact layer before reel/narrative owners run. Never compound offsets.
    void RestoreCharacterTurnScales(){
        if(playerTurnApplied&&body!=null){body.localScale=playerUnturnedScale;body.localPosition=playerPosePosition;body.rotation=playerPoseRotation;}
        if(fatherTurnApplied&&storyFatherArt!=null){storyFatherArt.localScale=fatherUnturnedScale;storyFatherArt.localPosition=fatherPosePosition;storyFatherArt.rotation=fatherPoseRotation;}
        playerTurnApplied=fatherTurnApplied=false;
    }
    void UpdatePlayerTurn(float dt){
        if(attackAge>=0)playerTurn.Snap(sampledAttackAge<0?facing:swingFacing);else playerTurn.Tick(facing,dt,CharacterTurnSeconds);
        playerImage.flipX=playerTurn.Facing<0;
    }
    void CompletePlayerTurnForAttack(){
        RestoreCharacterTurnScales();playerTurn.Snap(facing);playerImage.flipX=facing<0;
        hand.localPosition=PlayerHandLocalPosition();
    }
    void TickPlayerMotion(float dt){
        playerMotion.Tick(dt,Feet,facing,jumpAge,attackAge<0?-1:attackAge+dt,CharacterMotionStrength);
        // Pose before the actual axe sweep, so collision samples the same grip as rendering.
        ApplyPlayerMotion();
    }
    void ApplyPlayerMotion(){
        playerUnturnedScale=body.localScale;playerPosePosition=body.localPosition;playerPoseRotation=body.rotation;
        var pose=playerMotion.Current;
        // The reel already animates the whole cut-out and owns its camera-relative placement.
        if(ReelPresentationVisible){pose.lift=pose.roll=0;pose.scale=Vector3.one;}
        var scale=Vector3.Scale(playerUnturnedScale,pose.scale);scale.x*=playerTurn.Width;
        body.localScale=scale;body.localPosition+=Vector3.up*pose.lift;
        body.rotation=playerPoseRotation*Quaternion.Euler(0,0,pose.roll);playerTurnApplied=true;
    }
    void ApplyCharacterTurnScales(float dt){
        ApplyPlayerMotion();
        if(storyFatherArt==null||!storyFatherRoot.gameObject.activeInHierarchy)return;
        fatherUnturnedScale=storyFatherArt.localScale;fatherPosePosition=storyFatherArt.localPosition;fatherPoseRotation=storyFatherArt.rotation;
        int direction=fatherUnturnedScale.x<0?-1:1;
        if(fatherSwingAge>=0||carryStarted)fatherTurn.Snap(direction);else fatherTurn.Tick(direction,dt,CharacterTurnSeconds*1.1f);
        var pose=fatherMotion.Tick(dt,storyFatherRoot.position,direction,fatherJumpAge,fatherSwingAge,carryStarted?0:CharacterMotionStrength*.85f);
        var scale=Vector3.Scale(fatherUnturnedScale,pose.scale);scale.x=Mathf.Abs(scale.x)*fatherTurn.Width*fatherTurn.Facing;
        storyFatherArt.localScale=scale;storyFatherArt.localPosition+=Vector3.up*pose.lift;
        storyFatherArt.rotation=fatherPoseRotation*Quaternion.Euler(0,0,pose.roll);fatherTurnApplied=true;
    }
    void ReactForestSpeech(string speaker){
        if(speaker=="Father"||speaker=="父亲"||speaker=="俄高勒")fatherMotion.Speak();
        else if(speaker=="Gaolequ"||speaker=="Gao"||speaker=="高勒趣")playerMotion.Speak();
    }
    void ResetCharacterTurns(){RestoreCharacterTurnScales();playerTurn.Snap(1);fatherTurn.Snap(1);playerMotion.Reset();fatherMotion.Reset();}
}}
