using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Events;

public class HeavyModuleBalance : MonoBehaviour
{
    [Header("카메라")]
    public Camera cam;

    [Header("드래그 (무게감 있는 추종 - Pseudo-Haptic)")]
    public float followSpeed = 3f;

    [Header("안착 목표")]
    public Transform slotTarget;
    public float toleranceRadius = 0.15f;

    [Header("흔들림 (자꾸 밀려나는 무게, Perlin 드리프트)")]
    public float driftStrength = 0.5f;
    public float driftSpeed = 1.5f;

    [Header("판정")]
    public float requiredHoldTime = 2f;

    [Header("UI 피드백 (선택)")]
    public Image balanceGaugeFill;
    public Image driftIndicator;
    public Color inToleranceColor = Color.green;
    public Color outOfToleranceColor = Color.red;

    public UnityEvent onSeated;
    public UnityEvent onDropped;

    private bool dragging;
    private bool seated;
    private float holdTimer;
    private Vector3 dragOffset;
    private float driftSeedX;
    private float driftSeedY;

    void Start()
    {
        driftSeedX = Random.Range(0f, 100f);
        driftSeedY = Random.Range(0f, 100f);
    }

    void OnMouseDown()
    {
        if (seated) return;
        dragging = true;
        dragOffset = transform.position - GetMouseWorldPos();
    }

    void OnMouseUp()
    {
        if (seated) return;
        dragging = false;
        holdTimer = 0f;
        UpdateGauge();
        onDropped?.Invoke();
    }

    void Update()
    {
        if (seated || !dragging) return;

        Vector3 targetPos = GetMouseWorldPos() + dragOffset;
        transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);

        Vector3 drift = new Vector3(
            (Mathf.PerlinNoise(Time.time * driftSpeed, driftSeedX) - 0.5f) * 2f,
            (Mathf.PerlinNoise(Time.time * driftSpeed, driftSeedY) - 0.5f) * 2f,
            0f) * driftStrength * Time.deltaTime;
        transform.position += drift;

        bool inTolerance = Vector3.Distance(transform.position, slotTarget.position) <= toleranceRadius;

        if (driftIndicator != null)
        {
            driftIndicator.color = inTolerance ? inToleranceColor : outOfToleranceColor;
        }

        if (inTolerance)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= requiredHoldTime)
            {
                Seat();
                return;
            }
        }
        else
        {
            holdTimer = 0f;
        }

        UpdateGauge();
    }

    void Seat()
    {
        seated = true;
        dragging = false;
        transform.position = slotTarget.position;
        UpdateGauge();
        onSeated?.Invoke();
    }

    void UpdateGauge()
    {
        if (balanceGaugeFill != null) balanceGaugeFill.fillAmount = holdTimer / requiredHoldTime;
    }

    Vector3 GetMouseWorldPos()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        float depth = cam.WorldToScreenPoint(transform.position).z;
        Vector3 screenPos = new Vector3(mouseScreen.x, mouseScreen.y, depth);
        return cam.ScreenToWorldPoint(screenPos);
    }
}
