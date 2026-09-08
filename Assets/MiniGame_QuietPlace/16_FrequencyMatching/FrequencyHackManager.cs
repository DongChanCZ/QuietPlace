using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3층 컴퓨터의 진입점 (비밀번호 -> 주파수 맞추기).
///
/// 맵 프리팹 안의 ComputerTerminal 이 씬에 있는 UI 를 직접 참조하지 않도록 이 매니저를 거친다.
/// 다른 미니게임 매니저들과 같은 구조다.
///
/// 문을 여는 키패드는 이쪽이 아니라 DoorHackManager(빨리치기)가 담당한다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class FrequencyHackManager : MonoBehaviour
{
    public static FrequencyHackManager Instance { get; private set; }

    [Header("미니게임 UI 전체 (평소에는 꺼둔다)")]
    [SerializeField] private GameObject minigameRoot;

    [Header("1단계: 비밀번호 - 비우면 바로 주파수로 간다")]
    [SerializeField] private GameObject passwordRoot;
    [SerializeField] private PasswordEntry passwordEntry;

    [Header("2단계: 주파수 정합")]
    [SerializeField] private GameObject puzzleRoot;
    [SerializeField] private FrequencyMatchingPuzzle puzzle;

    private ComputerTerminal pendingComputer;
    private IInteractor pendingPlayer;
    private MonoBehaviour suppressedInteractor;
    private PlayerController lockedPlayer;

    public bool IsRunning => pendingComputer != null;

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
        // UI 가 안 보이는 상황에서도 빠져나올 수 있어야 한다
        if (!IsRunning) return;
        if (Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
    }

    public void Begin(ComputerTerminal computer, IInteractor player)
    {
        if (IsRunning || computer == null) return;

        if (minigameRoot == null || puzzle == null)
        {
            Debug.LogError($"[{nameof(FrequencyHackManager)}] minigameRoot 또는 puzzle 이 비어 있다.", this);
            return;
        }

        pendingComputer = computer;
        pendingPlayer = player;
        SuppressPlayer(player);

        minigameRoot.SetActive(true);

        // 비밀번호를 이미 통과했으면 다시 묻지 않는다.
        // 보통 난이도는 비밀번호를 묻지 않고 바로 주파수 맞추기로 간다.
        bool needPassword = MinigameTuning.UsePasswordStage
                            && passwordEntry != null && passwordRoot != null && !computer.PasswordSolved;
        if (needPassword)
        {
            if (puzzleRoot != null) puzzleRoot.SetActive(false);
            passwordRoot.SetActive(true);
            passwordEntry.onCorrect.AddListener(HandlePasswordCorrect);
            return;
        }

        ShowPuzzleStage();
    }

    private void HandlePasswordCorrect()
    {
        if (passwordEntry != null) passwordEntry.onCorrect.RemoveListener(HandlePasswordCorrect);
        if (pendingComputer != null) pendingComputer.MarkPasswordSolved();

        ShowPuzzleStage();
    }

    private void ShowPuzzleStage()
    {
        if (passwordRoot != null) passwordRoot.SetActive(false);
        if (puzzleRoot != null) puzzleRoot.SetActive(true);

        puzzle.onFullyCharged.AddListener(HandleCharged);
        puzzle.onAllBandsMatched.AddListener(HandleMatched);
    }

    /// 네 대역을 다 맞춘 순간. 미니게임을 닫아 조작을 돌려주고, 송출은 월드에서
    /// 혼자 차오르게 한다. 플레이어는 그동안 숨어서 버틴다.
    private void HandleMatched()
    {
        ComputerTerminal computer = pendingComputer;
        IInteractor player = pendingPlayer;

        Finish();

        if (computer != null) computer.OnFrequencySet(player);

        if (FrequencyBroadcast.Instance != null) FrequencyBroadcast.Instance.Begin(computer, player);
        else if (computer != null) computer.OnSolved(player);   // 송출 매니저가 없으면 바로 완료 처리
    }

    private void HandleCharged()
    {
        ComputerTerminal computer = pendingComputer;
        IInteractor player = pendingPlayer;

        Finish();

        if (computer != null) computer.OnSolved(player);
    }

    public void Cancel()
    {
        if (!IsRunning) return;
        Finish();
    }

    private void Finish()
    {
        if (puzzle != null)
        {
            puzzle.onFullyCharged.RemoveListener(HandleCharged);
            puzzle.onAllBandsMatched.RemoveListener(HandleMatched);
        }
        if (passwordEntry != null) passwordEntry.onCorrect.RemoveListener(HandlePasswordCorrect);
        if (minigameRoot != null) minigameRoot.SetActive(false);

        RestorePlayer();
        pendingComputer = null;
        pendingPlayer = null;
    }

    // 슬라이더를 마우스로 끌어야 하므로 커서를 풀고, 그 사이 조작은 잠근다.
    private void SuppressPlayer(IInteractor player)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        suppressedInteractor = player as MonoBehaviour;
        if (suppressedInteractor == null) return;

        suppressedInteractor.enabled = false;

        lockedPlayer = suppressedInteractor.GetComponent<PlayerController>();
        if (lockedPlayer == null) return;

        // 비밀번호를 타이핑하는 동안 WASD 로 걸어다니면 안 된다.
        lockedPlayer.InputLocked = true;
        lockedPlayer.CameraLocked = true;
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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
