using System.Collections;
using UnityEngine;

/// <summary>
/// 건물에 전기가 들어오는 순간의 연출.
///
/// 발전기실(변압기)과 배전반실(퓨즈+차단기) 두 곳을 다 고쳐야 불이 켜지므로, 이 연출을
/// 부르는 쪽은 상황에 따라 둘 중 아무 쪽이나 된다. 그래서 연출을 어느 미니게임에도 두지
/// 않고 따로 뺐다 - 마지막으로 끝낸 쪽이 이걸 부른다.
///
/// 조작 잠금은 부르는 쪽이 이미 해둔 상태라고 본다(양쪽 다 컷신 중에는 조작을 빼앗는다).
/// 시간은 멈추지 않는다 - 괴물은 계속 돌아다니고, timeScale 0 이면 이 코루틴도 멈춘다.
/// </summary>
[DefaultExecutionOrder(-900)]
public class PowerOnCutscene : MonoBehaviour
{
    public static PowerOnCutscene Instance { get; private set; }

    [Header("플레이어 (비우면 소리만 낸다)")]
    [SerializeField] private PlayerController player;

    [Header("전원이 들어올 때 나는 소리")]
    [Tooltip("건물이 살아나는 공간음")]
    [SerializeField] private AudioClip ambienceClip;
    [SerializeField] private AudioSource ambienceSource;
    [Tooltip("어딘가에서 괴물이 반응하는 포효")]
    [SerializeField] private AudioClip growlClip;
    [SerializeField] private AudioSource growlSource;
    [Tooltip("포효를 공간음보다 이만큼 늦게 낸다(초) - 겹쳐 나오면 둘 다 묻힌다")]
    [SerializeField] private float growlDelay = 1.1f;

    [Header("카메라")]
    [Tooltip("올려다볼 각도(도). 음수가 위쪽. 시작 시선과 무관한 절대값이다.")]
    [SerializeField] private float lookUpPitch = -32f;
    [Tooltip("가장 트인 방향으로 몸을 돌린다. 벽을 마주본 채 위를 보면 천장 조명이 안 보인다.")]
    [SerializeField] private bool turnToOpenSpace = true;
    [Tooltip("트인 방향을 찾을 때 몇 방향을 재볼지")]
    [SerializeField] private int probeCount = 12;
    [SerializeField] private float probeDistance = 14f;
    [SerializeField] private float panSeconds = 1.8f;
    [Tooltip("불이 다 켜진 뒤 이만큼 더 보여준다(초)")]
    [SerializeField] private float holdSeconds = 3.2f;

    public bool IsPlaying { get; private set; }

    // 클립 이름은 코드에 둔다 - 인스펙터 참조는 병합 때 빠지지만 이 문자열은 남는다.
    const string AmbienceName = "Eerie void ambience_2";
    const string GrowlName = "Far_creepy_monster_growling";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        ambienceClip = AudioLibrary.Resolve(ambienceClip, AmbienceName);
        growlClip = AudioLibrary.Resolve(growlClip, GrowlName);
        ambienceSource = AudioLibrary.EnsureSource(ambienceSource, transform, "PowerAmbienceSource", 0f, 0f, 0f, 0.7f);
        growlSource = AudioLibrary.EnsureSource(growlSource, transform, "PowerGrowlSource", 0f, 0f, 0f, 0.85f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 소리를 깔고, 가장 트인 쪽으로 몸을 돌려 천장을 올려다본다.
    /// BuildingPower 가 조명을 하나씩 켜는 동안 그 장면이 화면에 들어온다.
    /// </summary>
    public IEnumerator Play()
    {
        IsPlaying = true;

        // 건물이 살아나는 소리. 포효는 조금 늦게 얹어 "불이 켜진 걸 저쪽도 알았다" 로 읽히게.
        PlayNow(ambienceSource, ambienceClip);
        if (growlSource != null && growlClip != null) StartCoroutine(PlayDelayed(growlSource, growlClip, growlDelay));

        if (player != null)
        {
            float fromPitch = player.LookPitch;
            float toPitch = Mathf.Clamp(lookUpPitch, player.minPitch, player.maxPitch);

            Quaternion fromRot = player.transform.rotation;
            Quaternion toRot = turnToOpenSpace ? OpenestDirection() : fromRot;

            float t = 0f;
            while (t < panSeconds)
            {
                t += Time.deltaTime;
                float k = Smooth(t / panSeconds);
                player.LookPitch = Mathf.Lerp(fromPitch, toPitch, k);
                player.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
                yield return null;
            }
            player.transform.rotation = toRot;
            player.LookPitch = toPitch;
        }

        if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);

        IsPlaying = false;
        Debug.Log("[PowerOnCutscene] 전원 복구 연출 끝");
    }

    /// <summary>
    /// 눈높이에서 사방으로 재보고 가장 멀리까지 트인 방향을 고른다.
    /// 그쪽이 방이 열려 있는 쪽이라 천장 조명이 여러 개 화면에 들어온다.
    /// </summary>
    private Quaternion OpenestDirection()
    {
        Vector3 eye = player.playerCamera != null
            ? player.playerCamera.transform.position
            : player.transform.position + Vector3.up * 1.6f;

        int probes = Mathf.Max(4, probeCount);
        Vector3 best = player.transform.forward;
        float bestClear = -1f;

        for (int i = 0; i < probes; i++)
        {
            float angle = 360f * i / probes;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            RaycastHit hit;
            float clear = Physics.Raycast(eye, dir, out hit, probeDistance, ~0, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : probeDistance;

            if (clear <= bestClear) continue;
            bestClear = clear;
            best = dir;
        }

        Debug.Log($"[PowerOnCutscene] 가장 트인 방향 {best.ToString("0.00")} ({bestClear:0.0}m)");
        return Quaternion.LookRotation(best, Vector3.up);
    }

    private static void PlayNow(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip = clip;
        source.time = 0f;
        source.Play();
    }

    private static IEnumerator PlayDelayed(AudioSource source, AudioClip clip, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        PlayNow(source, clip);
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
