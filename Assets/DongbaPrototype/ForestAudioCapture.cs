using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace DongbaPrototype {
// Diagnostic only: instantiated by --forest-audio-check / --forest-audio-preview.
// Audio is the real listener mix, not the pre-exported source clips.
public sealed class ForestAudioCapture : MonoBehaviour {
    float[] samples;int written,rate,channels=2;volatile bool recording;
    string folder;double began;int frameIndex,pending;RenderTexture screen,small;bool fastFrames;
    readonly List<string> frames=new List<string>();
    public void Begin(string output,bool video,bool uncompressedFrames=false){
        fastFrames=uncompressedFrames;folder=output;Directory.CreateDirectory(folder);rate=AudioSettings.outputSampleRate;
        samples=new float[rate*2*180];written=0;began=AudioSettings.dspTime;recording=true;
        if(video)StartCoroutine(CaptureFrames());
    }
    void OnAudioFilterRead(float[] data,int count){
        if(!recording)return;channels=count;int offset=written;
        int n=Math.Min(data.Length,samples.Length-offset);
        if(n>0){Array.Copy(data,0,samples,offset,n);Volatile.Write(ref written,offset+n);}
    }
    IEnumerator CaptureFrames(){
        Directory.CreateDirectory(folder+"/frames");screen=new RenderTexture(Screen.width,Screen.height,0);
        small=new RenderTexture(960,540,0,RenderTextureFormat.ARGB32);screen.Create();small.Create();
        double next=AudioSettings.dspTime;
        while(recording){
            yield return new WaitForEndOfFrame();double now=AudioSettings.dspTime;if(now<next||pending>2)continue;
            next=now+1.0/30;int index=frameIndex++;double stamp=now-began;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);Graphics.Blit(screen,small);Interlocked.Increment(ref pending);
            AsyncGPUReadback.Request(small,0,TextureFormat.RGBA32,r=>{
                // Recording a camera move must not stall it with PNG compression on the main thread.
                // BMPs are temporary diagnostic output; timestamps preserve real playback speed.
                if(fastFrames&&!r.hasError){
                    var pixels=r.GetData<byte>().ToArray();string path=folder+"/frames/"+index.ToString("D5")+".bmp";
                    frames.Add(index+","+stamp.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
                    ThreadPool.QueueUserWorkItem(_=>{try{WriteFrameBitmap(path,pixels);}finally{Interlocked.Decrement(ref pending);}});return;
                }
                try{if(r.hasError)return;var png=ImageConversion.EncodeNativeArrayToPNG(r.GetData<byte>(),UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,960,540);
                    File.WriteAllBytes(folder+"/frames/"+index.ToString("D5")+".png",png.ToArray());png.Dispose();frames.Add(index+","+stamp.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
                }finally{Interlocked.Decrement(ref pending);}
            });
        }
    }
    static void WriteFrameBitmap(string path,byte[] rgba){
        for(int i=0;i<rgba.Length;i+=4){byte red=rgba[i];rgba[i]=rgba[i+2];rgba[i+2]=red;}
        using(var w=new BinaryWriter(File.Create(path))){
            w.Write((ushort)0x4d42);w.Write(54+rgba.Length);w.Write(0);w.Write(54);
            w.Write(40);w.Write(960);w.Write(540);w.Write((ushort)1);w.Write((ushort)32);
            w.Write(0);w.Write(rgba.Length);w.Write(0);w.Write(0);w.Write(0);w.Write(0);w.Write(rgba);
        }
    }
    public IEnumerator Finish(){
        recording=false;yield return null;while(pending>0)yield return null;
        if(screen!=null){screen.Release();Destroy(screen);small.Release();Destroy(small);}
        int count=Volatile.Read(ref written);double energy=0;float peak=0;int clipped=0;
        using(var w=new BinaryWriter(File.Create(folder+"/game-mix.wav"))){
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)channels);w.Write(rate);w.Write(rate*channels*2);w.Write((short)(channels*2));w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
            for(int i=0;i<count;i++){float s=samples[i];peak=Mathf.Max(peak,Mathf.Abs(s));energy+=s*s;if(Mathf.Abs(s)>=.999f)clipped++;w.Write((short)(Mathf.Clamp(s,-1,1)*32767));}
        }
        File.WriteAllText(folder+"/mix-metrics.txt","samples="+count+"\nsampleRate="+rate+"\nchannels="+channels+"\nseconds="+(count/(double)(rate*channels))+"\npeak="+peak+"\nrms="+Math.Sqrt(energy/Math.Max(1,count))+"\nclipped="+clipped+"\n");
        File.WriteAllLines(folder+"/frames.csv",frames);samples=null;
    }
}
}
