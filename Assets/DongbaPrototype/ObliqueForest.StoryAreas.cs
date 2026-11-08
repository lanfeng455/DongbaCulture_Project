using System.Collections;using System.IO;using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    void ComposeStoryAreas(){
        Vector3[] homeTrees={new Vector3(-15,0,2.8f),new Vector3(-14.8f,0,8),new Vector3(-8,0,10),new Vector3(-1.5f,0,8.5f),new Vector3(-.5f,0,3.2f)};
        for(int i=0;i<homeTrees.Length;i++)AddDictionaryPlant(homeTrees[i],true,1000+i,"plant_"+(i%3).ToString("00"),3.2f+(i%3)*.24f);
        Vector3[] border={new Vector3(16.5f,0,-5.8f),new Vector3(21.2f,0,-7.5f),new Vector3(27,0,-6.2f),new Vector3(31.4f,0,-3.7f),new Vector3(31.2f,0,4.5f),new Vector3(27.5f,0,7.4f),new Vector3(22,0,8),new Vector3(17.4f,0,5.8f)};
        for(int i=0;i<border.Length;i++)AddDictionaryPlant(border[i],true,1020+i,"plant_"+((i+1)%3).ToString("00"),3.4f+(i%4)*.17f);
        Vector3[] homePlants={new Vector3(-12,0,-.5f),new Vector3(-13.1f,0,-1.3f),new Vector3(-11.6f,0,-2.5f),new Vector3(-3.5f,0,-.5f),new Vector3(-2.7f,0,-1.5f),new Vector3(-3.7f,0,-2.7f)};
        for(int i=0;i<homePlants.Length;i++)AddDictionaryPlant(homePlants[i],false,1050+i,"plant_"+(3+i%6).ToString("00"),.45f+(i%3)*.08f);
        Vector3[] edgePlants={new Vector3(15.2f,0,-2.5f),new Vector3(15.9f,0,2.3f),new Vector3(28.7f,0,-3.5f),new Vector3(29.1f,0,3.3f)};
        for(int i=0;i<edgePlants.Length;i++)AddDictionaryPlant(edgePlants[i],false,1070+i,"plant_"+(3+i).ToString("00"),.5f+(i%2)*.08f);
        for(int i=0;i<18;i++){float t=i/17f;Vector3 p=new Vector3(Mathf.Lerp(-9,1,t),0,-3.8f+Mathf.Sin(i*2.3f)*.55f);AddAreaGroundMark(p,i);}
        for(int i=0;i<10;i++)AddAreaGroundMark(new Vector3(14.5f+i*.55f,0,(i%2==0?-1:1)*(1.4f+Mathf.Sin(i)*.2f)),i+20);
    }
    void AddAreaGroundMark(Vector3 p,int i){var root=new GameObject("Sparse ground strokes / area composition").transform;root.position=p;var art=Billboard("Existing dictionary ground mark",root,dictionarySprites["mark_01"],.11f+(i%3)*.025f);var r=art.GetComponent<SpriteRenderer>();r.color=new Color(1,1,1,.43f);r.sortingOrder=GroundDepthOrder(p.z);}
    IEnumerator CaptureStoryAreas(){yield return new WaitForSeconds(2);SetStoryModeEnabled(true);RestartRun();Directory.CreateDirectory("Verification/story-areas");player.position=ForestSpawn;FollowPlayerCamera(100);yield return null;CaptureSceneToTexture("Verification/story-areas/home.png");for(int i=0;i<3;i++)AdvanceFatherIntro();yield return null;CaptureSceneToTexture("Verification/story-areas/home-layout.png");journeyStage=JourneyStage.Boar;SpawnStoryBoar();player.position=BoarClearing+new Vector3(-4,0,-2);storyFatherRoot.position=player.position+Vector3.left;FollowPlayerCamera(100);yield return null;CaptureSceneToTexture("Verification/story-areas/boar-clearing.png");File.WriteAllText("Verification/story-areas/checks.txt","Hearth present="+(storyHearth!=null)+"\nOpening player side clear="+Free(ForestSpawn)+"\nOpening father side clear="+Free(FatherHomeStart)+"\nHearth blocks walking="+!Free(HearthPosition)+"\nHouse door clear="+Free(new Vector3(-7,0,-2.5f))+"\nBoar center clear="+Free(BoarClearing)+"\nWest entrance clear="+Free(new Vector3(15,0,0))+"\nEast capture exit clear="+Free(new Vector3(30,0,0)));Application.Quit();}
}}
