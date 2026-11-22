using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckFeatureAudit(){
        yield return new WaitForSeconds(1);SetStoryModeEnabled(false);RestartRun();
        const string folder="Verification/feature-audit";Directory.CreateDirectory(folder);var log=new StringBuilder();
        tonePreviewFreezeAnimals=true;foreach(var a in animals)a.root.gameObject.SetActive(false);foreach(var p in plants)p.root.gameObject.SetActive(false);
        var victim=animals[0];victim.root.gameObject.SetActive(true);player.position=new Vector3(-18,0,-3);FollowPlayerCamera(100);
        for(int side=-1;side<=1;side+=2){
            victim.health=2;victim.root.position=Feet+Vector3.right*(side*.9f);facing=side;heading=Vector3.right*side;yield return null;
            yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.32f);CaptureSceneToTexture(folder+(side<0?"/axe-left.png":"/axe-right.png"));yield return new WaitForSeconds(.4f);
            log.AppendLine((side<0?"Left":"Right")+" visible blade hits once="+(victim.health==1)+"; health="+victim.health);
        }
        victim.health=2;victim.art.localScale=victim.initialScale*.4f;victim.root.position=Feet+Vector3.right*1.95f;facing=1;heading=Vector3.right;yield return null;
        yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.7f);
        log.AppendLine("Old radius alone cannot hit beyond blade="+(victim.health==2));
        victim.health=2;victim.root.gameObject.SetActive(true);victim.art.localScale=victim.initialScale;victim.root.position=Feet+Vector3.left*.9f;facing=1;heading=Vector3.right;
        var kb=Keyboard.current;if(kb==null)kb=InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(kb,new KeyboardState(Key.A,Key.J));yield return null;yield return null;InputSystem.QueueStateEvent(kb,new KeyboardState());
        yield return new WaitForSeconds(.3f);bool locked=swingFacing==-1&&facing==-1;
        InputSystem.QueueStateEvent(kb,new KeyboardState(Key.D));yield return new WaitForSeconds(.1f);InputSystem.QueueStateEvent(kb,new KeyboardState());
        log.AppendLine("Reverse plus attack locks one direction="+(locked&&facing==-1&&victim.health==1)+"; locked="+locked+" facing="+facing+" hp="+victim.health);yield return new WaitForSeconds(.5f);
        victim.health=2;victim.root.position=Feet+Vector3.right*.9f;facing=1;heading=Vector3.right;int damageBefore=damageCount;
        yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.7f);victim.root.position=Feet+Vector3.right*.9f;
        yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.7f);
        log.AppendLine("Two real swings kill animal and cost two ecology="+(victim.health==0&&damageCount==damageBefore+2));
        SpawnStoryBoar();journeyStage=JourneyStage.Boar;storyBoar.root.position=Feet+Vector3.right*1.4f;storyBoar.health=6;
        yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.7f);
        log.AppendLine("Blade contacts great boar once="+(storyBoar.health==5)+"; health="+storyBoar.health);journeyStage=JourneyStage.Off;storyBoar.root.gameObject.SetActive(false);
        victim.root.gameObject.SetActive(false);var tree=plants.Find(b=>b.tree);tree.root.gameObject.SetActive(true);
        tree.root.position=Feet+Vector3.right*.75f;facing=1;heading=Vector3.right;tree.health=3;yield return null;
        yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.7f);log.AppendLine("Blade contacts trunk once="+(tree.health==2));
        RestartRun();tonePreviewFreezeAnimals=true;player.position=tree.home;interactionTarget=null;tree.health=0;tree.fall=1;int before=careCount;Care();
        log.AppendLine("Sandbox replant starts small and scores once="+(tree.targetScale<.4f&&careCount==before+1));
        log.AppendLine("Replant cannot trap player inside new trunk="+Free(Feet));
        FollowPlayerCamera(100);yield return new WaitForSeconds(.2f);CaptureSceneToTexture(folder+"/replant-start.png");
        float first=tree.targetScale;yield return new WaitForSeconds(4);float middle=tree.targetScale;CaptureSceneToTexture(folder+"/replant-growing.png");
        yield return new WaitForSeconds(11);CaptureSceneToTexture(folder+"/replant-grown.png");
        log.AppendLine("Sandbox grows progressively to full size="+(first<middle&&middle<.8f&&tree.targetScale>=.999f));
        log.AppendLine("Growth itself does not award ecology points="+(careCount==before+1));
        var original=initialAnimalHomes[0];animals[0].home+=Vector3.right*8;RestartRun();
        log.AppendLine("Restart restores initial animal homes="+(animals[0].home==original&&animals[0].root.position==original));
        log.AppendLine("Restart clears attack and replanted growth="+(attackAge<0&&swingContacts.Count==0&&tree.targetScale==1&&Balance==0));
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
