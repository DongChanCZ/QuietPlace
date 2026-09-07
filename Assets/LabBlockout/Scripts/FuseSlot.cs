using UnityEngine;

/// The empty bay on the 2F switchboard. Points at the part it is waiting for,
/// so no shared carrying state has to be added to IInteractor for one item.
///
/// Named for the fuse it first held. The part is data, not code - itemName and
/// the pickup reference decide what actually goes in, so the same pair works for
/// the transformer module without a second script.
[DisallowMultipleComponent]
public sealed class FuseSlot : Interactable
{
    [SerializeField] FusePickup part;
    [SerializeField] Transform anchor;
    [SerializeField] string displayName = "배전반";
    [SerializeField] string itemName = "변압기";

    /// World rotation the part takes in the bay. Absolute, not a turn relative
    /// to however it happened to be lying in storage - where it sits on a shelf
    /// has nothing to do with how it seats in the slot.
    [SerializeField] Vector3 installEuler = new Vector3(0f, 90f, 0f);

    /// 켜면 곧바로 꽂히지 않고 배전반 수리 미니게임(차단기 내리기 -> 잔류전하 방전 ->
    /// 각도 맞춰 삽입 -> 차단기 올리기)을 먼저 통과해야 설치된다.
    /// 끄면 기존과 동일하게 즉시 설치된다.
    [SerializeField] bool requiresRepair;

    /// Whether the bay is seated. The breaker reads this - it trips back down
    /// on an empty bay - and this is the only thing outside needs to know.
    public bool Filled { get { return part != null && part.Installed; } }

    /// The plate bolted over the bay, if there is one. Nothing can be seated
    /// through it, so the prompt has to say that rather than silently doing
    /// nothing when the player presses E on a covered slot.
    [SerializeField] BoltedPanel cover;

    void Awake()
    {
        Debug.Assert(part && anchor, "FuseSlot is missing its part or anchor.");
    }

    public override string Prompt(IInteractor player)
    {
        if (part == null || anchor == null || part.Installed) return null;
        if (cover != null && !cover.Open) return null;   // 고정판이 자기 안내문을 낸다
        return part.Taken ? "E  " + itemName + " 설치" : displayName + " · " + itemName + " 없음";
    }

    public override void Interact(IInteractor player)
    {
        if (part == null || anchor == null || part.Installed) return;
        if (cover != null && !cover.Open)
        {
            if (player != null) player.ShowNotice("고정판을 먼저 떼어내야 합니다.");
            return;
        }
        if (!part.Taken)
        {
            if (player != null) player.ShowNotice("빈 슬롯입니다. " + itemName + "를 찾아오세요.");
            return;
        }

        if (requiresRepair && FuseRepairManager.Instance != null)
        {
            FuseRepairManager.Instance.Begin(this, player);
            return; // 설치는 미니게임을 통과한 뒤 CompleteInstall 에서 이뤄진다
        }

        CompleteInstall(player);
    }

    /// 실제로 부품을 꽂는다. 미니게임을 쓰지 않으면 Interact 가 바로 부르고,
    /// 쓰면 FuseRepairManager 가 4단계를 통과한 뒤에 부른다.
    public void CompleteInstall(IInteractor player)
    {
        if (part == null || anchor == null || part.Installed) return;
        part.InstallAt(anchor, Quaternion.Euler(installEuler));
        if (player != null) player.ShowNotice(itemName + "를 설치했습니다.");
    }
}
