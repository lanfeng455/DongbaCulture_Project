using DongbaPrototype;
using TMPro;
using UnityEngine;

public partial class DialogueDisplay {
    [System.Serializable]
    sealed class SpeakerPortraitBinding {
        public string speaker;
        public string[] aliases;
        public Sprite glyph;
        public Color tint=Color.white;
    }
    [Header("说话人头像（关卡现有字形）")]
    [Tooltip("Speaker 与台词表对应；别名仅用于头像，不修改演出指令。白色精灵可用 Tint 染成深褐墨色。")]
    [SerializeField] SpeakerPortraitBinding[] m_speakerPortraits=new SpeakerPortraitBinding[0];
    bool paperStyled,paperAtTop,paperManual;
    TMP_Text paperContinue;
    UnityEngine.UI.Image paperPortrait;RectTransform paperPortraitLeaf;
    string paperPortraitSpeaker;float paperTextLeft=30,paperTextRight=30;
    Vector2 paperLastCanvas;
    public bool ManualContinueVisible=>paperContinue!=null&&paperContinue.gameObject.activeSelf&&m_background.gameObject.activeSelf;
    public RectTransform SubtitlePanel=>m_background!=null?m_background.rectTransform:null;
    internal Sprite PaperSpeakerGlyph=>paperPortrait!=null&&paperPortraitLeaf.gameObject.activeSelf?paperPortrait.sprite:null;
    Sprite ResolvePaperSpeakerGlyph(string speaker,out Color tint){
        tint=Color.white;string key=(speaker??"").Trim();
        if(key.Length==0)return null;
        foreach(var binding in m_speakerPortraits){
            if(binding==null)continue;
            bool match=string.Equals(key,binding.speaker?.Trim(),System.StringComparison.OrdinalIgnoreCase);
            if(!match&&binding.aliases!=null)foreach(var alias in binding.aliases)
                if(string.Equals(key,alias?.Trim(),System.StringComparison.OrdinalIgnoreCase)){match=true;break;}
            if(match&&binding.glyph!=null){tint=binding.tint;return binding.glyph;}
        }
        switch(key.ToLowerInvariant()){
            case "father":case "egaole":case "父亲":case "俄高勒":
                var forest=FindFirstObjectByType<ObliqueForest>();var father=forest!=null?forest.InterfaceFatherSprite:null;
                return father!=null?father:DongbaUIStyle.Glyph("father_glyph");
            case "gao":case "gaolequ":case "高勒趣":return DongbaUIStyle.Glyph("player_glyph");
            case "shu":case "署":return DongbaUIStyle.Glyph("shu_glyph");
            default:return null;
        }
    }
    public void SetPaperPresentation(bool top,bool manual){
        if(UsesHeadsetPresentation)top=false;
        if(m_canvas==null||m_background==null)return;
        bool changed=!paperStyled||paperAtTop!=top||paperManual!=manual;
        paperStyled=true;paperAtTop=top;paperManual=manual;
        if(paperContinue==null){
            var obj=new GameObject("Advance prompt",typeof(RectTransform));obj.transform.SetParent(m_background.transform,false);paperContinue=obj.AddComponent<TextMeshProUGUI>();
            paperContinue.font=ResolveFont();paperContinue.fontSize=16;paperContinue.color=DongbaUIStyle.Accent;paperContinue.alignment=TextAlignmentOptions.BottomRight;paperContinue.raycastTarget=false;
            var rect=paperContinue.rectTransform;rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.pivot=Vector2.zero;rect.offsetMin=new Vector2(26,14);rect.offsetMax=new Vector2(-26,40);
        }
        paperContinue.gameObject.SetActive(manual);
        if(!changed)return;
        m_canvas.sortingOrder=30010;
        m_referenceResolution=new Vector2(1600,900);EnsureCanvasScaler();
        m_widthRatio=.72f;m_minHeightInPixels=142;m_bottomMarginInPixels=34;m_fontSize=27;m_textPadding=new Vector2(30,22);
        m_backgroundColor=DongbaUIStyle.Paper;m_textColor=DongbaUIStyle.Ink;m_speakerColor=DongbaUIStyle.Accent;
        m_background.sprite=DongbaUIStyle.PaperSprite;m_background.type=UnityEngine.UI.Image.Type.Sliced;m_background.raycastTarget=false;
        if(m_background.GetComponent<UnityEngine.UI.Shadow>()==null)DongbaUIStyle.EdgeShadow(m_background,.18f);
        m_text.alignment=TextAlignmentOptions.TopLeft;m_text.fontSize=m_fontSize;m_text.color=m_textColor;m_text.raycastTarget=false;
        UpdatePaperPortrait(true);
        ApplyHeadsetReadability();
        ApplyLayout();if(m_visibleEntry!=null){m_text.text=FormatLine(m_visibleEntry);FitBackgroundToText(m_text.text);}
    }
    void UpdatePaperPortrait(bool force=false){
        if(!paperStyled||m_text==null)return;
        string speaker=m_visibleEntry!=null?m_visibleEntry.speaker:"";
        if(!force&&speaker==paperPortraitSpeaker)return;paperPortraitSpeaker=speaker;
        Sprite glyph=ResolvePaperSpeakerGlyph(speaker,out var tint);
        if(paperPortraitLeaf==null){
            paperPortraitLeaf=DongbaUIStyle.Rect("Speaker cut-paper leaf",m_background.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(-16,13),new Vector2(132,150));
            var leaf=DongbaUIStyle.Plate(paperPortraitLeaf,new Color(.95f,.93f,.87f));DongbaUIStyle.EdgeShadow(leaf,.13f);
            paperPortrait=DongbaUIStyle.Picture(paperPortraitLeaf,"Existing speaker glyph",null,new Vector2(20,-13),new Vector2(90,113));
            // Preserve the same image box, but center wide glyphs inside it when fitting aspect.
            paperPortrait.rectTransform.pivot=new Vector2(.5f,.5f);
            paperPortrait.rectTransform.anchoredPosition=new Vector2(65,-69.5f);
            DongbaUIStyle.Stroke(paperPortraitLeaf,new Vector2(20,-132),new Vector2(90,2),DongbaUIStyle.Accent);
        }
        paperPortraitLeaf.gameObject.SetActive(glyph!=null);paperPortrait.sprite=glyph;paperPortrait.color=tint;
        paperTextLeft=glyph!=null?145:30;paperTextRight=30;
        m_text.rectTransform.offsetMin=new Vector2(paperTextLeft,m_textPadding.y+(paperManual?34:0));
        m_text.rectTransform.offsetMax=new Vector2(-paperTextRight,-m_textPadding.y);
        if(m_visibleEntry!=null)FitBackgroundToText(FormatLine(m_visibleEntry));
    }
    void LateUpdate(){
        if(!paperStyled||m_canvas==null)return;
        // Pause obscures the presentation without consuming or restarting its dialogue.
        m_canvas.enabled=(!UsesHeadsetPresentation||headsetPresentationVisible)&&(DongbaInterface.Instance==null||!DongbaInterface.Instance.ModalOpen);
        UpdatePaperPortrait();
        if(paperContinue!=null)paperContinue.text=UsesHeadsetPresentation?HeadsetContinuePrompt:DongbaLanguage.Chinese?"任意键   继续":"ANY KEY   Continue";
        var size=((RectTransform)m_canvas.transform).rect.size;
        if(size!=paperLastCanvas){paperLastCanvas=size;ApplyLayout();if(m_text!=null)FitBackgroundToText(m_text.text);}
    }
}
