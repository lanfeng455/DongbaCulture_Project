using System;using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    static readonly Vector3 HearthPosition=new Vector3(-8.5f,0,-2.2f);
    static readonly Vector3 FatherHomeStart=new Vector3(-10.05f,0,-2.65f);
    Transform storyHearth;Transform storyHome;float boarPassAge;Vector3 boarPassStart;bool boarPassComplete;
    void EnsureStoryHome(){if(storyHome==null){storyHome=Billboard("Family home / dictionary grass house PDF338",null,Get("home_glyph"),2.65f);storyHome.position=new Vector3(-8.5f,0,-.8f);storyHome.GetComponent<SpriteRenderer>().sortingOrder=60;storyHome.GetComponent<SpriteRenderer>().sharedMaterial=new Material(Resources.Load<Shader>("HouseWash"));
        var contact=Sprite.Create(soft,new Rect(0,0,64,64),new Vector2(.5f,.5f),64);var ground=Billboard("House ground contact / neutral ink wash",null,contact,2.85f,true);ground.position=storyHome.position+new Vector3(0,.025f,-.25f);var size=ground.localScale;size.y*=.52f;ground.localScale=size;ground.GetComponent<SpriteRenderer>().color=new Color(.31f,.28f,.20f,.35f);
        AddDictionaryPlant(storyHome.position+new Vector3(-1.65f,0,-.55f),false,1100,"plant_06",.38f);
        AddDictionaryPlant(storyHome.position+new Vector3(1.7f,0,-.42f),false,1101,"plant_07",.42f);}if(storyHome!=null)storyHome.gameObject.SetActive(true);
        if(storyHearth==null){storyHearth=Billboard("Family hearth / dictionary PDF312 printed240",null,Get("hearth_glyph"),.68f);storyHearth.position=HearthPosition;storyHearth.GetComponent<SpriteRenderer>().sortingOrder=100-Mathf.RoundToInt(HearthPosition.z*10);}
    }
    void UpdateBoarPass(float dt){
        boarPassAge+=dt;storyBoar.root.position=Vector3.MoveTowards(storyBoar.root.position,BoarClearing,13*dt);
        float x=storyBoar.root.position.x,river=RiverX(storyBoar.root.position.z);float jump=Mathf.Abs(x-river)<1.5f?Mathf.Sin(Mathf.Clamp01((x-river+1.5f)/3)*Mathf.PI)*1.15f:0;
        storyBoar.art.localPosition=Vector3.up*(jump+.035f*Mathf.Sin(boarPassAge*24));storyBoar.renderer.flipX=false;storyBoar.renderer.sortingOrder=100-Mathf.RoundToInt(storyBoar.root.position.z*10);
        if(boarPassAge>1.1f&&!boarPassComplete&&Array.IndexOf(Environment.GetCommandLineArgs(),"--story-journey-check")>=0&&!System.IO.File.Exists("Verification/story-journey/boar-pass.png"))CaptureSceneToTexture("Verification/story-journey/boar-pass.png");
        if(Vector3.Distance(storyBoar.root.position,BoarClearing)<.1f){storyBoar.art.localPosition=Vector3.zero;boarPassComplete=true;journeyStage=JourneyStage.Invitation;Tell("Father: Did you see that boar? Come here a moment.");}
    }
}}
