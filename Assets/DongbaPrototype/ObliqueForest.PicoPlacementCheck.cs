#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    IEnumerator VerifyPicoPlacementOrientation(Action<bool,string> check){
        var pico=FindFirstObjectByType<DongbaXR.DongbaPicoMR>();
        if(cam.TryGetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>(out var pose))pose.enabled=false;
        xrSimulating=true;xrSimStick=Vector2.zero;xrSimA=xrSimX=xrSimTrigger=false;
        StartForestMode(false);ResetForestSubtitles();DongbaInterface.Instance?.Close(false);
        tonePreviewFreezeAnimals=true;
        careCount=7;damageCount=2;int initialBalance=Balance,animalCount=animals.Count,plantCount=plants.Count;
        Vector3 playerBefore=Feet;float units=pico.worldUnitsPerMetre;
        string folder="Verification/mr-placement";Directory.CreateDirectory(folder);
        // Reproduce the recorded old back-side view without changing any artwork.
        cam.transform.position=new Vector3(10,66,75);cam.transform.LookAt(Vector3.zero);
        AnchorXRReadingFrame();yield return new WaitForSeconds(.6f);
        CaptureSceneToTexture(folder+"/before-back-side.png");
        var surface=new GameObject("Placement verification scanned surface");surface.layer=30;
        surface.transform.SetParent(pico.origin.transform,false);
        var surfaceLocal=new Vector3(.15f,0,-.1f);
        surface.transform.localPosition=surfaceLocal-Vector3.up*.005f;
        var collider=surface.AddComponent<BoxCollider>();collider.size=new Vector3(4,.01f,4);
        foreach(float heading in new[]{0f,90f,180f,270f,32f,137f}){
            pico.SimulateRepositionInputForVerification(false,0);
            pico.SimulateRepositionInputForVerification(true,pico.repositionHoldSeconds+.1f);
            yield return null;
            var rawRotation=Quaternion.Euler(0,heading,0);
            var localHead=surfaceLocal-rawRotation*Vector3.forward*1.25f+Vector3.up*1.1f;
            cam.transform.SetPositionAndRotation(pico.origin.transform.TransformPoint(localHead),pico.origin.transform.rotation*rawRotation*Quaternion.Euler(42,0,0));
            var selectedPoint=pico.origin.transform.TransformPoint(surfaceLocal);
            pico.SimulateSurfacePlacementForVerification(selectedPoint);
            yield return null;yield return null;
            string label="MR placement "+heading.ToString("F0")+" deg: ";
            check(pico.origin.transform.TransformPoint(surfaceLocal).magnitude<.002f,label+"scanned hit maps exactly to forest origin");
            check(Mathf.Abs(cam.transform.position.x)<.002f&&cam.transform.position.z<-.9f*units&&Mathf.Abs(cam.transform.position.y-1.1f*units)<.002f,label+"viewer starts on authored front, with height preserved");
            var east=cam.WorldToViewportPoint(new Vector3(12,0,0));var west=cam.WorldToViewportPoint(new Vector3(-12,0,0));
            check(east.x>west.x&&east.z>0&&west.z>0,label+"east bank is screen-right and west bank is screen-left");
            check(XRStickMovement(Vector2.right).x>.999f&&XRStickMovement(Vector2.up).z>.999f,label+"stick directions agree with the displayed map");
            check(pico.origin.transform.localScale==Vector3.one*units&&Vector3.Dot(pico.origin.transform.up,Vector3.up)>.999f,label+"rig is rigid, upright and never mirrored");
            check(Vector3.Dot(xrReadingAnchor.position-cam.transform.position,Vector3.forward)>0&&XRPanelAboveHorizon(xrObjectiveNote),label+"upper information area faces the viewer on the aligned side");
            check(Physics.Raycast(cam.transform.position,-cam.transform.position.normalized,out var hit,300,1<<30)&&hit.point.magnitude<.03f,label+"scanned surface ray stays aligned after rotation");
            check(Balance==initialBalance&&animals.Count==animalCount&&plants.Count==plantCount&&Vector3.Distance(Feet,playerBefore)<.002f&&!StoryModeEnabled,label+"reposition preserves gameplay state");
            if(heading%90==0)CaptureSceneToTexture(folder+"/aligned-"+heading.ToString("F0")+".png");
            var fixedRotation=pico.origin.transform.rotation;var fixedPosition=pico.origin.transform.position;int revision=xrReadingRevision;
            cam.transform.rotation*=Quaternion.Euler(0,35,0);yield return null;
            check(Quaternion.Angle(fixedRotation,pico.origin.transform.rotation)<.001f&&Vector3.Distance(fixedPosition,pico.origin.transform.position)<.001f&&xrReadingRevision==revision,label+"head turns do not rotate the placed forest or UI");
        }
        // Return to a normal front-side fixture for the rest of the UI tests.
        pico.SimulateRepositionInputForVerification(false,0);pico.SimulateRepositionInputForVerification(true,pico.repositionHoldSeconds+.1f);
        yield return null;
        cam.transform.SetPositionAndRotation(pico.origin.transform.TransformPoint(new Vector3(0,1.1f,-1.25f)),pico.origin.transform.rotation*Quaternion.Euler(42,0,0));
        pico.SimulateSurfacePlacementForVerification(pico.origin.transform.TransformPoint(Vector3.zero));
        Destroy(surface);yield return null;
        StartForestMode(false);yield return null;
    }
}}
#endif
