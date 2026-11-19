using DongbaPrototype;
using TMPro;
using UnityEngine;

public partial class DialogueDisplay {
    bool paperStyled,paperAtTop,paperManual;
    TMP_Text paperContinue;
    Vector2 paperLastCanvas;
    public bool ManualContinueVisible=>paperContinue!=null&&paperContinue.gameObject.activeSelf&&m_background.gameObject.activeSelf;
    public RectTransform SubtitlePanel=>m_background!=null?m_background.rectTransform:null;
    public void SetPaperPresentation(bool top,bool manual){
        if(m_canvas==null||m_background==null)return;
        bool changed=!paperStyled||paperAtTop!=top||paperManual!=manual;
        paperStyled=true;paperAtTop=top;paperManual=manual;
        if(paperContinue==null){
            var obj=new GameObject("Advance prompt",typeof(RectTransform));obj.transform.SetParent(m_background.transform,false);paperContinue=obj.AddComponent<TextMeshProUGUI>();
            paperContinue.font=ResolveFont();paperContinue.fontSize=17;paperContinue.color=DongbaUIStyle.Muted;paperContinue.alignment=TextAlignmentOptions.BottomRight;paperContinue.raycastTarget=false;
            var rect=paperContinue.rectTransform;rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.pivot=Vector2.zero;rect.offsetMin=new Vector2(26,14);rect.offsetMax=new Vector2(-26,40);
        }
        paperContinue.gameObject.SetActive(manual);
        if(!changed)return;
        m_canvas.sortingOrder=30010;
        m_referenceResolution=new Vector2(1600,900);EnsureCanvasScaler();
        m_widthRatio=.72f;m_minHeightInPixels=104;m_bottomMarginInPixels=28;m_fontSize=27;m_textPadding=new Vector2(28,20);
        m_backgroundColor=DongbaUIStyle.Paper;m_textColor=DongbaUIStyle.Ink;m_speakerColor=DongbaUIStyle.Accent;
        m_background.sprite=DongbaUIStyle.PaperSprite;m_background.type=UnityEngine.UI.Image.Type.Sliced;m_background.raycastTarget=false;
        m_text.alignment=TextAlignmentOptions.TopLeft;m_text.fontSize=m_fontSize;m_text.color=m_textColor;m_text.raycastTarget=false;
        m_text.rectTransform.offsetMin=new Vector2(m_textPadding.x,m_textPadding.y+(manual?34:0));
        m_text.rectTransform.offsetMax=-m_textPadding;
        ApplyLayout();if(m_visibleEntry!=null){m_text.text=FormatLine(m_visibleEntry);FitBackgroundToText(m_text.text);}
    }
    void LateUpdate(){
        if(!paperStyled||m_canvas==null)return;
        // Pause obscures the presentation without consuming or restarting its dialogue.
        m_canvas.enabled=DongbaInterface.Instance==null||!DongbaInterface.Instance.ModalOpen;
        if(paperContinue!=null)paperContinue.text=DongbaLanguage.Chinese?"回车   继续":"ENTER   Continue";
        var size=((RectTransform)m_canvas.transform).rect.size;
        if(size!=paperLastCanvas){paperLastCanvas=size;ApplyLayout();if(m_text!=null)FitBackgroundToText(m_text.text);}
    }
}
