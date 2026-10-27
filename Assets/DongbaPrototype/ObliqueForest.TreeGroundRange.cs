using System.Collections;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {
public partial class ObliqueForest {
    [Header("Tree ground range (visual only)")]
    public float TreeGroundRangeRadius=2.2f;
    Sprite treeRangeSprite;Material treeRangeMaterial;int treeRangeCount;
    void AddTreeGroundRange(Being tree){
        if(treeRangeSprite==null){var texture=Texture2D.whiteTexture;treeRangeSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width);treeRangeMaterial=new Material(Resources.Load<Shader>("TreeGroundRange"));}
        var t=new GameObject("Tree and undergrowth ground range / decorative").transform;t.SetParent(tree.root,false);t.localPosition=new Vector3(0,.018f,0);t.rotation=Quaternion.Euler(90,0,0);t.localScale=Vector3.one*(TreeGroundRangeRadius*2);
        var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=treeRangeSprite;r.sharedMaterial=treeRangeMaterial;r.sortingOrder=-88;treeRangeCount++;
        tree.groundRange=r;tree.groundRangeProperties=new MaterialPropertyBlock();tree.groundRangeProperties.SetFloat("_Vitality",1);r.SetPropertyBlock(tree.groundRangeProperties);
    }
    void UpdateTreeGroundRanges(float dt,float decline){
        if(treeRangeMaterial==null)return;treeRangeMaterial.SetFloat("_Decline",decline);
        foreach(var b in plants)if(b.groundRange!=null){b.groundRangeVitality=Mathf.MoveTowards(b.groundRangeVitality,b.health>0?1:0,dt/1.4f);b.groundRangeProperties.SetFloat("_Vitality",b.groundRangeVitality);b.groundRange.SetPropertyBlock(b.groundRangeProperties);}
    }
    IEnumerator CaptureTreeRanges(){
        string folder="Verification/tree-ranges";Directory.CreateDirectory(folder);yield return new WaitForSeconds(2);
        Vector3[] points={ForestSpawn,new Vector3(-28,0,7),new Vector3(RiverX(-8)-2,0,-8)};
        for(int i=0;i<points.Length;i++){player.position=points[i];FollowPlayerCamera(100);yield return new WaitForSeconds(.2f);CaptureSceneToTexture(folder+"/view-"+i+".png");}
        int trees=plants.FindAll(b=>b.tree).Count;File.WriteAllText(folder+"/checks.txt","Trees="+trees+"; ground ranges="+treeRangeCount+"; all trees covered="+(trees==treeRangeCount)+"; radius="+TreeGroundRangeRadius+"; visual only, no collider added.");Application.Quit();
    }
}
}
