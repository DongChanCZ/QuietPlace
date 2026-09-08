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

    [Header("실물 차단기 레버 - 화면 게이지에 맞춰 같이 움직인다")]
    [SerializeField] private BreakerLever breakerLever;

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
        if (Keyboard.current.xKey.wasPressedThisFrame) Cancel();   // 미니게임 나가기는 X
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

        // 프리팹은 씬 오브젝트를 담을 수 없으므로 시작할 때 실물 레버를 넣어준다.
        if (sequence.breakerDownHold != null)
        {
            sequence.breakerDownHold.lever = breakerLever;
            sequence.breakerDownHold.leverGoesUp = false;   // 내리기
        }
        if (sequence.breakerUpHold != null)
        {
            sequence.breakerUpHold.lever = breakerLever;
            sequence.breakerUpHold.leverGoesUp = true;      // 올리기
        }

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

        // 퓨즈 쪽이 끝났다고만 알린다. 발전기실(변압기)까지 끝났을 때 BuildingPower 가
        // 스스로 전원을 넣는다 - 한쪽만 고쳤을 때 불이 켜지면 스토리가 무너진다.
        if (BuildingPower.Instance != null) BuildingPower.Instance.MarkFuseInstalled();
    }

    /// <summary>중간에 그만둘 때. 설치는 하지 않는다. 다시 들어오면 첫 단계부터다.</summary>
    public void Cancel()
    {
        if (!IsRunning) return;
        Finish();
    }

    /// <summary>공포로 손을 놓쳤을 때. Begin 이 매번 첫 단계로 되돌리므로 Cancel 과 같다.</summary>
    public void Abort()
    {
        Cancel();
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
        if (suppressedInteractor == null) return;

        suppressedInteractor.enabled = false;

        lockedPlayer = suppressedInteractor.GetComponent<PlayerController>();
        if (lockedPlayer == null) return;

        // 3단계는 A/D 로 퓨즈를 돌리고 2단계는 R/S/T 를 쓴다. A/D/S 가 전부 이동 키라서
        // 입력을 막지 않으면 퓨즈를 돌리는 동안 플레이어 몸도 같이 움직인다.
        lockedPlayer.InputLocked = true;
        if (lockPlayerLook) lockedPlayer.CameraLocked = true;
    }

    private void RestorePlayer()
    {
        if (lockedPlayer != null)
        {
            lockedPlayer.InputLocked = false;
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
