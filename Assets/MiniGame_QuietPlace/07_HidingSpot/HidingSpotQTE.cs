using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class HidingSpotQTE : MonoBehaviour
{
    [Header("QTE")]
    public string playerTag = "Player";
    public float interactRange = 2f;
    public GameObject qtePromptUI;
    public float qteDuration = 3f;

    [Header("숨기")]
    public PlayerMovement playerMovement;

    // GameScene 의 플레이어는 PlayerMovement 가 아니라 PlayerController 를 쓴다.
    // 비워두면 Start 에서 Player 태그로 직접 찾는다. 이게 없으면 숨은 채로 걸어다닌다.
    public PlayerController playerController;
    public Transform hideSnapPoint;
    public Renderer[] playerRenderersToHide;
    public Animator playerAnimator;
    public string hideAnimTrigger = "Hide";

    [Tooltip("숨었을 때 앉은 자세로 몸높이/시점을 낮출지 - 테이블 밑처럼 웅크려야 하는 " +
             "곳만 켠다. 캐비닛처럼 서서 숨는 곳은 꺼둔다(서 있는 채로 숨는 게 맞다).")]
    public bool crouchWhileHidden;

    [Header("발 그림자")]
    public Transform monster;
    public GameObject footShadowVisual;
    public float shadowVisibleRange = 4f;
    public float shadowMoveSpeed = 2f;

    [Header("나오기")]
    public string unhideAnimTrigger = "Unhide";
    public float exitForward = 1.10f;   // 숨은 자리에서 앞으로 이만큼 나온다

    public UnityEvent onHideSuccess;
    public UnityEvent onQteFailed;
    public UnityEvent onUnhide;

    public bool IsPlayerHidden { get; private set; }

    /// 어디든 숨어 있는 중인가. 숨은 동안 Esc 는 '나오기' 라서
    /// 일시정지 메뉴가 같이 뜨면 안 된다.
    public static bool AnyoneHidden { get; private set; }

    /// 공포 100% 순간의 비명. FearingState 와 같은 클립 이름 - 숨은 동안은 그 상태로
    /// 안 넘어가므로(3인칭 전환은 나중 작업) 여기서 직접 재생한다.
    const string FearScreamName = "malescreamHigh";

    private Transform player;
    private bool qteActive;
    private float qteTimer;
    private int hiddenFrame = -1;   // 숨은 그 프레임에 바로 다시 나오지 않게
    private int unhidFrame = -1;    // 나온 그 프레임에 바로 다시 숨지 않게

    /// HidingSpot 이 붙어 있으면 E 는 그쪽(PlayerInteractor 경유)이 처리한다.
    /// 여기서 또 읽으면 한 번의 E 로 '나오기 -> 다시 숨기' 가 같은 프레임에 일어나
    /// 영영 못 나온다. Esc 는 조준이 막혔을 때의 탈출구라 여기 남겨둔다.
    [System.NonSerialized] public bool externalOwnsEKey;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            player = playerObj.transform;
            if (playerMovement == null) playerMovement = playerObj.GetComponent<PlayerMovement>();
            if (playerController == null) playerController = playerObj.GetComponent<PlayerController>();
        }

        if (qtePromptUI != null) qtePromptUI.SetActive(false);
        if (footShadowVisual != null) footShadowVisual.SetActive(false);
    }

    void Update()
    {
        HandleUnhide();
        HandleQte();
        HandleFootShadow();
        TickFearWhileHidden();
    }

    /// <summary>
    /// 숨은 동안은 playerController.enabled = false 라 PlayerController.Update() 가 안 돌아서
    /// 공포(fear)가 안 쌓인다 - 이 컴포넌트는 계속 켜져 있으니 대신 여기서 불려준다.
    /// 산소는 손대지 않는다 - 숨어도 오직 공포만 쌓이게 한다.
    ///
    /// 가득 차도 FearingState 로 넘기지 않는다 - 그러면 3인칭 전환/노출이 필요해지는데
    /// 그건 나중 작업이다. 지금은 비명만 내고 소음 수치는 그대로 0 유지 -
    /// 숨어서 안전한 상태 자체는 깨지지 않아야 한다(괴물의 공격 판정은 오직
    /// currentNoiseLevel 로만 걸리므로, 이걸 안 건드리면 공격도 안 걸린다).
    /// </summary>
    void TickFearWhileHidden()
    {
        if (!IsPlayerHidden || playerController == null) return;

        PlayerStats stats = playerController.stats;
        stats.TickFear(Time.deltaTime);

        if (stats.IsFearMaxed)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(FearScreamName);
            stats.fear = 0f; // 정식 Fearing 지속시간이 없으므로 바로 리셋해 다음 번에 다시 쌓이게 한다.
        }
    }

    void HandleUnhide()
    {
        if (!IsPlayerHidden || Time.frameCount == hiddenFrame) return;
        if (Keyboard.current == null) return;

        bool esc = Keyboard.current.escapeKey.wasPressedThisFrame;
        bool e = !externalOwnsEKey && Keyboard.current.eKey.wasPressedThisFrame;
        if (!esc && !e) return;
        Unhide();
    }

    /// 숨은 자리에서 나온다. E 나 Esc, 또는 외부(몬스터가 떠났을 때 등)에서 부른다.
    public void Unhide()
    {
        if (!IsPlayerHidden) return;
        IsPlayerHidden = false;
        unhidFrame = Time.frameCount;
        AnyoneHidden = false;

        foreach (Renderer r in playerRenderersToHide)
        {
            if (r != null) r.enabled = true;
        }
        if (playerAnimator != null && !string.IsNullOrEmpty(unhideAnimTrigger))
            playerAnimator.SetTrigger(unhideAnimTrigger);
        if (footShadowVisual != null) footShadowVisual.SetActive(false);

        // 숨은 자리 앞으로 한 걸음. 막혀 있으면 제자리에 둔다 -
        // 벽 속으로 밀어 넣는 것보다 그 자리에 서 있는 편이 낫다.
        if (player != null && hideSnapPoint != null)
        {
            Vector3 exit = hideSnapPoint.position + hideSnapPoint.forward * exitForward;
            CharacterController cc = player.GetComponent<CharacterController>();
            float r = cc != null ? cc.radius : 0.30f;
            float h = cc != null ? cc.height : 1.80f;
            bool blocked = Physics.CheckCapsule(exit + Vector3.up * (r + 0.02f),
                                                exit + Vector3.up * Mathf.Max(h - r - 0.02f, r + 0.03f),
                                                r, ~0, QueryTriggerInteraction.Ignore);
            if (!blocked) MovePlayerTo(exit);
        }

        if (crouchWhileHidden && playerController != null) playerController.SnapSittingView(false);
        RestoreControl();

        onUnhide?.Invoke();
    }

    /// <summary>
    /// 조작을 되돌린다(숨기 나오기 / QTE 실패). playerController.enabled = false 는
    /// OnDisable() 에서 커서 잠금을 풀어버리는데(사망 연출 등에서는 그게 맞는 동작이다),
    /// 여기서 다시 켤 때는 정상 플레이로 돌아가는 것이므로 커서 잠금도 같이 되살려야 한다.
    /// </summary>
    void RestoreControl()
    {
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerController == null) return;

        playerController.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// CharacterController 는 켜진 채로 transform 을 옮기면 다음 프레임에 원래 자리로
    /// 되밀린다. 잠깐 끄고 옮겨야 실제로 이동한다.
    void MovePlayerTo(Vector3 position)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = position;
        if (cc != null) cc.enabled = true;
    }

    public void BeginQte()
    {
        if (IsPlayerHidden || qteActive) return;
        qteActive = true;
        qteTimer = 0f;
        if (qtePromptUI != null) qtePromptUI.SetActive(true);

        // 특정 상황이 되면 조작을 잠그고 QTE 판정만 받는다
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerController != null) playerController.enabled = false;
    }

    void HandleQte()
    {
        if (!qteActive) return;

        bool inRange = player != null && Vector3.Distance(transform.position, player.position) <= interactRange;

        if (inRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Hide();
            return;
        }

        qteTimer += Time.deltaTime;
        if (qteTimer >= qteDuration)
        {
            qteActive = false;
            if (qtePromptUI != null) qtePromptUI.SetActive(false);

            // 실패 - 숨는 데 실패했으니 조작권을 돌려준다
            RestoreControl();

            onQteFailed?.Invoke();
        }
    }

    public void Hide()
    {
        // 나온 그 프레임에 같은 입력으로 다시 숨지 않게
        if (Time.frameCount == unhidFrame) return;
        HideNow();
    }

    private void HideNow()
    {
        qteActive = false;
        IsPlayerHidden = true;
        AnyoneHidden = true;
        hiddenFrame = Time.frameCount;

        if (qtePromptUI != null) qtePromptUI.SetActive(false);

        // 정해진 동작: 숨는 지점으로 스냅 이동
        if (hideSnapPoint != null && player != null)
        {
            player.rotation = hideSnapPoint.rotation;
            MovePlayerTo(hideSnapPoint.position);
        }

        foreach (Renderer r in playerRenderersToHide)
        {
            if (r != null) r.enabled = false;
        }

        if (playerAnimator != null) playerAnimator.SetTrigger(hideAnimTrigger);

        // 테이블 밑처럼 웅크려야 하는 곳은 앉은 자세 시점으로 즉시 맞춘다 - 서 있는 채로
        // 얼면 시점이 천판 위로 튀어나와 이상해 보인다. PlayerController 를 끄기 전에
        // 해야 한다(꺼지면 Update 가 안 돌아 보간이 진행 안 되므로 즉시 대입이 필요).
        if (crouchWhileHidden && playerController != null) playerController.SnapSittingView(true);

        // 조작/시점 잠금. BeginQte 를 거쳐 왔으면 이미 꺼져 있지만, HidingSpot.Interact 의
        // 평범한 E 상호작용은 BeginQte 없이 곧장 Hide() 를 부르므로 여기서도 직접 꺼야
        // 확실히 잠긴다 - 안 그러면 숨은 채로 마우스가 시점을 계속 돌리고 WASD 로 걸어서
        // 나가버린다.
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerController != null) playerController.enabled = false;

        onHideSuccess?.Invoke();
    }

    void HandleFootShadow()
    {
        if (!IsPlayerHidden || monster == null || footShadowVisual == null) return;

        float distance = Vector3.Distance(transform.position, monster.position);
        bool visible = distance <= shadowVisibleRange;
        footShadowVisual.SetActive(visible);

        if (visible)
        {
            float offset = Mathf.Sin(Time.time * shadowMoveSpeed) * 0.3f;
            Vector3 pos = footShadowVisual.transform.localPosition;
            pos.x = offset;
            footShadowVisual.transform.localPosition = pos;
        }
    }
}
