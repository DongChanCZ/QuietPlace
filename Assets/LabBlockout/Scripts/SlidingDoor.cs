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

    [SerializeField] bool startOpen;

    Vector3 shutA, shutB;
    float t;                 // 0 shut, 1 open
    int direction;           // -1 closing, 0 idle, +1 opening
    bool open;

    public Access Locking => access;
    public bool IsOpen => open;
    public string Label => label;

    /// Raised when the player opens this door. The main entrance subscribes so
    /// that opening it brings the doorway down.
    public event System.Action Opened;

    void Awake()
    {
        if (leafA != null) shutA = leafA.position;
        if (leafB != null) shutB = leafB.position;
        // 1F, 2F 는 대부분 구간에서 전력이 없다. 거기서 키를 눌러야 열리는 문은
        // 앞뒤가 안 맞으므로 처음부터 열린 채로 둔다. 문짝은 프리팹에 닫힌 위치
        // 그대로 두고 Awake 에서 한 번만 옮긴다 - 프리팹 구조는 건드리지 않는다.
        if (startOpen && access != Access.Sealed) { open = true; t = 1f; Apply(); }
        else t = 0f;
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
                // Past the guard room every 1F and 2F doorway stands open, so a
                // "close the door" prompt on each one is noise. Only the doorways
                // authored open are silenced - props the player operates, like the
                // hiding cabinet, keep their prompt or they read as scenery.
                if (startOpen && player != null && player.HasFlashlight) return null;
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
        if (open) { var handler = Opened; if (handler != null) handler(); }
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
