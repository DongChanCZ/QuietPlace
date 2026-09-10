using UnityEngine;

// 퀵슬롯 테스트용 - 지정한 ItemType으로 퀵슬롯에 등록된다.
[DisallowMultipleComponent]
public sealed class TestItemPickup : Interactable
{
    [SerializeField] ItemType itemType;
    [SerializeField] string displayName = "테스트 아이템";

    /// GameCheckpoint 가 되살릴 때 슬롯에 있던 종류와 맞춰보려고 읽는다.
    public ItemType ItemType => itemType;

    // 이미 같은 종류를 들고 있으면 조준 대상이 아니다 (Brick/Flashlight/Keycard와 동일 패턴).
    public override bool Available(IInteractor player) =>
        QuickSlotManager.Instance == null || !QuickSlotManager.Instance.HasItem(itemType);

    public override string Prompt(IInteractor player)
    {
        if (QuickSlotManager.Instance != null && QuickSlotManager.Instance.HasItem(itemType)) return null;
        return "E  " + displayName + " 획득";
    }

    public override void Interact(IInteractor player)
    {
        QuickSlotManager.Instance?.AddItem(itemType, displayName);
        gameObject.SetActive(false);
    }
}
