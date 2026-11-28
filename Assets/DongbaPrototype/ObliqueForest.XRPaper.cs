using UnityEngine;
namespace DongbaPrototype { public partial class ObliqueForest {
    Material xrPaperMaterial;
    public void ConfigureXRPaper(){
        if(!XRTrackingControlsCamera||groundMaterial==null)return;
        // Match the puzzle tabletop: opaque, unlit paper throughout the playable
        // interior. Only the outer perimeter blends into the real room.
        xrPaperMaterial=new Material(Resources.Load<Shader>("XRForestPaper"));
        xrPaperMaterial.mainTexture=paper;
        var ground=GameObject.Find("Stationary paper ground");ground.GetComponent<Renderer>().sharedMaterial=xrPaperMaterial;
        Destroy(groundMaterial);groundMaterial=xrPaperMaterial;
    }
    void OnDestroy(){
        ReleaseStormMeshes();
        if(xrPaperMaterial!=null)Destroy(xrPaperMaterial);
        if(xrHudInk!=null)Destroy(xrHudInk);
        if(xrReadingAnchor!=null)Destroy(xrReadingAnchor.gameObject);
    }
}}
