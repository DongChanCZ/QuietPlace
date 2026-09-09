using UnityEngine;

[DisallowMultipleComponent]
public sealed class BrickPickup : Interactable
{
    [SerializeField] string displayName = "벽돌";

    // 이미 벽돌을 들고 있으면 프롬프트 자체를 안 띄운다 (Flashlight/Keycard와 동일 패턴).
    // 오브젝트는 사라지지 않으므로, 들고 있던 벽돌을 던져서 비우면 다시 여기서 얻을 수 있다.
    /// 집어 가는 물건은 뒤/속에 있는 기계보다 먼저 잡혀야 한다.
    public override int AimPriority => 10;

    /// 이미 벽돌을 들고 있으면 조준 대상이 아니다.
    public override bool Available(IInteractor player) =>
        QuickSlotManager.Instance == null || !QuickSlotManager.Instance.HasItem(ItemType.Brick);

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
