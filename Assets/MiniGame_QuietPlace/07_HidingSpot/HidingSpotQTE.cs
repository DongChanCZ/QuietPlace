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

    private Transform player;
    private bool qteActive;
    private float qteTimer;
    private int hiddenFrame = -1;   // 숨은 그 프레임에 바로 다시 나오지 않게

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
    }

    void HandleUnhide()
    {
        if (!IsPlayerHidden || Time.frameCount == hiddenFrame) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current.eKey.wasPressedThisFrame && !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        Unhide();
    }

    /// 숨은 자리에서 나온다. E 나 Esc, 또는 외부(몬스터가 떠났을 때 등)에서 부른다.
    public void Unhide()
    {
        if (!IsPlayerHidden) return;
        IsPlayerHidden = false;
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

        if (playerMovement != null) playerMovement.enabled = true;
        if (playerController != null) playerController.enabled = true;

        onUnhide?.Invoke();
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
            if (playerMovement != null) playerMovement.enabled = true;
            if (playerController != null) playerController.enabled = true;

            onQteFailed?.Invoke();
        }
    }

    public void Hide()
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

        // 숨은 동안은 계속 조작 잠금 상태 유지 (playerMovement.enabled = false 그대로)

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
