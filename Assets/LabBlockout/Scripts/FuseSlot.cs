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

    /// 부품을 꽂기 전에 거쳐야 하는 수리 미니게임. None 이면 기존처럼 즉시 설치된다.
    /// 슬롯마다 다른 미니게임을 붙일 수 있어야 해서 bool 이 아니라 종류로 둔다.
    [SerializeField] RepairKind requiresRepair = RepairKind.None;

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

    // 들고 온 부품을 꽂는 곳이라 손이 차 있는 상태로 써야 한다
    public override bool AllowedWhileHandsFull => true;

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

        // 설치는 미니게임을 통과한 뒤 CompleteInstall 에서 이뤄진다.
        // 매니저가 씬에 없으면(프리뷰 씬 등) 게이트를 건너뛰고 그냥 설치한다.
        switch (requiresRepair)
        {
            case RepairKind.TransformerModule when TransformerRepairManager.Instance != null:
                TransformerRepairManager.Instance.Begin(this, player);
                return;

            case RepairKind.SwitchboardFuse when FuseRepairManager.Instance != null:
                FuseRepairManager.Instance.Begin(this, player);
                return;
        }

        CompleteInstall(player);
    }

    /// 실제로 부품을 꽂는다. 미니게임을 쓰지 않으면 Interact 가 바로 부르고,
    /// 쓰면 FuseRepairManager 가 4단계를 통과한 뒤에 부른다.
    public void CompleteInstall(IInteractor player)
    {
        if (part == null || anchor == null || part.Installed) return;

        // 두 손으로 들고 온 부품이면 먼저 손에서 놓는다. 그러지 않으면 HeldItem 이 이미
        // 꽂힌 물건을 계속 가리키고, 무게로 인한 속도/시야 보정도 풀리지 않는다.
        PlayerController controller = (player as MonoBehaviour)?.GetComponent<PlayerController>();
        if (controller != null && ReferenceEquals(controller.HeldItem, part)) controller.DropHeldItem();

        part.InstallAt(anchor, Quaternion.Euler(installEuler));
        if (player != null) player.ShowNotice(itemName + "를 설치했습니다.");
    }
}
