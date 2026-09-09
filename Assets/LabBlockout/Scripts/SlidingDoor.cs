using UnityEngine;

/// A bi-parting facility door. The two leaves slide apart along the opening and
/// their colliders travel with them, so a shut door really blocks and an open
/// one really lets you through.
///
/// Set swingDegrees and the leaves hinge instead of sliding - a cabinet whose
/// doors slid sideways into nothing looked wrong. It is the same door otherwise:
/// the locking, the prompt, the immediate-open the checks use. Splitting a
/// second script off for one prop would have duplicated all of that.
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

    /// Leave at 0 to slide. Anything else hinges the leaves that many degrees
    /// about their own pivot, so the pivot has to sit on the hinge edge rather
    /// than in the middle of the leaf.
    [SerializeField] float swingDegrees;

    [SerializeField] float seconds = 0.85f;
    [SerializeField] Access access = Access.Unlocked;
    [SerializeField] string label = "문";

    [SerializeField] bool startOpen;

    Vector3 shutA, shutB;
    Quaternion shutRotA = Quaternion.identity, shutRotB = Quaternion.identity;
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
        if (leafA != null) { shutA = leafA.position; shutRotA = leafA.localRotation; }
        if (leafB != null) { shutB = leafB.position; shutRotB = leafB.localRotation; }
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
        if (t <= 0f || t >= 1f) { direction = 0; SetLeafColliders(true); }
    }

    /// 움직이는 동안만 문짝 콜라이더를 끈다.
    ///
    /// 문짝은 트랜스폼을 직접 옮겨 여는데, 그 자리에 CharacterController 가 서 있으면
    /// 물리 엔진이 몸을 밀어내며 벽 밖으로 튕겨 보낸다. 캐비닛에 들어가 문을 닫으면
    /// 건물 밖으로 나가지던 게 이것 때문이다. 0.5~0.9초 동안만 꺼두므로
    /// 닫힌 문은 여전히 막고, 열린 문은 여전히 통과된다.
    void SetLeafColliders(bool on)
    {
        if (leafA != null)
            foreach (Collider c in leafA.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger) c.enabled = on;
        if (leafB != null)
            foreach (Collider c in leafB.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger) c.enabled = on;
    }

    void Apply()
    {
        // Smoothstep so the leaves ease rather than snapping at both ends.
        float e = t * t * (3f - 2f * t);
        if (swingDegrees != 0f)
        {
            // About each leaf's own up, so a hinged door on a tilted parent still
            // swings around a vertical edge rather than tipping over.
            float a = swingDegrees * e;
            if (leafA != null) leafA.localRotation = shutRotA * Quaternion.Euler(0f, -a, 0f);
            if (leafB != null) leafB.localRotation = shutRotB * Quaternion.Euler(0f, a, 0f);
            return;
        }
        Vector3 offset = slideAxis.normalized * (slideDistance * e);
        if (leafA != null) leafA.position = shutA - offset;
        if (leafB != null) leafB.position = shutB + offset;
    }

    // 손이 차 있어도 문은 열 수 있어야 한다 - 어깨로 밀거나 팔꿈치로 누르는 식
    public override bool AllowedWhileHandsFull => true;

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
        SetLeafColliders(false);
        player.ShowNotice(label + (open ? " 열림" : " 닫힘"));
        if (open) { var handler = Opened; if (handler != null) handler(); }
    }

    /// 키패드 해킹처럼 외부 장치가 문을 여는 경로. Sealed 여도 통하고,
    /// SetOpenImmediate 와 달리 Update 로 스르륵 열린다.
    public void ForceOpen(bool unseal = true)
    {
        if (unseal && access == Access.Sealed) access = Access.Unlocked;
        if (open) return;

        open = true;
        direction = 1;
        SetLeafColliders(false);

        var handler = Opened;
        if (handler != null) handler();
    }

    /// Used by the play-mode checks, which cannot wait for Update to run.
    public void SetOpenImmediate(bool wantOpen)
    {
        open = wantOpen;
        t = wantOpen ? 1f : 0f;
        direction = 0;
        Apply();
        SetLeafColliders(true);
        Physics.SyncTransforms();
    }

    public void Configure(Transform a, Transform b, Vector3 axis, float distance, Access mode, string name)
    {
        leafA = a; leafB = b; slideAxis = axis; slideDistance = distance; access = mode; label = name;
        if (a != null) shutA = a.position;
        if (b != null) shutB = b.position;
    }
}
