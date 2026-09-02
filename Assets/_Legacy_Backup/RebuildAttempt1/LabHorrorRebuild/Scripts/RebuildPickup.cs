using UnityEngine;
public sealed class RebuildPickup:MonoBehaviour,IRebuildInteractable
{public enum Kind{Keycard,Flashlight}public Kind kind;public string Prompt(RebuildPlayer p)=>kind==Kind.Keycard?"카드키 획득":"손전등 획득";public void Interact(RebuildPlayer p){if(kind==Kind.Keycard){p.GiveKeycard();RebuildGame.Instance.Notify("카드키를 획득했습니다");}else{p.GiveFlashlight();RebuildGame.Instance.Notify("손전등 획득 — F키로 사용");}gameObject.SetActive(false);}}
