using System;
using System.Collections.Generic;
using UnityEngine;

namespace DongbaPrototype {
    // Serialized authoring data only. Progress belongs to a runner, never to this asset.
    [CreateAssetMenu(menuName="Dongba/Dialogue Graph", fileName="DialogueGraph")]
    public sealed class ForestDialogueGraph : ScriptableObject {
        public enum Kind { Start, Dialogue, Wait, Event, Branch, End }
        public enum Entry { MissingKnife, ShuConfrontation, BoarPass, GrainGiveaway, GrainPickup }
        public enum Cue { None, NoticeKnife, Hesitate, Depart }
        public enum Action { FatherSearchKnife, ShuTakeFather, DeliverGrain }
        [Serializable] public sealed class Node {
            public string id, title;
            public Kind kind;
            public Vector2 position;
            public Entry entry;
            public string speaker;
            [TextArea] public string zh, en, note;
            public string next, otherwise;
            public float seconds=1;
            public Cue cue;
            public Action action;
            public string Text(bool chinese) => chinese ? zh : en;
        }
        // 森林图保持强校验（每条分支必须以对应森林事件收尾）；谜题图在 Inspector 关掉此开关
        public bool forestRules=true;
        public List<Node> nodes=new List<Node>();
        public Node Find(string id) => nodes.Find(n=>n!=null&&n.id==id);
        public Node StartFor(Entry entry) => nodes.Find(n=>n!=null&&n.kind==Kind.Start&&n.entry==entry);
        public List<string> Validate() {
            var errors=new List<string>(); var ids=new HashSet<string>(); var entries=new HashSet<Entry>();
            foreach(var n in nodes) {
                if(n==null||string.IsNullOrWhiteSpace(n.id)||!ids.Add(n.id)){errors.Add("节点编号缺失或重复。");continue;}
                var label=string.IsNullOrWhiteSpace(n.title)?n.id:n.title;
                if(n.kind==Kind.Start&&!entries.Add(n.entry))errors.Add(label+"：同一入口只能有一个起点。");
                if(n.kind==Kind.Dialogue&&(string.IsNullOrWhiteSpace(n.zh)||string.IsNullOrWhiteSpace(n.en)))errors.Add(label+"：请填写中文和英文。");
                if(n.kind==Kind.Dialogue&&((n.zh??"").StartsWith("@")||(n.en??"").StartsWith("@")))errors.Add(label+"：台词不能以动作指令 @ 开头，请使用事件节点。");
                if(n.kind==Kind.Wait&&(float.IsNaN(n.seconds)||float.IsInfinity(n.seconds)||n.seconds<0||n.seconds>30))errors.Add(label+"：等待应在0–30秒之间。");
            }
            foreach(var n in nodes) {
                if(n==null)continue;
                if(n.kind!=Kind.End)CheckLink(n,n.next,errors);
                if(n.kind==Kind.Branch)CheckLink(n,n.otherwise,errors);
            }
            if(entries.Count==0)errors.Add("至少需要一个入口。");
            if(errors.Count>0)return errors;
            var visited=new HashSet<string>(); var visiting=new HashSet<string>();
            foreach(var n in nodes)if(!Acyclic(n,visited,visiting)){errors.Add("连线形成循环。第一版只支持向前推进的流程，请断开回路。");break;}
            if(errors.Count>0)return errors;
            // Every branch must hand control back to the same existing game event exactly once.
            if(forestRules)foreach(var n in nodes)if(n.kind==Kind.Start&&!EndsWithEvent(n,n.entry,0,new Dictionary<string,bool>()))
                errors.Add(n.title+"：每条分支都必须在结束前执行一次对应事件（父亲找刀／署抓走父亲），事件之后直接连接结束。");
            return errors;
        }
        void CheckLink(Node n,string target,List<string> errors) {
            var linked=Find(target);
            if(string.IsNullOrEmpty(target)||linked==null)errors.Add(n.title+"：有未连接的出口。");
            else if(linked.kind==Kind.Start)errors.Add(n.title+"：不能连接回入口。");
        }
        bool Acyclic(Node n,HashSet<string> done,HashSet<string> active) {
            if(done.Contains(n.id))return true;if(!active.Add(n.id))return false;
            if(n.kind!=Kind.End&&!Acyclic(Find(n.next),done,active))return false;
            if(n.kind==Kind.Branch&&!Acyclic(Find(n.otherwise),done,active))return false;
            active.Remove(n.id);done.Add(n.id);return true;
        }
        bool EndsWithEvent(Node n,Entry entry,int count,Dictionary<string,bool> cache) {
            string key=n.id+":"+count;if(cache.TryGetValue(key,out bool result))return result;
            if(n.kind==Kind.Event) {
                if(n.action!=(entry==Entry.MissingKnife?Action.FatherSearchKnife:Action.ShuTakeFather)||Find(n.next).kind!=Kind.End)return false;
                count++;
            }
            result=n.kind==Kind.End ? count==1 : EndsWithEvent(Find(n.next),entry,count,cache);
            if(result&&n.kind==Kind.Branch)result=EndsWithEvent(Find(n.otherwise),entry,count,cache);
            cache[key]=result;return result;
        }
    }
    // Shared by editor rehearsal and the game. No UnityEditor dependency and no scene mutation here.
    public sealed class ForestDialogueRunner {
        readonly ForestDialogueGraph graph;
        readonly Func<bool> condition;
        readonly System.Action<ForestDialogueGraph.Node> entered;
        public ForestDialogueGraph.Node Current {get;private set;}
        public bool Running {get;private set;}
        public float Elapsed {get;private set;}
        public int Revision {get;private set;}
        public ForestDialogueRunner(ForestDialogueGraph graph,Func<bool> condition,System.Action<ForestDialogueGraph.Node> entered) {
            this.graph=graph;this.condition=condition;this.entered=entered;
        }
        public void Start(string id) {Current=graph.Find(id);Running=Current!=null;Elapsed=0;Revision++;Pump(true);}
        public void Stop() {Running=false;Current=null;Elapsed=0;Revision++;}
        public void Continue() {if(Running&&Current.kind==ForestDialogueGraph.Kind.Dialogue)MoveNext();}
        public void Tick(float dt) {
            if(!Running||Current.kind!=ForestDialogueGraph.Kind.Wait)return;
            Elapsed+=Mathf.Max(0,dt);if(Elapsed>=Current.seconds)MoveNext();
        }
        void MoveNext() {Current=graph.Find(Current.next);Elapsed=0;Revision++;Pump(true);}
        void Pump(bool notify) {
            int budget=128;
            while(Running&&budget-->0) {
                if(Current==null){Stop();return;}
                if(notify)entered?.Invoke(Current);
                if(!Running)return;
                if(Current.kind==ForestDialogueGraph.Kind.Dialogue||Current.kind==ForestDialogueGraph.Kind.Wait)return;
                if(Current.kind==ForestDialogueGraph.Kind.End){Running=false;return;}
                var next=Current.kind==ForestDialogueGraph.Kind.Branch&&!condition()?Current.otherwise:Current.next;
                Current=graph.Find(next);Elapsed=0;Revision++;
            }
            if(budget<=0)Stop();
        }
    }
}
