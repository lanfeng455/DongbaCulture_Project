using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Ivan.Diorama;
using Ivan.Scroll;
namespace DongbaPrototype {public sealed class PaperPuzzleMotionCheck:MonoBehaviour {
    const string Folder="Verification/character-feel-20261124/puzzle";
    int failures,frame;
    void Check(bool ok,string text){if(!ok)failures++;File.AppendAllText(Folder+"/checks.txt",(ok?"PASS ":"FAIL ")+text+"\n");}
    IEnumerator Wait(float seconds){float t=0;while(t<seconds){yield return new WaitForEndOfFrame();t+=Time.deltaTime;if(Array.IndexOf(Environment.GetCommandLineArgs(),"--character-feel-preview")>=0)DongbaLanguageCapture.Write(Camera.main,Folder+"/frames/"+(frame++).ToString("D4")+".png");}}
    public IEnumerator Run(){
        Directory.CreateDirectory(Folder+"/frames");File.WriteAllText(Folder+"/checks.txt","");
        yield return SceneManager.LoadSceneAsync("Beat_2_Cleansing_Impurity");yield return new WaitForSeconds(1);
        DongbaInterface.Instance?.Close(false);
        foreach(var s in FindObjectsByType<StorySequencePlayer>(FindObjectsSortMode.None))s.enabled=false;
        foreach(var s in FindObjectsByType<StoryTripwire>(FindObjectsSortMode.None))s.enabled=false;
        foreach(var s in FindObjectsByType<ScrollPlayDirector>(FindObjectsSortMode.None)){s.SetMode(ScrollPlayDirector.PlayMode.Manual);s.enabled=false;}
        DialogueDisplay.Instance?.HideDialogue();
        var actor=FindFirstObjectByType<DioramaPlayerController>();actor.ClearAutoMoveInput();actor.SetStageMovementLock(false);
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>("PaperPuzzleMotion");InputSystem.EnableDevice(keyboard);keyboard.MakeCurrent();
        yield return Wait(.6f);Time.captureFramerate=30;
        var foot=actor.transform;var art=actor.BodyRenderer.transform;var sprite=actor.BodyRenderer.sprite;
        var start=foot.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return Wait(1);
        Check(Vector3.Distance(foot.position,start)>1,"Puzzle character still moves through the real Rigidbody motor");
        var motion=(PaperActorMotion)typeof(DioramaPlayerController).GetField("m_paperMotion",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
        Check(motion.MovingWeight>.5f,"Puzzle walking drives shared step animation");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(.4f);
        Check(motion.MovingWeight<.05f,"Puzzle release settles instead of walking in place");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A,Key.J));yield return null;yield return null;
        int locked=(int)typeof(DioramaPlayerController).GetField("m_attackDirection",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
        Check(locked==-1&&actor.BodyRenderer.flipX,"Reverse plus attack locks the puzzle actor and tool together");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(.8f);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        float peak=0,low=2;for(int i=0;i<40;i++){yield return Wait(1f/30);peak=Mathf.Max(peak,art.localPosition.y);low=Mathf.Min(low,motion.Current.scale.y);}
        Check(peak>1.3f&&low<.97f,"Puzzle jump and landing use the same paper response");
        var before=foot.position;actor.ReactToSpeech();yield return Wait(.3f);Check(Mathf.Abs(motion.Current.roll)>1&&Vector3.Distance(before,foot.position)<.02f,"Dialogue gesture does not move the puzzle collider");yield return Wait(.5f);
        actor.ApplyKnockback(Vector3.right*2,.3f);yield return Wait(.2f);Check(Mathf.Abs(motion.Current.roll)>3,"Existing beast knockback also gives a visible flinch");yield return Wait(.5f);
        var rock=FindFirstObjectByType<Ivan.Puzzle.PushableRock>();
        var rockBody=rock.GetComponent<Rigidbody>();var rockCollider=rock.GetComponentInChildren<Collider>();var actorBody=actor.GetComponent<Rigidbody>();
        var pushStart=rock.transform.position;var approach=rockCollider.bounds.center+Vector3.left*(rockCollider.bounds.extents.x+.27f);approach.y=actor.transform.position.y;
        actorBody.position=approach;actor.transform.position=approach;actorBody.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return Wait(.3f);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return Wait(.5f);
        float push=(float)typeof(DioramaPlayerController).GetField("m_pushWeight",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
        Check(push>.2f&&Vector3.Distance(pushStart,rock.transform.position)>.01f,"Pushing a real red rock adds effort without replacing physical movement");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(.5f);
        push=(float)typeof(DioramaPlayerController).GetField("m_pushWeight",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
        Check(push<.03f,"Releasing the red rock smoothly removes the effort pose");
        var fox=StageActorRegistry.Find("Fox");var foxArt=fox.GetComponentInChildren<SpriteRenderer>().transform;var foxStart=foxArt.position;
        fox.ReactToSpeech();yield return Wait(.8f);Check(Vector3.Distance(foxArt.position,foxStart)<.02f,"Fox reaction returns to its authored position");
        Check(actor.BodyRenderer.sprite==sprite,"Puzzle gestures retain the original glyph");
        Time.captureFramerate=0;InputSystem.RemoveDevice(keyboard);Application.Quit(failures==0?0:1);
    }
}}
