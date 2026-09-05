using UnityEngine;

// 퀵슬롯 테스트용 - 지정한 ItemType으로 퀵슬롯에 등록된다.
[DisallowMultipleComponent]
public sealed class TestItemPickup : Interactable
{
    [SerializeField] ItemType itemType;
    [SerializeField] string displayName = "테스트 아이템";

    public override string Prompt(IInteractor player) => "E  " + displayName + " 획득";

    public override void Interact(IInteractor player)
    {
        QuickSlotManager.Instance?.AddItem(itemType);
        gameObject.SetActive(false);
    }
}
