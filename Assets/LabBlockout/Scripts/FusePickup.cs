using UnityEngine;

/// The fuse the player carries from the warehouse shelf to the switchboard.
///
/// One object moves and nothing is spawned or destroyed: picking it up hides
/// this same GameObject, installing it puts it back at the slot. That keeps the
/// carried fuse and the installed fuse the same mesh, so they cannot drift apart
/// the way a separate "installed" copy would.
[DisallowMultipleComponent]
public sealed class FusePickup : Interactable
{
    [SerializeField] string displayName = "퓨즈";

    public bool Taken { get; private set; }
    public bool Installed { get; private set; }

    /// How the fuse sits on the shelf. The FBX carries its own axis correction,
    /// so the slot rotates relative to this instead of building a rotation from
    /// scratch and losing it.
    public Quaternion RestRotation { get; private set; }

    void Awake()
    {
        RestRotation = transform.rotation;
    }

    public override string Prompt(IInteractor player)
    {
        return Taken ? null : "E  " + displayName + " 획득";
    }

    public override void Interact(IInteractor player)
    {
        if (Taken) return;
        Taken = true;
        gameObject.SetActive(false);
        if (player != null) player.ShowNotice(displayName + "를 챙겼습니다.");
    }

    /// Called by FuseSlot. Put the same object down at the slot.
    public void InstallAt(Transform anchor, Quaternion rotation)
    {
        if (Installed || anchor == null) return;
        transform.SetPositionAndRotation(anchor.position, rotation);
        gameObject.SetActive(true);
        Installed = true;
    }
}
