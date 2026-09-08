using UnityEngine;

/// <summary>
/// 숨을 수 있는 곳(캐비닛, 테이블 밑, 사물함...).
///
/// 프로젝트에는 이미 IHidingSpot 과 PlayerController.EnterHiding / HidingState 가 있었지만
/// 인터페이스를 구현한 클래스가 하나도 없어서 실제로는 숨을 수가 없었다. 이 컴포넌트가
/// 그 자리를 채운다.
///
/// 괴물은 시야가 아니라 소리로만 찾아온다(MonsterHearing). HidingState 가 숨어 있는 동안
/// 플레이어 소음 등급을 한 단계 낮추므로, 숨는 것 자체가 곧 몸을 감추는 효과가 된다.
///
/// 조작은 Q 하나다. 겨누고 Q 를 누르면 들어가고, 숨은 상태에서 Q 를 다시 누르거나
/// 이동 키를 누르면 나온다(HidingState 가 이동 입력을 나가는 신호로 쓴다).
/// </summary>
[DisallowMultipleComponent]
public sealed class HidingSpot : Interactable, IHidingSpot
{
    [SerializeField] string displayName = "캐비닛";

    [Header("숨었을 때 플레이어가 서 있을 자리 - 비우면 이 오브젝트 위치")]
    [SerializeField] Transform hiddenViewPoint;

    [Header("들어갈 때 나는 소음 (문 여닫는 소리)")]
    [SerializeField] float noiseLevelBump = 2f;
    [SerializeField] float noiseRangeBump = 6f;

    [Header("애니메이터 상태 이름")]
    [SerializeField] string hideAnimationName = "Hide";

    [Header("숨는 동안 닫을 문짝 (선택)")]
    [SerializeField] Transform door;
    [SerializeField] Vector3 doorOpenEuler;
    [SerializeField] Vector3 doorClosedEuler = new Vector3(0f, 90f, 0f);
    [SerializeField] float doorSeconds = 0.25f;

    [Header("사운드 - SoundManager 에 등록된 이름")]
    [SerializeField] string enterSoundName = "";
    [SerializeField] string exitSoundName = "";

    /// 안에 사람이 있는가. 한 곳에 둘이 들어가지 않게 한다.
    public bool Occupied { get; private set; }

    // ---------------------------------------------------------------- IHidingSpot

    public string HideAnimationName => hideAnimationName;
    public float NoiseLevelBump => noiseLevelBump;
    public float NoiseRangeBump => noiseRangeBump;
    public Transform HiddenViewPoint => hiddenViewPoint != null ? hiddenViewPoint : transform;

    public void OnHideEnter(PlayerController player)
    {
        if (player == null) return;

        occupant = player;
        Occupied = true;
        standingPosition = player.transform.position;
        standingRotation = player.transform.rotation;

        Transform target = HiddenViewPoint;
        player.TeleportTo(target.position, target.rotation);

        // 캐비닛은 사람 몸통보다 얕아서 CharacterController 가 매 프레임 뒷판에서
        // 밀어낸다. 위치를 다시 붙여도 밀림이 이기므로 숨는 동안은 컨트롤러를 끈다.
        if (player.Controller != null) player.Controller.enabled = false;

        doorTarget = 1f;
        Play(enterSoundName);
    }

    public void OnHideExit(PlayerController player)
    {
        occupant = null;
        Occupied = false;
        doorTarget = 0f;
        Play(exitSoundName);

        if (player == null) return;

        // 들어간 자리로 되돌린 뒤에 컨트롤러를 다시 켠다. 순서가 바뀌면 캐비닛
        // 안에서 켜지면서 벽에 끼인다.
        player.TeleportTo(standingPosition, standingRotation);
        if (player.Controller != null) player.Controller.enabled = true;
    }

    // ---------------------------------------------------------------- Interactable

    /// 손이 차 있으면 숨을 수 없다 - 변압기를 든 채로 캐비닛에 들어갈 수는 없다.
    public override bool AllowedWhileHandsFull => false;

    public override string Prompt(IInteractor player)
    {
        if (Occupied) return null;
        return "Q  " + displayName + "에 숨기";
    }

    /// E 로는 숨지 않는다. E 는 문/설치/줍기에 이미 쓰이고 있어서, 숨을 곳 앞에서
    /// 다른 걸 하려다 잘못 숨는 일이 생긴다. 안내만 하고 실제 진입은 Q 가 담당한다.
    public override void Interact(IInteractor player)
    {
        if (player == null || Occupied) return;
        player.ShowNotice("Q 를 눌러 " + displayName + "에 숨습니다.");
    }

    // ---------------------------------------------------------------- 문 여닫기

    private PlayerController occupant;
    private Vector3 standingPosition;
    private Quaternion standingRotation;
    private float doorTarget;
    private float doorT;

    void Awake()
    {
        Debug.Assert(hiddenViewPoint, "HidingSpot 에 HiddenViewPoint 가 없다. 오브젝트 위치를 그대로 쓴다.");
        if (door != null) door.localRotation = Quaternion.Euler(doorOpenEuler);
    }

    void Update()
    {
        if (door == null) return;
        if (Mathf.Approximately(doorT, doorTarget)) return;

        doorT = Mathf.MoveTowards(doorT, doorTarget, Time.deltaTime / Mathf.Max(0.01f, doorSeconds));
        door.localRotation = Quaternion.Slerp(
            Quaternion.Euler(doorOpenEuler), Quaternion.Euler(doorClosedEuler), Mathf.SmoothStep(0f, 1f, doorT));
    }

    private void Play(string soundName)
    {
        if (string.IsNullOrEmpty(soundName) || SoundManager.Instance == null) return;
        SoundManager.Instance.PlaySFX(soundName);
    }
}
