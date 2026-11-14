// Forest dialogue for the game's adaptation. These are not quotations from a scripture.
// Keep navigation and button instructions in the HUD, not in the characters' voices.
namespace DongbaPrototype {public partial class ObliqueForest {
    static readonly string[] FatherIntroLines={
        "Father: Come on, Gaolequ. Let's hunt, and bring back some wood.",
        "Gaolequ: I'm ready, Father.",
        "Father: Stay close. We won't go far."
    };
    static readonly string[] HuntInvitationLines={
        "Father: Did you see the size of that boar?",
        "Father: It's gone across the river. Come on!"
    };
    const string BoarAidLine="Father: I'll keep it busy!";
    static readonly string[] HomewardLines={"Father: That's enough. Let's go home."};
    static readonly string[] MissingKnifeLines={
        "Father: My knife... I must have left it back there.",
        "Father: Wait here. I'll be right back."
    };
    static readonly string[] MissingKnifeWithoutPauseLines={"Father: Wait a moment...",MissingKnifeLines[0],MissingKnifeLines[1]};
    static readonly string[] ShuQuestionLines={"Shu: Is this your knife?"};
    static readonly string[] ShuAccusationLines={"Shu: So it was you who killed my wild boar!"};
    const string WaitingNarration="Three days and three nights pass.";
    static readonly string[] RealizationLines={
        "Gaolequ: He said he'd be right back...",
        "Gaolequ: I have to find him."
    };
    static readonly string[] RestorationArrivalLines={
        "His father never made it home alive. Gaolequ returned to the forest alone.",
        "Gaolequ: These trees... we cut them down.",
        "Gaolequ: I'll plant trees here again."
    };
    string RestorationClosingLine(){
        // Let the line address what is actually in the shot. Wildlife returns gradually;
        // completing restoration must not claim that an animal has arrived off camera.
        foreach(var animal in animals){
            if(animal.health<=0||!animal.root.gameObject.activeInHierarchy||animal.migrationState==2||UnityEngine.Vector3.Distance(Feet,animal.root.position)>12)continue;
            var point=cam.WorldToViewportPoint(animal.renderer.bounds.center);
            if(point.z>0&&point.x>.05f&&point.x<.95f&&point.y>.05f&&point.y<.95f)return "Gaolequ: You're back.";
        }
        return "Gaolequ: I'll come back tomorrow.";
    }
}}
