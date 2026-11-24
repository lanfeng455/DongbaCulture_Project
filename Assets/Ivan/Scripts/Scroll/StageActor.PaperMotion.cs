using UnityEngine;
namespace Ivan.Scroll {public partial class StageActor {
    readonly DongbaPrototype.PaperActorMotion paperMotion=new DongbaPrototype.PaperActorMotion(.9f);
    Ivan.Diorama.DioramaPlayerController paperPlayer;
    Transform paperArt;Vector3 paperBasePosition,paperBaseScale,paperPivot;Quaternion paperBaseRotation;bool paperApplied;
    void InitializePaperMotion(){
        paperPlayer=GetComponentInParent<Ivan.Diorama.DioramaPlayerController>();
        paperMotion.Reset();
        // Multipart beasts and puzzle pieces already have authored actions. Never animate
        // their physics root or independently offset parts (the old detached-snake bug).
        if(paperPlayer!=null||m_renderer==null||m_renderer.sprite==null||m_renderers.Length!=1)return;
        var art=m_renderer.transform;
        if(art.GetComponentsInChildren<Collider>(true).Length>0||art.GetComponent<Rigidbody>()!=null)return;
        paperArt=art;paperPivot=new Vector3(m_renderer.sprite.bounds.center.x,m_renderer.sprite.bounds.min.y,0);
    }
    public void ReactToSpeech(){if(paperPlayer!=null)paperPlayer.ReactToSpeech();else paperMotion.Speak();}
    void RestorePaperPose(){
        if(!paperApplied||paperArt==null)return;
        paperArt.localPosition=paperBasePosition;paperArt.localScale=paperBaseScale;paperArt.localRotation=paperBaseRotation;paperApplied=false;
    }
    void ApplyPaperPose(){
        if(paperArt==null||DongbaPrototype.DongbaInterface.GameplayBlocked)return;
        var foot=paperArt.TransformPoint(paperPivot);
        var pose=paperMotion.Tick(Time.deltaTime,foot,m_isFacingLeft?-1:1,-1,-1,.65f);
        paperBasePosition=paperArt.localPosition;paperBaseScale=paperArt.localScale;paperBaseRotation=paperArt.localRotation;
        paperArt.localScale=Vector3.Scale(paperBaseScale,pose.scale);paperArt.localRotation=paperBaseRotation*Quaternion.Euler(0,0,pose.roll);
        paperArt.position+=foot-paperArt.TransformPoint(paperPivot)+Vector3.up*pose.lift;paperApplied=true;
    }
}}
