using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    Transform knifeGroundBlood,bloodKnife;
    void UpdateKnifeGroundBlood(){
        if(droppedKnife==null){if(knifeGroundBlood!=null)Destroy(knifeGroundBlood.gameObject);knifeGroundBlood=bloodKnife=null;return;}
        if(knifeGroundBlood!=null&&bloodKnife==droppedKnife){knifeGroundBlood.gameObject.SetActive(droppedKnife.gameObject.activeInHierarchy);return;}
        if(knifeGroundBlood!=null)Destroy(knifeGroundBlood.gameObject);
        bloodKnife=droppedKnife;
        // Keep ground ink out of the tilted/scaled knife hierarchy: it must remain flat on paper.
        knifeGroundBlood=new GameObject("Brown blood beneath lost knife / ordinary ink wash").transform;
        knifeGroundBlood.SetParent(transform,false);knifeGroundBlood.position=droppedKnife.position+new Vector3(.04f,.035f,-.26f);knifeGroundBlood.rotation=Quaternion.Euler(90,0,0);
        var mark=knifeGroundBlood.gameObject.AddComponent<SpriteRenderer>();mark.sprite=shadow;mark.sharedMaterial=plainTreeInk;
        mark.color=new Color(.46f,.20f,.105f,.75f);mark.sortingOrder=droppedKnife.GetComponent<SpriteRenderer>().sortingOrder-1;
        var size=mark.sprite.bounds.size;knifeGroundBlood.localScale=new Vector3(.95f/size.x,.55f/size.y,1);
        knifeGroundBlood.gameObject.SetActive(droppedKnife.gameObject.activeInHierarchy);
    }
}}
