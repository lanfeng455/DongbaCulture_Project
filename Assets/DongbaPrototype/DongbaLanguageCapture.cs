using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {
// Only used by command-line verification. Capture overlays as well as the scene.
public static class DongbaLanguageCapture {
    public static void Write(Camera camera,string path){
        var canvases=new List<Canvas>();var distances=new List<float>();var cameras=new List<Camera>();var orders=new List<int>();
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.renderMode==RenderMode.ScreenSpaceOverlay){canvases.Add(canvas);distances.Add(canvas.planeDistance);cameras.Add(canvas.worldCamera);orders.Add(canvas.sortingOrder);canvas.sortingOrder=31000+Mathf.Clamp(canvas.sortingOrder,0,1000);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;}
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var target=RenderTexture.GetTemporary(1600,900,24);var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
        finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);Object.Destroy(pixels);for(int i=0;i<canvases.Count;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];canvases[i].sortingOrder=orders[i];}Canvas.ForceUpdateCanvases();}
    }
}
}
