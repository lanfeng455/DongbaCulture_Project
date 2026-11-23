using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    GameObject knifeShotPaper;
    float knifeShotZoom=6.8f;

    void FrameKnifeEncounter(ref Vector3 center,ref float zoom){
        bool returning=StoryModeEnabled&&journeyStage==JourneyStage.ReturnForKnife;
        bool emptyShot=StoryModeEnabled&&(narrativeBeat==NarrativeBeat.EmptyClearing||journeyStage==JourneyStage.Captured);
        SetKnifeShotPaper(returning||emptyShot);
        if(!returning&&!emptyShot)return;
        if(emptyShot){center=captureCameraCenter;zoom=knifeShotZoom;return;}
        if(!shuConfrontationStarted)return;
        if(!carryStarted&&captureBeatAge<.8f&&storyShu!=null&&droppedKnife!=null){
            // Frame the actual death site, never the authored clearing or the player
            // waiting on the west bank. Keep all three actors above the subtitle band.
            Bounds bounds=storyFatherImage.bounds;
            bounds.Encapsulate(storyShu.GetComponent<SpriteRenderer>().bounds);
            bounds.Encapsulate(droppedKnife.GetComponent<SpriteRenderer>().bounds);
            float left=float.PositiveInfinity,right=float.NegativeInfinity;
            float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
            for(int i=0;i<8;i++){
                var p=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,(i&4)==0?bounds.min.z:bounds.max.z);
                float x=Vector3.Dot(p,cam.transform.right),y=Vector3.Dot(p,cam.transform.up);
                left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);
            }
            knifeShotZoom=Mathf.Max(5.8f,(right-left)/(cam.aspect*1.44f),(top-bottom)/1.15f);
            float viewCenterY=(top+bottom)*.5f-knifeShotZoom*.14f;
            captureCameraCenter=new Vector3((left+right)*.5f,0,viewCenterY/cam.transform.up.z);
        }
        // Freeze the shot for the abduction so Shu really exits the frame.
        center=captureCameraCenter;zoom=knifeShotZoom;
    }

    void SetKnifeShotPaper(bool visible){
        if(visible&&knifeShotPaper==null){
            var source=GameObject.Find("Stationary paper ground");
            if(source==null)return;
            // Extend only the paper behind an edge-of-map cutscene. Preserve the
            // original UV density/phase; do not enlarge the playable map or its art.
            var mesh=Instantiate(source.GetComponent<MeshFilter>().sharedMesh);
            var vertices=mesh.vertices;var uv=mesh.uv;
            for(int i=0;i<vertices.Length;i++){
                vertices[i]=new Vector3(vertices[i].x*3,vertices[i].y,vertices[i].z*3);
                uv[i]=(uv[i]-Vector2.one)*3+Vector2.one;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.RecalculateBounds();
            knifeShotPaper=new GameObject("Capture shot paper continuity");
            knifeShotPaper.transform.SetParent(transform,false);
            knifeShotPaper.transform.position=source.transform.position-Vector3.up*.02f;
            knifeShotPaper.transform.rotation=source.transform.rotation;
            knifeShotPaper.transform.localScale=source.transform.lossyScale;
            knifeShotPaper.AddComponent<MeshFilter>().sharedMesh=mesh;
            var image=knifeShotPaper.AddComponent<MeshRenderer>();image.sharedMaterial=groundMaterial;image.sortingOrder=-101;
        }
        if(knifeShotPaper!=null)knifeShotPaper.SetActive(visible);
    }
} }
