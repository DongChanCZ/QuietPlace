using UnityEngine;
using UnityEngine.UI;

// 선택된(든) 아이템으로 뭘 할 수 있는지 슬롯 바 위에 여러 줄로 안내한다.
// 줄 순서는 고정: Tab(넣기/꺼내기) -> Q(던지기) -> F(사용하기). 해당 안 되는 줄은 건너뛴다.
public class HeldItemHintUI : MonoBehaviour
{
    [SerializeField] PlayerInteractor player;
    [SerializeField] Text hintText;

    void OnEnable()
    {
        if (QuickSlotManager.Instance != null) QuickSlotManager.Instance.OnSlotsChanged += Refresh;
        // 손전등 켜기/끄기는 QuickSlotManager를 안 거치므로 따로 구독해야 F 문구가 바로 바뀐다.
        if (player != null) player.OnFlashlightToggled += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (QuickSlotManager.Instance != null) QuickSlotManager.Instance.OnSlotsChanged -= Refresh;
        if (player != null) player.OnFlashlightToggled -= Refresh;
    }

    void Refresh()
    {
        QuickSlotManager manager = QuickSlotManager.Instance;
        if (hintText == null) return;

        if (manager == null)
        {
            hintText.enabled = false;
            return;
        }

        // 들고 있는(선택된) 상태 - Tab을 누르면 넣는다. Q/F는 든 물건 기준으로 뜬다.
        ItemType item;
        if (manager.TryGetItem(manager.SelectedIndex, out item))
        {
            string text = "Tab  넣기";
            if (QuickSlotManager.CanThrow(item, player != null ? player.BatteryLevel : 0f))
                text += "\nQ  던지기";
            string fLine = UseLineFor(item);
            if (fLine != null)
                text += "\n" + fLine;

            hintText.text = text;
            hintText.enabled = true;
            return;
        }

        // 넣어둔 상태 - Tab을 누르면 다시 꺼낸다. 단, 그 자리에 실제로 뭔가 있을 때만
        // 보여준다 - 빈 슬롯을 눌러놨거나 마지막으로 든 게 이미 사라졌으면 꺼낼 게 없다.
        if (manager.SelectedIndex < 0 && manager.TryGetItem(manager.LastSelectedIndex, out item))
        {
            hintText.text = "Tab  꺼내기";
            hintText.enabled = true;
            return;
        }

        // 빈 슬롯(선택은 했지만 아이템이 없음)이면 아무 문구도 안 띄운다.
        hintText.enabled = false;
    }

    /// F 줄 문구. QuickSlotManager.IsUsable은 종류만 보지만, 배터리 없는 손전등처럼
    /// F를 눌러도 실제로는 아무 일도 안 일어나는 경우가 있다 - 그런 건 null로 뺀다.
    /// 종류별로 실제로 F가 하는 일을 그대로 문구에 담는다(켜기/끄기, 먹기 등).
    string UseLineFor(ItemType item)
    {
        if (!QuickSlotManager.IsUsable(item)) return null;

        if (item == ItemType.Flashlight)
        {
            if (player != null && player.BatteryLevel <= 0f) return null;
            bool on = player != null && player.FlashlightEnabled;
            return on ? "F  손전등 끄기" : "F  손전등 켜기";
        }

        if (item == ItemType.Medicine) return "F  약 먹기";

        return "F  물건 사용하기"; // 앞으로 IsUsable에 새 종류가 추가됐을 때의 기본값
    }
}
