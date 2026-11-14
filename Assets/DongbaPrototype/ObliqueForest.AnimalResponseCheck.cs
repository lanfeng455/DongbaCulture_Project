using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    const string ResponseCheckFolder="Verification/animal-response";
    void ResponseResult(string name,bool passed,string detail="") { File.AppendAllText(ResponseCheckFolder+"/checks.txt",name+"="+passed+" "+detail+"\n"); }
    int CountFollowers(){int n=0;foreach(var a in animals)if(a.health>0&&a.follow>0)n++;return n;}
    IEnumerator PreviewAnimalResponse(bool decline){
        yield return new WaitForSeconds(1);SetStoryModeEnabled(false);RestartRun();player.position=new Vector3(-18,0,-3);lastDryPosition=Feet;FollowPlayerCamera(100);
        if(decline){while(Balance>-10)RegisterDamage();}else{while(Balance<FlourishThreshold)RegisterCare();}
    }
    IEnumerator CheckAnimalResponse(){
        yield return new WaitForSeconds(1);Directory.CreateDirectory(ResponseCheckFolder);File.WriteAllText(ResponseCheckFolder+"/checks.txt","Real forest layout; original animals, no cleared plants. Counters advanced through existing RegisterCare/RegisterDamage.\n");
        SetStoryModeEnabled(false);RestartRun();player.position=new Vector3(-18,0,-3);lastDryPosition=Feet;FollowPlayerCamera(100);
        yield return new WaitForSeconds(3);ResponseResult("Normal does not attract",CountFollowers()==0&&circleAnimals.Count==0);
        while(Balance<FlourishThreshold)RegisterCare();
        var lastAngles=new Dictionary<Being,float>();var arc=new Dictionary<Being,float>();int maxFollowers=0,maxCircle=0;float elapsed=0;int frames=0;float minSpacing=100;bool safe=true;
        while(elapsed<24){
            yield return null;elapsed+=Time.deltaTime;frames++;maxFollowers=Mathf.Max(maxFollowers,CountFollowers());maxCircle=Mathf.Max(maxCircle,circleAnimals.Count);
            foreach(var a in animals){if(a.health<=0||a.migrationState==2)continue;safe&=AnimalTerrainClear(a.root.position);
                foreach(var plant in plants)if(Vector3.Distance(a.root.position,plant.home)<AnimalPlantRadius(plant)-.02f)safe=false;
                foreach(var other in animals)if(other!=a&&other.health>0&&other.migrationState!=2)minSpacing=Mathf.Min(minSpacing,Vector3.Distance(a.root.position,other.root.position));
            }
            foreach(var a in circleAnimals){var d=a.root.position-Feet;float angle=Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg;
                if(lastAngles.TryGetValue(a,out float last)){if(!arc.ContainsKey(a))arc[a]=0;arc[a]+=Mathf.DeltaAngle(last,angle);}
                lastAngles[a]=angle;
            }
        }
        int orbiting=0;foreach(var pair in arc)if(pair.Value>200)orbiting++;
        ResponseResult("Nearest three actually orbit",maxCircle==3&&orbiting>=3,"count="+maxCircle+" arcs="+string.Join(",",arc.Values));
        ResponseResult("Initial local group respects six cap",maxFollowers>=3&&maxFollowers<=6,"max="+maxFollowers);
        ResponseResult("No river or root crossing",safe);ResponseResult("Animals keep spacing",minSpacing>=.64f,"minimum="+minSpacing);
        ResponseResult("Runtime frames sampled",frames/elapsed>45,"fps="+(frames/elapsed));CaptureSceneToTexture(ResponseCheckFolder+"/flourishing.png");
        // Move the player through the actual walkability checks, not a teleporting follow target.
        ecologyPreviewWalking=true;ecologyPreviewDirection=Vector3.left;yield return new WaitForSeconds(2);ecologyPreviewWalking=false;yield return new WaitForSeconds(4);
        int close=0;foreach(var a in animals)if(a.follow>0&&Vector3.Distance(a.root.position,Feet)<7)close++;
        ResponseResult("Followers accompany moving player",close>=3,"close="+close);
        ResponseResult("Additional nearby animals join",CountFollowers()>3&&CountFollowers()<=6,"count="+CountFollowers());
        // Actual feeding, growth and inherited AI; no preassigned follow/circle state.
        Being parent=null;foreach(var a in animals)if(a.health>0&&a.migrationState==0&&a.age==2&&!a.angry&&a.follow>0){parent=a;break;}
        int before=animals.Count,careBefore=careCount;
        if(parent!=null){parent.careReady=0;Feed(parent);}
        bool born=animals.Count==before+1;Being child=born?animals[animals.Count-1]:null;
        ResponseResult("Birth stays beside fed parent",born&&Vector3.Distance(child.root.position,parent.root.position)<3);
        if(child!=null){child.careReady=0;Feed(child);child.careReady=0;Feed(child);yield return new WaitForSeconds(8);}
        ResponseResult("Grown offspring shares friendly AI",child!=null&&child.follow>0);
        ResponseResult("Only maturity adds ecology",born&&child.age==2&&careCount==careBefore+1);
        // Separate population fixture: check the six-participant cap and arrival rotation.
        int added=0;for(int i=0;i<16&&added<3;i++)if(FindLocal(Feet,4,7,300+i,true,out var extra)){AddAnimal(extra,i%12);populationCapacity[animals[animals.Count-1].populationRegion]++;added++;}
        int oldVisits=visitsStarted;maxFollowers=0;elapsed=0;Being earliestOuter=null;bool rotated=false;
        while(elapsed<15){yield return null;elapsed+=Time.deltaTime;maxFollowers=Mathf.Max(maxFollowers,CountFollowers());
            if(earliestOuter!=null&&earliestOuter.follow<=0&&CountFollowers()==6)rotated=true;
            if(earliestOuter==null&&CountFollowers()==6)foreach(var a in animals)if(a.follow>0&&!circleAnimals.Contains(a)&&(earliestOuter==null||a.visitOrder<earliestOuter.visitOrder))earliestOuter=a;
        }
        ResponseResult("Capacity and rotation",maxFollowers==6&&visitsStarted>oldVisits,"max="+maxFollowers+" newVisits="+(visitsStarted-oldVisits)+" fixtureAdded="+added);
        ResponseResult("Earliest outer follower yields to newcomer",rotated);
        // A real first hit, not killing the selected participant, must cancel its friendly role.
        Being struck=circleAnimals.Count>0?circleAnimals[0]:null;
        if(struck!=null){ApplyEcologyHit(struck);struck.angry=true;yield return null;yield return null;}
        ResponseResult("Attack cancels friendliness",struck!=null&&struck.follow==0&&!circleAnimals.Contains(struck)&&struck.angry);
        while(Balance>-10)RegisterDamage();yield return new WaitForSeconds(4);
        ResponseResult("Decline releases all friends",CountFollowers()==0&&circleAnimals.Count==0);
        // Put the player near an existing calm adult, including returning/departing ones.
        Being chased=null;foreach(var a in animals)if(a.health>0&&a.migrationState!=2&&!a.angry&&a.age==2&&a.birth>.98f&&clock>=a.careReady){
            for(int i=0;i<12;i++){float angle=i*Mathf.PI/6;var spot=a.root.position+new Vector3(Mathf.Cos(angle)*2,0,Mathf.Sin(angle)*2);if(Free(spot)){player.position=spot;lastDryPosition=Feet;chased=a;break;}}if(chased!=null)break;
        }
        FollowPlayerCamera(100);float initial=chased==null?0:Vector3.Distance(chased.root.position,Feet);float furthest=initial,topSpeed=0;elapsed=0;
        while(elapsed<3){yield return null;elapsed+=Time.deltaTime;if(chased!=null){furthest=Mathf.Max(furthest,Vector3.Distance(chased.root.position,Feet));topSpeed=Mathf.Max(topSpeed,chased.moveSpeed);}}
        ResponseResult("Approach makes adult flee away",chased!=null&&furthest>initial+1.2f&&topSpeed>2.5f,"gain="+(furthest-initial)+" speed="+topSpeed);CaptureSceneToTexture(ResponseCheckFolder+"/decline.png");
        var visible=new Dictionary<Being,bool>();bool hiddenSafely=true;int previousExits=departuresCompleted;
        elapsed=0;while(elapsed<14){yield return null;elapsed+=Time.deltaTime;foreach(var a in animals){bool active=a.root.gameObject.activeSelf;if(visible.TryGetValue(a,out bool was)&&was&&!active&&a.health>0)hiddenSafely&=AnimalOutsideViewAt(a,a.root.position);visible[a]=active;}}
        ResponseResult("Decline reduces active animals",departuresCompleted>0,"exits="+departuresCompleted);
        ResponseResult("Departures hide only outside view",hiddenSafely&&visibleHideErrors==0);
        while(Balance<0)RegisterCare();elapsed=0;int returnsBefore=returnsStarted;
        while(elapsed<16){yield return null;elapsed+=Time.deltaTime;}
        ResponseResult("Recovery returns animals gradually",returnsStarted>returnsBefore,"returned="+(returnsStarted-returnsBefore));
        ResponseResult("No visible respawn",visibleSpawnErrors==0);
        RestartRun();yield return null;ResponseResult("Restart resets animal response",!friendlyWeather&&CountFollowers()==0&&circleAnimals.Count==0);
        if(animals.Count>=2){var a=animals[0];var b=animals[1];a.root.position=new Vector3(-19,0,-3);b.root.position=a.root.position+Vector3.right*.7f;
            ResponseResult("Close grown pair can separate but not overlap further",CircleStepClear(a.root.position-Vector3.right*.1f,a)&&!CircleStepClear(a.root.position+Vector3.right*.1f,a));}
        Application.Quit();
    }
}}
