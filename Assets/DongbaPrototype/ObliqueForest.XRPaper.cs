using UnityEngine;
namespace DongbaPrototype { public partial class ObliqueForest {
    Texture2D xrPaperCoverage;Material xrPaperMaterial;
    public void ConfigureXRPaper(float opacity){
        if(!XRTrackingControlsCamera||groundMaterial==null)return;
        const int width=256,height=128;
        var pixels=new Color32[width*height];
        // A single baked coverage mask: paper gathers under groves and riverbanks,
        // thins through clearings, and dissolves at the perimeter. No new glyphs.
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            float wx=(x/(float)(width-1)-.5f)*92,wz=(y/(float)(height-1)-.5f)*42;
            float noise=Mathf.PerlinNoise(wx*.22f+19,wz*.24f+31);
            float edge=Mathf.Min(46-Mathf.Abs(wx),21-Mathf.Abs(wz));
            float border=Mathf.SmoothStep(0,1,Mathf.Clamp01((edge-.5f-(noise-.5f)*1.8f)/2.4f));
            float nearest=100;
            foreach(var tree in plants)if(tree.tree)nearest=Mathf.Min(nearest,Vector2.Distance(new Vector2(wx,wz),new Vector2(tree.home.x,tree.home.z)));
            float grove=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((nearest-1.2f)/5f));
            float river=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(wx-RiverX(wz))-1.1f)/3f));
            float home=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Vector2.Distance(new Vector2(wx,wz),new Vector2(HearthPosition.x,HearthPosition.z))-2)/5));
            float density=Mathf.Max(grove,Mathf.Max(river,home));
            float alpha=border*Mathf.Lerp(.23f,.94f,density)*Mathf.Lerp(.91f,1,noise);
            pixels[y*width+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255));
        }
        xrPaperCoverage=new Texture2D(width,height,TextureFormat.RGBA32,false){name="MR grove paper coverage",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        xrPaperCoverage.SetPixels32(pixels);xrPaperCoverage.Apply(false,true);
        xrPaperMaterial=new Material(Resources.Load<Shader>("XRForestPaper"));
        xrPaperMaterial.mainTexture=paper;xrPaperMaterial.SetTexture("_Coverage",xrPaperCoverage);xrPaperMaterial.SetFloat("_Opacity",opacity);
        var ground=GameObject.Find("Stationary paper ground");ground.GetComponent<Renderer>().sharedMaterial=xrPaperMaterial;
        Destroy(groundMaterial);groundMaterial=xrPaperMaterial;
    }
    void OnDestroy(){
        if(xrPaperCoverage!=null)Destroy(xrPaperCoverage);
        if(xrPaperMaterial!=null)Destroy(xrPaperMaterial);
        if(xrHudInk!=null)Destroy(xrHudInk);
    }
}}
