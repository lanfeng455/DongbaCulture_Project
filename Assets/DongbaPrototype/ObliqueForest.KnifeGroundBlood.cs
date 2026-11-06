using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    Transform knifeGroundBlood;
    void UpdateKnifeGroundBlood(){
        if(droppedKnife==null){knifeGroundBlood=null;return;}
        if(knifeGroundBlood!=null&&knifeGroundBlood.IsChildOf(droppedKnife))return;
        knifeGroundBlood=new GameObject("Faint brown blood beneath lost knife / ordinary ink wash").transform;
        knifeGroundBlood.SetParent(droppedKnife,false);knifeGroundBlood.position=droppedKnife.position+new Vector3(.04f,.018f,.015f);knifeGroundBlood.rotation=Quaternion.Euler(90,0,0);
        var mark=knifeGroundBlood.gameObject.AddComponent<SpriteRenderer>();mark.sprite=shadow;
        // Reuse the transparent soft ink asset. Same warm brown as the great boar.
        mark.color=new Color(.46f,.20f,.105f,.34f);mark.sortingOrder=droppedKnife.GetComponent<SpriteRenderer>().sortingOrder-1;
        knifeGroundBlood.localScale=Vector3.one;var size=mark.sprite.bounds.size;
        var inherited=knifeGroundBlood.lossyScale;knifeGroundBlood.localScale=new Vector3(.50f/(size.x*inherited.x),.25f/(size.y*inherited.y),1);
    }
}}
