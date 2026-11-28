using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    Transform xrReadingAnchor;
    bool xrReadingAnchored;
    int xrReadingRevision;
    const float XRReadingWidthMetres=1.18f;
    const float XRReadingCanvasHeight=700;
    float XRReadingWallDepth=>ForestHalfDepth+.004f*xrUnits;

    void EnsureXRReadingAnchor(){
        if(xrReadingAnchor!=null)return;
        xrReadingAnchor=new GameObject("PICO forest paper side").transform;
        xrReadingAnchor.localScale=Vector3.one*xrUnits;
    }

    // The forest is the horizontal bottom; the reading plane is its far vertical
    // side. Its lower edge meets the ground, independently of eye height or gaze.
    // Placement already aligns the forest front to the viewer; do not rotate UI
    // separately or lift it relative to the headset again.
    public void AnchorXRReadingFrame(){
        if(!XRTrackingControlsCamera||cam==null||xrUnits<=0)return;
        EnsureXRReadingAnchor();
        float height=XRReadingWidthMetres*XRReadingCanvasHeight/1000f*xrUnits;
        xrReadingAnchor.SetPositionAndRotation(new Vector3(0,height,XRReadingWallDepth),Quaternion.identity);
        xrReadingAnchor.localScale=Vector3.one*xrUnits;
        xrReadingAnchored=true;xrReadingRevision++;
        Debug.Log("[DongbaMRUI] Forest side anchored to ground edge; revision="+xrReadingRevision);
    }

    public string XRReadingDiagnostic=>xrReadingAnchor==null?"not configured":
        "sideFixed="+xrReadingAnchored+" revision="+xrReadingRevision+" headParent="+xrReadingAnchor.IsChildOf(cam.transform)+
        " anchor="+xrReadingAnchor.position.ToString("F2")+" subtitles=head-follow";
}}
