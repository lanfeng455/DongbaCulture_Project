using System.Collections.Generic;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    // The cutting head of the existing axe, measured in its unmodified 256px canvas.
    // These are collision geometry, not new glyph strokes. Handle/grip remains separate.
    static readonly Vector2[] BladeOutline={new Vector2(149,185),new Vector2(176,162),new Vector2(232,160),new Vector2(201,119),new Vector2(111,84),new Vector2(128,137)};
    sealed class GlyphContactMesh {public Vector2[] vertices;public ushort[] triangles;}
    readonly Dictionary<Sprite,GlyphContactMesh> contactMeshes=new Dictionary<Sprite,GlyphContactMesh>();
    readonly HashSet<Being> swingContacts=new HashSet<Being>();
    readonly Vector3[] bladeWorld=new Vector3[6];
    readonly Vector2[] bladeLocal=new Vector2[6];
    int swingFacing=1;Vector3 swingDirection;float sampledAttackAge=-1;Vector3 previousGrip;

    GlyphContactMesh ContactMesh(Sprite sprite){
        if(!contactMeshes.TryGetValue(sprite,out var mesh)){
            mesh=new GlyphContactMesh{vertices=sprite.vertices,triangles=sprite.triangles};contactMeshes.Add(sprite,mesh);
        }return mesh;
    }
    void PrepareAxeContact(){foreach(var b in plants)if(b.tree)ContactMesh(treeGlyphSets[b.glyphKey].trunk);foreach(var b in animals)ContactMesh(b.renderer.sprite);}
    void BeginAxeAttack(){CompletePlayerTurnForAttack();attackAge=0;hit=false;sampledAttackAge=-1;swingContacts.Clear();swingFacing=facing;swingDirection=Vector3.right*swingFacing;}
    static float AxeSwing(float age){return age<.16f?Mathf.Lerp(20,85,Mathf.SmoothStep(0,1,age/.16f)):Mathf.Lerp(85,-110,Mathf.SmoothStep(0,1,(age-.16f)/.3f));}
    void PoseAxe(float age){axe.localRotation=Quaternion.Euler(0,0,swingFacing*AxeSwing(age));var scale=axe.localScale;scale.x=Mathf.Abs(scale.x)*swingFacing;axe.localScale=scale;}
    void UpdateAxeAttack(float dt){
        if(attackAge<0){sampledAttackAge=-1;return;}
        if(sampledAttackAge<0){swingContacts.Clear();swingFacing=facing;swingDirection=Vector3.right*swingFacing;sampledAttackAge=0;previousGrip=HandPosition();}
        if(attackAge<.16f&&attackAge+dt>=.16f)PlayForestVariant("axe",3,HandPosition());
        facing=swingFacing;attackAge+=dt;axe.gameObject.SetActive(true);axe.localPosition=Vector3.zero;
        // Sample the actual rendered arc, including frame-to-frame hand travel. Damage
        // begins on the downswing, never while raising the axe; one hit per target/swing.
        float start=Mathf.Max(.16f,sampledAttackAge),end=Mathf.Min(.46f,attackAge);
        if(end>=start){
            int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(AxeSwing(end)-AxeSwing(start))/4));
            for(int n=0;n<=steps;n++){
                float age=Mathf.Lerp(start,end,n/(float)steps);PoseAxe(age);
                float travel=Mathf.InverseLerp(sampledAttackAge,attackAge,age);
                Vector3 shift=Vector3.Lerp(previousGrip,HandPosition(),travel)-HandPosition();
                var sprite=axe.GetComponent<SpriteRenderer>().sprite;
                for(int i=0;i<bladeWorld.Length;i++)bladeWorld[i]=axe.TransformPoint(new Vector3((BladeOutline[i].x/256*sprite.rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,(BladeOutline[i].y/256*sprite.rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0))+shift;
                foreach(var b in plants)if(b.tree)TryAxeContact(b);
                foreach(var b in animals)TryAxeContact(b);
                if(journeyStage==JourneyStage.Boar&&storyBoar!=null)TryAxeContact(storyBoar);
            }
        }
        PoseAxe(attackAge);sampledAttackAge=attackAge;previousGrip=HandPosition();
        if(attackAge>.62f){attackAge=-1;sampledAttackAge=-1;axe.gameObject.SetActive(false);}
    }
    void TryAxeContact(Being b){
        if(b.health<=0||!b.root.gameObject.activeInHierarchy||swingContacts.Contains(b)||Vector3.Distance(b.root.position,Feet)>6)return;
        // Prevent hits on a different ground-depth row merely overlapping on screen.
        if(Mathf.Abs(b.root.position.z-Feet.z)>1.45f)return;
        var mesh=ContactMesh(b.renderer.sprite);
        for(int i=0;i<bladeLocal.Length;i++){
            var p=b.art.InverseTransformPoint(bladeWorld[i]);
            bladeLocal[i]=new Vector2(b.renderer.flipX?-p.x:p.x,b.renderer.flipY?-p.y:p.y);
        }
        if(!BladeTouchesMesh(mesh))return;
        swingContacts.Add(b);hit=true;ApplyEcologyHit(b);
        if(b.tree)b.shake=.45f;
        else {b.angry=true;var p=b.root.position+swingDirection*.35f;if(ClearAnimalStep(p,b))b.root.position=p;if(b.health<=0)b.root.gameObject.SetActive(false);}
    }
    bool BladeTouchesMesh(GlyphContactMesh mesh){
        for(int t=0;t<mesh.triangles.Length;t+=3){
            Vector2 a=mesh.vertices[mesh.triangles[t]],b=mesh.vertices[mesh.triangles[t+1]],c=mesh.vertices[mesh.triangles[t+2]];
            for(int i=1;i<bladeLocal.Length-1;i++)if(TrianglesTouch(bladeLocal[0],bladeLocal[i],bladeLocal[i+1],a,b,c))return true;
        }return false;
    }
    static float Cross2(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
    static bool Separated(Vector2 a,Vector2 b,Vector2 c,Vector2 x,Vector2 y,Vector2 z){
        Vector2 normal=new Vector2(a.y-b.y,b.x-a.x);float p=Vector2.Dot(normal,a),q=Vector2.Dot(normal,c);
        float u=Vector2.Dot(normal,x),v=Vector2.Dot(normal,y),w=Vector2.Dot(normal,z);
        return Mathf.Max(u,Mathf.Max(v,w))<Mathf.Min(p,q)||Mathf.Min(u,Mathf.Min(v,w))>Mathf.Max(p,q);
    }
    static bool TrianglesTouch(Vector2 a,Vector2 b,Vector2 c,Vector2 x,Vector2 y,Vector2 z){
        if(Mathf.Abs(Cross2(b-a,c-a))<.000001f||Mathf.Abs(Cross2(y-x,z-x))<.000001f)return false;
        return !Separated(a,b,c,x,y,z)&&!Separated(b,c,a,x,y,z)&&!Separated(c,a,b,x,y,z)&&!Separated(x,y,z,a,b,c)&&!Separated(y,z,x,a,b,c)&&!Separated(z,x,y,a,b,c);
    }
}}
