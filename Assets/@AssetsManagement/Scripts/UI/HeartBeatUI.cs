using UnityEngine;
using UnityEngine.UI;

public class HeartBeatUI : MonoBehaviour
{
    public PlayerController player;
    public Image heartImage;

    [Header("Size")]
    public float baseScale = 2.3f;

    [Header("Bounce (normal heartbeat pulse, disabled while Stopping)")]
    public float bounceSpeed = 2.5f;
    public float bounceAmount = 0.12f;

    [Header("Shake (critical oxygen warning, just before Gasping)")]
    public float shakeIntensity = 8f;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color cautionColor = new Color(1f, 0.55f, 0f); // Stopping 아니지만 산소 50% 미만
    public Color warningColor = Color.red;

    private RectTransform rt;
    private Vector2 basePosition;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        basePosition = rt.anchoredPosition;
        if (heartImage == null) heartImage = GetComponent<Image>();
    }

    void Update()
    {
        if (player == null || heartImage == null) return;

        PlayerStateType state = player.CurrentStateType;
        bool isStopping = state == PlayerStateType.Stopping;
        bool isGasping = state == PlayerStateType.Gasping;
        float oxygen = player.stats.oxygen;
        bool belowGaspingThreshold = oxygen < player.stats.gaspingThreshold;
        bool critical = isStopping && oxygen < player.stats.criticalOxygenThreshold;

        // Stopping/Gasping 둘 다 이동이 불가능한 상태라, 다시 움직일 수 있는 상태로 돌아갈 때까지 빨간색 유지
        bool warning = (isStopping && belowGaspingThreshold) || isGasping;
        // 아직 움직일 수 있는 상태인데 산소가 50% 밑으로 떨어진 경우 - 위험 전 단계 경고
        bool caution = !isStopping && !isGasping && belowGaspingThreshold;
        heartImage.color = warning ? warningColor : caution ? cautionColor : normalColor;

        if (isStopping)
        {
            rt.anchoredPosition = critical ? basePosition + Random.insideUnitCircle * shakeIntensity : basePosition;
            rt.localScale = new Vector3(baseScale, baseScale, 1f);
        }
        else
        {
            rt.anchoredPosition = basePosition;
            float effectiveBounceSpeed = oxygen >= player.stats.oxygenMax ? bounceSpeed * 0.5f : bounceSpeed;
            float scale = baseScale * (1f + Mathf.Sin(Time.time * effectiveBounceSpeed * Mathf.PI * 2f) * bounceAmount);
            rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
