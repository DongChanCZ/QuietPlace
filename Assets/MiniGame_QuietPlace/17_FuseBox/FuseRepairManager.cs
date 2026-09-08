using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 배전반 수리 미니게임의 진입점. 맵 프리팹 안의 FuseSlot 이 씬에 있는 UI 를 직접 참조할 수 없어서
/// (프리팹에서 씬 오브젝트로의 참조는 Unity 가 허용하지 않는다) 이 매니저를 통해 연결한다.
/// NoiseManager / SoundManager 와 같은 방식이다.
///
/// 흐름: FuseSlot.Interact -> Begin() -> 미니게임 UI 활성 -> 4단계 통과
///       -> FuseBoxRepairSequence.onPowerRestored -> 슬롯에 부품 설치 + 전원 복구 연출
/// </summary>
[DefaultExecutionOrder(-1000)] // FuseSlot 의 Interact 보다 먼저 Instance 가 준비되어야 함
public class FuseRepairManager : MonoBehaviour
{
    public static FuseRepairManager Instance { get; private set; }

    [Header("미니게임 UI 전체 루트 (평소에는 꺼둔다)")]
    [SerializeField] private GameObject minigameRoot;

    [Header("4단계를 진행시키는 시퀀서")]
    [SerializeField] private FuseBoxRepairSequence sequence;

    [Header("미니게임 중 플레이어 시야를 잠글지")]
    [SerializeField] private bool lockPlayerLook = true;

    private FuseSlot pendingSlot;
    private IInteractor pendingPlayer;
    private MonoBehaviour suppressedInteractor;
    private PlayerController lockedPlayer;

    public bool IsRunning => pendingSlot != null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (minigameRoot != null) minigameRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 퓨즈 미니게임도 같은 이유로 Esc 취소가 필요하다 (커서가 풀린 채 갇히는 것 방지)
        if (!IsRunning) return;
        if (Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
    }

    /// <summary>FuseSlot 이 호출한다. 이미 진행 중이면 무시한다.</summary>
    public void Begin(FuseSlot slot, IInteractor player)
    {
        if (IsRunning || slot == null) return;

        if (minigameRoot == null || sequence == null)
        {
            Debug.LogError($"[{nameof(FuseRepairManager)}] minigameRoot 또는 sequence 가 비어 있어 미니게임을 열 수 없다.", this);
            return;
        }

        pendingSlot = slot;
        pendingPlayer = player;

        SuppressPlayer(player);

        minigameRoot.SetActive(true);
        sequence.onPowerRestored.AddListener(HandlePowerRestored);
        sequence.Begin(); // 재시도 시에도 1단계부터 다시 시작하도록 명시적으로 초기화
    }

    /// <summary>4단계를 모두 통과했을 때 시퀀서가 부르는 지점.</summary>
    private void HandlePowerRestored()
    {
        FuseSlot slot = pendingSlot;
        IInteractor player = pendingPlayer;

        Finish();

        if (slot != null) slot.CompleteInstall(player);

        // 퓨즈가 꽂혔으니 건물에 전원이 들어온다. 조명/차단기 레버는 BuildingPower 가 들고 있다.
        if (BuildingPower.Instance != null) BuildingPower.Instance.Restore();
    }

    /// <summary>중간에 그만둘 때. 설치는 하지 않는다.</summary>
    public void Cancel()
    {
        if (!IsRunning) return;
        Finish();
    }

    private void Finish()
    {
        if (sequence != null) sequence.onPowerRestored.RemoveListener(HandlePowerRestored);
        if (minigameRoot != null) minigameRoot.SetActive(false);

        RestorePlayer();

        pendingSlot = null;
        pendingPlayer = null;
    }

    // 미니게임이 E 연타를 쓰는데 PlayerInteractor 도 E 로 상호작용을 잡는다.
    // 그대로 두면 연타 중에 같은 슬롯이 계속 재실행되므로 잠시 꺼둔다.
    private void SuppressPlayer(IInteractor player)
    {
        suppressedInteractor = player as MonoBehaviour;
        if (suppressedInteractor != null) suppressedInteractor.enabled = false;

        if (!lockPlayerLook || suppressedInteractor == null) return;

        lockedPlayer = suppressedInteractor.GetComponent<PlayerController>();
        if (lockedPlayer != null) lockedPlayer.CameraLocked = true;
    }

    private void RestorePlayer()
    {
        if (lockedPlayer != null)
        {
            lockedPlayer.CameraLocked = false;
            lockedPlayer = null;
        }

        if (suppressedInteractor != null)
        {
            suppressedInteractor.enabled = true;
            suppressedInteractor = null;
        }
    }
}
