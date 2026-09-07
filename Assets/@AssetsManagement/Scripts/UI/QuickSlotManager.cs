using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ItemType { Flashlight, Bottle, Brick, Card, Fuse, Medicine }

// 습득한 아이템을 획득 순서대로 보관한다. 1~6 키로 그 위치를 선택 상태로 만들고,
// 빈 슬롯을 선택하면 아무것도 안 든 상태가 된다 (선택 인덱스는 그대로 유지, 그 자리에 아이템이 없을 뿐).
[DefaultExecutionOrder(-900)]
public class QuickSlotManager : MonoBehaviour
{
    public const int SlotCount = 6;

    public static QuickSlotManager Instance { get; private set; }

    public event Action OnSlotsChanged;
    public event Action<ItemType, string, int> OnItemAcquired; // (아이템, 표시 이름, 0-based 슬롯 인덱스)

    readonly List<ItemType> items = new List<ItemType>();
    int selectedIndex = -1;

    public int SelectedIndex => selectedIndex;

    // F로 실제 사용 가능한 아이템인지 - 습득 메시지 문구 분기에 쓰인다.
    public static bool IsUsable(ItemType type) => type == ItemType.Flashlight || type == ItemType.Medicine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame) SelectSlot(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) SelectSlot(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) SelectSlot(2);
        else if (keyboard.digit4Key.wasPressedThisFrame) SelectSlot(3);
        else if (keyboard.digit5Key.wasPressedThisFrame) SelectSlot(4);
        else if (keyboard.digit6Key.wasPressedThisFrame) SelectSlot(5);
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= SlotCount || selectedIndex == index) return;
        selectedIndex = index;
        OnSlotsChanged?.Invoke();
    }

    public void AddItem(ItemType item, string displayName)
    {
        if (items.Count >= SlotCount || items.Contains(item)) return;
        items.Add(item);
        int index = items.Count - 1;
        OnSlotsChanged?.Invoke();
        OnItemAcquired?.Invoke(item, displayName, index);
    }

    public bool TryGetItem(int index, out ItemType item)
    {
        if (index >= 0 && index < items.Count) { item = items[index]; return true; }
        item = default;
        return false;
    }

    public bool TryGetSelectedItem(out ItemType item) => TryGetItem(selectedIndex, out item);
}
