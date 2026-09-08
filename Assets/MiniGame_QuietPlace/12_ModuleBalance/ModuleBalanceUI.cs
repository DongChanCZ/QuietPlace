using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 무거운 변압기 모듈을 자리에 맞춰 넣는 단계 - 화면 UI 방식.
///
/// 원래는 3D 월드의 큐브를 OnMouseDown 으로 끄는 HeavyModuleBalance 였다. 그런데
/// 이 미니게임이 열릴 때 카메라는 플레이어가 보고 있던 각도로 잠기고, 어두운 방에서
/// 작은 큐브가 배전반 뒤에 가리거나 화면 밖으로 나가 버려서 "무엇을 어떻게 움직여야
/// 하는지" 자체가 안 보였다. 목표 지점도 렌더러 없는 빈 Transform 이라 보이지 않았다.
/// 그래서 나머지 미니게임들과 같이 화면 좌표에서 확실히 보이도록 다시 만들었다.
///
/// 조작: 모듈을 왼쪽 버튼으로 잡고 슬롯 안으로 옮긴 뒤, 그 안에서 정해진 시간을 버틴다.
/// 무게 때문에 커서를 늦게 따라오고(followSpeed) 계속 밀려난다(Perlin 드리프트).
/// </summary>
public class ModuleBalanceUI : MonoBehaviour
{
    [Header("끌어야 하는 모듈")]
    public RectTransform module;

    [Header("넣어야 하는 자리")]
    public RectTransform slot;

    [Header("모듈이 움직일 수 있는 영역")]
    public RectTransform bounds;

    [Header("이 픽셀 안을 눌러야 모듈을 잡는다")]
    public float grabRadius = 150f;

    [Header("커서를 따라오는 속도 - 낮으면 무겁게 느껴진다")]
    public float followSpeed = 3.2f;

    [Header("자꾸 밀려나는 무게 (픽셀/초)")]
    public float driftStrength = 220f;
    public float driftSpeed = 1.3f;

    [Header("슬롯 중심에서 이 픽셀 안에 있으면 안착 판정")]
    public float toleranceRadius = 48f;

    [Header("이 시간을 버텨야 들어간다(초)")]
    public float requiredHoldTime = 2f;

    [Header("UI")]
    public Image gaugeFill;
    public Image slotFrame;           // 안에 들어오면 색이 바뀐다
    public Color inToleranceColor = new Color(0.4f, 0.95f, 0.5f);
    public Color outOfToleranceColor = new Color(0.95f, 0.4f, 0.35f);
    public TMP_Text hintLabel;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip seatSound;

    public UnityEvent onSeated;
    public UnityEvent onDropped;

    const string HintIdle = "모듈을 왼쪽 버튼으로 잡아 슬롯에 넣어라";
    const string HintOut = "슬롯에서 벗어났다 · 무게에 밀리지 않게 붙잡아라";
    const string HintDone = "모듈이 안착됐다";

    private bool dragging;
    private bool seated;
    private float holdTimer;
    private Vector2 grabOffset;
    private Vector2 startPosition;
    private float driftSeedX;
    private float driftSeedY;

    void Awake()
    {
        driftSeedX = Random.Range(0f, 100f);
        driftSeedY = Random.Range(0f, 100f);
    }

    void OnEnable()
    {
        seated = false;
        dragging = false;
        holdTimer = 0f;

        if (module != null)
        {
            // 처음 위치는 프리팹에 잡아둔 자리다. 다시 열 때 그 자리로 되돌린다.
            if (startPosition == Vector2.zero) startPosition = module.anchoredPosition;
            module.anchoredPosition = startPosition;
        }

        Refresh(HintIdle, false);
    }

    void Update()
    {
        if (seated || module == null || slot == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 cursor;
        bool cursorValid = TryGetLocalCursor(mouse.position.ReadValue(), out cursor);

        if (mouse.leftButton.wasPressedThisFrame && cursorValid)
        {
            Vector2 toModule = module.anchoredPosition - cursor;
            if (toModule.magnitude <= grabRadius)
            {
                dragging = true;
                grabOffset = toModule;
            }
        }

        if (!mouse.leftButton.isPressed && dragging)
        {
            dragging = false;
            holdTimer = 0f;
            Refresh(HintIdle, false);
            onDropped?.Invoke();
        }

        if (dragging && cursorValid) MoveModule(cursor);

        bool inTolerance = Vector2.Distance(module.anchoredPosition, slot.anchoredPosition) <= toleranceRadius;

        // 손을 놓고 있으면 시간이 차지 않는다. 붙잡고 버티는 게 이 단계의 내용이다.
        if (dragging && inTolerance)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= requiredHoldTime) { Seat(); return; }
            Refresh("그대로 유지 · " + (requiredHoldTime - holdTimer).ToString("0.0") + "초", true);
        }
        else
        {
            if (holdTimer > 0f) holdTimer = 0f;
            Refresh(dragging ? HintOut : HintIdle, inTolerance);
        }
    }

    /// 커서를 늦게 따라오고, 그러는 동안 무게로 계속 밀려난다.
    private void MoveModule(Vector2 cursor)
    {
        Vector2 target = cursor + grabOffset;
        Vector2 pos = Vector2.Lerp(module.anchoredPosition, target, followSpeed * Time.deltaTime);

        Vector2 drift = new Vector2(
            (Mathf.PerlinNoise(Time.time * driftSpeed, driftSeedX) - 0.5f) * 2f,
            (Mathf.PerlinNoise(Time.time * driftSpeed, driftSeedY) - 0.5f) * 2f) * driftStrength * Time.deltaTime;

        pos += drift;
        module.anchoredPosition = ClampToBounds(pos);
    }

    private Vector2 ClampToBounds(Vector2 pos)
    {
        if (bounds == null) return pos;

        Vector2 half = bounds.rect.size * 0.5f;
        Vector2 moduleHalf = module.rect.size * 0.5f;
        return new Vector2(
            Mathf.Clamp(pos.x, -half.x + moduleHalf.x, half.x - moduleHalf.x),
            Mathf.Clamp(pos.y, -half.y + moduleHalf.y, half.y - moduleHalf.y));
    }

    private void Seat()
    {
        seated = true;
        dragging = false;
        holdTimer = requiredHoldTime;
        module.anchoredPosition = slot.anchoredPosition;

        Refresh(HintDone, true);
        if (audioSource != null && seatSound != null) audioSource.PlayOneShot(seatSound);
        onSeated?.Invoke();
    }

    /// 커서를 이 UI 의 로컬 좌표로. 화면 밖이면 false.
    private bool TryGetLocalCursor(Vector2 screen, out Vector2 local)
    {
        local = Vector2.zero;

        RectTransform space = bounds != null ? bounds : (RectTransform)transform;
        Canvas canvas = space.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, cam, out local);
    }

    /// message 가 null 이면 문구는 그대로 두고 게이지/색만 갱신한다.
    private void Refresh(string message, bool inTolerance)
    {
        if (gaugeFill != null)
            gaugeFill.fillAmount = Mathf.Clamp01(holdTimer / Mathf.Max(0.01f, requiredHoldTime));

        if (slotFrame != null)
            slotFrame.color = inTolerance ? inToleranceColor : outOfToleranceColor;

        if (hintLabel != null && message != null) hintLabel.text = message;
    }
}
