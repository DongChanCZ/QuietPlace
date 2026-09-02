using UnityEngine;
public sealed class RebuildGame:MonoBehaviour
{
 public static RebuildGame Instance{get;private set;} public string Objective{get;private set;}="정문을 통해 경비실로 들어가세요"; public string Notice{get;private set;}=""; float until;
 void Awake()=>Instance=this; public void SetObjective(string v)=>Objective=v; public void Notify(string v){Notice=v;until=Time.time+2.5f;} public bool ShowNotice=>Time.time<until;
}
