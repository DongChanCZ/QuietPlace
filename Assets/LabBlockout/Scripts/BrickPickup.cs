using UnityEngine;

[DisallowMultipleComponent]
public sealed class BrickPickup : Interactable
{
    [SerializeField] string displayName = "벽돌";

    // 이미 벽돌을 들고 있으면 프롬프트 자체를 안 띄운다 (Flashlight/Keycard와 동일 패턴).
    // 오브젝트는 사라지지 않으므로, 들고 있던 벽돌을 던져서 비우면 다시 여기서 얻을 수 있다.
    public override string Prompt(IInteractor player)
    {
        if (QuickSlotManager.Instance != null && QuickSlotManager.Instance.HasItem(ItemType.Brick)) return null;
        return "E  " + displayName + " 획득";
    }

    public override void Interact(IInteractor player)
    {
        QuickSlotManager.Instance?.AddItem(ItemType.Brick, displayName);
    }
}
