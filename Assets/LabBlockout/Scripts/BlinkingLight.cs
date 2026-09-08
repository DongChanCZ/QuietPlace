using UnityEngine;

/// 경보등 점멸. 자식 라이트를 전부 같이 흔든다 - 램프 하나에 벌브용 스포트와
/// 바닥을 때리는 스포트가 같이 달려 있어서, 한쪽만 깜빡이면 밝기가 어긋나 보인다.
///
/// 파형은 사이렌이 우는 모양이다 - 서서히 차올라 정점에서 머물다 서서히 꺼진다.
/// 딱딱 켜고 끄면 고장난 형광등으로 읽히지, 경보등으로는 안 읽힌다.
[DisallowMultipleComponent]
public sealed class BlinkingLight : MonoBehaviour
{
    [SerializeField] float period = 2.8f;     // 한 번 차올랐다 꺼지는 주기(초)
    [SerializeField] float dimLevel = 0.06f;  // 바닥까지 죽었을 때 남는 잔광

    Light[] lights;
    float[] fullIntensity;
    float phase;

    void Awake()
    {
        lights = GetComponentsInChildren<Light>(true);
        fullIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) fullIntensity[i] = lights[i].intensity;
    }

    void Update()
    {
        phase += Time.deltaTime / Mathf.Max(period, 0.01f);
        if (phase >= 1f) phase -= 1f;

        float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);   // 0 에서 올라갔다 0 으로
        float shaped = wave * wave * (3f - 2f * wave);                 // 정점과 바닥에서 더 오래 머문다
        float level = Mathf.Lerp(dimLevel, 1f, shaped);

        for (int i = 0; i < lights.Length; i++) lights[i].intensity = fullIntensity[i] * level;
    }
}
