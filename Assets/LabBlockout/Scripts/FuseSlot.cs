using UnityEngine;

/// The empty slot on the 2F switchboard. Points at the fuse it is waiting for,
/// so no shared carrying state has to be added to IInteractor for one item.
[DisallowMultipleComponent]
public sealed class FuseSlot : Interactable
{
    [SerializeField] FusePickup fuse;
    [SerializeField] Transform anchor;
    [SerializeField] string displayName = "배전반";

    /// World rotation the fuse takes in the slot. It lies flat in storage but
    /// stands upright once seated, so this is absolute rather than a turn
    /// relative to how it happened to be lying on the shelf. (270, 0, 0) is the
    /// FBX's own axis correction - the model standing on its end.
    [SerializeField] Vector3 installEuler = new Vector3(270f, 0f, 0f);

    void Awake()
    {
        Debug.Assert(fuse && anchor, "FuseSlot is missing its fuse or anchor.");
    }

    public override string Prompt(IInteractor player)
    {
        if (fuse == null || anchor == null || fuse.Installed) return null;
        return fuse.Taken ? "E  퓨즈 설치" : displayName + " · 퓨즈 없음";
    }

    public override void Interact(IInteractor player)
    {
        if (fuse == null || anchor == null || fuse.Installed) return;
        if (!fuse.Taken)
        {
            if (player != null) player.ShowNotice("빈 퓨즈 슬롯입니다. 퓨즈를 찾아오세요.");
            return;
        }
        fuse.InstallAt(anchor, Quaternion.Euler(installEuler));
        if (player != null) player.ShowNotice("퓨즈를 설치했습니다.");
    }
}
