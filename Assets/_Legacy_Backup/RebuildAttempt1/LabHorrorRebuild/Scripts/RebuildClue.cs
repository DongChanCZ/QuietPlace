using UnityEngine;
public sealed class RebuildClue:MonoBehaviour,IRebuildInteractable
{public enum Kind{LobbyMap,TornDocument}public Kind kind;bool inspected;public string Prompt(RebuildPlayer p)=>inspected?"이미 조사함":kind==Kind.LobbyMap?"시설 안내도 조사":"찢어진 문서 조사";public void Interact(RebuildPlayer p){if(inspected)return;inspected=true;if(kind==Kind.LobbyMap){RebuildGame.Instance.Notify("안내도: 비상계단은 로비 오른쪽에 있습니다");RebuildGame.Instance.SetObjective("로비 반대편의 찢어진 문서를 찾으세요");}else{RebuildGame.Instance.Notify("문서에 2층 기록보관실이 표시되어 있습니다");RebuildGame.Instance.SetObjective("비상계단을 통해 2층으로 이동하세요");}}}
