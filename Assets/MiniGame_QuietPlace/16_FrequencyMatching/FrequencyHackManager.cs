using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3층 키패드 해킹(주파수 맞추기) 미니게임의 진입점.
///
/// 맵 프리팹 안의 KeypadTerminal 이 씬에 있는 UI 를 직접 참조하지 않도록 이 매니저를 거친다.
/// FuseRepairManager / TransformerRepairManager 와 같은 구조다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class FrequencyHackManager : MonoBehaviour
{
    public static FrequencyHackManager Instance { get; private set; }

    [Header("미니게임 UI 전체 (평소에는 꺼둔다)")]
    [SerializeField] private GameObject minigameRoot;

    [Header("주파수 퍼즐")]
    [SerializeField] private FrequencyMatchingPuzzle puzzle;

    private KeypadTerminal pendingKeypad;
    private MonoBehaviour suppressedInteractor;
    private PlayerController lockedPlayer;

    public bool IsRunning => pendingKeypad != null;

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

    public void Begin(KeypadTerminal keypad, IInteractor player)
    {
        if (IsRunning || keypad == null) return;

        if (minigameRoot == null || puzzle == null)
        {
            Debug.LogError($"[{nameof(FrequencyHackManager)}] minigameRoot 또는 puzzle 이 비어 있다.", this);
            return;
        }

        pendingKeypad = keypad;
        SuppressPlayer(player);

        minigameRoot.SetActive(true);
        puzzle.onFullyCharged.AddListener(HandleCharged);
    }

    private void HandleCharged()
    {
        KeypadTerminal keypad = pendingKeypad;
        Finish();
        if (keypad != null) keypad.OnHacked();
    }

    public void Cancel()
    {
        if (!IsRunning) return;
        Finish();
    }

    private void Finish()
    {
        if (puzzle != null) puzzle.onFullyCharged.RemoveListener(HandleCharged);
        if (minigameRoot != null) minigameRoot.SetActive(false);

        RestorePlayer();
        pendingKeypad = null;
    }

    // 슬라이더를 마우스로 끌어야 하므로 커서를 풀고, 그 사이 상호작용/시야는 잠근다.
    private void SuppressPlayer(IInteractor player)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        suppressedInteractor = player as MonoBehaviour;
        if (suppressedInteractor == null) return;

        suppressedInteractor.enabled = false;

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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
