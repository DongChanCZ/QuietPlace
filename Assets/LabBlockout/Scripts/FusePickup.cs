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

    [Header("무게 - 낮을수록 느려진다")]
    [SerializeField, Range(0.1f, 1f)] float moveSpeedMultiplier = 0.45f;
    [SerializeField] float carryLookDownPitch = 12f;
    [SerializeField] float carryEyeDrop = 0.15f;

    /// 플레이어가 가지고 있는가. 손에 든 물건을 내려놓으면 다시 false 가 되어 재획득할 수 있다.
    public bool Taken { get; private set; }
    public bool Installed { get; private set; }

    private Rigidbody body;
    private Transform originalParent;

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

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        originalParent = transform.parent;
        originalLocalScale = transform.localScale;
    }

    // ---------------------------------------------------------------- Interactable

    public override string Prompt(IInteractor player)
    {
        if (Installed || Taken) return null;
        return carryInHands ? "E  " + displayName + " 들기" : "E  " + displayName + " 획득";
    }

    public override void Interact(IInteractor player)
    {
        if (Taken || Installed) return;

        if (!carryInHands)
        {
            Taken = true;
            gameObject.SetActive(false);
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
        if (!Installed) Taken = false;
    }

    private void SetPhysicsFrozen(bool frozen)
    {
        if (body == null) return;
        body.isKinematic = frozen;
        if (frozen) body.linearVelocity = Vector3.zero;
    }

    /// Called by FuseSlot. Put the same object down at the slot.
    public void InstallAt(Transform anchor, Quaternion rotation)
    {
        if (Installed || anchor == null) return;

        Installed = true;
        Taken = true;

        // 손에 들려 있었다면 FuseSlot 이 먼저 DropHeldItem 으로 놓아주므로 부모는 이미 원복돼 있다.
        transform.SetParent(originalParent, true);
        transform.localScale = originalLocalScale;
        SetPhysicsFrozen(true); // 꽂힌 뒤에는 물리로 굴러가지 않아야 한다
        transform.SetPositionAndRotation(anchor.position, rotation);
        gameObject.SetActive(true);
    }
}
