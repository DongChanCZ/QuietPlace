using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3층 문 키패드를 뜯어서 회로를 조작하는 미니게임(영어 빨리치기)의 진입점.
///
/// 맵 프리팹 안의 KeypadTerminal 이 씬에 있는 UI 를 직접 참조할 수 없으므로 이 매니저를 거친다.
/// 다른 미니게임 매니저들과 같은 구조다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class DoorHackManager : MonoBehaviour
{
    public static DoorHackManager Instance { get; private set; }

    [Header("미니게임 UI 전체 (평소에는 꺼둔다)")]
    [SerializeField] private GameObject minigameRoot;

    [Header("빨리치기 퍼즐")]
    [SerializeField] private DoorHackTyping typing;

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
        if (!IsRunning) return;
        if (Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
    }

    public void Begin(KeypadTerminal keypad, IInteractor player)
    {
        if (IsRunning || keypad == null) return;

        if (minigameRoot == null || typing == null)
        {
            Debug.LogError($"[{nameof(DoorHackManager)}] minigameRoot 또는 typing 이 비어 있다.", this);
            return;
        }

        pendingKeypad = keypad;
        SuppressPlayer(player);

        typing.onHackSuccess.AddListener(HandleSuccess);
        minigameRoot.SetActive(true); // DoorHackTyping.OnEnable 이 첫 글자를 띄운다
    }

    private void HandleSuccess()
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
        if (typing != null) typing.onHackSuccess.RemoveListener(HandleSuccess);
        if (minigameRoot != null) minigameRoot.SetActive(false);

        RestorePlayer();
        pendingKeypad = null;
    }

    // 글자를 받아쳐야 하는데 WASD 도 그 글자에 들어간다. 입력을 잠그지 않으면
    // 타이핑하는 동안 플레이어가 걸어다닌다. 커서는 쓰지 않으므로 잠긴 채로 둔다.
    private void SuppressPlayer(IInteractor player)
    {
        suppressedInteractor = player as MonoBehaviour;
        if (suppressedInteractor == null) return;

        suppressedInteractor.enabled = false;

        lockedPlayer = suppressedInteractor.GetComponent<PlayerController>();
        if (lockedPlayer == null) return;

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
    }
}
