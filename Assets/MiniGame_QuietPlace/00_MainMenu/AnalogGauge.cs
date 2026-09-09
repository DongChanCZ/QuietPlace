using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낡은 아날로그 전압계. 0~1 값을 바늘 각도로 옮긴다.
///
/// 값을 그대로 각도에 꽂으면 디지털 막대를 둥글게 만든 것에 지나지 않는다. 실제 계기의
/// 바늘은 질량이 있어서 목표를 지나쳐 넘어갔다 돌아오고, 전류가 흔들리면 미세하게 떨린다.
/// 그래서 스프링(따라가는 힘) + 감쇠(가라앉는 힘)로 바늘을 물리적으로 움직이고,
/// 값이 클수록 커지는 떨림을 얹는다. 게이지가 살아 있는 것처럼 보이는 건 이 두 가지다.
///
/// 눈금과 바늘은 에디터에서 미리 만들어 꽂는다(AnalogGaugeBuilder). 런타임에 만들지 않는다.
/// </summary>
public class AnalogGauge : MonoBehaviour
{
    [Header("바늘 - 아래쪽 끝이 회전축(pivot)이어야 한다")]
    [SerializeField] private RectTransform needle;

    [Header("바늘이 훑는 각도 (0일 때 -> 1일 때). 시계방향이 음수다")]
    [SerializeField] private float minAngle = 48f;
    [SerializeField] private float maxAngle = -48f;

    [Header("바늘의 무게 - 따라가는 힘 / 가라앉는 힘")]
    [Tooltip("클수록 민첩하게 따라간다")]
    [SerializeField] private float springStrength = 220f;
    [Tooltip("작을수록 목표를 지나쳐 크게 흔들린다")]
    [SerializeField] private float damping = 16f;

    [Header("떨림 - 값이 클수록 심해진다(도)")]
    [SerializeField] private float jitterDegrees = 0.9f;
    [SerializeField] private float jitterSpeed = 14f;

    [Header("위험 구간 표시 (없어도 된다)")]
    [SerializeField] private Graphic dangerArc;
    [Range(0f, 1f)]
    [SerializeField] private float dangerFrom = 0.8f;

    [Header("바늘이 위험 구간에 들어갔을 때의 색")]
    [SerializeField] private Graphic needleGraphic;
    [SerializeField] private Color needleNormal = new Color(0.12f, 0.11f, 0.10f);
    [SerializeField] private Color needleDanger = new Color(0.78f, 0.16f, 0.12f);

    [Header("숫자 표시 (없어도 된다)")]
    [SerializeField] private TMP_Text readout;
    [SerializeField] private string readoutSuffix = " V";
    [SerializeField] private float readoutScale = 240f;

    [Header("바늘 끝이 지나간 자리에 남는 빛 (없어도 된다)")]
    [SerializeField] private Graphic glowLamp;

    /// 지금 지시하는 값(0~1). 바늘이 따라가는 목표다.
    public float Value01 { get; private set; }

    /// 바늘이 실제로 가리키는 값. 지나쳐 넘어간 만큼 1을 넘을 수 있다.
    public float NeedleValue01 => _needle01;

    public bool InDanger => _needle01 >= dangerFrom;

    private float _needle01;
    private float _velocity;
    private float _jitterSeed;
    private bool _lastDanger;

    void Awake()
    {
        _jitterSeed = Random.value * 100f;
    }

    void OnEnable()
    {
        // 열 때마다 바늘은 0 에서 시작한다. 남아 있던 값에서 튀어오르면 이상하다.
        Value01 = 0f;
        _needle01 = 0f;
        _velocity = 0f;
        _lastDanger = false;
        ApplyNeedle();
        ApplyDanger(true);
    }

    /// <summary>계기에 보낼 값. 매 프레임 불러도 된다.</summary>
    public void SetValue01(float value)
    {
        Value01 = Mathf.Clamp01(value);
    }

    /// <summary>바늘을 목표에 즉시 붙인다(연출 없이 값만 맞출 때).</summary>
    public void SnapTo(float value)
    {
        Value01 = Mathf.Clamp01(value);
        _needle01 = Value01;
        _velocity = 0f;
        ApplyNeedle();
        ApplyDanger(true);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 스프링-감쇠. 목표를 지나쳐 넘어갔다 돌아온다.
        float accel = (Value01 - _needle01) * springStrength - _velocity * damping;
        _velocity += accel * dt;
        _needle01 += _velocity * dt;

        // 바늘은 눈금판 밖으로 못 나간다. 끝에 부딪히면 튕겨 되돌아온다.
        if (_needle01 < -0.02f) { _needle01 = -0.02f; _velocity = -_velocity * 0.25f; }
        else if (_needle01 > 1.06f) { _needle01 = 1.06f; _velocity = -_velocity * 0.25f; }

        ApplyNeedle();
        ApplyDanger(false);
    }

    private void ApplyNeedle()
    {
        if (needle == null) return;

        // 떨림은 값이 클 때(전압이 높을 때) 심해진다.
        float shake = 0f;
        if (jitterDegrees > 0f)
        {
            float t = Time.time * jitterSpeed + _jitterSeed;
            shake = (Mathf.PerlinNoise(t, _jitterSeed) - 0.5f) * 2f * jitterDegrees * Mathf.Clamp01(_needle01);
        }

        float angle = Mathf.Lerp(minAngle, maxAngle, _needle01) + shake;
        needle.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (readout != null)
            readout.text = Mathf.RoundToInt(Mathf.Max(0f, _needle01) * readoutScale) + readoutSuffix;
    }

    private void ApplyDanger(bool force)
    {
        bool danger = InDanger;
        if (!force && danger == _lastDanger) return;
        _lastDanger = danger;

        if (needleGraphic != null) needleGraphic.color = danger ? needleDanger : needleNormal;
        if (glowLamp != null) glowLamp.gameObject.SetActive(danger);
        if (dangerArc != null)
        {
            Color c = dangerArc.color;
            c.a = danger ? 0.85f : 0.35f;
            dangerArc.color = c;
        }
    }
}
