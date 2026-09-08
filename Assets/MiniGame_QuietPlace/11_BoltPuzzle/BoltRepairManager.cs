using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 볼트 풀기 미니게임의 진입점.
///
/// 맵 프리팹 안의 BoltedPanel 이 씬에 있는 UI 를 직접 참조할 수 없으므로 이 매니저를 거친다.
/// FuseRepairManager / TransformerRepairManager / FrequencyHackManager 와 같은 구조다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class BoltRepairManager : MonoBehaviour
{
    public static BoltRepairManager Instance { get; private set; }

    [Header("미니게임 UI 전체 (평소에는 꺼둔다)")]
    [SerializeField] private GameObject minigameRoot;

    [Header("볼트 돌리기 퍼즐")]
    [SerializeField] private BoltTurnPuzzle puzzle;

    private BoltedPanel pendingPanel;
    private IInteractor pendingPlayer;
    private MonoBehaviour suppressedInteractor;
    private PlayerController lockedPlayer;

    public bool IsRunning => pendingPanel != null;

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
        if (!IsRunning) return;
        if (Keyboard.current == null) return;
        if (Keyboard.current.xKey.wasPressedThisFrame) Cancel();   // 미니게임 나가기는 X
    }

    public void Begin(BoltedPanel panel, IInteractor player)
    {
        if (IsRunning || panel == null) return;

        if (minigameRoot == null || puzzle == null)
        {
            Debug.LogError($"[{nameof(BoltRepairManager)}] minigameRoot 또는 puzzle 이 비어 있다.", this);
            return;
        }

        pendingPanel = panel;
        pendingPlayer = player;

        // 이미 몇 바퀴 돌려둔 상태에서 나갔다 들어와도 이어서 하도록 남은 바퀴만 요구한다.
        puzzle.requiredTurns = Mathf.Max(1, panel.RequiredTurns - panel.Turns);

        SuppressPlayer(player);

        minigameRoot.SetActive(true); // OnEnable 이 퍼즐을 초기화한다
        puzzle.onTurnCompleted.AddListener(HandleTurn);
        puzzle.onAllTurnsDone.AddListener(HandleDone);
    }

    // 화면에서 한 바퀴 돌릴 때마다 월드의 볼트 메시도 같이 돌아간다.
    private void HandleTurn()
    {
        if (pendingPanel != null) pendingPanel.RegisterTurn();
    }

    private void HandleDone()
    {
        BoltedPanel panel = pendingPanel;
        IInteractor player = pendingPlayer;

        Finish();

        if (panel != null) panel.CompleteOpen(player);
    }

    public void Cancel()
    {
        if (!IsRunning) return;
        Finish();
    }

    /// <summary>공포로 손을 놓쳤을 때. 돌린 바퀴까지 되돌려 처음부터 하게 만든다.</summary>
    public void Abort()
    {
        if (!IsRunning) return;

        BoltedPanel panel = pendingPanel;
        Finish();
        if (panel != null) panel.ResetTurns();
    }

    private void Finish()
    {
        if (puzzle != null)
        {
            puzzle.onTurnCompleted.RemoveListener(HandleTurn);
            puzzle.onAllTurnsDone.RemoveListener(HandleDone);
        }
        if (minigameRoot != null) minigameRoot.SetActive(false);

        RestorePlayer();

        pendingPanel = null;
        pendingPlayer = null;
    }

    // 마우스로 볼트를 돌려야 하므로 커서를 풀고, 그 사이 상호작용/시야는 잠근다.
    private void SuppressPlayer(IInteractor player)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        suppressedInteractor = player as MonoBehaviour;
        if (suppressedInteractor == null) return;

        suppressedInteractor.enabled = false;

        lockedPlayer = suppressedInteractor.GetComponent<PlayerController>();
        if (lockedPlayer == null) return;

        // 미니게임 키가 이동 키와 겹친다(A/D, S). 입력 자체를 막아야 몸이 안 움직인다.
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
