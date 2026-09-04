using UnityEngine;

/// The empty slot on the 2F switchboard. Points at the fuse it is waiting for,
/// so no shared carrying state has to be added to IInteractor for one item.
[DisallowMultipleComponent]
public sealed class FuseSlot : Interactable
{
    [SerializeField] FusePickup fuse;
    [SerializeField] Transform anchor;
    [SerializeField] string displayName = "배전반";

    /// Turn applied to the fuse's shelf orientation so it lies along the slot.
    /// On the shelf it runs left-right; in the board it has to run front-back.
    /// Tune this instead of re-exporting the FBX.
    [SerializeField] Vector3 installEuler = new Vector3(0f, 90f, 0f);

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
        fuse.InstallAt(anchor, fuse.RestRotation * Quaternion.Euler(installEuler));
        if (player != null) player.ShowNotice("퓨즈를 설치했습니다.");
    }
}
