using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DongbaPrototype {
    public sealed class ForestDialogueGraphBuildCheck : IPreprocessBuildWithReport {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report){ForestDialogueGraphVerification.ValidateShippingGraph();}
    }
    public static class ForestDialogueGraphVerification {
        public static void ValidateShippingGraph(){
            var graph=AssetDatabase.LoadAssetAtPath<ForestDialogueGraph>(ForestDialogueGraphWindow.AssetPath);
            if(graph==null)throw new BuildFailedException("ForestCaptureGraph is missing.");
            var errors=graph.Validate();
            foreach(var entry in new[]{ForestDialogueGraph.Entry.MissingKnife,ForestDialogueGraph.Entry.ShuConfrontation})if(graph.StartFor(entry)==null)errors.Add("Missing forest entry: "+entry);
            if(errors.Count>0)throw new BuildFailedException(string.Join("\n",errors));
        }
        [MenuItem("Dongba/Dialogue Graph/运行数据与播放检查")]
        public static void Run(){
            ValidateShippingGraph();var source=AssetDatabase.LoadAssetAtPath<ForestDialogueGraph>(ForestDialogueGraphWindow.AssetPath);
            var log=new List<string>();var graph=UnityEngine.Object.Instantiate(source);
            try {
                void Require(bool ok,string text){if(!ok)throw new Exception(text);log.Add("PASS "+text);}
                var json=EditorJsonUtility.ToJson(graph);var clone=ScriptableObject.CreateInstance<ForestDialogueGraph>();
                EditorJsonUtility.FromJsonOverwrite(json,clone);
                Require(clone.Find("shu_question").zh==graph.Find("shu_question").zh&&clone.nodes.Count==16,"Serialized graph preserves bilingual text and node identities");UnityEngine.Object.DestroyImmediate(clone);
                foreach(bool previewVersion in new[]{true,false}){
                    var spoken=new List<string>();int events=0;
                    var runner=new ForestDialogueRunner(graph,()=>previewVersion,n=>{if(n.kind==ForestDialogueGraph.Kind.Dialogue)spoken.Add(n.id);if(n.kind==ForestDialogueGraph.Kind.Event)events++;});
                    runner.Start("knife_start");
                    for(int i=0;i<400&&runner.Running;i++){if(runner.Current.kind==ForestDialogueGraph.Kind.Dialogue)runner.Continue();else runner.Tick(.02f);}
                    Require(!runner.Running&&events==1&&spoken.Count==(previewVersion?3:4),"Knife branch "+previewVersion+" plays once then releases existing father search");
                    runner.Stop();Require(!runner.Running&&runner.Current==null,"Stopped runner has no lingering dialogue");
                }
                int captures=0;var confrontation=new ForestDialogueRunner(graph,()=>true,n=>{if(n.kind==ForestDialogueGraph.Kind.Event)captures++;});
                confrontation.Start("shu_start");Require(confrontation.Current.id=="shu_question","Start reaches first dialogue");
                confrontation.Tick(20);Require(confrontation.Current.id=="shu_question","Dialogue never advances on a timer");
                confrontation.Continue();confrontation.Tick(.6f);Require(confrontation.Current.id=="shu_silence","Silence retains its duration");
                confrontation.Tick(.8f);Require(confrontation.Current.id=="shu_accusation","Wait continues into accusation");confrontation.Continue();confrontation.Tick(10);confrontation.Continue();
                Require(captures==1&&!confrontation.Running,"Capture event fires once, completion cannot replay it");
                var question=graph.Find("shu_question");string old=question.next;question.next="missing";Require(graph.Validate().Count>0,"Dangling links rejected");question.next="shu_question";Require(graph.Validate().Count>0,"Cycles rejected");question.next=old;
                string zh=question.zh;question.zh="";Require(graph.Validate().Count>0,"Missing translation rejected");question.zh=zh;
                var action=graph.Find("shu_event");action.action=ForestDialogueGraph.Action.FatherSearchKnife;Require(graph.Validate().Count>0,"Incorrect scene handoff event rejected");action.action=ForestDialogueGraph.Action.ShuTakeFather;
                old=question.next;question.next="shu_end";Require(graph.Validate().Count>0,"Premature end cannot leave the father stuck");question.next=old;
                Require(graph.Validate().Count==0,"Source graph remains valid after validation tests");
                Directory.CreateDirectory("Verification/dialogue-graph");File.WriteAllLines("Verification/dialogue-graph/editor-checks.txt",log);Debug.Log(string.Join("\n",log));
            }finally{UnityEngine.Object.DestroyImmediate(graph);}
        }
        public static void Build(){
            Run();JourneyReelBuildPreparation.Prepare();
            string output=Environment.GetEnvironmentVariable("DONGBA_GRAPH_BUILD");
            if(string.IsNullOrEmpty(output))output="Builds/Current/ForestPrototype.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText("Verification/dialogue-graph/build.txt",result.summary.result+" errors="+result.summary.totalErrors);
            if(result.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Dialogue graph build failed.");
        }
        public static void OpenWindow(){ForestDialogueGraphWindow.Open();}
    }
}
