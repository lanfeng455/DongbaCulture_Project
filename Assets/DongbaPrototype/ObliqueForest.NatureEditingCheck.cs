using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DongbaPrototype { public partial class ObliqueForest {
    Vector3 NatureTestSpot(Vector3 origin,Being b,bool animal,bool tree){
        for(int i=0;i<500;i++){float a=i*2.39996f,r=.8f+(i/16)*.35f;var p=origin+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);if(NaturePlacement(p,b,animal,tree,out _))return p;}
        throw new Exception("No legal nature fixture near "+origin);
    }
    Vector3 NatureDemoFooting(){
        Vector3 best=ForestSpawn;float score=float.MaxValue;
        for(float z=-15;z<2;z+=1)for(float x=-29;x<-8;x+=1){
            var p=new Vector3(x,0,z);if(!Free(p)||River(p))continue;
            bool clear=true;foreach(var tree in plants)if(tree.tree&&Vector3.Distance(p,tree.home)<3.6f){clear=false;break;}
            if(!clear)continue;float s=Vector3.Distance(p,new Vector3(-20,0,-12));if(s<score){score=s;best=p;}
        }
        return best;
    }
    IEnumerator NatureWait(Being b,float timeout=35){float elapsed=0;while(b!=null&&b.editBusy&&elapsed<timeout){elapsed+=Time.deltaTime;yield return null;}}
    IEnumerator NatureKey(params Key[] keys){
        var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return null;yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
    }
    IEnumerator NatureClick(Vector2 position){
        var mouse=Mouse.current;if(mouse==null)mouse=InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=position}.WithButton(MouseButton.Left));yield return null;yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;
    }
    IEnumerator NatureClickButton(int index){var rect=(RectTransform)natureButtons[index].transform;yield return NatureClick(RectTransformUtility.WorldToScreenPoint(forestCanvas.worldCamera,rect.TransformPoint(rect.rect.center)));}
    IEnumerator NatureDesktopInputCheck(Action<bool,string> check){
        CloseNatureEditor();player.position=NatureDemoFooting();lastDryPosition=Feet;FollowPlayerCamera(100);yield return null;
        yield return NatureKey(Key.B);check(NatureEditorOpen,"Nature PC: real B input opens editor");
        yield return NatureClickButton(0);check(naturePage==NaturePage.Plants,"Nature PC: mouse clicks plant catalog button");
        yield return NatureClickButton(1);check(naturePage==NaturePage.Position&&natureKey=="plant_01","Nature PC: mouse chooses Cypress glyph");
        Vector3 spot=NatureTestSpot(Feet+Vector3.left*3,null,false,true);int count=plants.Count;
        yield return NatureClick(cam.WorldToScreenPoint(spot));
        check(plants.Count==count+1,"Nature PC: ground click confirms preview once");
        var tree=plants.Last();yield return NatureWait(tree);
        yield return NatureKey(Key.LeftCtrl,Key.Z);yield return NatureWait(tree);
        check(plants.Count==count,"Nature PC: Ctrl+Z undoes the actual mouse addition");
        NatureGo(NaturePage.Plants);yield return null;yield return NatureClickButton(0);
        InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=cam.WorldToScreenPoint(spot)});yield return null;yield return null;yield return null;
        Vector3 chosen=natureCandidate;yield return NatureClickButton(0);var confirmed=plants.Last();yield return NatureWait(confirmed);
        check(plants.Count==count+1&&Vector3.Distance(confirmed.home,chosen)<.05f,"Nature PC: menu Confirm retains the ground preview instead of placing under UI");
        yield return NatureKey(Key.LeftCtrl,Key.Z);yield return NatureWait(confirmed);
        yield return NatureKey(Key.Escape);check(!NatureEditorOpen&&!DongbaInterface.GameplayBlocked,"Nature PC: Escape closes editor before opening pause");
        yield return NatureKey(Key.B);var foot=Feet;yield return NatureKey(Key.A);check(Vector3.Distance(Feet,foot)>.01f,"Nature PC: movement remains available with editor open");
        float attack=attackAge;int ecology=Balance;yield return NatureKey(Key.J,Key.E);check(attackAge==attack&&Balance==ecology,"Nature PC: axe and care do not leak through editor");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);NatureGo(NaturePage.Plants);yield return null;yield return null;
        check(natureTitle.text.Contains("植物")&&natureButtons[0].GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("松"),"Nature PC: Chinese labels use the same dictionary objects");
        Directory.CreateDirectory("Verification/nature-edit");DongbaLanguageCapture.Write(cam,"Verification/nature-edit/catalog-zh.png");
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;yield return null;DongbaLanguageCapture.Write(cam,"Verification/nature-edit/catalog-en.png");
        CloseNatureEditor();
        int frames=0;float elapsed=0;while(elapsed<2){yield return null;frames++;elapsed+=Time.unscaledDeltaTime;}
        Debug.Log("Nature normal-play FPS: "+(frames/elapsed).ToString("F1"));check(frames/elapsed>45,"Nature PC: normal-play update rate above 45 FPS");
    }
    IEnumerator VerifyNatureEditing(Action<bool,string> check){
        StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        yield return null;yield return null;
        check(!natureUnlocked&&!StoryModeEnabled,"Nature: fresh sandbox starts locked");
        int beforePlants=plants.Count;
        check(!NatureRequest(NatureAction.Plant,null,"plant_00",0,new Vector3(-18,0,0))&&plants.Count==beforePlants,"Nature: locked requests cannot create objects");
        // Actual existing care methods, not setting the unlock flag or awarding a new currency.
        foreach(var tree in plants.ToArray()){if(tree.tree&&!tree.cultivated)GrowAround(tree);if(Balance>=FlourishThreshold)break;}
        yield return null;
        check(natureUnlocked&&careCount>=FlourishThreshold,"Nature: existing cultivation unlocks on first flourishing");
        int care=careCount,damage=damageCount;damageCount=careCount+1;yield return null;check(natureUnlocked,"Nature: unlock survives later decline");damageCount=damage;
        ToggleNatureEditor();yield return null;
        check(NatureEditorOpen&&naturePanel.gameObject.activeSelf,"Nature: compact invitation UI opens in sandbox");
        NatureGo(NaturePage.Plants);yield return null;
        check(natureButtons.Count>=6&&dictionarySprites.Keys.Count(k=>k.StartsWith("plant_"))>=9,"Nature: existing tree and small-plant catalog is available");
        var treeA=plants.First(b=>b.tree&&b.cultivated);Vector3 home=treeA.home;int growth=treeA.growth;float scale=treeA.targetScale;var glyph=treeA.fullGlyph;
        Vector3 spot=NatureTestSpot(home+Vector3.right*3,treeA,false,true);
        check(NatureRequest(NatureAction.Move,treeA,null,0,spot),"Nature: plant relocation accepted");
        check(!Free(spot)&&!CircleStepClear(spot,animals[0]),"Nature: moving tree destination is reserved against player and animal entry");
        check(!NatureRequest(NatureAction.Plant,null,"plant_01",0,spot),"Nature: concurrent invitations cannot claim the same location");yield return NatureWait(treeA);
        check(treeA.home==spot&&treeA.fullGlyph==glyph&&treeA.cultivated&&treeA.growth==growth&&Mathf.Approximately(treeA.targetScale,scale),"Nature: relocation preserves identity, glyph, growth and care");
        check(!NatureRequest(NatureAction.Move,treeA,null,0,new Vector3(RiverX(0),0,0)),"Nature: river placement rejected");
        check(!NatureRequest(NatureAction.Move,treeA,null,0,new Vector3(100,0,0)),"Nature: outside forest placement rejected");
        check(!NatureRequest(NatureAction.Move,treeA,null,0,Feet),"Nature: cannot plant on the player");
        check(UndoNature(),"Nature: relocation undo accepted");yield return NatureWait(treeA);
        check(treeA.home==home,"Nature: undo restores original plant location");
        check(NatureRequest(NatureAction.Sleep,treeA,null,0,home),"Nature: sleep accepted");yield return NatureWait(treeA);
        check(sleepingPlants.Contains(treeA)&&!plants.Contains(treeA)&&!treeA.root.gameObject.activeSelf&&treeA.health>0,"Nature: sleep removes active footprint without stump or damage");
        check(NatureRequest(NatureAction.Restore,treeA,null,0,spot),"Nature: dormant plant restores at new position");yield return NatureWait(treeA);
        check(plants.Count(b=>b==treeA)==1&&!sleepingPlants.Contains(treeA)&&treeA.cultivated&&treeA.growth==growth,"Nature: restore retains reward flags and has no duplicate");
        treeA.careReady=0;GrowAround(treeA);check(careCount==care,"Nature: sleep and restore cannot repeat the cultivation reward");
        foreach(string key in new[]{"plant_00","plant_01","plant_02","plant_07"}){
            var p=NatureTestSpot(Feet+new Vector3(4,0,2),null,false,key!="plant_07");
            int count=plants.Count;check(NatureRequest(NatureAction.Plant,null,key,0,p),"Nature: invite "+key+" independently of mother plants");
            var added=plants.Last();yield return NatureWait(added);check(plants.Count==count+1&&added.glyphKey==key,"Nature: seed unfolds to requested original glyph "+key);
            check(UndoNature(),"Nature: undo new "+key);yield return NatureWait(added);
            check(plants.Count==count&&!sleepingPlants.Contains(added),"Nature: undo addition leaves no extra dormant duplicate "+key);
        }
        var animal=animals.First(a=>a.health>0);animal.home=animal.root.position=NatureTestSpot(new Vector3(-18,0,-16),animal,true,false);
        animal.age=1;animal.birth=.7f;animal.careReady=clock+3;animal.protectedUntil=clock+12;
        animal.populationRegion=PopulationRegion(animal.home.x);int id=NatureIdentity(animal);var animalStart=animal.home;
        Vector3 area=NatureTestSpot(animalStart+Vector3.right*2,animal,true,false);
        check(NatureRequest(NatureAction.Range,animal,null,0,area),"Nature: animal activity area request accepted");yield return NatureWait(animal);
        check(!animal.editBusy&&Vector3.Distance(animal.home,area)<.01f&&animal.age==1&&NatureIdentity(animal)==id,"Nature: animal walks to its area preserving juvenile identity");
        check(!NatureRequest(NatureAction.Range,animal,null,0,new Vector3(RiverX(0),0,0)),"Nature: animal activity area cannot occupy river");
        check(NatureRequest(NatureAction.Leave,animal,null,0,area),"Nature: return-to-forest begins with a real path");
        yield return new WaitForSeconds(.3f);check(animal.root.gameObject.activeSelf&&animal.editBusy&&animals.Contains(animal),"Nature: departing animal stays alive and visible while walking");
        yield return NatureWait(animal);
        check(forestCompanions.Contains(animal)&&!animals.Contains(animal)&&!animal.root.gameObject.activeSelf,"Nature: archive only after reaching the forest perimeter");
        for(int i=0;i<8;i++){populationCheckAt=0;UpdatePopulation(.5f);yield return null;}
        check(!animals.Contains(animal)&&forestCompanions.Contains(animal),"Nature: ordinary ecological returns never recall manually archived companions");
        check(NatureRequest(NatureAction.Recall,animal,null,0,area),"Nature: recall same companion accepted");
        check(Vector3.Distance(animal.root.position,area)>3&&animal.renderer.color.a<.01f,"Nature: recall starts beyond forest boundary, not at target");yield return NatureWait(animal);
        check(!animal.editBusy&&animal.age==1&&NatureIdentity(animal)==id&&animals.Count(a=>a==animal)==1,"Nature: recalled individual retains growth and appears once");
        int oldCount=animals.Count;var newArea=NatureTestSpot(area+Vector3.left*3,null,true,false);
        check(NatureRequest(NatureAction.Invite,null,null,4,newArea),"Nature: new species invitation accepted");var newAnimal=animals.Last();
        check(newAnimal.speciesIndex==4&&Vector3.Distance(newAnimal.root.position,newArea)>3&&newAnimal.renderer.color.a==0,"Nature: new animal also starts at an entrance");yield return NatureWait(newAnimal);
        check(animals.Count==oldCount+1&&!newAnimal.editBusy&&newAnimal.editResident,"Nature: new animal reaches its autonomous living area");
        check(UndoNature(),"Nature: new animal can be undone through departure");yield return NatureWait(newAnimal);
        check(animals.Count==oldCount&&!forestCompanions.Contains(newAnimal),"Nature: undo new animal does not create an archived duplicate");
        check(NatureRequest(NatureAction.Invite,null,null,4,newArea),"Nature: another animal invitation can begin");var interrupted=animals.Last();
        yield return new WaitForSeconds(.65f);float alpha=interrupted.renderer.color.a;
        check(UndoNature()&&interrupted.root.gameObject.activeSelf,"Nature: undo in-flight arrival reverses boundary motion without a visible deletion");
        yield return NatureWait(interrupted);check(!animals.Contains(interrupted),"Nature: interrupted invitation leaves no active extra animal");
        for(int i=0;i<3;i++){
            check(NatureRequest(NatureAction.Sleep,treeA,null,0,treeA.home),"Nature: repeated sleep "+i);yield return NatureWait(treeA);
            check(UndoNature(),"Nature: undo sleep "+i);yield return NatureWait(treeA);
        }
        NatureChoose(NatureAction.Plant,null,"plant_02");int countBeforeCancel=plants.Count;CancelNatureEditor();
        check(plants.Count==countBeforeCancel&&naturePage==NaturePage.Home,"Nature: cancel preview creates nothing");
        check(careCount==care&&damageCount==damage,"Nature: all composition operations leave ecology and damage unchanged");
        check(animals.Distinct().Count()==animals.Count&&plants.Distinct().Count()==plants.Count,"Nature: repeated operations never duplicate active object references");
        tonePreviewFreezeAnimals=false;CloseNatureEditor();var roamStart=animal.root.position;yield return new WaitForSeconds(5);
        check(animal.editResident&&animal.follow==0&&Vector3.Distance(animal.root.position,animal.home)<4,"Nature: assigned companion remains autonomous near its chosen region");
        if(!XRTrackingControlsCamera)yield return NatureDesktopInputCheck(check);
        if(XRTrackingControlsCamera){
            var aim=new Ray(animal.home+Vector3.up*30,Vector3.down);
            ProcessNatureXR(false,false,false,false,Vector2.zero,aim,true);ProcessNatureXR(true,false,false,false,Vector2.zero,aim,true);UpdateNatureUI();
            check(NatureEditorOpen,"Nature MR: left stick click opens invitation panel");
            Directory.CreateDirectory("Verification/nature-edit");CaptureSceneToTexture("Verification/nature-edit/mr-invitation.png");
            ProcessNatureXR(true,false,false,false,Vector2.zero,aim,true);check(NatureEditorOpen,"Nature MR: held toggle does not flicker");
            float attackBefore=attackAge;ProcessXRInput(Vector2.zero,true,true,true,false,false,false);
            check(attackAge==attackBefore&&careCount==care,"Nature MR: editing owns A, X and trigger without attack or feed leakage");
            ProcessNatureXR(false,true,false,false,Vector2.zero,aim,true);check(!NatureEditorOpen,"Nature MR: B closes root menu");
        }
        StartForestMode(true);yield return null;
        check(!NatureEditorOpen&&!natureUnlocked&&sleepingPlants.Count==0&&forestCompanions.Count==0&&natureMotions.Count==0,"Nature: mode switch resets composition state");
        check(plants.Count==initialPlantCount&&animals.Count==initialAnimalCount&&natureInitialPlants.All(b=>b.home==natureOriginalHomes[b]),"Nature: restart restores original map and objects after editing");
        check(!ForestCareAvailable&&ForestDamageAvailable&&!NatureRequest(NatureAction.Plant,null,"plant_00",0,spot),"Nature: story hunting restrictions and no-edit rule preserved");
        StartForestMode(false);yield return null;
    }
    IEnumerator PlayNatureEditing(){
        yield return new WaitForSeconds(1);StartForestMode(false);DongbaInterface.Instance?.Close(false);
        player.position=NatureDemoFooting();lastDryPosition=Feet;FollowPlayerCamera(100);
        foreach(var tree in plants.ToArray()){if(tree.tree&&!tree.cultivated)GrowAround(tree);if(Balance>=FlourishThreshold)break;}
        yield return null;ToggleNatureEditor();
    }
    IEnumerator CheckNatureEditing(){
        yield return new WaitForSeconds(1);var log=new StringBuilder();bool pass=true;
        yield return VerifyNatureEditing((ok,message)=>{pass&=ok;log.AppendLine((ok?"PASS ":"FAIL ")+message);Debug.Log((ok?"PASS ":"FAIL ")+message);});
        Directory.CreateDirectory("Verification/nature-edit");File.WriteAllText("Verification/nature-edit/checks.txt",log.ToString());Application.Quit(pass?0:1);
    }
    IEnumerator PreviewNatureEditing(){
        yield return new WaitForSeconds(1);StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        player.position=NatureDemoFooting();lastDryPosition=Feet;FollowPlayerCamera(100);
        foreach(var tree in plants.ToArray()){if(tree.tree&&!tree.cultivated)GrowAround(tree);if(Balance>=FlourishThreshold)break;}
        yield return new WaitForSeconds(1);ToggleNatureEditor();NatureGo(NaturePage.Plants);
        string folder="Verification/nature-edit";Directory.CreateDirectory(folder+"/frames");
        int savedRate=Time.captureFramerate;Time.captureFramerate=30;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);Being added=null,companion=null;Vector3 destination=Vector3.zero;
        try{
            for(int frame=0;frame<960;frame++){
                if(frame==60){destination=NatureTestSpot(Feet+new Vector3(3,0,1),null,false,true);NatureChoose(NatureAction.Plant,null,"plant_01");natureCandidate=destination;natureHasGround=true;if(Mouse.current!=null)InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=cam.WorldToScreenPoint(destination)});}
                if(frame==110){NatureRequest(NatureAction.Plant,null,"plant_01",0,destination);added=plants.Last();NatureGo(NaturePage.Home);}
                if(frame==195){NatureSelect(added);}
                if(frame==225){destination=NatureTestSpot(Feet+new Vector3(-3,0,1),added,false,true);NatureRequest(NatureAction.Move,added,null,0,destination);NatureGo(NaturePage.Home);}
                if(frame==315){NatureRequest(NatureAction.Sleep,added,null,0,destination);NatureGo(NaturePage.Sleeping);}
                if(frame==400){NatureRequest(NatureAction.Restore,added,null,0,destination);NatureGo(NaturePage.Home);}
                if(frame==490){destination=NatureTestSpot(Feet+new Vector3(2,0,-2),null,true,false);NatureRequest(NatureAction.Invite,null,null,4,destination);companion=animals.Last();NatureGo(NaturePage.Home);}
                if(frame==750&&companion!=null&&!companion.editBusy){NatureSelect(companion);}
                if(frame==780&&companion!=null&&!companion.editBusy){NatureRequest(NatureAction.Leave,companion,null,0,companion.home);NatureGo(NaturePage.Companions);}
                yield return null;UpdateNatureUI();yield return new WaitForEndOfFrame();
                if(frame==30||frame==90||frame==210||frame==380||frame==700)DongbaLanguageCapture.Write(cam,folder+"/preview-"+frame+".png");
                texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply(false);File.WriteAllBytes(folder+"/frames/"+frame.ToString("D4")+".jpg",texture.EncodeToJPG(88));
            }
        }finally{Time.captureFramerate=savedRate;Destroy(texture);}
        Application.Quit();
    }
}}
