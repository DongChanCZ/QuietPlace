using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 괴물에게 잡혔을 때의 죽음 연출.
///
/// 흐름은 이렇다.
///   1. 조작을 빼앗고 카메라를 괴물 정면으로 돌린다 - 무엇에게 죽는지 보여준다.
///   2. 괴물이 팔을 휘두르는 동작이 닿는 순간(impactSeconds)에 비명과 심장 모니터 소리.
///   3. 그 타격에 맞아 날아간 것처럼 카메라를 옆 벽으로 처박는다.
///   4. 화면이 회색으로 잠기고, 잠시 뒤 마지막 세이브포인트에서 다시 시작한다.
///
/// 카메라를 직접 몰아야 하므로 PlayerController 를 끈다. 켜져 있으면 ApplyCamera 가
/// 매 프레임 카메라 위치와 회전을 다시 써버려서 연출이 한 프레임도 보이지 않는다.
/// 씬은 연출이 끝나면 어차피 다시 불러오므로 되돌릴 것은 없다.
/// </summary>
[DefaultExecutionOrder(-500)]
public class PlayerDeathSequence : MonoBehaviour
{
    public static PlayerDeathSequence Instance { get; private set; }

    [Header("플레이어")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Camera cam;

    [Header("1. 괴물을 향해 카메라를 돌리는 시간(초)")]
    [SerializeField] private float faceSeconds = 0.4f;

    [Header("괴물 몸통 어디를 볼지 - 0 이면 가운데, 1 이면 정수리")]
    [Range(0f, 1f)]
    [SerializeField] private float lookAtBodyHeight = 0.45f;

    [Header("렌더러를 못 찾았을 때만 쓰는 값 (피벗에서 위로 m)")]
    [SerializeField] private float lookAtHeightFallback = 1.0f;

    [Header("2. 팔이 닿는 순간 - 공격 애니메이션 시작 기준(초)")]
    [SerializeField] private float impactSeconds = 0.95f;

    [Header("사운드")]
    [SerializeField] private AudioClip screamClip;
    [SerializeField] private AudioClip heartMonitorClip;
    [Tooltip("심장 모니터를 이 지점부터 재생한다(초). 평탄음이 시작되는 자리.")]
    [SerializeField] private float heartMonitorStartTime = 5f;

    [Tooltip("삐 소리를 카메라가 눕고 나서 이만큼 뒤에 낸다(초). 비명과 겹치지 않게.")]
    [SerializeField] private float heartMonitorDelayAfterSlam = 0.5f;

    [SerializeField] private AudioSource screamSource;
    [SerializeField] private AudioSource monitorSource;

    [Header("3. 맞고 옆으로 튕기기")]
    [SerializeField] private float slamSeconds = 0.28f;

    [Tooltip("맞아서 옆으로 밀리는 거리(m). 살짝 튕기는 정도.")]
    [SerializeField] private float kickDistance = 0.45f;
    [SerializeField] private float kickRollDegrees = 26f;
    [SerializeField] private float kickPitchDegrees = 12f;

    [Header("옆에 벽이 있을 때만 - 부딪히는 느낌")]
    [Tooltip("이 거리 안에 벽이 있으면 벽에 부딪히는 연출로 바뀐다(m)")]
    [SerializeField] private float wallProbeDistance = 1.4f;
    [Tooltip("벽면에서 이만큼 앞에 멈춘다(m)")]
    [SerializeField] private float wallStandoff = 0.3f;
    [SerializeField] private float wallRollDegrees = 58f;
    [SerializeField] private float wallPitchDegrees = 22f;
    [Tooltip("벽에 부딪힌 뒤 되튕겨 나오는 거리(m)")]
    [SerializeField] private float wallRecoilDistance = 0.14f;
    [SerializeField] private float wallRecoilSeconds = 0.13f;
    [Tooltip("벽에 부딪히면 떨림을 이 배로 키운다")]
    [SerializeField] private float wallShakeMultiplier = 2.4f;

    [Header("떨림")]
    [Tooltip("맞은 뒤 떨리는 정도(m)")]
    [SerializeField] private float shakeAmount = 0.035f;
    [SerializeField] private float shakeSeconds = 0.5f;

    [Header("4. 회색으로 잠기기")]
    [SerializeField] private Graphic greyOverlay;
    [SerializeField] private Color greyColor = new Color(0.62f, 0.62f, 0.63f, 1f);
    [SerializeField] private float greyFadeSeconds = 1.6f;
    [Tooltip("회색이 다 덮인 뒤 이만큼 기다렸다가 다시 시작한다(초)")]
    [SerializeField] private float holdSeconds = 1.8f;

    public bool IsPlaying { get; private set; }

    // 클립 이름은 코드에 둔다 - 병합으로 사라지지 않는다.
    const string ScreamName = "Crush_malescream";
    const string HeartMonitorName = "heart_monitor_beep_Die";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (greyOverlay != null)
        {
            SetOverlayAlpha(0f);
            greyOverlay.gameObject.SetActive(false);
        }

        // 인스펙터 참조는 병합 때 빠질 수 있다. 빠졌으면 이름으로 찾고 소스도 만든다.
        screamClip = AudioLibrary.Resolve(screamClip, ScreamName);
        heartMonitorClip = AudioLibrary.Resolve(heartMonitorClip, HeartMonitorName);
        screamSource = AudioLibrary.EnsureSource(screamSource, transform, "ScreamSource", 0f, 0f, 0f, 1f);
        monitorSource = AudioLibrary.EnsureSource(monitorSource, transform, "HeartMonitorSource", 0f, 0f, 0f, 0.85f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 괴물이 공격을 시작할 때 부른다. 연출을 맡았으면 true -
    /// 그러면 괴물 쪽에서 따로 게임오버를 부르지 않는다.
    /// </summary>
    public static bool Begin(Transform monster)
    {
        if (Instance == null) return false;
        if (Instance.IsPlaying) return true;      // 이미 죽는 중이면 두 번 재생하지 않는다
        if (Instance.cam == null) return false;

        Instance.IsPlaying = true;
        Instance.StartCoroutine(Instance.Run(monster));
        return true;
    }

    private IEnumerator Run(Transform monster)
    {
        TakeOverControl();

        Transform camT = cam.transform;
        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;

        // ---------- 1. 괴물을 본다 ----------
        Quaternion faceRot = startRot;
        if (monster != null)
        {
            Vector3 dir = LookTarget(monster) - startPos;
            if (dir.sqrMagnitude > 0.0001f) faceRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        float t = 0f;
        while (t < faceSeconds)
        {
            t += Time.deltaTime;
            camT.rotation = Quaternion.Slerp(startRot, faceRot, Smooth(t / faceSeconds));
            yield return null;
        }
        camT.rotation = faceRot;

        // ---------- 2. 팔이 닿기를 기다린다 ----------
        float wait = impactSeconds - faceSeconds;
        if (wait > 0f) yield return new WaitForSeconds(wait);

        PlayScream();

        // ---------- 3. 맞고 옆으로 튕긴다 ----------
        // 맞은 쪽 반대로 밀리는 게 자연스럽다. 괴물이 왼쪽에서 쳤으면 오른쪽으로.
        Vector3 away = camT.right;
        if (monster != null)
        {
            Vector3 toMonster = monster.position - startPos;
            toMonster.y = 0f;
            if (Vector3.Dot(toMonster, camT.right) > 0f) away = -camT.right;
        }

        // 밀리는 쪽에 벽이 가까이 있으면 거기 부딪히는 연출로 바뀐다.
        // 없으면 그냥 옆으로 살짝 튕기고 끝난다.
        RaycastHit hit;
        bool intoWall = Physics.Raycast(startPos, away, out hit,
            wallProbeDistance + wallStandoff, ~0, QueryTriggerInteraction.Ignore);

        float travel = intoWall
            ? Mathf.Max(0.1f, hit.distance - wallStandoff)
            : kickDistance;

        Vector3 slamPos = startPos + away * travel;
        slamPos.y = startPos.y - (intoWall ? 0.22f : 0.10f);   // 다리가 꺾이며 내려앉는다

        // 벽에 부딪히면 벽 쪽을 향해 크게 꺾이고, 아니면 살짝만 기운다.
        float rollSign = Vector3.Dot(away, camT.right) >= 0f ? -1f : 1f;
        float roll = (intoWall ? wallRollDegrees : kickRollDegrees) * rollSign;
        float pitch = intoWall ? wallPitchDegrees : kickPitchDegrees;
        Vector3 slamForward = intoWall
            ? Vector3.Slerp(camT.forward, away, 0.5f).normalized   // 벽면으로 얼굴이 돌아간다
            : Vector3.Slerp(camT.forward, away, 0.18f).normalized; // 살짝만 밀린 시선

        Quaternion slamRot = Quaternion.LookRotation(slamForward, Vector3.up)
                           * Quaternion.Euler(pitch, 0f, roll);

        t = 0f;
        while (t < slamSeconds)
        {
            t += Time.deltaTime;
            float k = Crash(t / slamSeconds);
            camT.position = Vector3.Lerp(startPos, slamPos, k);
            camT.rotation = Quaternion.Slerp(faceRot, slamRot, k);
            yield return null;
        }
        camT.position = slamPos;
        camT.rotation = slamRot;

        // 벽에 부딪혔으면 살짝 되튕겨 나온다. 이 반동이 "박았다"는 느낌을 만든다.
        float shake = shakeAmount;
        if (intoWall)
        {
            shake *= wallShakeMultiplier;

            Vector3 recoilPos = slamPos - away * wallRecoilDistance;
            Quaternion recoilRot = slamRot * Quaternion.Euler(-pitch * 0.35f, 0f, roll * 0.18f);

            t = 0f;
            while (t < wallRecoilSeconds)
            {
                t += Time.deltaTime;
                float k = Smooth(t / wallRecoilSeconds);
                camT.position = Vector3.Lerp(slamPos, recoilPos, k);
                camT.rotation = Quaternion.Slerp(slamRot, recoilRot, k);
                yield return null;
            }
            slamPos = recoilPos;
            camT.position = slamPos;
            camT.rotation = recoilRot;
        }

        Debug.Log($"[PlayerDeathSequence] 튕김 - {(intoWall ? "벽에 부딪힘 (" + hit.collider.name + ", " + hit.distance.ToString("0.00") + "m)" : "벽 없음 - 옆으로 " + travel.ToString("0.00") + "m")}");

        // 삐 소리는 여기서. 비명이 먼저 울리고 카메라가 누운 뒤에 들어와야
        // "맞았다 -> 쓰러졌다 -> 죽었다" 순서로 읽힌다.
        StartCoroutine(PlayHeartMonitorAfter(heartMonitorDelayAfterSlam));

        // ---------- 4. 떨림 + 회색 ----------
        if (greyOverlay != null)
        {
            greyOverlay.gameObject.SetActive(true);
            SetOverlayAlpha(0f);
        }

        float seed = Random.value * 50f;
        t = 0f;
        while (t < greyFadeSeconds)
        {
            t += Time.deltaTime;

            // 부딪힌 직후에만 떨린다. 점점 잦아든다.
            if (t < shakeSeconds && shake > 0f)
            {
                float decay = 1f - t / shakeSeconds;
                camT.position = slamPos + new Vector3(
                    (Mathf.PerlinNoise(seed, t * 22f) - 0.5f),
                    (Mathf.PerlinNoise(seed + 7f, t * 22f) - 0.5f),
                    (Mathf.PerlinNoise(seed + 19f, t * 22f) - 0.5f)) * (shake * 2f * decay);
            }

            SetOverlayAlpha(Smooth(t / greyFadeSeconds));
            yield return null;
        }
        SetOverlayAlpha(1f);

        if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);

        // 소지품을 남기고 씬을 다시 불러온다 - 마지막 세이브포인트에서 시작한다.
        if (GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
    }

    // ---------------------------------------------------------------- 부품

    /// <summary>
    /// 괴물의 어디를 볼지. transform.position 을 쓰면 안 된다 - 이 괴물은 피벗이
    /// 허리 높이(y≈1.2)에 있어서, 거기에 사람 키를 더하면 머리 위 천장을 겨눈다.
    /// 렌더러 경계를 쓰면 피벗이 어디 있든 몸통을 정확히 잡는다.
    /// </summary>
    private Vector3 LookTarget(Transform monster)
    {
        Renderer[] parts = monster.GetComponentsInChildren<Renderer>();
        if (parts == null || parts.Length == 0)
            return monster.position + Vector3.up * lookAtHeightFallback;

        Bounds b = parts[0].bounds;
        for (int i = 1; i < parts.Length; i++) b.Encapsulate(parts[i].bounds);

        return b.center + Vector3.up * (b.extents.y * lookAtBodyHeight);
    }

    private void TakeOverControl()
    {
        if (player != null)
        {
            player.InputLocked = true;
            player.CameraLocked = true;
            player.enabled = false;      // ApplyCamera 가 카메라를 다시 쓰지 못하게
        }
        if (interactor != null) interactor.enabled = false;

        // 죽는 중에 미니게임 창이 떠 있으면 회색 화면 위로 남는다.
        MinigameAbort.AbortAll("죽었다");
    }

    /// 팔이 닿는 순간. 비명만 낸다 - 삐 소리는 쓰러진 뒤에 온다.
    private void PlayScream()
    {
        if (screamSource == null || screamClip == null) return;

        screamSource.clip = screamClip;
        screamSource.time = 0f;
        screamSource.Play();

        Debug.Log($"[PlayerDeathSequence] 타격 - 비명={screamSource.isPlaying}");
    }

    /// 심장 모니터는 평탄음이 시작되는 지점부터 틀어야 죽는 순간과 맞는다.
    private IEnumerator PlayHeartMonitorAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (monitorSource == null || heartMonitorClip == null) yield break;

        monitorSource.clip = heartMonitorClip;
        monitorSource.time = Mathf.Clamp(heartMonitorStartTime, 0f, Mathf.Max(0f, heartMonitorClip.length - 0.05f));
        monitorSource.Play();

        Debug.Log($"[PlayerDeathSequence] 삐 소리 시작 (클립 {monitorSource.time:0.0}초 지점부터)");
    }

    private void SetOverlayAlpha(float a)
    {
        if (greyOverlay == null) return;
        Color c = greyColor;
        c.a = greyColor.a * Mathf.Clamp01(a);
        greyOverlay.color = c;
    }

    /// 부드럽게 시작하고 부드럽게 멈춘다.
    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    /// 처박히는 움직임 - 초반에 확 밀리고 끝에서 급히 멈춘다.
    private static float Crash(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - (1f - x) * (1f - x) * (1f - x);
    }
}
