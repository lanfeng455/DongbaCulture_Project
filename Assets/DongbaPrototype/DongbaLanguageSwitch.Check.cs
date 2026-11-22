using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DongbaPrototype {
public sealed partial class DongbaLanguageSwitch {
    IEnumerator CheckLanguagePresentation(){
        const string folder="Verification/language-presentation";Directory.CreateDirectory(folder);
        string report=folder+"/checks.txt";File.WriteAllText(report,"");bool passed=true;
        Action<string,bool> check=(name,ok)=>{passed&=ok;File.AppendAllText(report,name+"="+ok+"\n");};
        bool hadPreference=PlayerPrefs.HasKey(DongbaLanguage.PreferenceKey);int preference=PlayerPrefs.GetInt(DongbaLanguage.PreferenceKey,0);
        yield return new WaitForSeconds(1);
        check("Title language choice is available",SceneManager.GetActiveScene().name=="Title"&&SwitchCanvas.enabled);
        Button start=null;
        foreach(var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            for(int i=0;i<button.onClick.GetPersistentEventCount();i++)if(button.onClick.GetPersistentTarget(i) is InkBloomTransition)start=button;
        check("Real start button is bound to ink transition",start!=null);
        if(start==null){Application.Quit(1);yield break;}
        start.onClick.Invoke();check("Start hides control immediately",!SwitchCanvas.enabled);
        bool hidden=true,captured=false;float age=0;int frames=0;
        while(age<30){
            yield return new WaitForEndOfFrame();age+=Time.unscaledDeltaTime;frames++;hidden&=!SwitchCanvas.enabled;
            if(!captured&&age>.25f&&Camera.main!=null){DongbaLanguageCapture.Write(Camera.main,folder+"/ink-transition.png");captured=true;}
            if(FindFirstObjectByType<InkBloomOverlay>()==null&&FindFirstObjectByType<ObliqueForest>()!=null)break;
        }
        check("Control stays hidden through cover load and reveal",hidden&&age<30&&frames>2);
        yield return new WaitForSeconds(.4f);
        check("Opening dialogue keeps language button hidden",!SwitchCanvas.enabled&&SceneManager.GetActiveScene().name=="ObliqueForest");
        DongbaLanguageCapture.Write(Camera.main,folder+"/opening.png");
        var prior=DongbaLanguage.Current;
        var mouse=Mouse.current;if(mouse==null)mouse=InputSystem.AddDevice<Mouse>();
        var point=RectTransformUtility.WorldToScreenPoint(null,HitRect.TransformPoint(HitRect.rect.center));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return null;
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
        check("Hidden control cannot receive mouse input",DongbaLanguage.Current==prior);
        yield return PresentationKey(Key.F8);check("F8 can still change subtitle language",DongbaLanguage.Current!=prior);
        yield return PresentationKey(Key.F8);
        for(int i=0;i<3;i++)yield return PresentationKey(Key.Enter);
        yield return new WaitForSeconds(.4f);check("Free play restores language button",SwitchCanvas.enabled);
        DongbaLanguageCapture.Write(Camera.main,folder+"/free-play.png");
        yield return PresentationKey(Key.R);yield return new WaitForEndOfFrame();check("Restart hides it again for opening",!SwitchCanvas.enabled);
        yield return PresentationKey(Key.F5);yield return new WaitForEndOfFrame();check("Sandbox restores it",SwitchCanvas.enabled);
        // Destruction/disable of an interrupted presenter must not leave the persistent UI stuck.
        var blocker=new GameObject("Test presentation owner").AddComponent<Canvas>();
        SetPresentationBlocked(blocker,true);check("Temporary presenter blocks control",!SwitchCanvas.enabled);
        Destroy(blocker.gameObject);yield return null;yield return new WaitForEndOfFrame();check("Destroyed presenter releases control",SwitchCanvas.enabled);
        File.AppendAllText(report,"Start uses the real button callback; other actions use keyboard and mouse input.\n");
        if(hadPreference)PlayerPrefs.SetInt(DongbaLanguage.PreferenceKey,preference);else PlayerPrefs.DeleteKey(DongbaLanguage.PreferenceKey);PlayerPrefs.Save();
        Application.Quit(passed?0:1);
    }
    static IEnumerator PresentationKey(Key key){
        var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return new WaitForEndOfFrame();
    }
}}
