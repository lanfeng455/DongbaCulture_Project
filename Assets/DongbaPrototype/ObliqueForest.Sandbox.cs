using UnityEngine;
namespace DongbaPrototype { public partial class ObliqueForest {
    // Both entries share the same forest, interactions and ecology; only story ownership changes.
    public void StartForestMode(bool story) {
        StoryModeEnabled=story;
        if(story&&storyFatherRoot==null)CreateStoryFather();
        RestartRun();
        if(XRTrackingControlsCamera)openingSurveyAge=OpeningSurveySeconds;
        if(!story)storyOpeningState=StoryOpeningState.Off;
        xrHelpOpen=false;
    }
    string SandboxState => DongbaInterface.Choose(
        ecologyVisual<-.66f?"Withering":ecologyVisual<-.12f?"Under strain":Balance>=FlourishThreshold?"Flourishing":Balance>0?"Growing":"A living forest",
        ecologyVisual<-.66f?"林地衰败":ecologyVisual<-.12f?"生机渐退":Balance>=FlourishThreshold?"万物繁盛":Balance>0?"生机渐长":"林间生息");
    string SandboxNote => SandboxState+"\n"+DongbaInterface.Choose("Explore · Tend · Feed · Replant","自由探索 · 培育 · 喂养 · 补种");
}}
