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
    public Transform hideSnapPoint;
    public Renderer[] playerRenderersToHide;
    public Animator playerAnimator;
    public string hideAnimTrigger = "Hide";

    [Header("발 그림자")]
    public Transform monster;
    public GameObject footShadowVisual;
    public float shadowVisibleRange = 4f;
    public float shadowMoveSpeed = 2f;

    public UnityEvent onHideSuccess;
    public UnityEvent onQteFailed;

    public bool IsPlayerHidden { get; private set; }

    private Transform player;
    private bool qteActive;
    private float qteTimer;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null) player = playerObj.transform;

        if (qtePromptUI != null) qtePromptUI.SetActive(false);
        if (footShadowVisual != null) footShadowVisual.SetActive(false);
    }

    void Update()
    {
        HandleQte();
        HandleFootShadow();
    }

    public void BeginQte()
    {
        if (IsPlayerHidden || qteActive) return;
        qteActive = true;
        qteTimer = 0f;
        if (qtePromptUI != null) qtePromptUI.SetActive(true);

        // 특정 상황이 되면 조작을 잠그고 QTE 판정만 받는다
        if (playerMovement != null) playerMovement.enabled = false;
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

            onQteFailed?.Invoke();
        }
    }

    void Hide()
    {
        qteActive = false;
        IsPlayerHidden = true;

        if (qtePromptUI != null) qtePromptUI.SetActive(false);

        // 정해진 동작: 숨는 지점으로 스냅 이동
        if (hideSnapPoint != null && player != null)
        {
            player.position = hideSnapPoint.position;
            player.rotation = hideSnapPoint.rotation;
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
