using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    public GameObject XRReelSheet=>journeyReelCanvas!=null?journeyReelCanvas.gameObject:null;
    public bool XRPuzzleActive {get;private set;}
    public float XRWorldUnits=>xrUnits;
    public float XRStageWidth=>xrUnits*1.35f;
    public Vector3 XRStageCentre=>new Vector3(0,XRStageWidth*9f/32f,ForestHalfDepth);
    public void SetXRPuzzleActive(bool active){
        XRPuzzleActive=active;
        if(active){xrHelpOpen=false;if(forestCanvas!=null)forestCanvas.gameObject.SetActive(false);}
    }
}}
