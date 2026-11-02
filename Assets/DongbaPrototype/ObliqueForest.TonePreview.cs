using System.Collections;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {
public partial class ObliqueForest {
    bool tonePreviewFreezeAnimals;
    IEnumerator CaptureTonePreview(){
        string folder="Verification/tone-preview";Directory.CreateDirectory(folder+"/frames");
        tonePreviewFreezeAnimals=true;player.position=ForestSpawn;FollowPlayerCamera(100);
        yield return new WaitForSeconds(1);Time.captureFramerate=30;
        var rt=new RenderTexture(960,540,24);var tex=new Texture2D(960,540,TextureFormat.RGB24,false);
        var prior=cam.targetTexture;var active=RenderTexture.active;Being cut=null;
        Vector3 cameraPosition=cam.transform.position;Quaternion cameraRotation=cam.transform.rotation;
        int treeCount=plants.Count,animalCount=animals.Count;
        try{for(int frame=0;frame<900;frame++){
            if(frame==90)while(Balance<10)RegisterCare();
            if(frame==270){ecologyPreviewWalking=true;ecologyPreviewDirection=Vector3.left;}
            if(frame==300)ecologyPreviewDirection=Vector3.right;
            if(frame==330){ecologyPreviewWalking=false;player.position=ForestSpawn;FollowPlayerCamera(100);}
            if(frame==360)while(Balance>-10)RegisterDamage();
            if(frame==600)while(Balance<0)RegisterCare();
            if(frame==750){float best=10;foreach(var b in plants)if(b.tree&&Vector3.Distance(b.home,Feet)<best){best=Vector3.Distance(b.home,Feet);cut=b;}if(cut!=null){ApplyEcologyHit(cut);ApplyEcologyHit(cut);ApplyEcologyHit(cut);}}
            // Fixed camera across the comparison and recording; test-only animal freeze.
            cam.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
            if(frame==60||frame==240||frame==570||frame==735){string name=frame==60?"normal":frame==240?"flourishing":frame==570?"declining":"recovered";CaptureSceneToTexture(folder+"/"+name+".png",1600,900);File.AppendAllText(folder+"/checks.txt",name+": balance="+Balance+" visual="+ecologyVisual+" camera="+cam.transform.position+" player="+Feet+"\n");}
            if(frame==870){CaptureSceneToTexture(folder+"/felled-range.png",1600,900);File.AppendAllText(folder+"/checks.txt","Felled range vitality="+(cut==null?-1:cut.groundRangeVitality)+"; wind objects="+GameObject.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length+" total renderers\n");if(cut==null||cut.groundRangeVitality>.001f)throw new System.Exception("Felled range did not fade");}
            cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply(false,false);File.WriteAllBytes(folder+"/frames/"+frame.ToString("D4")+".jpg",tex.EncodeToJPG(85));cam.targetTexture=prior;RenderTexture.active=active;yield return null;
        }
        int windCount=0;foreach(var t in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(t.name.StartsWith("Dongba wind glyph"))windCount++;
        File.AppendAllText(folder+"/checks.txt","Generated wind glyphs="+windCount+"; tree/plant count unchanged="+(plants.Count==treeCount)+"; animal count unchanged="+(animals.Count==animalCount)+"\nTest directly changes ecology counters; animal positions frozen for same-layout comparisons. Camera fixed. Existing leaves are not replaced.\n");
        }finally{tonePreviewFreezeAnimals=false;ecologyPreviewWalking=false;cam.targetTexture=prior;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(tex);}
        Application.Quit();
    }
}
}
