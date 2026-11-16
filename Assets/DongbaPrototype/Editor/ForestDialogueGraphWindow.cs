using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using DuNode=DongbaPrototype.ForestDialogueGraph.Node;

namespace DongbaPrototype {
    public sealed class ForestDialogueGraphWindow : EditorWindow {
        public const string AssetPath="Assets/DongbaPrototype/Resources/ForestCaptureGraph.asset";
        const string PreviewRequest="Dongba.DialogueGraph.Preview";
        [SerializeField] ForestDialogueGraph graph;
        [SerializeField] string selected;
        [SerializeField] Vector2 pan=new Vector2(35,45);
        [SerializeField] float zoom=.85f;
        Vector2 detailScroll;
        string connecting;bool falsePort,dragging,panning;int dragGroup;
        ForestDialogueRunner preview;bool previewChinese=true,previewVersion=true;
        string message="",previewEvent="";double lastTick;
        GUIStyle titleStyle,bodyStyle,smallStyle;
        internal Vector2 GuiOffset {get;private set;}
        readonly string[] kinds={"入口","对话","等待","事件","条件","结束"};
        readonly string[] entries={"西岸发现丢刀","东岸与署对峙"};
        readonly string[] cues={"无额外动作","发现刀不见了","父亲愣住","父亲准备出发"};
        readonly string[] actions={"父亲返回东岸找刀","署抓走父亲"};
        [MenuItem("Dongba/Dialogue Graph/打开剧情节点编辑器")]
        public static void Open() {
            var w=GetWindow<ForestDialogueGraphWindow>();w.titleContent=new GUIContent("东巴剧情节点");
            w.minSize=new Vector2(1000,650);
            if(w.graph==null)w.graph=AssetDatabase.LoadAssetAtPath<ForestDialogueGraph>(AssetPath);
            w.FrameEntry(ForestDialogueGraph.Entry.MissingKnife);
            w.Show();
        }
        [OnOpenAsset] public static bool OpenAsset(int instanceId,int line) {
            if(EditorUtility.InstanceIDToObject(instanceId) is ForestDialogueGraph asset){Open();var w=GetWindow<ForestDialogueGraphWindow>();w.graph=asset;w.FrameAll();return true;}return false;
        }
        void OnEnable(){EditorApplication.update+=Tick;Undo.undoRedoPerformed+=OnUndo;lastTick=EditorApplication.timeSinceStartup;}
        void OnDisable(){EditorApplication.update-=Tick;Undo.undoRedoPerformed-=OnUndo;}
        void OnUndo(){preview?.Stop();connecting=null;Repaint();}
        void Tick(){var now=EditorApplication.timeSinceStartup;if(preview!=null&&preview.Running){preview.Tick((float)(now-lastTick));Repaint();}lastTick=now;if(Application.isPlaying)Repaint();}
        void Styles(){
            if(titleStyle!=null)return;
            titleStyle=new GUIStyle(EditorStyles.boldLabel){wordWrap=true,normal={textColor=new Color(.94f,.94f,.91f)}};
            bodyStyle=new GUIStyle(EditorStyles.label){wordWrap=true,normal={textColor=new Color(.86f,.86f,.83f)}};
            smallStyle=new GUIStyle(EditorStyles.miniLabel){wordWrap=true,normal={textColor=new Color(.68f,.73f,.73f)}};
        }
        void OnGUI(){
            GuiOffset=GUIUtility.GUIToScreenPoint(Vector2.zero)-position.position;
            Styles();Toolbar();
            if(graph==null){EditorGUILayout.HelpBox("请先打开 ForestCaptureGraph，或从 Project 中双击对话图资产。",MessageType.Info);return;}
            float sidebar=350;
            var canvas=new Rect(0,47,position.width-sidebar,position.height-47);
            DrawCanvas(canvas);
            GUILayout.BeginArea(new Rect(position.width-sidebar+8,52,sidebar-16,position.height-58));
            detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
            DrawDetails();GUILayout.Space(15);DrawPreview();
            EditorGUILayout.EndScrollView();GUILayout.EndArea();
            if(!string.IsNullOrEmpty(message))GUI.Label(new Rect(12,position.height-35,canvas.width-24,32),message,smallStyle);
        }
        void Toolbar(){
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                var next=(ForestDialogueGraph)EditorGUILayout.ObjectField(graph,typeof(ForestDialogueGraph),false,GUILayout.Width(240));
                if(next!=graph){graph=next;selected=null;preview?.Stop();FrameAll();}
                using(new EditorGUI.DisabledScope(graph==null||Application.isPlaying)){
                    if(GUILayout.Button("保存",EditorStyles.toolbarButton,GUILayout.Width(48)))Save();
                    if(GUILayout.Button("校验",EditorStyles.toolbarButton,GUILayout.Width(48)))ValidateGraph();
                }
                if(GUILayout.Button("查看全图",EditorStyles.toolbarButton,GUILayout.Width(72)))FrameAll();
                if(GUILayout.Button("丢刀段",EditorStyles.toolbarButton,GUILayout.Width(55)))FrameEntry(ForestDialogueGraph.Entry.MissingKnife);
                if(GUILayout.Button("对峙段",EditorStyles.toolbarButton,GUILayout.Width(55)))FrameEntry(ForestDialogueGraph.Entry.ShuConfrontation);
                GUILayout.FlexibleSpace();
                if(graph!=null)GUILayout.Label(EditorUtility.IsDirty(graph)?"● 尚未保存":"已保存",EditorStyles.miniLabel);
                GUILayout.Label(Mathf.RoundToInt(zoom*100)+"%",GUILayout.Width(40));
            }
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                GUILayout.Label("添加",GUILayout.Width(32));
                using(new EditorGUI.DisabledScope(graph==null||Application.isPlaying)){
                    foreach(var kind in new[]{ForestDialogueGraph.Kind.Dialogue,ForestDialogueGraph.Kind.Wait,ForestDialogueGraph.Kind.Event,ForestDialogueGraph.Kind.Branch,ForestDialogueGraph.Kind.End}){
                        if(GUILayout.Button(kinds[(int)kind],EditorStyles.toolbarButton,GUILayout.Width(48)))AddNode(kind,new Vector2(100-pan.x,120-pan.y)/zoom);
                    }
                }
                GUILayout.FlexibleSpace();GUILayout.Label("拖节点 · 空白拖动画布 · 滚轮缩放 · 点出口再点入口连线",EditorStyles.miniLabel);
            }
        }
        Vector2 ScreenPoint(Vector2 world)=>world*zoom+pan;
        Rect NodeRect(DuNode n)=>new Rect(ScreenPoint(n.position),new Vector2(224,n.kind==ForestDialogueGraph.Kind.Branch?142:112)*zoom);
        Vector2 Input(DuNode n)=>NodeRect(n).position+new Vector2(0,38*zoom);
        Vector2 Output(DuNode n,bool otherwise=false)=>NodeRect(n).position+new Vector2(224*zoom,(otherwise?106:66)*zoom);
        void DrawCanvas(Rect area){
            GUI.BeginGroup(area);EditorGUI.DrawRect(new Rect(0,0,area.width,area.height),new Color(.105f,.125f,.14f));
            Handles.BeginGUI();Handles.color=new Color(.17f,.19f,.21f);
            float grid=28*zoom;for(float x=pan.x%grid;x<area.width;x+=grid)Handles.DrawLine(new Vector3(x,0),new Vector3(x,area.height));
            for(float y=pan.y%grid;y<area.height;y+=grid)Handles.DrawLine(new Vector3(0,y),new Vector3(area.width,y));
            foreach(var n in graph.nodes){if(n.kind!=ForestDialogueGraph.Kind.End)DrawLink(n,n.next,false);if(n.kind==ForestDialogueGraph.Kind.Branch)DrawLink(n,n.otherwise,true);}
            if(connecting!=null){var n=graph.Find(connecting);if(n!=null)Curve(Output(n,falsePort),Event.current.mousePosition,new Color(.9f,.72f,.34f));}
            Handles.EndGUI();
            var live=Application.isPlaying?FindFirstObjectByType<ObliqueForest>()?.ActiveDialogueNode:null;
            foreach(var n in graph.nodes){
                Rect r=NodeRect(n);bool active=(preview!=null&&preview.Running&&preview.Current==n)||live==n.id;
                if(selected==n.id||active)EditorGUI.DrawRect(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),active?new Color(.36f,.78f,.52f):new Color(.83f,.68f,.4f));
                EditorGUI.DrawRect(r,new Color(.18f,.21f,.23f));
                Color accent=n.kind==ForestDialogueGraph.Kind.Dialogue?new Color(.23f,.4f,.43f):n.kind==ForestDialogueGraph.Kind.Wait?new Color(.43f,.34f,.25f):new Color(.29f,.31f,.39f);
                EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,28*zoom),accent);
                titleStyle.fontSize=Mathf.Max(10,Mathf.RoundToInt(13*zoom));bodyStyle.fontSize=Mathf.Max(9,Mathf.RoundToInt(12*zoom));smallStyle.fontSize=Mathf.Max(9,Mathf.RoundToInt(11*zoom));
                GUI.Label(new Rect(r.x+10*zoom,r.y+4*zoom,r.width-20*zoom,24*zoom),kinds[(int)n.kind]+"  "+n.title,titleStyle);
                string summary=n.kind==ForestDialogueGraph.Kind.Dialogue?(string.IsNullOrEmpty(n.speaker)?"旁白":n.speaker)+"\n"+(previewChinese?n.zh:n.en):
                    n.kind==ForestDialogueGraph.Kind.Wait?n.seconds.ToString("0.##")+" 秒  ·  "+cues[(int)n.cue]:
                    n.kind==ForestDialogueGraph.Kind.Event?actions[(int)n.action]:n.kind==ForestDialogueGraph.Kind.Branch?"当前是否为叙事预览版？\n是 → 上出口\n否 → 下出口":n.kind==ForestDialogueGraph.Kind.Start?entries[(int)n.entry]:"恢复原有游戏流程";
                GUI.Label(new Rect(r.x+12*zoom,r.y+35*zoom,r.width-26*zoom,r.height-43*zoom),summary,bodyStyle);
                if(n.kind!=ForestDialogueGraph.Kind.Start)Port(Input(n),false);
                if(n.kind!=ForestDialogueGraph.Kind.End)Port(Output(n),true);
                if(n.kind==ForestDialogueGraph.Kind.Branch)Port(Output(n,true),true);
            }
            CanvasInput(area);GUI.EndGroup();
        }
        void Port(Vector2 p,bool output){EditorGUI.DrawRect(new Rect(p.x-6,p.y-6,12,12),output?new Color(.85f,.68f,.38f):new Color(.45f,.72f,.7f));}
        void DrawLink(DuNode n,string id,bool alt){var target=graph.Find(id);if(target!=null)Curve(Output(n,alt),Input(target),alt?new Color(.67f,.55f,.45f):new Color(.45f,.66f,.64f));}
        void Curve(Vector2 a,Vector2 b,Color color){float offset=Mathf.Max(45,Mathf.Abs(b.x-a.x)*.4f);Handles.DrawBezier(a,b,a+Vector2.right*offset,b-Vector2.right*offset,color,null,2);}
        void CanvasInput(Rect area){
            var e=Event.current;bool inside=new Rect(0,0,area.width,area.height).Contains(e.mousePosition);
            if(e.type==EventType.ScrollWheel&&inside){var before=(e.mousePosition-pan)/zoom;zoom=Mathf.Clamp(zoom-e.delta.y*.045f,.35f,1.35f);pan=e.mousePosition-before*zoom;e.Use();Repaint();}
            if(e.type==EventType.KeyDown&&!EditorGUIUtility.editingTextField){
                if(e.keyCode==KeyCode.Escape){connecting=null;preview?.Stop();e.Use();}
                if(e.keyCode==KeyCode.Delete&&!Application.isPlaying){DeleteSelected();e.Use();}
                if(e.control&&e.keyCode==KeyCode.S){Save();e.Use();}
            }
            if(e.type==EventType.MouseDown&&inside){
                if(e.button==2){panning=true;e.Use();return;}
                if(e.button==1&&!Application.isPlaying){var at=(e.mousePosition-pan)/zoom;var menu=new GenericMenu();foreach(ForestDialogueGraph.Kind kind in Enum.GetValues(typeof(ForestDialogueGraph.Kind))){var copy=kind;menu.AddItem(new GUIContent("添加/"+kinds[(int)kind]),false,()=>AddNode(copy,at));}menu.ShowAsContext();e.Use();return;}
                if(e.button!=0)return;
                if(!Application.isPlaying)foreach(var n in graph.nodes){
                    if(connecting!=null&&n.kind!=ForestDialogueGraph.Kind.Start&&Vector2.Distance(e.mousePosition,Input(n))<12){var from=graph.Find(connecting);if(from!=n)Edit("连接对话节点",()=>{if(falsePort)from.otherwise=n.id;else from.next=n.id;});connecting=null;e.Use();return;}
                    if(n.kind!=ForestDialogueGraph.Kind.End&&Vector2.Distance(e.mousePosition,Output(n))<12){connecting=n.id;falsePort=false;e.Use();return;}
                    if(n.kind==ForestDialogueGraph.Kind.Branch&&Vector2.Distance(e.mousePosition,Output(n,true))<12){connecting=n.id;falsePort=true;e.Use();return;}
                }
                var hit=graph.nodes.LastOrDefault(n=>NodeRect(n).Contains(e.mousePosition));
                if(hit!=null){selected=hit.id;dragging=!Application.isPlaying;Undo.IncrementCurrentGroup();dragGroup=Undo.GetCurrentGroup();GUI.FocusControl(null);}
                else {panning=true;GUI.FocusControl(null);}
                e.Use();Repaint();
            }
            if(e.type==EventType.MouseDrag){
                if(panning){pan+=e.delta;e.Use();Repaint();}
                else if(dragging){var n=graph.Find(selected);if(n!=null){Undo.RecordObject(graph,"移动剧情节点");n.position+=e.delta/zoom;EditorUtility.SetDirty(graph);}e.Use();Repaint();}
            }
            if(e.type==EventType.MouseUp){if(dragging)Undo.CollapseUndoOperations(dragGroup);dragging=panning=false;}
        }
        void DrawDetails(){
            GUILayout.Label("节点内容",EditorStyles.boldLabel);var n=graph.Find(selected);
            if(n==null){EditorGUILayout.HelpBox("点选节点，在这里编辑。中文、英文保存在同一个节点中。",MessageType.Info);return;}
            using(new EditorGUI.DisabledScope(Application.isPlaying)){
                EditorGUI.BeginChangeCheck();
                string title=EditorGUILayout.TextField("名称",n.title),speaker=n.speaker,zh=n.zh,en=n.en,note=n.note;
                float seconds=n.seconds;var cue=n.cue;var action=n.action;var entry=n.entry;
                if(n.kind==ForestDialogueGraph.Kind.Start)entry=(ForestDialogueGraph.Entry)EditorGUILayout.Popup("游戏入口",(int)entry,entries);
                if(n.kind==ForestDialogueGraph.Kind.Dialogue){
                    speaker=EditorGUILayout.TextField("说话人",speaker);GUILayout.Label("留空为旁白；现有角色 Father / Gaolequ / Shu",EditorStyles.miniLabel);
                    GUILayout.Label("中文");zh=EditorGUILayout.TextArea(zh??"",GUILayout.MinHeight(65));
                    GUILayout.Label("English");en=EditorGUILayout.TextArea(en??"",GUILayout.MinHeight(75));
                }
                if(n.kind==ForestDialogueGraph.Kind.Wait){seconds=EditorGUILayout.FloatField("等待秒数",seconds);cue=(ForestDialogueGraph.Cue)EditorGUILayout.Popup("已有动作",(int)cue,cues);}
                if(n.kind==ForestDialogueGraph.Kind.Event)action=(ForestDialogueGraph.Action)EditorGUILayout.Popup("已有游戏事件",(int)action,actions);
                if(n.kind==ForestDialogueGraph.Kind.Branch)EditorGUILayout.HelpBox("第一版条件：是否启用 NarrativePreview。上出口=是，下出口=否。",MessageType.None);
                GUILayout.Label("备注（不进字幕）");note=EditorGUILayout.TextArea(note??"",GUILayout.MinHeight(45));
                if(EditorGUI.EndChangeCheck())Edit("编辑剧情节点",()=>{n.title=title;n.speaker=speaker;n.zh=zh;n.en=en;n.note=note;n.seconds=seconds;n.cue=cue;n.action=action;n.entry=entry;});
                if(n.kind!=ForestDialogueGraph.Kind.End)LinkField(n,false);
                if(n.kind==ForestDialogueGraph.Kind.Branch)LinkField(n,true);
                if(GUILayout.Button("删除节点（可撤销）"))DeleteSelected();
            }
        }
        void LinkField(DuNode n,bool alt){
            var candidates=graph.nodes.Where(x=>x!=n&&x.kind!=ForestDialogueGraph.Kind.Start).ToList();
            var names=new[]{"— 未连接 —"}.Concat(candidates.Select(x=>x.title+" ["+kinds[(int)x.kind]+"]")).ToArray();
            string id=alt?n.otherwise:n.next;int current=candidates.FindIndex(x=>x.id==id)+1;
            int chosen=EditorGUILayout.Popup(alt?"否 →":n.kind==ForestDialogueGraph.Kind.Branch?"是 →":"接下来 →",current,names);
            if(chosen!=current)Edit("修改剧情连线",()=>{var target=chosen==0?"":candidates[chosen-1].id;if(alt)n.otherwise=target;else n.next=target;});
        }
        void DrawPreview(){
            GUILayout.Label("预演",EditorStyles.boldLabel);
            previewChinese=GUILayout.Toolbar(previewChinese?0:1,new[]{"中文","English"})==0;
            using(new EditorGUI.DisabledScope(preview!=null&&preview.Running))previewVersion=EditorGUILayout.Toggle("使用叙事预览分支",previewVersion);
            using(new EditorGUILayout.HorizontalScope()){
                if(GUILayout.Button("从选中节点预演"))StartRehearsal();
                if(GUILayout.Button("停止",GUILayout.Width(55)))preview?.Stop();
            }
            if(preview!=null){
                var n=preview.Current;
                EditorGUILayout.HelpBox(n==null?"已停止":!preview.Running?"本段结束":n.kind==ForestDialogueGraph.Kind.Dialogue?
                    (string.IsNullOrEmpty(n.speaker)?"旁白":n.speaker)+"\n"+n.Text(previewChinese):"等待 "+Mathf.Max(0,n.seconds-preview.Elapsed).ToString("0.0")+" 秒",MessageType.None);
                using(new EditorGUI.DisabledScope(!preview.Running||n==null||n.kind!=ForestDialogueGraph.Kind.Dialogue))if(GUILayout.Button("下一句"))preview.Continue();
                if(!string.IsNullOrEmpty(previewEvent))EditorGUILayout.LabelField("事件记录："+previewEvent,EditorStyles.wordWrappedMiniLabel);
            }
            GUILayout.Space(8);
            EditorGUILayout.HelpBox("这里预演台词和计时，事件只显示名称。实机测试会进入森林，并跳到这段剧情；运行时禁止修改图资产。",MessageType.Info);
            using(new EditorGUI.DisabledScope(Application.isPlaying||graph!=AssetDatabase.LoadAssetAtPath<ForestDialogueGraph>(AssetPath))){
                if(GUILayout.Button("实机测试：发现丢刀 → 过河 → 对峙"))PlayInGame(0);
                if(GUILayout.Button("实机测试：直接从署的质问开始"))PlayInGame(1);
            }
        }
        void StartRehearsal(){if(!ValidateGraph())return;var start=graph.Find(selected)??graph.StartFor(ForestDialogueGraph.Entry.MissingKnife);if(start==null)return;
            previewEvent="";preview=new ForestDialogueRunner(graph,()=>previewVersion,n=>{if(n.kind==ForestDialogueGraph.Kind.Event)previewEvent=actions[(int)n.action];});preview.Start(start.id);lastTick=EditorApplication.timeSinceStartup;}
        void AddNode(ForestDialogueGraph.Kind kind,Vector2 at){if(graph==null)return;var n=new DuNode{id=Guid.NewGuid().ToString("N"),kind=kind,title=kinds[(int)kind],position=at,seconds=1};Edit("添加剧情节点",()=>graph.nodes.Add(n));selected=n.id;}
        void DeleteSelected(){var n=graph.Find(selected);if(n==null)return;Edit("删除剧情节点",()=>{graph.nodes.Remove(n);foreach(var other in graph.nodes){if(other.next==n.id)other.next="";if(other.otherwise==n.id)other.otherwise="";}});selected=null;}
        void Edit(string label,System.Action action){preview?.Stop();Undo.RecordObject(graph,label);action();EditorUtility.SetDirty(graph);message="";Repaint();}
        bool ValidateGraph(){var errors=graph.Validate();message=errors.Count==0?"校验通过：连线、双语台词和结束事件完整。":string.Join("  ",errors.Take(2));Repaint();return errors.Count==0;}
        void Save(){if(graph==null||Application.isPlaying)return;AssetDatabase.SaveAssetIfDirty(graph);ValidateGraph();}
        public void FrameAll(){if(graph==null||graph.nodes.Count==0)return;float width=Mathf.Max(550,position.width-380),height=position.height-110;
            float minX=graph.nodes.Min(n=>n.position.x),minY=graph.nodes.Min(n=>n.position.y),maxX=graph.nodes.Max(n=>n.position.x)+224,maxY=graph.nodes.Max(n=>n.position.y)+142;
            zoom=Mathf.Clamp(Mathf.Min(width/(maxX-minX+50),height/(maxY-minY+50)),.35f,1.1f);pan=new Vector2(25-minX*zoom,25-minY*zoom);Repaint();}
        public void FrameEntry(ForestDialogueGraph.Entry entry){
            if(graph==null)return;var start=graph.StartFor(entry);if(start==null)return;
            var found=new List<DuNode>();var visited=new HashSet<string>();var queue=new Queue<DuNode>();queue.Enqueue(start);
            while(queue.Count>0){var n=queue.Dequeue();if(n==null||!visited.Add(n.id))continue;found.Add(n);if(n.kind!=ForestDialogueGraph.Kind.End)queue.Enqueue(graph.Find(n.next));if(n.kind==ForestDialogueGraph.Kind.Branch)queue.Enqueue(graph.Find(n.otherwise));}
            float minX=found.Min(n=>n.position.x),minY=found.Min(n=>n.position.y),maxX=found.Max(n=>n.position.x)+224,maxY=found.Max(n=>n.position.y)+142;
            zoom=Mathf.Clamp(Mathf.Min((position.width-400)/(maxX-minX+40),(position.height-150)/(maxY-minY+40)),.4f,1.1f);
            pan=new Vector2(25-minX*zoom,35-minY*zoom);selected=(found.FirstOrDefault(n=>n.kind==ForestDialogueGraph.Kind.Dialogue)??start).id;Repaint();
        }
        void PlayInGame(int entry){if(!ValidateGraph()||!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;Save();SessionState.SetInt(PreviewRequest,entry);EditorSceneManager.OpenScene("Assets/DongbaPrototype/ObliqueForest.unity");EditorApplication.isPlaying=true;}
        [InitializeOnLoadMethod] static void ResumePreview(){EditorApplication.update-=LaunchPendingPreview;EditorApplication.update+=LaunchPendingPreview;}
        static void LaunchPendingPreview(){int entry=SessionState.GetInt(PreviewRequest,-1);if(entry<0||!EditorApplication.isPlaying)return;var forest=FindFirstObjectByType<ObliqueForest>();if(forest==null||!forest.DialogueGraphPreviewReady)return;SessionState.EraseInt(PreviewRequest);forest.PreviewDialogueGraphCapture(entry==1);}
    }
}
