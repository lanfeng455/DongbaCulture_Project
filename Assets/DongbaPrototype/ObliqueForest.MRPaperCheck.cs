#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckMRPaper(){
        const string folder="Verification/mr-paper";Directory.CreateDirectory(folder);
        var report=new StringBuilder();bool passed=true;
        void Check(bool ok,string message){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+message);Debug.Log((ok?"PASS ":"FAIL ")+message);}
        SetStoryModeEnabled(true);RestartRun();SkipOpeningForVerification();ResetForestSubtitles();
        var ground=GameObject.Find("Stationary paper ground");int layer=ground.layer;ground.layer=29;
        var test=new GameObject("Paper-only alpha verification").AddComponent<Camera>();test.enabled=false;
        test.transform.SetPositionAndRotation(new Vector3(0,90,0),Quaternion.Euler(90,0,0));
        test.orthographic=true;test.orthographicSize=23;test.aspect=2;test.cullingMask=1<<29;test.clearFlags=CameraClearFlags.SolidColor;test.backgroundColor=Color.clear;
        var rt=RenderTexture.GetTemporary(920,460,24,RenderTextureFormat.ARGB32);test.targetTexture=rt;
        var image=new Texture2D(920,460,TextureFormat.RGBA32,false);
        var oldRT=RenderTexture.active;
        float minAlpha=1,minBrightness=1;float normalBrightness=0;int samples=0;
        Shader.SetGlobalFloat("_DongbaMRNight",0);test.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,920,460),0,0);image.Apply();
        // GPU pixels throughout groves and clearings, well inside the thin perimeter.
        for(int y=65;y<=395;y+=15)for(int x=60;x<=860;x+=20){var c=image.GetPixel(x,y);minAlpha=Mathf.Min(minAlpha,c.a);minBrightness=Mathf.Min(minBrightness,c.grayscale);normalBrightness+=c.grayscale;samples++;}
        normalBrightness/=samples;
        Check(minAlpha>.99f,"Every sampled interior location is opaque; no room-dependent dark patches, min alpha="+minAlpha);
        Check(minBrightness>.70f,"Paper remains bright with original fibers, min luminance="+minBrightness);
        Check(image.GetPixel(460,1).a<.01f,"Outside the paper stays transparent for passthrough");
        File.WriteAllBytes(folder+"/paper-alpha.png",image.EncodeToPNG());
        Shader.SetGlobalFloat("_DongbaMRNight",1);test.Render();image.ReadPixels(new Rect(0,0,920,460),0,0);image.Apply();
        float nightBrightness=0;bool nightOpaque=true;
        for(int y=65;y<=395;y+=15)for(int x=60;x<=860;x+=20){var c=image.GetPixel(x,y);nightBrightness+=c.grayscale;nightOpaque&=c.a>.99f;}
        nightBrightness/=samples;
        Check(nightOpaque&&nightBrightness<normalBrightness*.85f,"Story night still dims paper uniformly without revealing room patches");
        Shader.SetGlobalFloat("_DongbaMRNight",0);ground.layer=layer;test.targetTexture=null;RenderTexture.active=oldRT;RenderTexture.ReleaseTemporary(rt);Destroy(image);Destroy(test.gameObject);
        cam.transform.position=new Vector3(0,46,-55);cam.transform.LookAt(new Vector3(0,2,0));cam.fieldOfView=63;
        var runner=new GameObject("MR paper view verification").AddComponent<MRJourneyCheckRunner>();runner.View=cam;runner.Actor=body;
        yield return null;runner.Pending=folder+"/forest.png";while(runner.Pending!=null)yield return null;
        File.WriteAllText(folder+"/checks.txt",report.ToString());UnityEditor.EditorApplication.Exit(passed?0:1);
    }
}}
#endif
