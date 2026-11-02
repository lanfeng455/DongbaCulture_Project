using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {
public partial class ObliqueForest {
    IEnumerator CaptureFriendlyPreview(){
        string folder="Verification/friendly-preview";Directory.CreateDirectory(folder+"/frames");
        player.position=new Vector3(-8,0,-2.5f);lastDryPosition=Feet;FollowPlayerCamera(100);
        yield return new WaitForSeconds(1);Time.captureFramerate=30;
        var rt=new RenderTexture(960,540,24);var tex=new Texture2D(960,540,TextureFormat.RGB24,false);
        var prior=cam.targetTexture;var active=RenderTexture.active;
        try{for(int frame=0;frame<750;frame++){
            if(frame==60)while(Balance<18)RegisterCare();
            if(frame==330){ecologyPreviewWalking=true;ecologyPreviewDirection=Vector3.left;}
            if(frame==390)ecologyPreviewWalking=false;
            if(frame==600)while(Balance>7)RegisterDamage();
            if(frame%30==0){int followers=0,close=0;float speed=0;foreach(var a in animals)if(a.follow>0){followers++;if(Vector3.Distance(a.root.position,Feet)<3.5f)close++;speed=Mathf.Max(speed,a.moveSpeed);}
                File.AppendAllText(folder+"/checks.txt","t="+frame/30+" balance="+Balance+" followers="+followers+" close="+close+" maxSpeed="+speed.ToString("F2")+" player="+Feet+"\n");}
            cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply(false,false);File.WriteAllBytes(folder+"/frames/"+frame.ToString("D4")+".jpg",tex.EncodeToJPG(85));cam.targetTexture=prior;RenderTexture.active=active;yield return null;
        }}finally{ecologyPreviewWalking=false;cam.targetTexture=prior;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(tex);}
        Application.Quit();
    }
    IEnumerator CaptureAnimalEcology(){
        string folder="Verification/animal-ecology";Directory.CreateDirectory(folder+"/frames");
        player.position=new Vector3(-8,0,-2.5f);lastDryPosition=Feet;FollowPlayerCamera(100);
        yield return new WaitForSeconds(1);float measured=0;int ticks=0;while(measured<3){yield return null;measured+=Time.unscaledDeltaTime;ticks++;}
        File.WriteAllText(folder+"/checks.txt","Normal runtime FPS (before capture)="+(ticks/measured).ToString("F1")+"\n");
        Time.captureFramerate=30;var target=new RenderTexture(960,540,24);var tex=new Texture2D(960,540,TextureFormat.RGB24,false);var prior=cam.targetTexture;var active=RenderTexture.active;Being attacked=null;bool armed=false,attackChecked=false;
        try {for(int frame=0;frame<3750;frame++){
            if(frame==120)while(Balance>-10)RegisterDamage();
            if(frame==1500)while(Balance<0)RegisterCare();
            if(frame==2400)while(Balance<18)RegisterCare();
            if(frame==2880){ecologyPreviewWalking=true;ecologyPreviewDirection=Vector3.left;}
            if(frame==2925)ecologyPreviewWalking=false;
            if(frame==3120){float closest=20;foreach(var a in animals)if(a.follow>0){float d=Vector3.Distance(a.root.position,Feet);if(d<closest){closest=d;attacked=a;}}armed=attacked!=null;}
            if(armed&&attacked!=null){Vector3 d=attacked.root.position-Feet;ecologyPreviewDirection=d.normalized;ecologyPreviewWalking=d.magnitude>1.4f;
                if(!ecologyPreviewWalking){heading=d.normalized;facing=d.x<0?-1:1;attackAge=0;hit=false;armed=false;}}
            if(frame>=3180&&attacked!=null&&attacked.angry&&!attackChecked){File.AppendAllText(folder+"/checks.txt","Attack immediately cancels visit="+(attacked.follow==0&&attacked.migrationState==0)+"\n");attackChecked=true;}
            if(frame==3330){ecologyPreviewWalking=false;while(Balance>7)RegisterDamage();}
            if(frame==600||frame==1200){foreach(var a in animals)if(a.migrationState==1)File.AppendAllText(folder+"/checks.txt","DEPART "+a.root.name+" pos="+a.root.position+" goal="+a.navGoal+" next="+(HasAnimalRoute(a)?a.route[a.routeIndex].ToString():"none")+" blocked="+a.blockedTime+"\n");}
            if(frame%150==0){int visible=0,near=0,outgoing=0,hidden=0,returning=0,friendly=0,blocked=0;foreach(var a in animals){if(a.health<=0)continue;if(a.migrationState==2){hidden++;continue;}if(!AnimalOutsideViewAt(a,a.root.position))visible++;if(Vector3.Distance(a.root.position,Feet)<11)near++;if(a.migrationState==1)outgoing++;if(a.migrationState==3)returning++;if(a.follow>0)friendly++;if(a.blockedTime>.5f)blocked++;}
                File.AppendAllText(folder+"/checks.txt","t="+(frame/30)+" balance="+Balance+" visible="+visible+" near="+near+" outgoing="+outgoing+" hidden="+hidden+" returning="+returning+" friendly="+friendly+" blocked="+blocked+" exits="+departuresCompleted+" visits="+visitsStarted+"\n");}
            if(frame==60||frame==1440||frame==2340||frame==2820||frame==3690)CaptureSceneToTexture(folder+"/state-"+(frame/30)+".png");
            cam.targetTexture=target;cam.Render();RenderTexture.active=target;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply(false,false);File.WriteAllBytes(folder+"/frames/"+frame.ToString("D4")+".jpg",tex.EncodeToJPG(85));
            cam.targetTexture=prior;RenderTexture.active=active;yield return null;
        }
        File.AppendAllText(folder+"/checks.txt","Visible hides="+visibleHideErrors+"; visible returns="+visibleSpawnErrors+"; return starts="+returnsStarted+"; attack test completed="+attackChecked+"\n");
        }finally{cam.targetTexture=prior;RenderTexture.active=active;target.Release();Destroy(target);Destroy(tex);}
        Application.Quit();
    }
}
}
