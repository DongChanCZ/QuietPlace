using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 양 끝이 고정된 줄의 정재파(화음)를 그린다.
///
/// 교과서 그림처럼 줄 양쪽이 벽에 물려 있고, 화음 차수(n)에 따라 배와 마디의 수가 달라진다.
///   y = A · sin(n · π · t),  t = 0~1 (줄의 왼쪽 끝 ~ 오른쪽 끝)
/// n 이 정수면 오른쪽 끝도 정확히 0 이 되어 벽에 물린 모양이 된다. 슬라이더는 n 을 연속으로
/// 움직이므로, 정수에서 벗어나 있으면 끝이 벽에서 떠 있는 게 눈에 보인다 - 그게 "안 맞았다"는
/// 신호가 된다.
///
/// 점을 흩어놓는 대신 이웃한 표본을 잇는 굵은 선분으로 그린다. 점만 찍으면 파형이 촘촘해질수록
/// 끊겨 보여서 무엇을 맞추는지 알기 어렵다.
/// </summary>
public class FrequencyWaveView : MonoBehaviour
{
    [Header("파형을 이루는 선분들 (왼쪽 -> 오른쪽 순서)")]
    public RectTransform[] segments;

    [Header("줄의 길이(px)")]
    public float width = 620f;

    [Header("파형 높이(px)")]
    public float amplitude = 34f;

    [Header("값 0 / 1 일 때의 화음 차수")]
    public float minHarmonic = 1f;
    public float maxHarmonic = 4f;

    [Header("차수가 올라갈수록 진폭이 줄어드는 정도 (그림처럼)")]
    [Range(0f, 1f)]
    public float amplitudeFalloff = 0.35f;

    [Header("0이 아니면 파형이 제자리에서 진동한다(라디안/초)")]
    public float pulseSpeed;

    [Header("진동 폭 - 진폭이 이 비율만큼 커졌다 작아진다")]
    [Range(0f, 0.6f)]
    public float pulseAmount = 0.25f;

    /// 지금 그려진 화음 차수. 정수에 가까울수록 벽에 잘 물린 모양이다.
    public float Harmonic { get; private set; } = 1f;

    private float _value01;
    private float _phase;

    /// <summary>0~1 값을 화음 차수로 바꿔 파형을 다시 그린다.</summary>
    public void SetValue01(float value)
    {
        _value01 = Mathf.Clamp01(value);
        Redraw();
    }

    void OnEnable()
    {
        _phase = 0f;
        Redraw();
    }

    void Update()
    {
        if (pulseSpeed == 0f) return;

        _phase += pulseSpeed * Time.deltaTime;
        Redraw();
    }

    private void Redraw()
    {
        if (segments == null || segments.Length == 0) return;

        Harmonic = Mathf.Lerp(minHarmonic, maxHarmonic, _value01);

        // 높은 화음은 진폭이 작다(그림에서 빨강이 가장 크고 주황이 가장 작다).
        float span = Mathf.Max(0.001f, maxHarmonic - minHarmonic);
        float k = (Harmonic - minHarmonic) / span;
        float amp = amplitude * Mathf.Lerp(1f, 1f - amplitudeFalloff, k);

        if (pulseSpeed != 0f) amp *= 1f + Mathf.Sin(_phase) * pulseAmount;

        int count = segments.Length;
        float half = width * 0.5f;

        for (int i = 0; i < count; i++)
        {
            RectTransform seg = segments[i];
            if (seg == null) continue;

            // i 번째 선분은 표본 i 와 i+1 을 잇는다
            float t0 = (float)i / count;
            float t1 = (float)(i + 1) / count;

            Vector2 a = new Vector2(Mathf.Lerp(-half, half, t0), Mathf.Sin(t0 * Harmonic * Mathf.PI) * amp);
            Vector2 b = new Vector2(Mathf.Lerp(-half, half, t1), Mathf.Sin(t1 * Harmonic * Mathf.PI) * amp);

            Vector2 d = b - a;
            seg.anchoredPosition = (a + b) * 0.5f;
            seg.sizeDelta = new Vector2(d.magnitude, seg.sizeDelta.y);
            seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }

    /// <summary>선분 색을 한 번에 바꾼다.</summary>
    public void SetColor(Color c)
    {
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            Image img = segments[i].GetComponent<Image>();
            if (img != null) img.color = c;
        }
    }

    /// <summary>선 굵기를 바꾼다.</summary>
    public void SetThickness(float thickness)
    {
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            Vector2 s = segments[i].sizeDelta;
            segments[i].sizeDelta = new Vector2(s.x, thickness);
        }
    }
}
