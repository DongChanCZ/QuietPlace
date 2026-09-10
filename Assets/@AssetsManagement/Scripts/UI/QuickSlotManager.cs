using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ItemType { Flashlight, Bottle, Brick, Card, Fuse, Medicine }

// 습득한 아이템을 획득 순서대로 빈 슬롯에 채운다. 1~6 키로 그 위치를 선택 상태로 만들고,
// 빈 슬롯을 선택하면 아무것도 안 든 상태가 된다 (선택 인덱스는 그대로 유지, 그 자리에 아이템이 없을 뿐).
// 슬롯은 고정 6칸 배열이라 중간 슬롯을 비워도(RemoveItem) 뒤 슬롯들이 앞으로 당겨지지 않는다.
[DefaultExecutionOrder(-900)]
public class QuickSlotManager : MonoBehaviour
{
    public const int SlotCount = 6;

    public static QuickSlotManager Instance { get; private set; }

    public event Action OnSlotsChanged;
    public event Action<ItemType, string, int> OnItemAcquired; // (아이템, 표시 이름, 0-based 슬롯 인덱스)

    readonly ItemType?[] slots = new ItemType?[SlotCount];
    int selectedIndex = -1;
    int lastSelectedIndex = -1; // 넣어놔도(ClearSelection/Tab) 안 지워짐 - Tab으로 다시 꺼낼 슬롯 + 테두리 표시용

    public int SelectedIndex => selectedIndex;
    public int LastSelectedIndex => lastSelectedIndex;

    // F로 실제 사용 가능한 아이템인지 - 습득 메시지 문구 분기 및 사용 로직에 쓰인다.
    public static bool IsUsable(ItemType type) => type == ItemType.Flashlight || type == ItemType.Medicine;

    // 던질 수 있는 아이템인지 - 벽돌/유리병은 항상, 손전등은 배터리가 다 닳았을 때만
    // (배터리가 남아있는 동안은 손전등이 아니라 광원이라 던지는 대상이 아니다).
    public static bool CanThrow(ItemType type, float flashlightBatteryLevel) =>
        type == ItemType.Brick || type == ItemType.Bottle ||
        (type == ItemType.Flashlight && flashlightBatteryLevel <= 0f);

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
        else if (keyboard.tabKey.wasPressedThisFrame) ToggleHold();
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= SlotCount || selectedIndex == index) return;
        selectedIndex = index;
        lastSelectedIndex = index;
        OnSlotsChanged?.Invoke();
    }

    // 손전등 배터리 소진처럼 "들고 있던 걸 놓치는" 상황에 쓴다 - 다시 숫자를 눌러야 재선택됨.
    // lastSelectedIndex는 안 건드린다 - Tab으로 다시 꺼낼 수 있어야 하고 테두리도 계속 보여야 한다.
    public void ClearSelection()
    {
        if (selectedIndex < 0) return;
        selectedIndex = -1;
        OnSlotsChanged?.Invoke();
    }

    // Tab: 들고 있으면 넣고(ClearSelection), 넣어놨으면 마지막으로 선택했던 슬롯을 다시 든다.
    public void ToggleHold()
    {
        if (selectedIndex >= 0) { ClearSelection(); return; }
        if (lastSelectedIndex < 0) return;
        selectedIndex = lastSelectedIndex;
        OnSlotsChanged?.Invoke();
    }

    public bool HasItem(ItemType type) => Array.IndexOf(slots, (ItemType?)type) >= 0;

    public void AddItem(ItemType item, string displayName)
    {
        if (HasItem(item)) return; // 이미 보유 중
        int index = Array.IndexOf(slots, (ItemType?)null);
        if (index < 0) return; // 6칸 다 참
        slots[index] = item;
        OnSlotsChanged?.Invoke();
        OnItemAcquired?.Invoke(item, displayName, index);
    }

    public void RemoveItem(int index)
    {
        if (index < 0 || index >= SlotCount || slots[index] == null) return;
        slots[index] = null;
        OnSlotsChanged?.Invoke();
    }

    // 인덱스를 모르고 종류만 아는 경우(퓨즈 설치 등). 없으면 조용히 무시한다.
    public void RemoveItem(ItemType type)
    {
        int index = Array.IndexOf(slots, (ItemType?)type);
        if (index >= 0) RemoveItem(index);
    }

    public bool TryGetItem(int index, out ItemType item)
    {
        if (index >= 0 && index < SlotCount && slots[index].HasValue) { item = slots[index].Value; return true; }
        item = default;
        return false;
    }

    public bool TryGetSelectedItem(out ItemType item) => TryGetItem(selectedIndex, out item);
}
