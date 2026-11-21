using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace DongbaPrototype {
// A chapter note stays readable, but returns space to the forest after being read.
public sealed class DongbaChapterTab:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler {
    RectTransform panel,health,ribbon;Text title,detail,mark;CanvasGroup detailGroup;
    float revealUntil,amount=1;bool hovering,pinned;int lastToggleFrame=-1;
    string measuredTitle,measuredDetail;Font measuredTitleFont,measuredDetailFont;float titleHeight=36,detailHeight=30;
    public bool Expanded=>amount>.95f;
    public void Initialize(RectTransform p,RectTransform h,RectTransform r,Text t,Text d,Text m){panel=p;health=h;ribbon=r;title=t;detail=d;mark=m;detailGroup=d.gameObject.AddComponent<CanvasGroup>();Reveal();}
    public void Reveal(){revealUntil=Time.time+6;pinned=false;}
    public void Toggle(){if(lastToggleFrame==Time.frameCount)return;lastToggleFrame=Time.frameCount;pinned=!(pinned||Time.time<revealUntil);revealUntil=0;}
    public void OnPointerEnter(PointerEventData e){hovering=true;}public void OnPointerExit(PointerEventData e){hovering=false;}
    public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Toggle();}
    void OnEnable(){Reveal();}
    void OnDisable(){hovering=false;}
    void LateUpdate(){
        if(panel==null)return;
        bool open=hovering||pinned||Time.time<revealUntil;
        amount=Mathf.MoveTowards(amount,open?1:0,Time.unscaledDeltaTime*5);
        if(measuredTitle!=title.text||measuredDetail!=detail.text||measuredTitleFont!=title.font||measuredDetailFont!=detail.font){
            measuredTitle=title.text;measuredDetail=detail.text;measuredTitleFont=title.font;measuredDetailFont=detail.font;
            titleHeight=Mathf.Max(36,title.preferredHeight);detailHeight=Mathf.Max(30,detail.preferredHeight);
        }
        title.rectTransform.sizeDelta=new Vector2(title.rectTransform.sizeDelta.x,titleHeight);
        float bottom=46+titleHeight;
        detail.rectTransform.anchoredPosition=new Vector2(71,-bottom-8);detail.rectTransform.sizeDelta=new Vector2(detail.rectTransform.sizeDelta.x,detailHeight);
        detailGroup.alpha=amount;detailGroup.blocksRaycasts=false;
        float smooth=amount*amount*(3-2*amount),height=bottom+17+(string.IsNullOrEmpty(detail.text)?0:(detailHeight+8)*smooth);
        panel.sizeDelta=new Vector2(panel.sizeDelta.x,height);ribbon.sizeDelta=new Vector2(40,Mathf.Clamp(height-21,78,106));
        health.anchoredPosition=new Vector2(34,-28-height-12);mark.text=open?"−":"+";
    }
}
}
