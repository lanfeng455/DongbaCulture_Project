using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    Transform xrReadingAnchor;
    bool xrReadingAnchored;
    int xrReadingRevision;
    Vector3 xrReadingEye, xrReadingForward;

    void EnsureXRReadingAnchor(){
        if(xrReadingAnchor!=null)return;
        xrReadingAnchor=new GameObject("PICO upper reading area").transform;
        xrReadingAnchor.localScale=Vector3.one*xrUnits;
    }

    // Capture yaw and eye height once, not the downward placement gaze. The
    // reading centre is 19 degrees above the horizon, 1.45 real metres ahead.
    // Looking down plays the forest; looking up reads. Never chase the headset.
    public void AnchorXRReadingFrame(){
        if(!XRTrackingControlsCamera||cam==null||xrUnits<=0)return;
        EnsureXRReadingAnchor();
        xrReadingEye=cam.transform.position;
        xrReadingForward=Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up);
        if(xrReadingForward.sqrMagnitude<.01f)xrReadingForward=Vector3.ProjectOnPlane(-xrReadingEye,Vector3.up);
        if(xrReadingForward.sqrMagnitude<.01f)xrReadingForward=Vector3.forward;
        xrReadingForward.Normalize();
        Vector3 top=xrReadingEye+(xrReadingForward*1.45f+Vector3.up*.87f)*xrUnits;
        xrReadingAnchor.SetPositionAndRotation(top,Quaternion.LookRotation(xrReadingForward,Vector3.up));
        xrReadingAnchor.localScale=Vector3.one*xrUnits;
        xrReadingAnchored=true;xrReadingRevision++;
        Debug.Log("[DongbaMRUI] Upper reading area anchored; revision="+xrReadingRevision);
    }

    public string XRReadingDiagnostic=>xrReadingAnchor==null?"not configured":
        "upperFixed="+xrReadingAnchored+" revision="+xrReadingRevision+" headParent="+xrReadingAnchor.IsChildOf(cam.transform)+
        " anchor="+xrReadingAnchor.position.ToString("F2")+" subtitles=head-follow";
}}
