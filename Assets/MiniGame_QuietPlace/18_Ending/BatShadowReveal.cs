using System.Collections;
using UnityEngine;

/// <summary>
/// 엔딩 마지막 컷 - 지하 1층 벽에 드리우는 새 괴물의 그림자.
///
/// 3D 모델을 새로 만들지 않는다. 박쥐 귀와 날개 모양의 판(Quad)을 벽 앞에 세워두고
/// 스포트라이트를 쏴서 벽면에 그림자만 드리운다. 날개는 접힌 자세에서 펼쳐지는 것만
/// 회전으로 처리한다 - 실루엣이라 관절이 안 보이므로 이 정도로 충분하다.
///
/// 그림자를 만드는 판 자체는 카메라에 보이면 안 된다. 빌더가 판의 렌더러를
/// ShadowCastingMode.ShadowsOnly 로 맞춰 둔다.
/// </summary>
[DisallowMultipleComponent]
public class BatShadowReveal : MonoBehaviour
{
    [Header("그림자를 만드는 빛")]
    [SerializeField] private Light spot;
    [Tooltip("빛이 밝아지는 시간(초)")]
    [SerializeField] private float lightFadeSeconds = 1.2f;
    [SerializeField] private float litIntensity = 6f;

    [Header("접혔다 펴지는 날개")]
    [SerializeField] private Transform wingLeft;
    [SerializeField] private Transform wingRight;
    [Tooltip("접힌 각도 / 펼친 각도 (Y축, 도)")]
    [SerializeField] private float foldedYaw = 78f;
    [SerializeField] private float spreadYaw = 8f;
    [SerializeField] private float spreadSeconds = 2.6f;

    [Header("붉은 경고등")]
    [SerializeField] private Light warningLight;
    [SerializeField] private float warningPulsesPerSecond = 0.8f;
    [SerializeField] private float warningIntensity = 3.2f;

    [Header("이 컷 동안 꺼둘 방의 기존 조명")]
    [Tooltip("전원을 복구한 뒤라 지하 조명도 켜져 있다. 켜진 채로는 그림자가 묻힌다.")]
    [SerializeField] private Light[] dimLights;

    [Header("소리")]
    [Tooltip("기분 나쁜 울음소리")]
    [SerializeField] private AudioClip screechClip;
    [SerializeField] private AudioSource screechSource;
    [Tooltip("울음소리를 날개가 펴지기 시작한 뒤 이만큼 지나서 낸다(초)")]
    [SerializeField] private float screechDelay = 1.4f;

    [Tooltip("저음의 음산한 심장 소리 - 이 컷 동안 깔린다")]
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private AudioSource heartbeatSource;

    private bool _pulsing;
    private float[] _dimmed;

    // 클립 이름은 코드에 둔다 - 인스펙터 참조는 병합 때 빠지지만 이 문자열은 남는다.
    // bat_screech / bat_wings 파일을 Audio 폴더에 넣고 사운드 목록을 갱신하면 그것으로 바뀐다.
    const string ScreechName = "bat_screech";
    const string ScreechFallbackName = "Far_creepy_monster_growling";
    const string HeartbeatName = "heart_beat_stressed";

    void Awake()
    {
        screechClip = AudioLibrary.Resolve(screechClip, ScreechName);
        screechClip = AudioLibrary.Resolve(screechClip, ScreechFallbackName);
        heartbeatClip = AudioLibrary.Resolve(heartbeatClip, HeartbeatName);

        screechSource = AudioLibrary.EnsureSource(screechSource, transform, "ScreechSource", 0f, 0f, 0f, 1f);
        heartbeatSource = AudioLibrary.EnsureSource(heartbeatSource, transform, "HeartbeatSource", 0f, 0f, 0f, 0.7f);
        if (heartbeatSource != null) heartbeatSource.loop = true;

        Reset();
    }

    /// <summary>컷이 시작되기 전 상태로 되돌린다. 빌더가 만들어 둔 자세와 무관하게 여기서 확정한다.</summary>
    public void Reset()
    {
        _pulsing = false;
        if (spot != null) spot.intensity = 0f;
        if (warningLight != null) warningLight.intensity = 0f;
        SetWings(foldedYaw);
    }

    /// <summary>
    /// 마지막 컷. 경고등이 깜빡이기 시작하고, 빛이 밝아지며 벽에 실루엣이 뜨고,
    /// 접혀 있던 날개가 서서히 펼쳐진다.
    /// </summary>
    public IEnumerator Play()
    {
        // BuildingPower 가 시작할 때 씬의 조명을 전부 꺼버린다(전원 복구 전이므로).
        // 이 세트의 조명은 전원과 무관하게 이 컷에서만 쓰는 것이라 여기서 되살린다.
        if (spot != null) spot.enabled = true;
        if (warningLight != null) warningLight.enabled = true;

        DimRoomLights();

        if (heartbeatSource != null && heartbeatClip != null)
        {
            heartbeatSource.clip = heartbeatClip;
            heartbeatSource.Play();
        }

        _pulsing = true;

        // 빛이 켜지며 실루엣이 나타난다
        float t = 0f;
        while (t < lightFadeSeconds)
        {
            t += Time.deltaTime;
            if (spot != null) spot.intensity = Mathf.Lerp(0f, litIntensity, Smooth(t / lightFadeSeconds));
            yield return null;
        }
        if (spot != null) spot.intensity = litIntensity;

        if (screechSource != null && screechClip != null) StartCoroutine(Screech());

        // 날개가 펴진다
        t = 0f;
        while (t < spreadSeconds)
        {
            t += Time.deltaTime;
            SetWings(Mathf.Lerp(foldedYaw, spreadYaw, Smooth(t / spreadSeconds)));
            yield return null;
        }
        SetWings(spreadYaw);
    }

    /// <summary>암전과 함께 소리도 정리한다.</summary>
    public void StopSounds()
    {
        if (heartbeatSource != null) heartbeatSource.Stop();
        if (screechSource != null) screechSource.Stop();
        _pulsing = false;
    }

    /// 방의 기존 조명을 껐다가 원래 세기를 기억해 둔다.
    private void DimRoomLights()
    {
        if (dimLights == null) return;

        if (_dimmed == null || _dimmed.Length != dimLights.Length) _dimmed = new float[dimLights.Length];

        for (int i = 0; i < dimLights.Length; i++)
        {
            if (dimLights[i] == null) continue;
            _dimmed[i] = dimLights[i].intensity;
            dimLights[i].intensity = 0f;
        }
    }

    private IEnumerator Screech()
    {
        if (screechDelay > 0f) yield return new WaitForSeconds(screechDelay);
        if (screechSource == null || screechClip == null) yield break;

        screechSource.clip = screechClip;
        screechSource.time = 0f;
        screechSource.Play();
    }

    private void SetWings(float yaw)
    {
        if (wingLeft != null) wingLeft.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (wingRight != null) wingRight.localRotation = Quaternion.Euler(0f, -yaw, 0f);
    }

    void Update()
    {
        if (!_pulsing || warningLight == null) return;

        // 경고등은 컷 내내 느리게 뛴다. Sin 을 0~1 로 접어 꺼지는 구간을 만든다.
        float k = Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * warningPulsesPerSecond));
        warningLight.intensity = warningIntensity * k * k;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
