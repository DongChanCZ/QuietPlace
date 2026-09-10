using UnityEngine;

/// <summary>
/// 상태를 색으로 알려주는 작은 표시등. 기본은 빨강(고장)이고, 수리가 끝나면 초록으로 바뀐다.
///
/// UnityEvent 에서 TurnOn() 을 부를 수 있게 만들었다. UnityEvent 는 색 같은 값을
/// 직접 넣을 수 없어서, 색 전환을 이렇게 메서드로 감싸야 인스펙터에서 연결된다.
/// </summary>
[DisallowMultipleComponent]
public class IndicatorLed : MonoBehaviour
{
    [Header("색을 바꿀 렌더러 (비우면 이 오브젝트의 것)")]
    [SerializeField] private Renderer target;

    [Header("고장 / 정상")]
    [SerializeField] private Color offColor = new Color(0.85f, 0.12f, 0.12f);
    [SerializeField] private Color onColor = new Color(0.20f, 0.95f, 0.35f);

    [Header("발광 세기 - 어두운 맵에서 눈에 띄게")]
    [SerializeField] private float emissionIntensity = 2.5f;

    [Header("시작 상태")]
    [SerializeField] private bool startOn;

    [Header("같이 색을 바꿀 조명 (없으면 비워둔다)")]
    [SerializeField] private Light lamp;

    [Header("정상이 되면 끌 것 - 고장 중 깜빡이던 BlinkingLight 등")]
    [SerializeField] private Behaviour stopWhenOn;

    public bool IsOn { get; private set; }

    private Material mat;

    void Awake()
    {
        Renderer r = target != null ? target : GetComponent<Renderer>();
        if (r == null)
        {
            Debug.LogWarning($"[{nameof(IndicatorLed)}] 색을 바꿀 Renderer 를 찾지 못했다.", this);
            return;
        }

        // .material 은 이 렌더러만의 인스턴스를 만든다. .sharedMaterial 을 건드리면
        // 같은 머티리얼을 쓰는 다른 오브젝트까지 전부 색이 바뀐다.
        mat = r.material;
        Apply(startOn);
    }

    public void TurnOn() => Apply(true);
    public void TurnOff() => Apply(false);
    public void Toggle() => Apply(!IsOn);

    private void Apply(bool on)
    {
        IsOn = on;

        Color c = on ? onColor : offColor;

        // 실제 조명도 같은 색으로. 깜빡이던 것은 정상이 되면 멈춰야 계속 켜져 있다.
        if (lamp != null) lamp.color = c;
        if (stopWhenOn != null && on) stopWhenOn.enabled = false;

        if (mat == null) return;

        // URP Lit 은 _BaseColor 를, 구 Standard 는 _Color 를 쓴다. 있는 쪽만 설정한다.
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);

        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c * emissionIntensity);
    }
}
