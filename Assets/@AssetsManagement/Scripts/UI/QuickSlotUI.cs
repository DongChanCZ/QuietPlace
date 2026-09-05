using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct ItemIcon
{
    public ItemType type;
    public Sprite sprite;
}

// QuickSlotManager의 상태를 6개 슬롯 아이콘에 그대로 반영한다.
// 아이템이 없는 슬롯은 아이콘을 꺼두고(Image.enabled=false), 있는 슬롯은
// 선택 여부에 따라 흐리게/작게 또는 진하게/크게 보여준다.
public class QuickSlotUI : MonoBehaviour
{
    [SerializeField] ItemIcon[] itemIcons;
    [Range(0f, 1f)][SerializeField] float unselectedAlpha = 0.35f;
    [SerializeField] float selectedScale = 2.1f;

    Image[] icons;

    void Awake()
    {
        icons = new Image[QuickSlotManager.SlotCount];
        for (int i = 0; i < icons.Length; i++)
            icons[i] = transform.GetChild(i).GetChild(0).GetComponent<Image>();
    }

    void OnEnable()
    {
        if (QuickSlotManager.Instance != null) QuickSlotManager.Instance.OnSlotsChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (QuickSlotManager.Instance != null) QuickSlotManager.Instance.OnSlotsChanged -= Refresh;
    }

    void Refresh()
    {
        QuickSlotManager manager = QuickSlotManager.Instance;
        for (int i = 0; i < icons.Length; i++)
        {
            if (manager == null || !manager.TryGetItem(i, out ItemType slotItem))
            {
                icons[i].enabled = false;
                continue;
            }

            bool selected = manager.SelectedIndex == i;
            icons[i].enabled = true;
            icons[i].sprite = FindSprite(slotItem);
            Color c = icons[i].color;
            c.a = selected ? 1f : unselectedAlpha;
            icons[i].color = c;
            icons[i].rectTransform.localScale = selected ? Vector3.one * selectedScale : Vector3.one;
        }
    }

    Sprite FindSprite(ItemType type)
    {
        for (int i = 0; i < itemIcons.Length; i++)
            if (itemIcons[i].type == type) return itemIcons[i].sprite;
        return null;
    }
}
