using UnityEngine;
using UnityEngine.UI;

// 머그컵에서 김이 계속 피어오르는 효과.
// 퍼프 하나가 아래에서 위로 올라가며 커지고 옅어지는 것을 반복한다.
public class RisingSteamWisp : MonoBehaviour
{
    [SerializeField] private Image puff;

    [Header("상승")]
    [SerializeField] private float riseDistance = 130f;
    [SerializeField] private float cycleDuration = 4.5f;
    [Tooltip("여러 개를 겹칠 때 서로 다른 값을 주면 자연스러움")]
    [SerializeField] private float phaseOffset = 0f;

    [Header("좌우 흔들림")]
    [SerializeField] private float swayAmount = 18f;
    [SerializeField] private float swaySpeed = 0.8f;

    [Header("크기 / 투명도")]
    [SerializeField] private float startScale = 0.45f;
    [SerializeField] private float endScale = 1.15f;
    [Range(0f, 1f)]
    [SerializeField] private float peakAlpha = 0.16f;

    private RectTransform _rt;
    private Vector2 _basePos;
    private float _seed;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (puff == null) puff = GetComponent<Image>();
        _basePos = _rt.anchoredPosition;
        _seed = Random.Range(0f, 100f);
    }

    void OnEnable()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (puff == null) return;

        float cycle = cycleDuration <= 0.01f ? 1f : cycleDuration;
        float t = Mathf.Repeat((Time.time + phaseOffset) / cycle, 1f);

        // 위로 상승
        float y = _basePos.y + riseDistance * t;

        // 올라가면서 좌우로 흐느적
        float sway = Mathf.Sin(Time.time * swaySpeed + _seed) * swayAmount * t;
        _rt.anchoredPosition = new Vector2(_basePos.x + sway, y);

        // 올라갈수록 커짐
        float scale = Mathf.Lerp(startScale, endScale, t);
        _rt.localScale = Vector3.one * scale;

        // 나타났다가 사라짐 (양 끝에서 0)
        float fade = Mathf.Sin(t * Mathf.PI);
        Color c = puff.color;
        c.a = peakAlpha * fade;
        puff.color = c;
    }
}
