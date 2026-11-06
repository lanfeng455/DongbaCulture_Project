using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckBoarMarker(){
        yield return new WaitForSeconds(1);
        SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;SpawnStoryBoar();journeyStage=JourneyStage.Boar;boarStun=100;
        string folder="Verification/boar-marker";Directory.CreateDirectory(folder);var log=new StringBuilder();
        player.position=ForestSpawn;FollowPlayerCamera(100);yield return new WaitForEndOfFrame();DrawBoarTargetMarker();
        log.AppendLine("Off-screen boar label hidden="+!uiBoarPlate.activeSelf);CaptureSceneToTexture(folder+"/off-screen.png");
        player.position=BoarClearing+Vector3.left*3;FollowPlayerCamera(100);yield return new WaitForEndOfFrame();DrawBoarTargetMarker();
        log.AppendLine("Visible living boar label shown="+uiBoarPlate.activeSelf);CaptureSceneToTexture(folder+"/visible.png");
        Vector3 v=cam.WorldToViewportPoint(storyBoar.root.position+cam.transform.up*2.3f);var rect=(RectTransform)uiBoarPlate.transform;
        log.AppendLine("Label follows exact projected position="+(Vector2.Distance(rect.anchorMin,new Vector2(v.x,v.y))<.001f));
        storyBoar.health=0;DrawBoarTargetMarker();log.AppendLine("Dead boar label hidden="+!uiBoarPlate.activeSelf);
        storyBoar.health=6;journeyStage=JourneyStage.Leaving;DrawBoarTargetMarker();log.AppendLine("Departure hides label="+!uiBoarPlate.activeSelf);
        RestartRun();DrawBoarTargetMarker();log.AppendLine("Restart hides label="+!uiBoarPlate.activeSelf);
        SetStoryModeEnabled(false);DrawBoarTargetMarker();log.AppendLine("Sandbox hides label="+!uiBoarPlate.activeSelf);
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
