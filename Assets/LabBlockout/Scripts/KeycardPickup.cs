using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeycardPickup : Interactable
{
    [SerializeField] string displayName = "카드키";

    public override string Prompt(IInteractor player)
    {
        return player != null && player.HasKeycard ? null : "E  " + displayName + " 획득";
    }

    public override void Interact(IInteractor player)
    {
        if (player == null || player.HasKeycard) return;
        player.GiveKeycard();
        gameObject.SetActive(false);
    }
}
