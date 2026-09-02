using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 모니터 마지막 마침표가 번쩍이면서 아주 살짝 흔들리는 효과.
// 원본 이미지의 점은 패치로 가려두고, 이 합성 점이 대신 살아 움직인다.
public class FlickeringDot : MonoBehaviour
{
    [SerializeField] private Image dot;

    [Header("밝기 (평소 은은하게 -> 가끔 번쩍)")]
    [Range(0f, 1f)]
    [SerializeField] private float baseAlpha = 0.55f;
    [Range(0f, 1f)]
    [SerializeField] private float flashAlpha = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float dimAlpha = 0.12f;

    [Header("번쩍이는 주기")]
    [SerializeField] private float calmMin = 0.7f;
    [SerializeField] private float calmMax = 2.2f;
    [SerializeField] private float flashMin = 0.05f;
    [SerializeField] private float flashMax = 0.14f;

    [Header("은은한 흔들림")]
    [SerializeField] private float breathSpeed = 1.4f;
    [SerializeField] private float breathAmount = 0.18f;

    [Header("살짝 움직이는 느낌")]
    [SerializeField] private float jitterPixels = 1.6f;
    [SerializeField] private float jitterSpeed = 0.9f;
    [SerializeField] private float scaleJitter = 0.12f;

    private RectTransform _rt;
    private Vector2 _basePos;
    private Vector3 _baseScale;
    private float _seed;
    private float _levelTarget;
    private Coroutine _routine;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (dot == null) dot = GetComponent<Image>();
        _basePos = _rt.anchoredPosition;
        _baseScale = _rt.localScale;
        _seed = Random.Range(0f, 100f);
        _levelTarget = baseAlpha;
    }

    void OnEnable()
    {
        _levelTarget = baseAlpha;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FlashLoop());
    }

    void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    private IEnumerator FlashLoop()
    {
        while (true)
        {
            _levelTarget = baseAlpha;
            yield return new WaitForSeconds(Random.Range(calmMin, calmMax));

            // 번쩍
            _levelTarget = flashAlpha;
            yield return new WaitForSeconds(Random.Range(flashMin, flashMax));

            // 순간적으로 꺼졌다가
            _levelTarget = dimAlpha;
            yield return new WaitForSeconds(Random.Range(flashMin, flashMax));

            if (Random.value < 0.45f)
            {
                _levelTarget = flashAlpha;
                yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
                _levelTarget = dimAlpha;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.10f));
            }
        }
    }

    void Update()
    {
        if (dot == null) return;

        // 목표 밝기로 부드럽게 따라가며, 숨쉬듯 미세하게 흔들림
        float breath = 1f + Mathf.Sin(Time.time * breathSpeed + _seed) * breathAmount;
        float target = Mathf.Clamp01(_levelTarget * breath);

        Color c = dot.color;
        c.a = Mathf.MoveTowards(c.a, target, Time.deltaTime * 8f);
        dot.color = c;

        // 아주 살짝 떠는 움직임
        float nx = Mathf.PerlinNoise(Time.time * jitterSpeed, _seed) - 0.5f;
        float ny = Mathf.PerlinNoise(_seed, Time.time * jitterSpeed) - 0.5f;
        _rt.anchoredPosition = _basePos + new Vector2(nx, ny) * 2f * jitterPixels;

        float s = 1f + (Mathf.PerlinNoise(Time.time * jitterSpeed * 1.3f, _seed + 10f) - 0.5f) * 2f * scaleJitter;
        _rt.localScale = _baseScale * s;
    }
}
