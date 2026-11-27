using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator VerifyInvitationExpansion(Action<bool,string> check){
        StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        player.position=NatureDemoFooting();lastDryPosition=Feet;FollowPlayerCamera(100);careCount=FlourishThreshold;
        yield return null;yield return null;UpdateNatureUnlock();DeferNatureWelcome();natureFirstComplete=true;
        int ecology=Balance,initialAnimals=animals.Count;
        check(naturePlantKeys.Count==17&&AnimalKinds==18,"Library: 17 plant and 18 animal choices");
        check(treeGlyphSets["plant_09"].pieceOffsets[5]==Vector3.zero&&treeGlyphSets["plant_10"].pieceOffsets[3]==Vector3.zero,"Library: empty leaf slots cannot become floating fruit anchors");
        check(animals.All(a=>a.speciesIndex<InitialAnimalKinds),"Library: original map species and layout unchanged");
        for(int index=9;index<17;index++){
            string key="plant_"+index.ToString("00");bool tree=NatureTreeKey(key);
            var target=NatureTestSpot(Feet+new Vector3(-3,0,0),null,false,tree);int before=plants.Count;
            bool created=NatureRequest(NatureAction.Plant,null,key,0,target);check(created,"Library: invite "+key+" / "+dictionaryLabels[key]);if(!created){Debug.LogError("Invitation rejected: available="+NatureAvailable+" balance="+Balance+" count="+plants.Count+" initial="+initialPlantCount+" reason="+natureMessage);continue;}
            var b=plants.Last();yield return new WaitForSeconds(.18f);
            check(b.editBusy&&b.art.localScale.sqrMagnitude<.0001f&&natureMotions.Last().mark.seed.color.a>.8f,"Invitation: seed precedes glyph "+key);
            yield return NatureWait(b);
            check(!b.editBusy&&plants.Count==before+1&&b.fullGlyph==dictionarySprites[key]&&Vector3.Distance(b.home,target)<.01f,"Library: original glyph rooted "+key);
            check(b.tree==tree&&b.art.localScale==b.initialScale*b.targetScale,"Library: type and aspect preserved "+key);
            if(tree){
                check(treeGlyphSets.ContainsKey(key)&&breakArt.ContainsKey(key)&&b.leafImages.All(l=>l.sprite!=null),"Library: new tree has foliage and storm layers "+key);
                b.cultivated=true;b.growth=1;b.targetScale=.8f;
                check(NatureRequest(NatureAction.Sleep,b,null,0,b.home),"Invitation: sleep new tree "+key);yield return NatureWait(b);
                check(NatureRequest(NatureAction.Restore,b,null,0,target),"Invitation: awaken new tree "+key);yield return NatureWait(b);
                check(b.cultivated&&b.growth==1&&b.leafImages.All(l=>l.transform.localScale==Vector3.one),"Invitation: new tree retains care and complete original leaves "+key);
                NatureForget(b);natureHistory.Clear();
            }else{check(UndoNature(),"Library: undo new crop "+key);yield return NatureWait(b);}
        }
        for(int kind=12;kind<18;kind++){
            var target=NatureTestSpot(new Vector3(-26,0,-17.5f),null,true,false);
            bool accepted=NatureRequest(NatureAction.Invite,null,null,kind,target);check(accepted,"Library: call new animal "+kind);if(!accepted)continue;
            var b=animals.Last();
            check(b.renderer.color.a==0&&Vector3.Distance(b.root.position,target)>3,"Invitation: no spawn at target "+kind);
            yield return NatureWait(b,40);
            check(!b.editBusy&&b.speciesIndex==kind&&Vector3.Distance(b.home,target)<.01f,"Invitation: new animal walks in and acknowledges "+kind);
            DongbaLanguage.Set(DongbaLocale.Chinese,false);check(AnimalDisplayName(b)==dictionaryLabels["animal_"+kind.ToString("00")],"Library: Chinese name matches original animal "+kind);
            NatureForget(b);natureHistory.Clear();
        }
        check(animals.Count==initialAnimals&&Balance==ecology,"Invitation: added variety does not change ecology or duplicate companions");
        ToggleNatureEditor();NatureGo(NaturePage.Plants);naturePageIndex=2;natureUIStamp="";yield return new WaitForSeconds(.3f);
        check(natureButtons.Count>=8&&natureDetail.text.Contains("17"),"Library: third plant page is reachable");
        Directory.CreateDirectory("Verification/nature-expansion");
        if(!XRTrackingControlsCamera)DongbaLanguageCapture.Write(cam,"Verification/nature-expansion/catalog-plants.png");
        NatureGo(NaturePage.Animals);naturePageIndex=2;natureUIStamp="";yield return null;yield return null;
        check(natureButtons[0].GetComponentInChildren<UnityEngine.UI.Text>().text=="马","Library: new animal page begins with Horse");
        if(!XRTrackingControlsCamera)DongbaLanguageCapture.Write(cam,"Verification/nature-expansion/catalog-animals.png");
        else CaptureSceneToTexture("Verification/nature-expansion/catalog-mr.png");
        StartForestMode(true);yield return null;
        check(!NatureAvailable&&!NatureEditorOpen&&ForestDamageAvailable&&!ForestCareAvailable,"Invitation: story progression unchanged");
        StartForestMode(false);yield return null;
    }
    IEnumerator PreviewInvitationExpansion(){
        yield return new WaitForSeconds(1);StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        player.position=NatureDemoFooting();lastDryPosition=Feet;FollowPlayerCamera(100);careCount=FlourishThreshold;yield return null;yield return null;UpdateNatureUnlock();DeferNatureWelcome();natureFirstComplete=true;
        DongbaLanguage.Set(DongbaLocale.Chinese,false);
        string folder="Verification/nature-expansion/frames";Directory.CreateDirectory(folder);
        int rate=Time.captureFramerate;Time.captureFramerate=30;var capture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);Being tree=null;
        try{for(int frame=0;frame<900;frame++){
            if(frame==15){ToggleNatureEditor();NatureGo(NaturePage.Plants);naturePageIndex=1;natureUIStamp="";}
            if(frame==75){var p=NatureTestSpot(Feet+Vector3.left*3,null,false,true);NatureRequest(NatureAction.Plant,null,"plant_09",0,p);tree=plants.Last();CloseNatureEditor();}
            if(frame==165){var p=NatureTestSpot(Feet+new Vector3(1.4f,0,-.4f),null,false,false);NatureRequest(NatureAction.Plant,null,"plant_11",0,p);}
            if(frame==225){var p=NatureTestSpot(Feet+new Vector3(2,0,-1.5f),null,false,false);NatureRequest(NatureAction.Plant,null,"plant_12",0,p);}
            if(frame==285){var p=NatureTestSpot(Feet+new Vector3(-3,0,1),tree,false,true);NatureRequest(NatureAction.Move,tree,null,0,p);}
            if(frame==350){var p=NatureTestSpot(Feet+new Vector3(2,0,-2),null,true,false);NatureRequest(NatureAction.Invite,null,null,13,p);}
            if(frame==410){NatureRequest(NatureAction.Sleep,tree,null,0,tree.home);}
            if(frame==480){var p=NatureTestSpot(Feet+Vector3.left*3,tree,false,true);NatureRequest(NatureAction.Restore,tree,null,0,p);}
            if(frame==740){ToggleNatureEditor();NatureGo(NaturePage.Animals);naturePageIndex=2;natureUIStamp="";}
            yield return null;yield return new WaitForEndOfFrame();capture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);capture.Apply(false);File.WriteAllBytes(folder+"/"+frame.ToString("D4")+".jpg",capture.EncodeToJPG(88));
        }}finally{Time.captureFramerate=rate;Destroy(capture);}Application.Quit();
    }
}}
