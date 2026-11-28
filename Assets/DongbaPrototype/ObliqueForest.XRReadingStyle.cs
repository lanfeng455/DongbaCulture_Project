using UnityEngine;
using UnityEngine.UI;

namespace DongbaPrototype { public partial class ObliqueForest {
    Font xrReadingFont;
    void XRReadingText(Text label,int size){
        if(xrReadingFont==null)xrReadingFont=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
        if(xrReadingFont!=null)DongbaLocalizedLabel.SetReadingFont(label,xrReadingFont);
        label.fontSize=size;label.fontStyle=FontStyle.Normal;label.lineSpacing=1.16f;
        label.resizeTextForBestFit=false;label.verticalOverflow=VerticalWrapMode.Truncate;
        label.material=xrHudInk;
    }
    void XRTextBox(Text label,float x,float y,float w,float h,int size){
        var r=label.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);
        XRReadingText(label,size);label.alignment=TextAnchor.UpperLeft;
    }
    // Same actions, focus ordering and catalogue; only the headset reading layout
    // changes. The glyph has its own generous cell instead of colliding with text.
    void LayoutXRInvitations(){
        bool catalog=naturePage==NaturePage.Plants||naturePage==NaturePage.Animals||naturePage==NaturePage.Sleeping||naturePage==NaturePage.Companions;
        bool placing=naturePage==NaturePage.Position||naturePage==NaturePage.Remove;
        float height=catalog?670:placing?460:550;
        naturePanel.sizeDelta=new Vector2(900,height);
        natureContents.anchoredPosition=new Vector2(34,-188);natureContents.sizeDelta=new Vector2(832,356);
        XRTextBox(natureTitle,34,-22,832,58,36);
        XRTextBox(natureDetail,34,-92,832,88,26);
        XRTextBox(natureStatus,34,-height+90,832,80,24);
        foreach(var button in natureButtons){
            var r=(RectTransform)button.transform;var old=r.anchoredPosition;var size=r.sizeDelta;
            float x=old.x/486*832,w=size.x/486*832,y=old.y,h=size.y;
            if(catalog){
                if(size.y==49){x=old.x>0?428:0;w=404;y=old.y/55*88;h=76;}
                else if(size.y==34){x=428;w=404;y=-270;h=50;}
                else{y=-326;h=48;}
            }else if(placing){y=old.y/64*78;h=62;}
            else {y=old.y==0?0:old.y==-76?-96:old.y==-145?-196:old.y;h=size.y>=52?78:50;}
            r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);
            var focus=DongbaUIStyle.Stroke(r,new Vector2(0,-8),new Vector2(4,h-16),uiAccent);focus.name="Reading focus";focus.material=xrHudInk;
            focus.gameObject.SetActive(false);
            var icon=r.Find("Dictionary glyph") as RectTransform;
            if(icon!=null){icon.anchoredPosition=new Vector2(16,-10);icon.sizeDelta=new Vector2(64,h-20);}
            var label=button.GetComponentInChildren<Text>();
            XRTextBox(label,icon!=null?100:18,-4,w-(icon!=null?116:36),h-8,26);label.alignment=TextAnchor.MiddleLeft;
            foreach(var g in button.GetComponentsInChildren<Graphic>(true))g.material=xrHudInk;
        }
    }
    void LayoutXRWelcome(bool success){
        natureWelcome.sizeDelta=new Vector2(900,success?300:430);
        natureWelcome.anchoredPosition=new Vector2(0,-290);natureWelcome.localScale=Vector3.one;
        XRTextBox(natureWelcomeEyebrow,38,-28,824,38,24);
        XRTextBox(natureWelcomeTitle,38,-88,650,108,38);
        XRTextBox(natureWelcomeCopy,38,success?-202:-210,824,90,28);
        natureWelcomeGlyph.rectTransform.anchoredPosition=new Vector2(-38,-80);
        natureWelcomeGlyph.rectTransform.sizeDelta=new Vector2(118,114);
        var first=(RectTransform)natureWelcomeAccept.transform;first.anchoredPosition=new Vector2(38,-338);first.sizeDelta=new Vector2(500,64);
        var later=(RectTransform)natureWelcomeLater.transform;later.anchoredPosition=new Vector2(562,-338);later.sizeDelta=new Vector2(300,64);
        natureWelcomeAcceptText.rectTransform.sizeDelta=new Vector2(470,56);XRReadingText(natureWelcomeAcceptText,28);
        natureWelcomeLaterText.rectTransform.sizeDelta=new Vector2(270,56);XRReadingText(natureWelcomeLaterText,28);
        natureWelcomeEyebrow.text=success?N("AN INVITATION, ANSWERED","一次邀请，一次回应"):N("A NEW POSSIBILITY","新的能力");
        natureWelcomeTitle.text=success?N("The forest answered.","森林回应了你。"):N("Invite the living script","邀万物入画");
        natureWelcomeCopy.text=success?N("Keep shaping your forest. X undoes an invitation.","继续布置你的森林。按 X 可以撤销邀请。"):N("Welcome a plant or an animal.\nChoose where it belongs.","邀请植物与动物，\n由你决定它们在哪里生活。");
        natureWelcomeAcceptText.text=N("A   Invite a plant","A  邀请一株植物");
        natureWelcomeLaterText.text=N("B   Later","B  稍后");
    }
}}
