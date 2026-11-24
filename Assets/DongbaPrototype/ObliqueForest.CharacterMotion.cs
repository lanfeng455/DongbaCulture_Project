using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    [Header("Paper character motion")]
    [Range(.12f,.4f)] public float CharacterTurnSeconds=.26f;
    readonly GlyphTurnMotion playerTurn=new GlyphTurnMotion(),fatherTurn=new GlyphTurnMotion();
    Vector3 playerUnturnedScale,fatherUnturnedScale;
    bool playerTurnApplied,fatherTurnApplied;
    // Remove only our previous-frame multiplier before other animation owners run.
    // This keeps the reel's camera/actor scale and the father's narrative pose intact.
    void RestoreCharacterTurnScales(){
        if(playerTurnApplied&&body!=null)body.localScale=playerUnturnedScale;
        if(fatherTurnApplied&&storyFatherArt!=null)storyFatherArt.localScale=fatherUnturnedScale;
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
    void ApplyCharacterTurnScales(float dt){
        playerUnturnedScale=body.localScale;var scale=playerUnturnedScale;
        scale.x*=playerTurn.Width;body.localScale=scale;playerTurnApplied=true;
        if(storyFatherArt==null||!storyFatherRoot.gameObject.activeInHierarchy)return;
        fatherUnturnedScale=storyFatherArt.localScale;
        int direction=fatherUnturnedScale.x<0?-1:1;
        // An active knife or capture uses the exact story direction, never a half-turn.
        if(fatherSwingAge>=0||carryStarted)fatherTurn.Snap(direction);
        else fatherTurn.Tick(direction,dt,CharacterTurnSeconds*1.1f);
        scale=fatherUnturnedScale;scale.x=Mathf.Abs(scale.x)*fatherTurn.Width*fatherTurn.Facing;
        storyFatherArt.localScale=scale;fatherTurnApplied=true;
    }
    void ResetCharacterTurns(){RestoreCharacterTurnScales();playerTurn.Snap(1);fatherTurn.Snap(1);}
}}
