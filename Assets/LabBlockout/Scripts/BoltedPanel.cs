using UnityEngine;

/// The bolted plate over the switchboard's transformer bay.
///
/// It exists so the wrench has a reason to be findable. Without it the
/// transformer goes straight into an open hole and the toolbox in the corner is
/// scenery. The whole holder - plate and bolts - is switched off in one go, so
/// the collider that catches the player's look disappears with the geometry
/// instead of leaving an invisible panel to bump into.
[DisallowMultipleComponent]
public sealed class BoltedPanel : Interactable
{
    [SerializeField] FusePickup tool;
    [SerializeField] string displayName = "고정판";
    [SerializeField] string toolName = "몽키스패너";

    public bool Open { get; private set; }

    void Awake()
    {
        Debug.Assert(tool, "BoltedPanel is missing the tool it waits for.");
    }

    public override string Prompt(IInteractor player)
    {
        if (Open) return null;
        if (tool == null || !tool.Taken) return displayName + " · " + toolName + " 필요";
        return "E  볼트 풀기";
    }

    public override void Interact(IInteractor player)
    {
        if (Open) return;
        if (tool == null || !tool.Taken)
        {
            if (player != null) player.ShowNotice(toolName + "가 있어야 육각 볼트를 풀 수 있습니다.");
            return;
        }
        Open = true;
        if (player != null) player.ShowNotice(displayName + "을 떼어냈습니다.");
        gameObject.SetActive(false);
    }
}
