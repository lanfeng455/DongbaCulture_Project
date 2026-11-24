using UnityEngine;
namespace Ivan.Diorama {public partial class DioramaPlayerController {
    readonly DongbaPrototype.PaperActorMotion m_paperMotion=new DongbaPrototype.PaperActorMotion();
    readonly DongbaPrototype.GlyphTurnMotion m_paperTurn=new DongbaPrototype.GlyphTurnMotion();
    Vector3 m_paperBaseScale;Quaternion m_paperBaseRotation;int m_attackDirection=1;
    float m_pushUntil,m_pushWeight;
    void OnCollisionStay(Collision contact){
        var other=contact.rigidbody;
        if(other==null||other.isKinematic||m_moveInput.sqrMagnitude<.1f)return;
        Vector3 to=(contact.contactCount>0?contact.GetContact(0).point:other.position)-transform.position;to.y=0;
        if(Vector3.Dot(m_moveInput,to.normalized)>.25f)m_pushUntil=Time.time+.10f;
    }
    void AddPushPose(ref DongbaPrototype.PaperActorMotion.Pose pose,float dt){
        m_pushWeight=Mathf.Lerp(m_pushWeight,Time.time<m_pushUntil&&m_attackAge<0&&m_jumpAge<0?1:0,1-Mathf.Exp(-dt*14));
        pose.roll-=EffectiveFacing*4*m_pushWeight;pose.scale.y-=.025f*m_pushWeight;
    }
    public void ReactToSpeech(){m_paperMotion.Speak();}
}}
