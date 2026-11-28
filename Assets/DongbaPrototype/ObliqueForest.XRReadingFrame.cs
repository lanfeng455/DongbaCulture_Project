using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    Transform xrReadingAnchor;
    bool xrReadingAnchored;
    int xrReadingRevision;

    void EnsureXRReadingAnchor(){
        if(xrReadingAnchor!=null)return;
        xrReadingAnchor=new GameObject("PICO fixed forest reading frame").transform;
        xrReadingAnchor.localScale=Vector3.one*xrUnits;
    }

    // Snapshot the placement pose, never the per-frame headset pose. The frame
    // belongs to the placed forest and remains still when the viewer looks away.
    public void AnchorXRReadingFrame(){
        if(!XRTrackingControlsCamera||cam==null||xrUnits<=0)return;
        EnsureXRReadingAnchor();
        Vector3 forward=cam.transform.forward;
        Vector3 up=Vector3.ProjectOnPlane(Vector3.up,forward);
        if(up.sqrMagnitude<.001f)up=cam.transform.up;
        float surfaceDistance=Vector3.Distance(cam.transform.position,Vector3.zero)/xrUnits;
        float distance=Mathf.Clamp(surfaceDistance*.85f,.75f,1.4f);
        xrReadingAnchor.SetPositionAndRotation(cam.transform.position+forward*(distance*xrUnits),Quaternion.LookRotation(forward,up));
        // Keep readable angular size at floor/table viewing distances, then fix
        // both physical size and position until the forest is placed again.
        xrReadingAnchor.localScale=Vector3.one*(xrUnits*distance/1.25f);
        xrReadingAnchored=true;xrReadingRevision++;
        Debug.Log("[DongbaMRUI] World frame anchored; revision="+xrReadingRevision+" distance="+distance.ToString("F2")+"m");
    }

    public string XRReadingDiagnostic=>xrReadingAnchor==null?"not configured":
        "fixed="+xrReadingAnchored+" revision="+xrReadingRevision+" headParent="+xrReadingAnchor.IsChildOf(cam.transform)+
        " anchor="+xrReadingAnchor.position.ToString("F2")+" yaw="+xrReadingAnchor.eulerAngles.y.ToString("F1");
}}
