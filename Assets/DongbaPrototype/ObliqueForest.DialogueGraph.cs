using UnityEngine;

namespace DongbaPrototype {public partial class ObliqueForest {
    ForestDialogueGraph forestDialogueGraph;
    ForestDialogueRunner forestDialogueRunner;
    ForestDialogueGraph.Node forestGraphLine;
    bool GraphRunning => forestDialogueRunner!=null&&forestDialogueRunner.Running;
    public string ActiveDialogueNode => GraphRunning?forestDialogueRunner.Current.id:"";
    public bool DialogueGraphActive => GraphRunning;
    public bool DialogueGraphPreviewReady => player!=null&&clock>.1f;
    void ResetDialogueGraph() {forestDialogueRunner?.Stop();forestGraphLine=null;}
    bool BeginForestDialogueGraph(ForestDialogueGraph.Entry entry) {
        if(forestDialogueGraph==null)forestDialogueGraph=Resources.Load<ForestDialogueGraph>("ForestCaptureGraph");
        if(forestDialogueGraph==null)return false;
        var errors=forestDialogueGraph.Validate();var start=forestDialogueGraph.StartFor(entry);
        if(errors.Count>0||start==null){Debug.LogWarning("Dialogue graph unavailable; original dialogue retained. "+string.Join(" / ",errors));return false;}
        ResetDialogueGraph();narrativeBeat=NarrativeBeat.None;
        forestDialogueRunner=new ForestDialogueRunner(forestDialogueGraph,()=>NarrativePreview,EnterForestDialogueNode);
        forestDialogueRunner.Start(start.id);return true;
    }
    void EnterForestDialogueNode(ForestDialogueGraph.Node node) {
        forestGraphLine=null;narrativeBeat=NarrativeBeat.None;narrativeBeatAge=0;
        if(node.kind==ForestDialogueGraph.Kind.Dialogue) {
            forestGraphLine=node;
            // Manual control and input locking remain shared with the existing subtitles.
            BeginManualStoryDialogue(new[]{node.speaker+": "+node.en},()=>{
                forestGraphLine=null;forestDialogueRunner?.Continue();
            });
        }else if(node.kind==ForestDialogueGraph.Kind.Wait) {
            if(node.cue==ForestDialogueGraph.Cue.NoticeKnife)narrativeBeat=NarrativeBeat.MissingKnife;
            if(node.cue==ForestDialogueGraph.Cue.Hesitate)narrativeBeat=NarrativeBeat.Silence;
            if(node.cue==ForestDialogueGraph.Cue.Depart)narrativeBeat=NarrativeBeat.Depart;
        }else if(node.kind==ForestDialogueGraph.Kind.Event) {
            if(node.action==ForestDialogueGraph.Action.FatherSearchKnife)returnWait=0;
            if(node.action==ForestDialogueGraph.Action.ShuTakeFather)shuConfrontationFinished=true;
        }
    }
    bool UpdateForestDialogueGraph(float dt) {
        if(!GraphRunning)return false;
        forestDialogueRunner.Tick(dt);narrativeBeatAge=forestDialogueRunner.Elapsed;
        return true; // Do not advance the old state machine twice in the completion frame.
    }
    // Explicit development entry. It stages the original loss/river/capture sequence.
    public void PreviewDialogueGraphCapture(bool atConfrontation=false) {
        narrativePreviewEnabled=true;
        StageNarrativeCapture();
        if(!atConfrontation)return;
        journeyStage=JourneyStage.ReturnForKnife;storyOpeningState=StoryOpeningState.CaptureBeat;
        droppedKnife.gameObject.SetActive(true);EnsureStoryShu();
        storyFatherRoot.position=knifeDropPoint+Vector3.left*.6f;
        player.position=DeparturePoint;FollowPlayerCamera(100);
    }
}}
