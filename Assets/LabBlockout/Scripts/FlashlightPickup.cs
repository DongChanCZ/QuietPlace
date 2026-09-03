using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlashlightPickup : Interactable
{
    [SerializeField] string displayName = "손전등";
    public string DisplayName => displayName;

    public override string Prompt(IInteractor player)
    {
        return player != null && player.HasFlashlight ? null : "E  " + displayName + " 획득";
    }

    public override void Interact(IInteractor player)
    {
        Collect(player);
    }

    public void Collect(IInteractor player)
    {
        if (player == null || player.HasFlashlight) return;
        player.GiveFlashlight();
        gameObject.SetActive(false);
    }
}
