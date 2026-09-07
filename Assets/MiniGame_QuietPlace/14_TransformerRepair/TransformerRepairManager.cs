using UnityEngine;

/// <summary>
/// 변압기 모듈 수리 미니게임의 진입점 (볼트 풀기 -> 모듈 안착 -> 색선 연결).
///
/// FuseRepairManager 와 같은 이유로 싱글톤이다. 맵 프리팹 안의 FuseSlot 이
/// 씬에 있는 미니게임 오브젝트를 직접 참조할 수 없다.
///
/// 이 미니게임은 퓨즈 쪽과 달리 3D 월드에서 마우스로 조작한다(OnMouseDown / 드래그).
/// 그래서 두 가지를 추가로 처리해야 한다.
///  - 카메라 주입: DraggableWire 와 HeavyModuleBalance 가 cam 을 필요로 하는데
///    프리팹은 씬 카메라를 담을 수 없으므로 시작할 때 넣어준다.
///  - 커서 해제: PlayerController 가 커서를 잠가두는데 OnMouseDown 은 커서가 필요하다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class TransformerRepairManager : MonoBehaviour
{
    public static TransformerRepairManager Instance { get; private set; }

    [Header("미니게임 오브젝트 전체 루트 (평소에는 꺼둔다)")]
    [SerializeField] private GameObject minigameRoot;

    [Header("3단계를 진행시키는 시퀀서")]
    [SerializeField] private TransformerRepairSequence sequence;

    [Header("카메라를 넣어줄 대상들")]
    [SerializeField] private HeavyModuleBalance moduleBalance;
    [SerializeField] private DraggableWire[] wires;

    [Header("비우면 Camera.main 을 쓴다")]
    [SerializeField] private Camera overrideCamera;

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

    public void Begin(FuseSlot slot, IInteractor player)
    {
        if (IsRunning || slot == null) return;

        if (minigameRoot == null || sequence == null)
        {
            Debug.LogError($"[{nameof(TransformerRepairManager)}] minigameRoot 또는 sequence 가 비어 있다.", this);
            return;
        }

        Camera cam = overrideCamera != null ? overrideCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogError($"[{nameof(TransformerRepairManager)}] 카메라를 찾지 못했다. " +
                           "MainCamera 태그가 붙은 카메라가 있는지, 또는 Override Camera 를 채웠는지 확인할 것.", this);
            return;
        }

        pendingSlot = slot;
        pendingPlayer = player;

        InjectCamera(cam);
        SuppressPlayer(player);

        minigameRoot.SetActive(true);
        sequence.onTransformerFixed.AddListener(HandleFixed);
    }

    private void HandleFixed()
    {
        FuseSlot slot = pendingSlot;
        IInteractor player = pendingPlayer;

        Finish();

        if (slot != null) slot.CompleteInstall(player);
    }

    /// <summary>중간에 그만둘 때. 진행한 단계는 그대로 남는다(볼트는 이미 빠져 있는 등).</summary>
    public void Cancel()
    {
        if (!IsRunning) return;
        Finish();
    }

    private void Finish()
    {
        if (sequence != null) sequence.onTransformerFixed.RemoveListener(HandleFixed);
        if (minigameRoot != null) minigameRoot.SetActive(false);

        RestorePlayer();

        pendingSlot = null;
        pendingPlayer = null;
    }

    // cam 은 public 필드라 그대로 대입한다. 런타임 Find 를 쓰지 않으려고
    // 대상을 인스펙터에 미리 담아둔다(CLAUDE.md: Find 계열 금지).
    private void InjectCamera(Camera cam)
    {
        if (moduleBalance != null) moduleBalance.cam = cam;

        if (wires == null) return;
        for (int i = 0; i < wires.Length; i++)
            if (wires[i] != null) wires[i].cam = cam;
    }

    private void SuppressPlayer(IInteractor player)
    {
        // OnMouseDown / 드래그가 동작하려면 커서가 보이고 풀려 있어야 한다
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
