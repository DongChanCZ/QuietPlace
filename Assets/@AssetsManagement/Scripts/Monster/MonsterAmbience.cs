using UnityEngine;

/// <summary>
/// 괴물과의 거리에 따라 소리 분위기를 바꾼다.
///
///   - 일정 거리 안으로 들어오면 배경음이 평온한 심장 소리에서 빠른 심장 소리로 바뀐다.
///   - 멀리 있어 보이지 않을 때는 가끔 먼 포효와 먼 발소리가 들린다 - 어딘가에 있다는 압박.
///   - 주위를 살피는(Analyzing) 동작에 들어가는 순간 턱을 딱딱거리는 소리.
///
/// 괴물이 여러 마리라 가장 가까운 놈을 기준으로 판단한다. 매 프레임 다 훑을 필요가
/// 없어서 checkInterval 마다 한 번만 본다. 배열은 Awake 에서 한 번 잡고 재사용하므로
/// Update 안에서 새로 할당하는 것이 없다.
/// </summary>
[DefaultExecutionOrder(-400)]
public class MonsterAmbience : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] private Transform player;
    [SerializeField] private MonsterController[] monsters;

    [Header("눈으로 봤을 때 배경음을 빠른 심장 소리로")]
    [Tooltip("이 거리 안이고 + 화면에 보이고 + 시야가 막히지 않았을 때만 긴장 배경음으로 바뀐다(m)")]
    [SerializeField] private float closeDistance = 18f;
    [Tooltip("본 뒤에 이만큼 지나야 평온한 배경음으로 돌아온다(초). 시야에서 사라져도 바로 가라앉지 않는다.")]
    [SerializeField] private float calmDownSeconds = 6f;
    [Tooltip("이 거리 밖으로 나가면 안 보이는 것으로 치고 바로 가라앉는다(m)")]
    [SerializeField] private float calmDistance = 26f;
    [SerializeField] private string calmTrack = "heart_beat_normal_loop";
    [SerializeField] private string stressedTrack = "heart_beat_stressed";

    [Header("배경음 볼륨 - 긴장할 때만 조금 키운다")]
    [Range(0f, 1f)]
    [SerializeField] private float calmMusicVolume = 0.55f;
    [Range(0f, 1f)]
    [SerializeField] private float stressedMusicVolume = 0.78f;

    [Header("보이는지 판정")]
    [Tooltip("이 카메라의 화면 안에 들어와야 '봤다'로 친다")]
    [SerializeField] private Camera eyes;
    [Tooltip("시야를 막는 것으로 볼 레이어 (벽, 문 등)")]
    [SerializeField] private LayerMask sightBlockers = ~0;
    [Tooltip("괴물 몸통 어느 높이를 겨눠 시야를 확인할지 (피벗에서 위로 m)")]
    [SerializeField] private float sightHeight = 0.6f;

    [Header("안 보일 때 가끔 나는 소리")]
    [Tooltip("가장 가까운 괴물이 이 거리보다 멀 때만 먼 소리가 난다(m)")]
    [SerializeField] private float farDistance = 14f;
    [Tooltip("먼 소리 사이 간격(초) - 이 범위에서 무작위")]
    [SerializeField] private Vector2 farInterval = new Vector2(11f, 22f);
    [SerializeField] private AudioClip growlClip;
    [SerializeField] private AudioClip farFootstepClip;
    [Tooltip("포효가 나올 확률. 나머지는 발소리.")]
    [Range(0f, 1f)]
    [SerializeField] private float growlChance = 0.45f;
    [SerializeField] private AudioSource farSource;

    [Header("주위를 살필 때")]
    [SerializeField] private AudioClip scanClip;
    [SerializeField] private AudioSource scanSource;

    [Header("추적을 포기할 때 - 바닥 원판이 흰색(Idle)으로 바뀌는 그 순간")]
    [Tooltip("놓쳤다는 듯 가까이서 내는 포효")]
    [SerializeField] private AudioClip nearGrowlClip;
    [SerializeField] private AudioSource nearGrowlSource;

    [Header("몇 초마다 검사할지")]
    [SerializeField] private float checkInterval = 0.25f;

    private float _checkTimer;
    private float _farTimer;
    private bool _stressed;
    private float _calmTimer;
    private bool[] _wasScanning;
    private bool[] _wasChasing;

    /// 볼륨을 이 상태 기준으로 이미 맞춰뒀는가. 매번 SetVolume 을 부르지 않으려고 둔다.
    private bool _volumeSetFor;

    // 시야 검사용 버퍼. 프레임마다 새로 잡지 않으려고 미리 만들어 둔다.
    private readonly RaycastHit[] _sightHits = new RaycastHit[8];

    void Awake()
    {
        int n = monsters != null ? monsters.Length : 0;
        _wasScanning = new bool[n];
        _wasChasing = new bool[n];
        _farTimer = Random.Range(farInterval.x, farInterval.y);

        // 시작은 평온. _volumeSetFor 를 반대로 둬서 첫 판정에 반드시 한 번 적용되게 한다.
        _volumeSetFor = true;
    }

    void Update()
    {
        if (player == null || monsters == null || monsters.Length == 0) return;

        // 살피는 동작과 상태 전환은 짧게 지나갈 수 있으니 매 프레임 본다 - 비교뿐이라 싸다.
        CheckScanning();
        CheckGaveUp();

        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f) return;
        _checkTimer = checkInterval;

        float nearest = NearestDistance();
        bool seen = AnyMonsterVisible(nearest);

        UpdateMusic(nearest, seen);
        UpdateFarSounds(nearest, seen);
    }

    /// <summary>
    /// 괴물이 지금 눈에 보이는가. 거리만으로는 안 된다 - 벽 뒤에 있거나 등 뒤에 있는데
    /// 긴장 배경음이 먼저 깔리면 "보지도 못했는데 소리부터 난다"가 된다.
    /// 화면 안에 들어와 있고, 그 사이를 막는 것이 없어야 봤다고 본다.
    /// </summary>
    private bool AnyMonsterVisible(float nearest)
    {
        if (eyes == null || nearest > closeDistance) return false;

        Vector3 from = eyes.transform.position;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterController m = monsters[i];
            if (m == null || !m.gameObject.activeInHierarchy) continue;

            Vector3 target = m.transform.position + Vector3.up * sightHeight;
            if (Vector3.Distance(from, target) > closeDistance) continue;

            // 화면 밖이면 못 본 것이다.
            Vector3 view = eyes.WorldToViewportPoint(target);
            if (view.z <= 0f || view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f) continue;

            // 사이를 막는 것이 있으면 못 본 것이다.
            if (!IsBlocked(from, target, m.transform)) return true;
        }

        return false;
    }

    /// <summary>
    /// 카메라와 괴물 사이가 막혀 있는가.
    ///
    /// 그냥 Raycast 를 쏘면 못 쓴다 - 레이는 플레이어 자신의 콜라이더와 괴물 자신의
    /// 몸통에 먼저 맞아서 늘 "막혔다"가 나온다. 그 둘은 건너뛰고, 그보다 앞에 있는
    /// 제3의 물체가 있을 때만 막힌 것으로 본다.
    /// 버퍼는 Awake 에서 한 번 잡아 재사용하므로 프레임마다 새로 할당하지 않는다.
    /// </summary>
    private bool IsBlocked(Vector3 from, Vector3 target, Transform monster)
    {
        Vector3 delta = target - from;
        float dist = delta.magnitude;
        if (dist <= 0.05f) return false;

        int count = Physics.RaycastNonAlloc(from, delta / dist, _sightHits, dist,
            sightBlockers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Transform hit = _sightHits[i].collider.transform;

            if (monster != null && hit.IsChildOf(monster)) continue;          // 괴물 자기 몸통
            if (player != null && hit.IsChildOf(player.root)) continue;       // 플레이어 자기 몸

            return true;   // 사이를 막는 제3의 물체
        }

        return false;
    }

    /// 가장 가까운 괴물까지의 거리. 죽어 있거나 꺼진 놈은 세지 않는다.
    private float NearestDistance()
    {
        float best = float.PositiveInfinity;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterController m = monsters[i];
            if (m == null || !m.gameObject.activeInHierarchy) continue;

            float d = Vector3.Distance(player.position, m.transform.position);
            if (d < best) best = d;
        }

        return best;
    }

    private void UpdateMusic(float nearest, bool seen)
    {
        if (MusicManager.Instance == null) return;

        // 눈으로 본 순간부터 긴장한다. 시야에서 사라져도 바로 가라앉지 않고 잠시 남는다 -
        // 봤다가 숨었는데 음악이 뚝 끊기면 오히려 안심하게 된다.
        if (seen)
        {
            _stressed = true;
            _calmTimer = calmDownSeconds;
        }
        else if (_stressed)
        {
            // 멀리 떨어졌으면 기다리지 않고 바로 가라앉는다.
            if (nearest >= calmDistance) _stressed = false;
            else
            {
                _calmTimer -= checkInterval;
                if (_calmTimer <= 0f) _stressed = false;
            }
        }

        string want = _stressed ? stressedTrack : calmTrack;
        if (string.IsNullOrEmpty(want)) return;

        // 볼륨을 먼저 정한다. Crossfade 가 MusicManager 의 volume 을 목표로 잡으므로
        // PlayBGM 보다 뒤에 부르면 이번 전환에는 반영되지 않는다.
        if (_stressed != _volumeSetFor)
        {
            _volumeSetFor = _stressed;
            MusicManager.Instance.SetVolume(_stressed ? stressedMusicVolume : calmMusicVolume);
        }

        // PlayBGM 은 같은 곡이면 아무 것도 하지 않으므로 그냥 불러도 된다.
        MusicManager.Instance.PlayBGM(want);
    }

    private void UpdateFarSounds(float nearest, bool seen)
    {
        // 보이는 동안에는 먼 소리를 낼 이유가 없다. 타이머는 그대로 두어
        // 다시 안 보이게 됐을 때 곧바로 터지지 않게 한다.
        if (seen) return;

        _farTimer -= checkInterval;
        if (_farTimer > 0f) return;

        // 조건이 안 맞아 소리를 못 냈으면 타이머를 짧게만 미룬다.
        // 길게 미루면 조건이 맞는 순간을 계속 놓쳐서 소리가 영영 안 난다.
        if (nearest < farDistance || float.IsPositiveInfinity(nearest) || farSource == null)
        {
            _farTimer = checkInterval * 4f;
            return;
        }

        _farTimer = Random.Range(farInterval.x, farInterval.y);

        bool growl = Random.value < growlChance;
        AudioClip clip = growl ? growlClip : farFootstepClip;
        if (clip == null) clip = growl ? farFootstepClip : growlClip;   // 한쪽만 채워져 있어도 난다
        if (clip == null) return;

        // 소리가 나는 자리는 그 괴물 쪽 - 어느 방향에 있는지 귀로 짐작하게 한다.
        Transform from = NearestMonster();
        if (from != null) farSource.transform.position = from.position;

        farSource.clip = clip;
        farSource.Play();
    }

    private Transform NearestMonster()
    {
        Transform best = null;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterController m = monsters[i];
            if (m == null || !m.gameObject.activeInHierarchy) continue;

            float d = Vector3.Distance(player.position, m.transform.position);
            if (d >= bestDist) continue;

            best = m.transform;
            bestDist = d;
        }

        return best;
    }

    /// 살피는 동작에 "들어가는 순간"만 잡는다. 계속 켜져 있는 동안 소리가 겹치지 않게.
    private void CheckScanning()
    {
        if (_wasScanning == null || _wasScanning.Length != monsters.Length) return;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterController m = monsters[i];
            bool now = m != null && m.gameObject.activeInHierarchy && m.IsScanning;

            if (now && !_wasScanning[i]) PlayScan(m.transform);
            _wasScanning[i] = now;
        }
    }

    /// <summary>
    /// 추적을 포기하고 Idle 로 들어가는 순간을 잡는다.
    ///
    /// 바닥에 깔린 청각 범위 원판이 이때 흰색(1,1,1)으로 바뀐다 - MonsterHearing 의
    /// ColorForState 가 Idle 을 흰색으로 칠하기 때문이다. 즉 "흰색으로 바뀔 때" =
    /// 어그로가 풀리는 순간이고, 그 상태는 Shouting 애니메이션까지 이미 걸고 있다.
    /// 순찰(Patrol)에서 잠깐 멈추는 Idle 은 제외해야 포기하는 순간만 잡힌다.
    /// </summary>
    private void CheckGaveUp()
    {
        if (_wasChasing == null || _wasChasing.Length != monsters.Length) return;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterController m = monsters[i];
            if (m == null || !m.gameObject.activeInHierarchy) { _wasChasing[i] = false; continue; }

            MonsterStateType s = m.CurrentStateType;
            bool chasing = s == MonsterStateType.FirstDetection
                        || s == MonsterStateType.SecondDetection
                        || s == MonsterStateType.FinalDetection;

            // 쫓고 있다가 Idle 로 떨어진 순간에만 울린다.
            if (_wasChasing[i] && s == MonsterStateType.Idle) PlayNearGrowl(m.transform);

            _wasChasing[i] = chasing;
        }
    }

    private void PlayNearGrowl(Transform at)
    {
        if (nearGrowlSource == null || nearGrowlClip == null) return;

        nearGrowlSource.transform.position = at.position;
        nearGrowlSource.clip = nearGrowlClip;
        nearGrowlSource.Play();

        Debug.Log("[MonsterAmbience] 추적 포기 - 가까이서 포효 @ " + at.position.ToString("0.0"));
    }

    private void PlayScan(Transform at)
    {
        if (scanSource == null || scanClip == null) return;

        scanSource.transform.position = at.position;
        scanSource.clip = scanClip;
        scanSource.Play();
    }
}
