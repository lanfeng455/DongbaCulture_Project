using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    Transform xrReadingAnchor;
    bool xrReadingAnchored;
    int xrReadingRevision;
    Vector3 xrFrontOutward;
    float xrFrontBoundary;

    void EnsureXRReadingAnchor(){
        if(xrReadingAnchor!=null)return;
        xrReadingAnchor=new GameObject("PICO forest front edge").transform;
        xrReadingAnchor.localScale=Vector3.one*xrUnits;
    }

    // The forest is the floor of a diorama. The reading surface starts outside
    // its near edge and folds down towards the viewer, never over the trees.
    // Only placement changes this pose; looking or walking cannot drag it around.
    public void AnchorXRReadingFrame(){
        if(!XRTrackingControlsCamera||cam==null||xrUnits<=0)return;
        EnsureXRReadingAnchor();
        xrFrontOutward=Vector3.ProjectOnPlane(cam.transform.position,Vector3.up);
        if(xrFrontOutward.sqrMagnitude<.01f)xrFrontOutward=-Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up);
        if(xrFrontOutward.sqrMagnitude<.01f)xrFrontOutward=Vector3.back;
        xrFrontOutward.Normalize();
        // Support plane of the existing 92 x 42 paper map, including oblique views.
        xrFrontBoundary=46*Mathf.Abs(xrFrontOutward.x)+21*Mathf.Abs(xrFrontOutward.z);
        Vector3 edge=xrFrontOutward*(xrFrontBoundary+.06f*xrUnits)-Vector3.up*(.015f*xrUnits);
        Quaternion rotation=Quaternion.LookRotation((-xrFrontOutward-Vector3.up).normalized,Vector3.up);
        xrReadingAnchor.SetPositionAndRotation(edge,rotation);
        xrReadingAnchor.localScale=Vector3.one*xrUnits;
        xrReadingAnchored=true;xrReadingRevision++;
        Debug.Log("[DongbaMRUI] Diorama front anchored; revision="+xrReadingRevision);
    }

    public string XRReadingDiagnostic=>xrReadingAnchor==null?"not configured":
        "frontFixed="+xrReadingAnchored+" revision="+xrReadingRevision+" headParent="+xrReadingAnchor.IsChildOf(cam.transform)+
        " anchor="+xrReadingAnchor.position.ToString("F2")+" subtitles=head-follow";
}}
