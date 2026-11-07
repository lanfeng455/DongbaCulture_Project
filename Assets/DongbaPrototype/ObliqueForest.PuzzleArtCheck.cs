using System.Collections;
using System.IO;
using UnityEngine;
using Ivan.Puzzle;
using Ivan.Diorama;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckPuzzleArt(){
        var report="";
        var pair=FindFirstObjectByType<EntangledSnakePair>();if(pair!=null)pair.Split();yield return new WaitForSeconds(.6f);
        var snakes=FindObjectsByType<SnakeActor>(FindObjectsInactive.Include,FindObjectsSortMode.None);if(snakes.Length<2){File.WriteAllText("Verification/journey-reel/snake-checks.txt","Missing snakes="+snakes.Length);Application.Quit();yield break;}
        foreach(var d in FindObjectsByType<PuzzleDirectorBase>(FindObjectsSortMode.None))d.enabled=false;
        var controller=FindFirstObjectByType<DioramaPlayerController>();controller.enabled=false;var camera=Camera.main;
        var snake=snakes[0];camera.transform.position=snake.transform.position+new Vector3(0,17,-20);
        var field=typeof(SnakeActor).GetField("m_image",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        foreach(var a in snakes){a.EnterFree();var r=(SpriteRenderer)field.GetValue(a);report+=a.name+" root="+a.transform.position+" art="+r.transform.position+" child="+r.transform.IsChildOf(a.transform)+" shader="+r.sharedMaterial.shader.name+" instancing="+r.sharedMaterial.enableInstancing+"\n";}
        foreach(var a in snakes)foreach(var r in a.GetComponentsInChildren<SpriteRenderer>(true))report+="RENDER "+a.name+"/"+r.name+" at="+r.transform.position+" local="+r.transform.localPosition+" shader="+r.sharedMaterial.shader.name+" alpha="+r.color.a+"\n";
        yield return new WaitForSeconds(1);yield return new WaitForEndOfFrame();camera.transform.position=new Vector3(snake.transform.position.x,17,snake.transform.position.z-20);WriteJourneyFrame("Verification/journey-reel/snake-before.png");
        report+="Entangled appearance hidden="+(pair==null||!pair.gameObject.activeSelf)+"\n";
        yield return new WaitForSeconds(1);yield return new WaitForEndOfFrame();camera.transform.position=new Vector3(snake.transform.position.x,17,snake.transform.position.z-20);WriteJourneyFrame("Verification/journey-reel/snake-separated.png");
        var body=snake.GetComponent<Rigidbody>();var art=(SpriteRenderer)field.GetValue(snake);var start=body.position;var artStart=art.transform.position;body.AddForce(Vector3.right*8,ForceMode.Impulse);yield return new WaitForSeconds(1);
        report+="Snake visible="+(art.color.a>.99f)+"; appearance follows body="+((body.position-start-(art.transform.position-artStart)).magnitude<.02f)+"\n";
        report+="Snake body delta="+(body.position-start)+" art delta="+(art.transform.position-artStart)+"\n";
        yield return new WaitForEndOfFrame();camera.transform.position=new Vector3(start.x,17,start.z-20);WriteJourneyFrame("Verification/journey-reel/snake-pushed.png");File.WriteAllText("Verification/journey-reel/snake-checks.txt",report);Application.Quit();
    }
}}
