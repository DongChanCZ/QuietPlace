using UnityEngine;

/// A bi-parting facility door. The two leaves slide apart along the opening and
/// their colliders travel with them, so a shut door really blocks and an open
/// one really lets you through.
[DisallowMultipleComponent]
public sealed class SlidingDoor : Interactable
{
    public enum Access
    {
        Unlocked,   // opens on E
        Keycard,    // opens on E once the player is carrying a card
        Sealed,     // never opens - out of bounds
    }
    // Nothing uses Keycard right now, on purpose. The building has no power for
    // most of the route, so a card reader that works would contradict the story
    // the player is in the middle of. The mode stays because power comes back at
    // route beat 15 and doors can be gated again after that.

    [SerializeField] Transform leafA;
    [SerializeField] Transform leafB;
    [SerializeField] Vector3 slideAxis = Vector3.forward;   // world axis the leaves part along
    [SerializeField] float slideDistance = 0.95f;
    [SerializeField] float seconds = 0.85f;
    [SerializeField] Access access = Access.Unlocked;
    [SerializeField] string label = "문";

    Vector3 shutA, shutB;
    float t;                 // 0 shut, 1 open
    int direction;           // -1 closing, 0 idle, +1 opening
    bool open;

    public Access Locking => access;
    public bool IsOpen => open;
    public string Label => label;

    void Awake()
    {
        if (leafA != null) shutA = leafA.position;
        if (leafB != null) shutB = leafB.position;
        t = 0f;
    }

    void Update()
    {
        if (direction == 0) return;
        t = Mathf.Clamp01(t + direction * Time.deltaTime / Mathf.Max(seconds, 0.01f));
        Apply();
        if (t <= 0f || t >= 1f) direction = 0;
    }

    void Apply()
    {
        // Smoothstep so the leaves ease rather than snapping at both ends.
        float e = t * t * (3f - 2f * t);
        Vector3 offset = slideAxis.normalized * (slideDistance * e);
        if (leafA != null) leafA.position = shutA - offset;
        if (leafB != null) leafB.position = shutB + offset;
    }

    public override string Prompt(IInteractor player)
    {
        switch (access)
        {
            case Access.Sealed:
                return "출입 제한";
            case Access.Keycard:
                return player != null && player.HasKeycard
                    ? "E  " + label + " " + (open ? "닫기" : "열기")
                    : label + " · 카드키 필요";
            default:
                return "E  " + label + " " + (open ? "닫기" : "열기");
        }
    }

    public override void Interact(IInteractor player)
    {
        if (access == Access.Sealed)
        {
            player.ShowNotice("출입 제한 구역입니다.");
            return;
        }
        if (access == Access.Keycard && (player == null || !player.HasKeycard))
        {
            player.ShowNotice("카드키가 필요합니다.");
            return;
        }
        open = !open;
        direction = open ? 1 : -1;
        player.ShowNotice(label + (open ? " 열림" : " 닫힘"));
    }

    /// Used by the play-mode checks, which cannot wait for Update to run.
    public void SetOpenImmediate(bool wantOpen)
    {
        open = wantOpen;
        t = wantOpen ? 1f : 0f;
        direction = 0;
        Apply();
        Physics.SyncTransforms();
    }

    public void Configure(Transform a, Transform b, Vector3 axis, float distance, Access mode, string name)
    {
        leafA = a; leafB = b; slideAxis = axis; slideDistance = distance; access = mode; label = name;
        if (a != null) shutA = a.position;
        if (b != null) shutB = b.position;
    }
}
