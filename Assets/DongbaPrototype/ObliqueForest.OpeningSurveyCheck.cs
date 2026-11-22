using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DongbaPrototype { public partial class ObliqueForest {
    // Existing diagnostic shortcuts enter later story beats without playing the opening.
    void SkipOpeningForVerification(){openingSurveyAge=OpeningSurveySeconds;while(fatherIntroActive)AdvanceFatherIntro();}
    const string OpeningCheckFolder="Verification/story-coherence-20261122";
    void OpeningCheck(string name,bool ok){File.AppendAllText(OpeningCheckFolder+"/runtime.txt",(ok?"PASS ":"FAIL ")+name+"\n");if(!ok)Debug.LogError("Opening check: "+name);}
    IEnumerator OpeningShot(string name){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(OpeningCheckFolder+"/"+name+".png",t.EncodeToPNG());Destroy(t);}
    IEnumerator CheckOpeningSurvey(){
        Directory.CreateDirectory(OpeningCheckFolder);File.WriteAllText(OpeningCheckFolder+"/runtime.txt","");
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keys=InputSystem.AddDevice<Keyboard>();keys.MakeCurrent();
        yield return new WaitForSeconds(1);
        Screen.SetResolution(1598,898,false);yield return new WaitForSeconds(.3f);Screen.SetResolution(1600,900,false);yield return new WaitForSeconds(.4f);
        DongbaLanguage.Set(DongbaLocale.Chinese,false);SetStoryModeEnabled(true);RestartRun();
        var capture=cam.gameObject.AddComponent<ForestAudioCapture>();capture.Begin(OpeningCheckFolder+"/opening",true,true);
        var feet=Feet;var fatherFeet=storyFatherRoot.position;float balance=Balance;
        bool rootsStill=true,dialogueHidden=true,withinPaper=true;
        InputSystem.QueueStateEvent(keys,new KeyboardState(Key.D,Key.Enter,Key.J));
        while(OpeningSurveyActive){
            rootsStill&=Vector3.Distance(feet,Feet)<.01f&&Vector3.Distance(fatherFeet,storyFatherRoot.position)<.01f;
            dialogueHidden&=forestSubtitleKey==null&&fatherIntroLine==0;
            foreach(var corner in new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(1,1,0)}){
                var ray=cam.ViewportPointToRay(corner);float d;
                if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out d)){var p=ray.GetPoint(d);withinPaper&=Mathf.Abs(p.x)<46&&Mathf.Abs(p.z)<21;}
            }
            yield return null;
        }
        InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;
        OpeningCheck("Survey holds actors / blocks attack and early Enter",rootsStill&&Balance==balance&&fatherIntroLine==0);
        OpeningCheck("No dialogue before camera settles",dialogueHidden);
        OpeningCheck("Four viewport ground corners stay on paper",withinPaper);
        OpeningCheck("Survey joins hearth camera without snap",Vector3.Distance(cam.transform.position,HomeStoryCenter+CameraOffset)<.02f&&Mathf.Abs(cam.orthographicSize-HomeStoryZoom)<.01f);
        for(int i=0;i<FatherIntroLines.Length;i++){
            yield return new WaitForSeconds(2.6f);SplitStoryLine(FatherIntroLines[i],out var speaker,out var content);
            OpeningCheck("Opening Chinese line "+i,forestSubtitles.SubtitleText.text.Contains(DongbaLanguage.Text(content)));
            yield return HoldForestAudioKey(Key.Enter,.08f);
        }
        OpeningCheck("Fourth Enter releases hunting",!fatherIntroActive&&!OpeningSurveyActive);
        yield return capture.Finish();Destroy(capture);
        // Camera bounds at narrow and wide displays; no simulation time is advanced here.
        float aspect=cam.aspect,age=openingSurveyAge;bool aspectBounds=true;
        foreach(float a in new[]{4f/3,16f/9,21f/9})for(int i=0;i<=100;i++){
            cam.aspect=a;openingSurveyAge=OpeningSurveySeconds*i/100f;OpeningSurveyPose(out var c,out var z);
            aspectBounds&=Mathf.Abs(c.x)+z*a<46&&Mathf.Abs(c.z)+z/Mathf.Abs(cam.transform.up.z)<21;
        }
        cam.aspect=aspect;openingSurveyAge=age;OpeningCheck("Survey fits 4:3 / 16:9 / 21:9 paper bounds",aspectBounds);
        // Trigger the existing ecological threshold, then exercise the real boar-pass story event.
        storyFatherRoot.position=Feet+Vector3.left;while(Balance>StoryCaptureBalanceThreshold)RegisterDamage();
        float end=Time.realtimeSinceStartup+13;while(!ManualStoryDialogueActive&&Time.realtimeSinceStartup<end)yield return null;
        OpeningCheck("Damage threshold leads to boar pass then invitation",boarPassComplete&&ManualStoryDialogueActive&&manualStoryLines==HuntInvitationLines);
        if(ManualStoryDialogueActive){
            for(int i=0;i<HuntInvitationLines.Length;i++){
                yield return new WaitForSeconds(.3f);SplitStoryLine(HuntInvitationLines[i],out var speaker,out var content);
                OpeningCheck("Boar reaction Chinese "+i,forestSubtitles.SubtitleText.text.Contains(DongbaLanguage.Text(content)));
                yield return OpeningShot("04-boar-reaction-"+i);yield return HoldForestAudioKey(Key.Enter,.08f);
            }
        }
        // Death-state fixture: combat damage itself is unchanged in this patch.
        player.position=BoarClearing+Vector3.left*2;storyFatherRoot.position=BoarClearing+Vector3.left*3;storyBoar.health=0;
        yield return new WaitForSeconds(1.5f);
        OpeningCheck("Boar death opens two-line homeward conversation",ManualStoryDialogueActive&&manualStoryLines==HomewardLines);
        yield return OpeningShot("05-homeward-zh");DongbaLanguage.Set(DongbaLocale.English,false);yield return new WaitForSeconds(.3f);yield return OpeningShot("06-homeward-en");
        SplitStoryLine(HomewardLines[0],out var unused,out var first);OpeningCheck("English switch displays current homeward line",forestSubtitles.SubtitleText.text.Contains(first));
        AdvanceManualStoryDialogue();yield return new WaitForSeconds(.3f);SplitStoryLine(HomewardLines[1],out unused,out first);OpeningCheck("Enough for days concludes the hunt",forestSubtitles.SubtitleText.text.Contains(first));
        RestartRun();OpeningCheck("R/reset restarts establishing shot and dialogue",OpeningSurveyActive&&fatherIntroLine==0&&!ManualStoryDialogueActive);
        SetStoryModeEnabled(false);RestartRun();OpeningCheck("Sandbox has no opening camera or input lock",!OpeningSurveyActive&&!fatherIntroActive);
        DongbaLanguage.Set(DongbaLocale.Chinese,false);
        var fox=new GameObject("Fox dialogue check");DontDestroyOnLoad(fox);fox.AddComponent<FoxKnowledgeCheck>();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Beat_2_Cleansing_Impurity");
    }
}}

namespace DongbaPrototype {
// Runtime subtitle verification against the scene's actual library, not a copied test string.
public sealed class FoxKnowledgeCheck:MonoBehaviour {
    static bool VisibleContains(string actual,string expected){return System.Text.RegularExpressions.Regex.Replace(actual,"<[^>]+>","").Contains(System.Text.RegularExpressions.Regex.Replace(expected,"<[^>]+>",""));}
    IEnumerator Start(){
        yield return new WaitForSeconds(1);
        const string folder="Verification/story-coherence-20261122";
        var display=DialogueDisplay.Instance;
        foreach(var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)){
            var name=component.GetType().Name;
            if(name=="StoryTripwire"||name=="StorySequencePlayer"||name=="ScrollPlayDirector"){component.StopAllCoroutines();component.enabled=false;}
        }
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var library=(DialogueLibrarySO)typeof(DialogueDisplay).GetField("m_dialogueLibrary",flags).GetValue(display);
        foreach(int i in new[]{0,3,7,11,12,14,15,16,18,21}){
            var row=library.GetEntry(i);display.PlayFromTo(i,i,false,0);yield return null;display.CompleteCurrentLine();yield return new WaitForSeconds(.2f);
            bool ok=VisibleContains(display.SubtitleText.text,row.chineseContent);
            File.AppendAllText(folder+"/runtime.txt",(ok?"PASS ":"FAIL ")+"Fox scene actual Chinese row "+i+"\n");
            if(i==7||i==14){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/fox-"+i+".png",t.EncodeToPNG());Destroy(t);}
        }
        bool knowledge=!library.GetEntry(0).content.Contains("Shu")&&!library.GetEntry(3).content.Contains("Shu")&&library.GetEntry(14).speaker=="Fox"&&library.GetEntry(14).content.Contains("took him away")&&library.GetEntry(7).chineseContent.Contains("俄高勒");
        File.AppendAllText(folder+"/runtime.txt",(knowledge?"PASS ":"FAIL ")+"Fox supplies missing knowledge / father's Chinese name corrected\n");
        DongbaLanguage.Set(DongbaLocale.English,false);var reveal=library.GetEntry(14);display.PlayFromTo(14,14,false,0);yield return null;display.CompleteCurrentLine();yield return new WaitForSeconds(.2f);
        File.AppendAllText(folder+"/runtime.txt",(VisibleContains(display.SubtitleText.text,reveal.content)?"PASS ":"FAIL ")+"Fox reveal English version\n");
        Application.Quit();
    }
}}
