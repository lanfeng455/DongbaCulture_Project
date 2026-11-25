using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace DongbaPrototype {public sealed partial class DongbaInterface {
    IEnumerator CheckSandboxEntry(){
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return Snap("title-zh");CheckLayout("Chinese title",titleMenu.transform);
        DongbaLanguage.Set(DongbaLocale.English,false);yield return Snap("title-en");
        yield return ClickButton("Sandbox Mode");
        float until=Time.realtimeSinceStartup+45;while(SceneManager.GetActiveScene().name!="ObliqueForest"&&Time.realtimeSinceStartup<until)yield return null;
        yield return new WaitForSecondsRealtime(3);
        Check("Public title opens sandbox directly",forest!=null&&!forest.StoryModeEnabled);if(forest==null)yield break;
        yield return Snap("sandbox-normal");
        yield return KeyPress(Key.H);Check("Sandbox controls reachable",page==Page.Help);CheckLayout("Sandbox help",menuPage);yield return Snap("sandbox-controls-en");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;CheckLayout("Sandbox Chinese help",menuPage);yield return Snap("sandbox-controls-zh");
        yield return KeyPress(Key.Escape);yield return KeyPress(Key.Escape);Close();yield return null;
        yield return forest.VerifySandbox((ok,label)=>Check(label,ok));
        yield return KeyPress(Key.R);Check("R asks to restart forest",page==Page.Restart);yield return Snap("sandbox-restart");
        yield return ClickButton("Confirm");yield return new WaitForSecondsRealtime(.5f);
        Check("Confirmed menu reset remains sandbox",!forest.StoryModeEnabled&&page==Page.None);
        yield return KeyPress(Key.Escape);yield return ClickButton("Return to title");yield return ClickButton("Confirm");
        until=Time.realtimeSinceStartup+35;while(SceneManager.GetActiveScene().name!="Title"&&Time.realtimeSinceStartup<until)yield return null;
        yield return new WaitForSecondsRealtime(2);yield return ClickButton("Begin story");
        until=Time.realtimeSinceStartup+45;while(SceneManager.GetActiveScene().name!="ObliqueForest"&&Time.realtimeSinceStartup<until)yield return null;
        yield return new WaitForSecondsRealtime(2);
        Check("Story entry remains available after sandbox",forest!=null&&forest.StoryModeEnabled&&forest.InterfaceFatherPresent);
    }
}}
