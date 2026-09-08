using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 무거운 변압기 모듈을 자리에 맞춰 넣는 단계 - 화면 UI 방식.
///
/// 커서를 그대로 따라오면 슬롯 위에 커서를 올려두는 것만으로 끝나서 아무 긴장이 없다.
/// 그래서 위치를 직접 옮기지 않고 힘으로만 민다.
///
///  - 손은 스프링처럼 당긴다(pullStrength). 즉시 도달하지 않고 밀려 지나친다.
///  - 25kg 이 계속 아래로 끌어당긴다(gravity). 커서를 슬롯보다 위에 두고 버텨야 한다.
///  - 팔이 떨린다(tremor). 느리게 방향이 바뀌는 성분 + 빠른 진동 두 개를 겹쳐서,
///    가만히 두면 슬롯에서 밀려나므로 계속 반대로 밀어 붙여야 한다.
///  - 손을 놓으면 그대로 떨어진다.
///
/// 원래는 3D 월드의 큐브를 OnMouseDown 으로 끄는 HeavyModuleBalance 였는데, 어두운 방에서
/// 카메라 각도에 따라 큐브가 안 보이는 문제가 있어 화면 좌표로 옮겼다.
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
    public float grabRadius = 170f;

    [Header("손이 당기는 힘 - 낮으면 무겁고 둔하다")]
    public float pullStrength = 20f;

    [Header("속도가 죽는 정도 - 낮으면 더 출렁인다")]
    public float damping = 4.2f;

    [Header("아래로 계속 끌리는 무게 (픽셀/초^2)")]
    public float gravity = 900f;

    [Header("손을 놓으면 이 배수로 떨어진다")]
    public float releaseGravityScale = 2.5f;

    /// 크기가 일정한 힘이 서서히 방향을 바꾼다. Perlin 노이즈만 쓰면 값이 대부분 0 근처라
    /// 커서를 균형점에 놓고 가만히 있기만 해도 통과됐다. 항상 이만큼 밀어내야
    /// 플레이어가 계속 반대로 되받아치게 된다. 밀려나는 거리는 swayStrength/pullStrength.
    [Header("팔 떨림 - 방향이 서서히 도는 힘 (항상 이만큼 밀어낸다)")]
    public float swayStrength = 900f;

    [Header("그 힘이 도는 속도 (라디안/초)")]
    public float swaySpeed = 0.85f;

    [Header("팔 떨림 - 빠른 성분 (부들부들)")]
    public float tremorStrength = 620f;
    public float tremorSpeed = 11f;

    [Header("슬롯 중심에서 이 픽셀 안에 있으면 안착 판정")]
    public float toleranceRadius = 40f;

    [Header("이 시간을 버텨야 들어간다(초)")]
    public float requiredHoldTime = 3f;

    [Header("UI")]
    public Image gaugeFill;
    public Image slotFrame;
    public Color inToleranceColor = new Color(0.4f, 0.95f, 0.5f);
    public Color outOfToleranceColor = new Color(0.95f, 0.4f, 0.35f);
    public TMP_Text hintLabel;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip seatSound;

    public UnityEvent onSeated;
    public UnityEvent onDropped;

    const string HintIdle = "모듈을 왼쪽 버튼으로 잡아라";
    const string HintOut = "무게에 밀린다 · 커서를 슬롯보다 위에 두고 버텨라";
    const string HintDone = "모듈이 안착됐다";

    private bool dragging;
    private bool seated;
    private bool hasStart;
    private float holdTimer;
    private Vector2 grabOffset;
    private Vector2 startPosition;
    private Vector2 velocity;
    private float swayPhase;
    private float seedA, seedB, seedC, seedD;

    void Awake()
    {
        seedA = Random.Range(0f, 100f);
        seedB = Random.Range(0f, 100f);
        seedC = Random.Range(0f, 100f);
        seedD = Random.Range(0f, 100f);
    }

    void OnEnable()
    {
        seated = false;
        dragging = false;
        holdTimer = 0f;
        velocity = Vector2.zero;
        swayPhase = Random.Range(0f, Mathf.PI * 2f);

        if (module != null)
        {
            if (!hasStart) { startPosition = module.anchoredPosition; hasStart = true; }
            module.anchoredPosition = startPosition;
            module.localRotation = Quaternion.identity;
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

        if (mouse.leftButton.wasPressedThisFrame && cursorValid
            && (module.anchoredPosition - cursor).magnitude <= grabRadius)
        {
            dragging = true;
            grabOffset = module.anchoredPosition - cursor;
        }

        if (dragging && !mouse.leftButton.isPressed)
        {
            dragging = false;
            holdTimer = 0f;
            Refresh(HintIdle, false);
            onDropped?.Invoke();
        }

        Step(cursor, cursorValid);

        bool inTolerance = Vector2.Distance(module.anchoredPosition, slot.anchoredPosition) <= toleranceRadius;

        if (dragging && inTolerance)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= requiredHoldTime) { Seat(); return; }
            Refresh("버텨라 · " + (requiredHoldTime - holdTimer).ToString("0.0") + "초", true);
        }
        else
        {
            holdTimer = 0f;
            Refresh(dragging ? HintOut : HintIdle, inTolerance);
        }
    }

    /// 위치를 직접 옮기지 않고 힘만 준다. 그래서 둔하게 밀리고, 지나치고, 떨린다.
    private void Step(Vector2 cursor, bool cursorValid)
    {
        float dt = Time.deltaTime;
        Vector2 accel = Vector2.zero;

        if (dragging && cursorValid)
        {
            Vector2 target = cursor + grabOffset;
            accel += (target - module.anchoredPosition) * pullStrength;
            accel += Vector2.down * gravity;
            accel += Sway() + Tremor();
        }
        else
        {
            accel += Vector2.down * gravity * releaseGravityScale;
        }

        velocity += accel * dt;
        velocity -= velocity * Mathf.Min(1f, damping * dt);

        Vector2 next = module.anchoredPosition + velocity * dt;
        Vector2 clamped = ClampToBounds(next);

        // 벽에 닿으면 그 방향 속도를 죽인다. 안 그러면 벽에 붙어 떨기만 한다.
        if (!Mathf.Approximately(clamped.x, next.x)) velocity.x = 0f;
        if (!Mathf.Approximately(clamped.y, next.y)) velocity.y = 0f;

        module.anchoredPosition = clamped;

        // 무게가 실린 쪽으로 살짝 기운다
        module.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-velocity.x * 0.02f, -12f, 12f));
    }

    // 크기가 일정한 힘이 서서히 방향을 바꾼다. 세기에만 약간 흔들림을 줘서
    // 완전히 예측 가능하지는 않게 한다.
    private Vector2 Sway()
    {
        // 도는 속도 자체도 조금씩 변한다
        float rate = swaySpeed * (0.7f + Mathf.PerlinNoise(Time.time * 0.35f, seedA) * 0.6f);
        swayPhase += rate * Time.deltaTime;

        float wobble = 0.75f + Mathf.PerlinNoise(Time.time * 0.7f, seedB) * 0.5f;
        return new Vector2(Mathf.Cos(swayPhase), Mathf.Sin(swayPhase)) * swayStrength * wobble;
    }

    // 빠른 진동. 눈에 부들부들 떠는 것으로 보인다.
    private Vector2 Tremor()
    {
        return new Vector2(
            (Mathf.PerlinNoise(Time.time * tremorSpeed, seedC) - 0.5f) * 2f,
            (Mathf.PerlinNoise(Time.time * tremorSpeed, seedD) - 0.5f) * 2f) * tremorStrength;
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
        velocity = Vector2.zero;
        holdTimer = requiredHoldTime;

        module.anchoredPosition = slot.anchoredPosition;
        module.localRotation = Quaternion.identity;

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
