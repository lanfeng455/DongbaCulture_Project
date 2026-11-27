using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    CanvasGroup natureMenuFade;
    float natureMenuOpenedAt=-100;

    void AnimateNatureMenu(bool showing){
        if(!showing||!NatureEditorOpen)return;
        float reveal=Mathf.SmoothStep(0,1,(Time.unscaledTime-natureMenuOpenedAt)/.24f);
        natureMenuFade.alpha=reveal;
        naturePanel.anchoredPosition+=new Vector2(XRTrackingControlsCamera?0:18,-10)*(1-reveal);
        naturePanel.localScale*=Mathf.Lerp(.985f,1,reveal);
    }
    void PrepareNaturePresentation(NatureMotion m){
        var b=m.being;
        if(b.leafImages!=null){
            m.leafPositions=new Vector3[b.leafImages.Length];m.leafScales=new Vector3[b.leafImages.Length];
            for(int i=0;i<b.leafImages.Length;i++){m.leafPositions[i]=b.leafImages[i].transform.localPosition;m.leafScales[i]=b.leafImages[i].transform.localScale;}
        }
    }
    void RestoreNaturePresentation(NatureMotion m){
        var b=m.being;b.art.localPosition=Vector3.zero;b.art.rotation=billboard;
        if(b.shadow!=null)b.shadow.localScale=b.initialShadowScale;
        if(m.leafPositions!=null)for(int i=0;i<b.leafImages.Length;i++){
            b.leafImages[i].transform.localPosition=m.leafPositions[i];b.leafImages[i].transform.localScale=m.leafScales[i];
        }
    }
    void NatureAnimalAcknowledgement(Being b,float age,float duration){
        float u=Mathf.Clamp01(age/duration),beat=Mathf.Sin(u*Mathf.PI);
        b.art.localPosition=Vector3.up*(.045f*beat);
        b.art.rotation=billboard*Quaternion.Euler(0,0,beat*(b.renderer.flipX?2.5f:-2.5f));
        b.renderer.sortingOrder=DepthOrder(b.root.position.z);
    }
    bool TickNaturePlant(NatureMotion m){
        var b=m.being;bool moving=m.action==NatureAction.Move,sleeping=m.action==NatureAction.Sleep;
        float duration=moving?1.28f:sleeping?1.20f:1.65f,u=Mathf.Clamp01(m.age/duration);
        float unfold=1,sway=0;
        if(moving){
            // Acknowledge at the original roots, lift, travel, then settle at the new roots.
            float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.14f,.87f,u));
            b.root.position=Vector3.Lerp(m.from,m.to,travel)+Vector3.up*(Mathf.Sin(travel*Mathf.PI)*.42f);
            b.art.localScale=m.scale*(1-.055f*Mathf.Sin(travel*Mathf.PI));
            sway=Mathf.Sin(u*Mathf.PI*3)*Mathf.Sin(u*Mathf.PI)*3.2f;
            if(b.shadow!=null)b.shadow.localScale=b.initialShadowScale*(1-.35f*Mathf.Sin(travel*Mathf.PI));
        }else if(sleeping){
            unfold=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.74f,u));
            b.art.localScale=m.scale*unfold;sway=Mathf.Sin(u*Mathf.PI*2)*2.4f;
        }else{
            // The seed is seen landing before the glyph opens. Uniform scale keeps its original aspect.
            unfold=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.87f,u));
            float settle=u>.87f?Mathf.Sin((u-.87f)/.13f*Mathf.PI)*.025f:0;
            b.art.localScale=m.scale*(unfold+settle);sway=Mathf.Sin(u*Mathf.PI*3)*Mathf.Sin(u*Mathf.PI)*1.8f;
        }
        b.art.rotation=billboard*Quaternion.Euler(0,0,sway);b.renderer.sortingOrder=DepthOrder(b.root.position.z);
        if(m.leafPositions!=null){
            var glyph=treeGlyphSets[b.glyphKey];
            for(int i=0;i<b.leafImages.Length;i++){
                var image=b.leafImages[i];float amount=moving?1:sleeping?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f+i*.025f,.55f+i*.025f,u)):Mathf.SmoothStep(0,1,Mathf.InverseLerp(.36f+i*.035f,.70f+i*.035f,u));
                Vector3 anchor=glyph.pieceOffsets[i];image.transform.localScale=m.leafScales[i]*amount;
                image.transform.localPosition=Vector3.Lerp(anchor,m.leafPositions[i],amount);image.sortingOrder=b.renderer.sortingOrder+i+1;
            }
        }
        if(m.mark!=null){
            var mark=m.mark;bool arriving=!moving&&!sleeping;
            Vector3 position=b.root.position;
            float seedAlpha=0,windAlpha=0;
            if(arriving){
                float flight=Mathf.SmoothStep(0,1,Mathf.Clamp01(u/.36f));
                Vector3 from=m.to+new Vector3(-4.4f,1.7f,.7f);
                position=Vector3.Lerp(from,m.to,flight)+Vector3.up*(Mathf.Sin(flight*Mathf.PI)*.50f);
                seedAlpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.53f,u));windAlpha=.26f*Mathf.Sin(Mathf.Clamp01(u/.47f)*Mathf.PI);
            }else if(sleeping){
                float lift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.69f,1,u));
                position=m.from+new Vector3(-.8f*lift,1.05f*lift,.12f*lift);
                seedAlpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.47f,.70f,u))*(1-lift);windAlpha=.20f*Mathf.Sin(lift*Mathf.PI);
            }else{windAlpha=.14f*Mathf.Sin(u*Mathf.PI);}
            mark.root.position=position;mark.seed.enabled=!moving;mark.seed.color=new Color(.28f,.20f,.10f,seedAlpha);
            mark.seed.transform.rotation=billboard;mark.seed.sortingOrder=b.renderer.sortingOrder+9;
            mark.wind.enabled=true;mark.wind.transform.rotation=billboard;mark.wind.color=new Color(1,1,1,windAlpha);
            mark.wind.transform.localPosition=new Vector3(-.55f,.14f,0);mark.wind.sortingOrder=b.renderer.sortingOrder+8;
        }
        return u>=1;
    }
}}
