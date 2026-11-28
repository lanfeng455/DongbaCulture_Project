using System.Collections.Generic;
using UnityEngine;
namespace DongbaPrototype { public sealed partial class MRPuzzleStage {
    const float TableScale=2.4f;
    float stageAlpha;Vector3 focus;bool focusSet;
    Ivan.Diorama.DioramaPlayerController actor;
    Transform geometryRoot;MeshRenderer table;Material inkTemplate,tableInk;
    readonly Dictionary<Transform,Transform> mirrors=new();
    readonly List<Transform> sourceTransforms=new();
    readonly List<Visual> visuals=new();
    readonly Dictionary<Material,Material> materialCopies=new();
    sealed class Visual {
        public Renderer source,copy;public bool upright;public Transform card;
        public MaterialPropertyBlock block=new();public Vector3[] points;
    }
    public int SpatialVisualCount=>visuals.Count;
    public Vector3 ContentFocus=>focus;
    public Transform FindVisual(Transform source)=>mirrors.TryGetValue(source,out var copy)?copy:null;
    public Vector3 MapPoint(Vector3 point)=>(point-new Vector3(focus.x,0,focus.z))*TableScale;
    void InitializeGeometry(){
        geometryRoot=new GameObject("Spatial puzzle / render-only hierarchy").transform;geometryRoot.SetParent(transform,false);
        inkTemplate=new Material(Resources.Load<Shader>("MRPuzzleInk"));
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Fixed MR puzzle paper ground";Destroy(floor.GetComponent<Collider>());
        floor.transform.SetParent(transform,false);floor.layer=30;floor.transform.position=new Vector3(0,-.06f,0);floor.transform.localScale=new Vector3(9.2f,1,4.2f);
        table=floor.GetComponent<MeshRenderer>();tableInk=new Material(inkTemplate);tableInk.SetFloat("_Sprite",0);table.sharedMaterial=tableInk;table.sortingOrder=-10000;
    }
    Transform Mirror(Transform source){
        if(mirrors.TryGetValue(source,out var found))return found;
        var copy=new GameObject(source.name+" / MR visual").transform;copy.gameObject.layer=30;
        copy.SetParent(source.parent!=null?Mirror(source.parent):geometryRoot,false);
        mirrors.Add(source,copy);sourceTransforms.Add(source);return copy;
    }
    Material MaterialCopy(Material source,bool sprite){
        if(source==null)return inkTemplate;
        if(materialCopies.TryGetValue(source,out var copy))return copy;
        copy=new Material(inkTemplate){name=source.name+" / spatial"};copy.SetFloat("_Sprite",sprite?1:0);materialCopies.Add(source,copy);return copy;
    }
    void RegisterGeometry(Renderer source){
        if(source is ParticleSystemRenderer||source.GetComponentInParent<Canvas>()!=null)return;
        if(actor==null)actor=source.GetComponentInParent<Ivan.Diorama.DioramaPlayerController>();
        if(source.name=="Stationary paper ground"){
            if(source.sharedMaterial!=null){tableInk.mainTexture=source.sharedMaterial.mainTexture;tableInk.mainTextureScale=new Vector2(2,1);}
            return;
        }
        var node=Mirror(source.transform);Renderer copy=null;
        bool isSprite=source is SpriteRenderer;
        if(isSprite){copy=node.gameObject.AddComponent<SpriteRenderer>();}
        else if(source is MeshRenderer&&source.TryGetComponent<MeshFilter>(out var filter)){
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;copy=node.gameObject.AddComponent<MeshRenderer>();
        }else if(source is LineRenderer line){
            var l=node.gameObject.AddComponent<LineRenderer>();l.useWorldSpace=true;l.alignment=LineAlignment.View;l.textureMode=line.textureMode;l.numCapVertices=line.numCapVertices;l.numCornerVertices=line.numCornerVertices;copy=l;
        }
        if(copy==null)return;
        var mats=source.sharedMaterials;for(int i=0;i<mats.Length;i++)mats[i]=MaterialCopy(mats[i],isSprite);copy.sharedMaterials=mats;
        copy.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;copy.receiveShadows=false;
        bool upright=isSprite&&Mathf.Abs(Vector3.Dot(source.transform.forward,Vector3.up))<.85f;
        // Attached axe/foliage inherit their parent's card orientation and hand pivot.
        if(upright)for(var t=source.transform.parent;t!=null;t=t.parent)if(t.GetComponent<SpriteRenderer>()!=null){upright=false;break;}
        visuals.Add(new Visual{source=source,copy=copy,upright=upright,card=node});
    }
    void UpdateGeometry(){
        if(geometryRoot==null||ViewCamera==null)return;
        ViewCamera.enabled=false;
        var ray=ViewCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));var plane=new Plane(Vector3.up,Vector3.zero);
        Vector3 desired=plane.Raycast(ray,out var distance)?ray.GetPoint(distance):ViewCamera.transform.position;desired.y=0;
        if(actor!=null)desired.z=Mathf.Clamp(desired.z,actor.transform.position.z-5.5f,actor.transform.position.z+5.5f);
        // Authored pans shift the scenery on the fixed tabletop. The eye camera is untouched.
        focus=focusSet?Vector3.Lerp(focus,desired,1-Mathf.Exp(-Time.unscaledDeltaTime/ .28f)):desired;focusSet=true;
        geometryRoot.position=-focus*TableScale;geometryRoot.localScale=Vector3.one*TableScale;
        bool visible=stageAlpha>.001f&&(Source==null||Source.XRExperienceReady);
        table.enabled=visible;tableInk.SetFloat("_StageAlpha",stageAlpha);
        foreach(var pair in materialCopies){
            var source=pair.Key;var copy=pair.Value;if(source==null)continue;
            copy.mainTexture=source.mainTexture!=null?source.mainTexture:Texture2D.whiteTexture;
            copy.mainTextureScale=source.mainTextureScale;copy.mainTextureOffset=source.mainTextureOffset;
            copy.SetColor("_Color",source.HasProperty("_Color")?source.GetColor("_Color"):source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white);
        }
        for(int i=0;i<sourceTransforms.Count;i++){
            var source=sourceTransforms[i];if(source==null)continue;var copy=mirrors[source];
            var parent=source.parent!=null?Mirror(source.parent):geometryRoot;
            if(copy.parent!=parent)copy.SetParent(parent,false);copy.localPosition=source.localPosition;copy.localRotation=source.localRotation;copy.localScale=source.localScale;
            copy.gameObject.SetActive(source.gameObject.activeSelf);
        }
        foreach(var visual in visuals){
            var source=visual.source;var copy=visual.copy;if(source==null){if(copy!=null)copy.enabled=false;continue;}
            var bounds=source.bounds;var local=MapPoint(bounds.center);var extent=bounds.extents*TableScale;
            bool inTable=Mathf.Abs(local.x)<46+extent.x&&Mathf.Abs(local.z)<21+extent.z;
            copy.enabled=visible&&inTable&&!originalVisibility[source]&&source.enabled&&source.gameObject.activeInHierarchy;
            if(!copy.enabled)continue;
            if(visual.upright){
                Vector3 away=visual.card.position-eyes.transform.position;away.y=0;
                if(away.sqrMagnitude>.001f)visual.card.rotation=Quaternion.LookRotation(away)*Quaternion.Euler(0,0,source.transform.eulerAngles.z);
            }
            copy.sortingLayerID=source.sortingLayerID;copy.sortingOrder=source.sortingOrder;
            if(source is SpriteRenderer original&&copy is SpriteRenderer sprite){
                sprite.sprite=original.sprite;sprite.color=original.color;sprite.flipX=original.flipX;sprite.flipY=original.flipY;sprite.drawMode=original.drawMode;sprite.size=original.size;
            }else if(source is LineRenderer line&&copy is LineRenderer output){
                if(visual.points==null||visual.points.Length!=line.positionCount)visual.points=new Vector3[line.positionCount];
                line.GetPositions(visual.points);for(int i=0;i<visual.points.Length;i++)visual.points[i]=MapPoint(line.useWorldSpace?visual.points[i]:line.transform.TransformPoint(visual.points[i]));
                output.positionCount=visual.points.Length;output.SetPositions(visual.points);output.startWidth=line.startWidth*TableScale;output.endWidth=line.endWidth*TableScale;output.startColor=line.startColor;output.endColor=line.endColor;output.loop=line.loop;
            }else if(source is MeshRenderer&&source.TryGetComponent<MeshFilter>(out var mesh))copy.GetComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
            source.GetPropertyBlock(visual.block);visual.block.SetFloat("_StageAlpha",stageAlpha);copy.SetPropertyBlock(visual.block);
        }
    }
    void ReleaseGeometry(){
        foreach(var material in materialCopies.Values)if(material!=null)Destroy(material);materialCopies.Clear();
        if(inkTemplate!=null)Destroy(inkTemplate);if(tableInk!=null)Destroy(tableInk);
    }
}}
