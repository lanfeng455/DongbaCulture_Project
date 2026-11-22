// Forest dialogue for the game's adaptation. These are not quotations from a scripture.
// Keep navigation and button instructions in the HUD, not in the characters' voices.
namespace DongbaPrototype {public partial class ObliqueForest {
    static readonly string[] FatherIntroLines={
        "Father: Bring your axe, Gaolequ. Let's head into the forest.",
        "Gaolequ: How far are we going today?",
        "Father: Not far. We'll be home before dark."
    };
    static readonly string[] HuntInvitationLines={
        "Gaolequ: It's across the river... Are we still going after it?",
        "Father: We can't let one that big get away.",
        "Father: Come on. Stay with me."
    };
    const string BoarAidLine="Father: I'll keep it busy!";
    static readonly string[] HomewardLines={"Father: That's enough for today. Let's go home."};
    static readonly string[] MissingKnifeLines={
        "Father: My knife... I left it across the river.",
        "Gaolequ: I'll come with you.",
        "Father: No, wait here. I'll get it and come straight back."
    };
    static readonly string[] MissingKnifeWithoutPauseLines={"Father: Wait a moment...",MissingKnifeLines[0],MissingKnifeLines[1],MissingKnifeLines[2]};
    static readonly string[] ShuQuestionLines={"Shu: Is this your knife?"};
    static readonly string[] ShuAccusationLines={"Shu: So it was you who killed my wild boar!"};
    const string WaitingNarration="Three days and three nights pass.";
    static readonly string[] RealizationLines={
        "Gaolequ: He said he'd come straight back...",
        "Gaolequ: I can't keep waiting. I have to find him."
    };
    static readonly string[] RestorationArrivalLines={
        "His father never made it home alive. Gaolequ returned to the forest alone.",
        "Gaolequ: Back then, I thought the forest would never run out.",
        "Gaolequ: I'll start by bringing the trees back."
    };
    string RestorationClosingLine(){
        return RestorationAnimalInShot()?"Gaolequ: You're back.":"Gaolequ: I'll come back tomorrow.";
    }
    bool RestorationAnimalInShot(){
        // Let the line address what is actually in the shot. Wildlife returns gradually;
        // completing restoration must not claim that an animal has arrived off camera.
        foreach(var animal in animals){
            if(animal.health<=0||!animal.root.gameObject.activeInHierarchy||animal.migrationState==2||UnityEngine.Vector3.Distance(Feet,animal.root.position)>12)continue;
            var point=cam.WorldToViewportPoint(animal.renderer.bounds.center);
            if(point.z>0&&point.x>.05f&&point.x<.95f&&point.y>.05f&&point.y<.95f)return true;
        }
        return false;
    }
}}
