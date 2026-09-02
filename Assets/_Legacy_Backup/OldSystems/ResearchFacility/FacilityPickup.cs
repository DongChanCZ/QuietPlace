using UnityEngine;

public sealed class FacilityPickup : MonoBehaviour
{
    public enum PickupKind { Keycard, Flashlight }

    [SerializeField] PickupKind kind;
    [SerializeField] string displayName = "아이템";
    public string DisplayName => displayName;

    public void Configure(PickupKind pickupKind, string itemName)
    {
        kind = pickupKind;
        displayName = itemName;
    }

    public void Collect(FacilityPlayerController player)
    {
        if (player == null) return;
        if (kind == PickupKind.Keycard) player.CollectKeycard();
        else player.CollectFlashlight();
        Destroy(gameObject);
    }
}
