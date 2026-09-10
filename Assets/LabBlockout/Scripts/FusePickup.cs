using UnityEngine;

/// The part the player carries from storage to its slot (fuse, transformer module...).
///
/// One object moves and nothing is spawned or destroyed: picking it up hides or lifts
/// this same GameObject, installing it puts it back at the slot. That keeps the
/// carried part and the installed part the same mesh, so they cannot drift apart
/// the way a separate "installed" copy would.
///
/// 옮기는 방식이 두 가지다.
///  - carryInHands = false : 주머니에 넣는 작은 물건(퓨즈 등). 집으면 오브젝트를 숨긴다.
///  - carryInHands = true  : 두 손으로 드는 무거운 물건(변압기 모듈). 화면에 들린 채로
///    따라오고, 무게 때문에 걸음이 느려지고 시야가 낮아진다.
[DisallowMultipleComponent]
public sealed class FusePickup : Interactable, IHoldable
{
    [SerializeField] string displayName = "퓨즈";

    [Header("두 손으로 드는 무거운 물건인가")]
    [SerializeField] bool carryInHands;

    [Header("아래는 carryInHands 일 때만 쓰인다")]
    [SerializeField] string pickupAnimationName = "Holding";
    [SerializeField] Vector3 heldLocalOffset = new Vector3(0f, -0.35f, 0.8f);
    [SerializeField] float noiseLevelBump = 3f;
    [SerializeField] float noiseRangeBump = 3f;

    [Header("떨어뜨려 바닥에 부딪혔을 때 (무거운 물건만)")]
    [SerializeField] AudioClip dropSound;
    [SerializeField] AudioSource dropSource;
    [Tooltip("이 속도보다 세게 부딪혀야 소리가 난다(m/s). 살살 내려놓으면 조용하다.")]
    [SerializeField] float minImpactSpeed = 1.6f;
    [Tooltip("소음 크기. MonsterHearing 의 tier3(=3)을 벽 너머에서도 넘겨야 바로 뛰쳐온다.")]
    [SerializeField] float dropNoiseLoudness = 60f;
    [Tooltip("이 거리 밖이면 벽 유무와 무관하게 안 들린다(m)")]
    [SerializeField] float dropNoiseRange = 70f;
    [Tooltip("한 번 부딪힌 뒤 이 시간 안의 튕김은 무시한다(초)")]
    [SerializeField] float impactCooldown = 0.6f;

    [Tooltip("벽/층에 막혀도 괴물이 반드시 달려오게 할지")]
    [SerializeField] bool summonMonsters = true;

    [Header("무게 - 낮을수록 느려진다")]
    [SerializeField, Range(0.1f, 1f)] float moveSpeedMultiplier = 0.45f;
    [SerializeField] float carryLookDownPitch = 12f;
    [SerializeField] float carryEyeDrop = 0.15f;

    /// 플레이어가 가지고 있는가. 손에 든 물건을 내려놓으면 다시 false 가 되어 재획득할 수 있다.
    public bool Taken { get; private set; }
    public bool Installed { get; private set; }

    /// 한 번이라도 집었는가. Taken 은 내려놓으면 다시 false 가 되지만 "자리에서 빼냈다" 는
    /// 사실은 되돌아가지 않는다. 고장난 부품을 방 밖에 내려놨는데 슬롯이 다시 막히면 안 된다.
    public bool EverTaken { get; private set; }

    private Rigidbody body;
    private Transform originalParent;

    /// 상호작용 가능 표시. 집으면 끄고 내려놓으면 되살린다.
    private InteractableGlow glow;

    /// 처음 크기. 플레이어/카메라에 스케일이 걸려 있으면 부모를 바꿀 때마다
    /// world<->local 환산이 누적되어 물건이 점점 커진다. 매번 이 값으로 되돌린다.
    private Vector3 originalLocalScale;

    // ---------------------------------------------------------------- IHoldable

    public string PickupAnimationName => pickupAnimationName;
    public float NoiseLevelBump => noiseLevelBump;
    public float NoiseRangeBump => noiseRangeBump;
    public Transform HoldPoint => null; // 카메라 앞에 직접 매단다
    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float CarryLookDownPitch => carryLookDownPitch;
    public float CarryEyeDrop => carryEyeDrop;

    /// 클립 이름은 코드에 둔다 - 인스펙터 참조는 병합 때 빠지지만 이 문자열은 남는다.
    const string DropSoundName = "Metal_drop_1";

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        glow = GetComponent<InteractableGlow>();
        originalParent = transform.parent;
        originalLocalScale = transform.localScale;

        // 두 손으로 드는 무거운 물건만 떨어질 때 소리가 난다.
        if (carryInHands) dropSound = AudioLibrary.Resolve(dropSound, DropSoundName);
    }

    // ---------------------------------------------------------------- Interactable

    /// 주머니에 넣는 작은 물건(퓨즈, 몽키스패너)은 무거운 걸 들고 있어도 챙길 수 있다.
    /// 두 손으로 드는 물건만 손을 비워야 한다 - 변압기를 들고 변압기를 또 들 수는 없다.
    public override bool AllowedWhileHandsFull => !carryInHands;

    /// 집어 가는 물건은 뒤/속에 있는 기계보다 먼저 잡혀야 한다.
    public override int AimPriority => 10;

    /// 이 부품을 덮고 있는 고정판. FuseSlot 이 Awake 에서 자기 cover 를 알려준다.
    /// 판이 닫혀 있는 동안에는 손이 닿지 않아야 한다 - 볼트를 풀기 전에 안쪽 부품이
    /// 빠져나오면 볼트 미니게임 자체가 무의미해진다.
    private BoltedPanel sealedBehind;

    private bool Sealed => sealedBehind != null && !sealedBehind.Open;

    /// <summary>FuseSlot 이 부른다. 이 부품이 고정판 안쪽에 있다고 알린다.</summary>
    public void SealBehind(BoltedPanel panel)
    {
        sealedBehind = panel;
    }

    public override bool Available(IInteractor player) => !Taken && !Installed && !Sealed;

    public override string Prompt(IInteractor player)
    {
        if (Installed || Taken || Sealed) return null;
        return carryInHands ? "E  " + displayName + " 들기" : "E  " + displayName + " 획득";
    }

    public override void Interact(IInteractor player)
    {
        if (Taken || Installed) return;

        if (Sealed)
        {
            if (player != null) player.ShowNotice("고정판을 먼저 떼어내야 합니다.");
            return;
        }

        if (!carryInHands)
        {
            Taken = true;
            EverTaken = true;
            if (glow != null) glow.Consume();
            gameObject.SetActive(false);
            // 약병처럼 슬롯에 남아있는다 - 던지기/들기는 CanThrow/HeldModelFor 어디에도
            // Fuse 케이스가 없어서 저절로 둘 다 안 된다.
            QuickSlotManager.Instance?.AddItem(ItemType.Fuse, displayName);
            if (player != null) player.ShowNotice(displayName + "를 챙겼습니다.");
            return;
        }

        // IInteractor 는 PlayerController 를 노출하지 않으므로 컴포넌트로 찾아온다.
        PlayerController controller = (player as MonoBehaviour)?.GetComponent<PlayerController>();
        if (controller == null)
        {
            Debug.LogWarning($"[{nameof(FusePickup)}] PlayerController 를 찾지 못해 들 수 없다.", this);
            return;
        }
        if (controller.HeldItem != null)
        {
            if (player != null) player.ShowNotice("양손이 차 있습니다. 먼저 내려놓으세요.");
            return;
        }

        Taken = true;
        EverTaken = true;
        if (glow != null) glow.Consume();
        controller.EnterHolding(this);
        if (player != null) player.ShowNotice(displayName + "를 들었습니다.");
    }

    // ---------------------------------------------------------------- 들기 / 놓기

    public void OnPickedUp(PlayerController player)
    {
        SetPhysicsFrozen(true);

        Transform anchor = player.playerCamera != null ? player.playerCamera.transform : player.transform;
        transform.SetParent(anchor, false);
        transform.localPosition = heldLocalOffset;
        transform.localRotation = Quaternion.identity;

        // 부모(카메라)에 스케일이 걸려 있어도 눈에 보이는 크기가 그대로여야 한다.
        Vector3 parentScale = anchor.lossyScale;
        transform.localScale = new Vector3(
            parentScale.x != 0f ? originalLocalScale.x / parentScale.x : originalLocalScale.x,
            parentScale.y != 0f ? originalLocalScale.y / parentScale.y : originalLocalScale.y,
            parentScale.z != 0f ? originalLocalScale.z / parentScale.z : originalLocalScale.z);
    }

    public void OnThrown(Vector3 force)
    {
        ReleaseToWorld();
        if (body != null) body.AddForce(force, ForceMode.Impulse);
    }

    public void OnDropped()
    {
        ReleaseToWorld();
    }

    /// 손에서 놓아 월드에 되돌린다. 설치가 아니라 그냥 내려놓은 것이므로
    /// Taken 을 풀어서 다시 집을 수 있게 한다.
    private void ReleaseToWorld()
    {
        transform.SetParent(originalParent, true); // 월드 위치 유지
        transform.localScale = originalLocalScale; // 스케일 누적 방지
        SetPhysicsFrozen(false);
        if (Installed) return;

        Taken = false;
        if (glow != null) glow.Restore();   // 다시 집을 수 있으니 표시도 되살린다
    }

    /// <summary>
    /// 무거운 물건이 바닥에 부딪혔을 때. 큰 소리가 나고 괴물이 그 자리로 뛰쳐온다.
    ///
    /// 놓는 순간이 아니라 실제로 닿는 순간에 울려야 한다 - 그래야 "떨어뜨렸다"는
    /// 느낌이 난다. 들고 있는 동안은 Rigidbody 가 kinematic 이라 충돌이 오지 않는다.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (!carryInHands || Installed || Taken) return;
        if (Time.time < _nextImpactTime) return;                       // 튕기며 여러 번 울리지 않게
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        _nextImpactTime = Time.time + impactCooldown;

        Vector3 at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

        if (dropSound != null)
        {
            if (dropSource != null) dropSource.PlayOneShot(dropSound);
            else AudioSource.PlayClipAtPoint(dropSound, at);
        }

        if (NoiseManager.Instance != null)
            NoiseManager.Instance.Emit(new NoiseEvent(at, dropNoiseLoudness, dropNoiseRange, NoiseType.ObjectImpact));

        // 소음만으로는 벽/층 감쇠에 막혀 "무조건" 이 안 된다. 직접 불러들인다.
        if (summonMonsters) MonsterAlarm.Summon(at, dropNoiseLoudness);

        Debug.Log($"[FusePickup] {displayName} 을 떨어뜨렸다 - 소음 {dropNoiseLoudness} / 범위 {dropNoiseRange}m @ {at}");
    }

    private float _nextImpactTime;

    private void SetPhysicsFrozen(bool frozen)
    {
        if (body == null) return;

        // 이미 kinematic 인 몸에 속도를 쓰면 Unity 가 경고를 낸다. 얼리기 전에 먼저 지운다.
        if (frozen && !body.isKinematic) body.linearVelocity = Vector3.zero;
        body.isKinematic = frozen;
    }

    /// <summary>
    /// 세이브포인트 복원용. 플레이어가 챙겨간 것과 같은 상태로 만들고 화면에서 치운다.
    /// Interact 와 달리 손에 드는 처리를 하지 않는다 - 이미 지나간 일이라 물건만 사라져야 한다.
    /// </summary>
    public void ForceConsumed()
    {
        if (Installed) return;

        Taken = true;
        EverTaken = true;
        if (glow != null) glow.Consume();
        gameObject.SetActive(false);
    }

    /// Called by FuseSlot. Put the same object down at the slot.
    public void InstallAt(Transform anchor, Quaternion rotation)
    {
        if (Installed || anchor == null) return;

        Installed = true;
        Taken = true;
        EverTaken = true;
        if (glow != null) glow.Consume();
        QuickSlotManager.Instance?.RemoveItem(ItemType.Fuse); // 슬롯에 있었다면(약병처럼 챙긴 경우) 설치 순간 비운다

        // 손에 들려 있었다면 FuseSlot 이 먼저 DropHeldItem 으로 놓아주므로 부모는 이미 원복돼 있다.
        transform.SetParent(originalParent, true);
        transform.localScale = originalLocalScale;
        SetPhysicsFrozen(true); // 꽂힌 뒤에는 물리로 굴러가지 않아야 한다
        transform.SetPositionAndRotation(anchor.position, rotation);
        gameObject.SetActive(true);
    }
}
