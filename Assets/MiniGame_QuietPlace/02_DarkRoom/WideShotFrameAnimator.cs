using UnityEngine;
using UnityEngine.UI;

// 의자 위치가 미묘하게 다른 두 장의 와이드샷을 교차 페이드해서
// "방금 일어나서 의자가 아직 돌고 있다"는 느낌을 만든다.
// 흔들림은 점점 잦아들고, 아주 약한 잔여 움직임만 남아 연기가 계속 살아있게 보인다.
public class WideShotFrameAnimator : MonoBehaviour
{
    [Tooltip("위에 겹쳐지는 두 번째 프레임 (의자가 움직인 쪽)")]
    [SerializeField] private Image frameB;

    [Header("처음 흔들림 (의자가 돌다가 멈춤)")]
    [Range(0f, 1f)]
    [SerializeField] private float maxBlend = 1f;
    [Range(0.05f, 1.5f)]
    [SerializeField] private float frequency = 0.35f;
    [Range(0.5f, 15f)]
    [SerializeField] private float dampingSeconds = 5f;

    [Header("잔여 움직임 (연기가 계속 일렁이도록)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float residualBlend = 0.12f;
    [Range(0.02f, 1f)]
    [SerializeField] private float residualFrequency = 0.18f;

    [Header("시작 설정")]
    [Tooltip("켜지자마자 자동으로 시작할지 여부")]
    [SerializeField] private bool playOnEnable = false;

    private bool _playing;
    private float _startTime;

    private const float TwoPi = 6.2831853f;

    void OnEnable()
    {
        SetBlend(0f);
        if (playOnEnable) BeginMotion();
    }

    // 줌아웃으로 방이 드러나기 시작할 때 호출
    public void BeginMotion()
    {
        _playing = true;
        _startTime = Time.time;
    }

    public void StopMotion()
    {
        _playing = false;
        SetBlend(0f);
    }

    void Update()
    {
        if (!_playing) return;

        float t = Time.time - _startTime;

        // 0에서 시작해 두 프레임 사이를 오가다가 서서히 A 프레임으로 정착
        float swing = maxBlend
                    * Mathf.Exp(-t / dampingSeconds)
                    * (0.5f - 0.5f * Mathf.Cos(TwoPi * frequency * t));

        // 완전히 멈추지 않고 아주 약하게 계속 일렁이는 성분
        float residual = residualBlend * (0.5f - 0.5f * Mathf.Cos(TwoPi * residualFrequency * t));

        SetBlend(Mathf.Clamp01(swing + residual));
    }

    private void SetBlend(float blend)
    {
        if (frameB == null) return;
        Color c = frameB.color;
        c.a = blend;
        frameB.color = c;
    }
}
