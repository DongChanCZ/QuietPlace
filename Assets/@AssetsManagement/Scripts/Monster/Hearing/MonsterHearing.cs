using UnityEngine;

[RequireComponent(typeof(MonsterController))]
public class MonsterHearing : MonoBehaviour
{
    [Header("Obstruction")]
    public LayerMask obstructionMask; // 일반 벽/문
    [Range(0f, 1f)] public float defaultObstaclePassFactor = 0.6f; // 벽 1개당 통과율
    public LayerMask floorObstructionMask; // 바닥/천장 슬라브 전용 — 벽보다 훨씬 강하게 막음
    [Range(0f, 1f)] public float floorPassFactor = 0.15f;
    public int maxObstaclesPerCheck = 8;
    public float earHeight = 1.5f;

    [Header("Base Hearing")]
    public float baseHearingRange = 6f; // 몬스터 자신의 기본 청각 반경 (NoiseEvent.BaseRange에 더해짐)
    public float verticalTolerance = 3f; // 같은 층으로 볼 Y축 거리 (귀 높이 등으로 인한 기본 오차) — 이 안쪽은 가중치 없이 그대로 취급
    public float verticalDistanceWeight = 3f; // verticalTolerance를 넘는 초과분에만 곱해지는 가중치 — 클수록 다른 층 소리가 더 빨리 범위 밖으로 취급됨

    // 판정 대상은 Loudness 원본이 아니라 거리/벽 감쇠까지 다 곱한 최종 intensity라 raw Loudness보다
    // 훨씬 낮게 잡아야 한다. 실측(벽 1개 낀 5m, Walking) intensity≈0.86 - 벽 하나만 껴도 감쇠가
    // 커서(defaultObstaclePassFactor=0.6) 완전 개활지 가정보다 훨씬 낮게 나온다. 이 실측값 기준:
    //  - Idle(2) 완전 근접·개활지 intensity≈1.75 -> tier3 미만으로 묶어서 스쳐도 Final行 안 됨
    //  - Walking(3) 벽 하나 낀 5m intensity≈0.86 -> tier2 이상으로 실추적 시작
    //  - Gasping/Fearing(5)·투척(10)만 벽 있어도 tier3까지 도달
    [Header("Detection Thresholds (최종 청각 강도 - 거리/벽 감쇠 적용 후 값)")]
    public float tier1Threshold = 0.3f; // 거의 모든 근처 소리가 최소 한 번은 걸리는 정도
    public float tier2Threshold = 0.7f; // Walking이 벽 하나 정도 껴도 실추적으로 이어지게
    public float tier3Threshold = 3f;   // 이동 소음만으로는 도달 안 함 - 비명/투척급만

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool debugDrawRuntimeRays = false;
    public float rangeGizmoGroundOffset = -1f;
    public bool showRangeDisc = true; // 하이어라키에서 이 컴포넌트 체크박스로 범위 표시 on/off

    private MonsterController controller;
    private RaycastHit[] hitBuffer;
    private Vector3 lastDebugFrom, lastDebugTo;
    private float lastDebugIntensity;
    private bool hasDebugSample;
    private Transform baseRangeDisc;

    void Awake()
    {
        controller = GetComponent<MonsterController>();
        hitBuffer = new RaycastHit[maxObstaclesPerCheck]; // 1회만 할당
        baseRangeDisc = RangeGizmo.CreateDisc(transform, "BaseHearingRange_Gizmo", new Color(1f, 0.9f, 0.2f, 0.05f));
    }

    void Update()
    {
        if (baseRangeDisc == null) return;
        baseRangeDisc.gameObject.SetActive(showRangeDisc);
        if (!showRangeDisc) return;

        RangeGizmo.UpdateDisc(baseRangeDisc, transform.position, baseHearingRange, rangeGizmoGroundOffset);
        RangeGizmo.SetColor(baseRangeDisc, ColorForState(controller.CurrentStateType));
    }

    private static Color ColorForState(MonsterStateType state)
    {
        switch (state)
        {
            case MonsterStateType.Idle: return new Color(1f, 1f, 1f, 0.06f);
            case MonsterStateType.Patrol: return new Color(1f, 0.9f, 0.2f, 0.05f);
            case MonsterStateType.FirstDetection: return new Color(1f, 0.55f, 0.1f, 0.06f);
            case MonsterStateType.SecondDetection: return new Color(1f, 0.1f, 0.1f, 0.07f);
            case MonsterStateType.FinalDetection: return new Color(0.8f, 0f, 0f, 0.08f); // 요청에 없어 Second보다 진한 빨강으로 구분
            case MonsterStateType.Attack: return new Color(0.6f, 0.1f, 0.9f, 0.09f);
            default: return new Color(0.5f, 0.5f, 0.5f, 0.05f); // Neutralization 등
        }
    }

    void OnEnable()
    {
        if (NoiseManager.Instance != null) NoiseManager.Instance.OnNoiseEmitted += HandleNoise;
    }

    void OnDisable()
    {
        if (NoiseManager.Instance != null) NoiseManager.Instance.OnNoiseEmitted -= HandleNoise;
    }

    private void HandleNoise(NoiseEvent evt)
    {
        Vector3 ear = transform.position + Vector3.up * earHeight;
        Vector3 delta = evt.Position - ear;
        float realDist = delta.magnitude; // Raycast용 실제 거리
        float perceivedDist = PerceivedDistance(delta); // 판정용 — Y축 가중치 적용

        float effectiveRange = evt.BaseRange + baseHearingRange;
        if (perceivedDist > effectiveRange) return; // 거리 컷 — Raycast 자체를 안 함

        float distanceAttenuation = 1f - Mathf.Clamp01(perceivedDist / effectiveRange);
        float wallAttenuation = ComputeWallAttenuation(ear, evt.Position, realDist);
        float intensity = evt.Loudness * distanceAttenuation * wallAttenuation;

        if (debugDrawRuntimeRays)
            Debug.DrawLine(ear, evt.Position, wallAttenuation < 0.99f ? Color.red : Color.yellow, 0.5f);
        lastDebugFrom = ear; lastDebugTo = evt.Position; lastDebugIntensity = intensity; hasDebugSample = true;

        NoiseTier tier = intensity >= tier3Threshold ? NoiseTier.Final
                        : intensity >= tier2Threshold ? NoiseTier.Second
                        : intensity >= tier1Threshold ? NoiseTier.First
                        : NoiseTier.None;

        controller.OnNoiseHeard(tier, evt.Position, evt.Loudness);
    }

    private float PerceivedDistance(Vector3 delta)
    {
        float horizontal = new Vector2(delta.x, delta.z).magnitude;
        float rawVertical = Mathf.Abs(delta.y);
        // 귀 높이 등 같은 층 안에서도 늘 존재하는 오차(verticalTolerance)는 가중치 없이 그대로,
        // 그걸 넘어서는 초과분(=대개 층간 차이)에만 verticalDistanceWeight를 곱한다.
        float weightedVertical = Mathf.Min(rawVertical, verticalTolerance)
                                + Mathf.Max(0f, rawVertical - verticalTolerance) * verticalDistanceWeight;
        return Mathf.Sqrt(horizontal * horizontal + weightedVertical * weightedVertical);
    }

    // Attack 판정용 — 사이에 벽이 하나라도 있으면 공격 불가 (벽에 붙어있을 때 벽 너머를 공격하는 것 방지)
    public bool HasClearLineOfSight(Vector3 targetPos)
    {
        Vector3 eye = transform.position + Vector3.up * earHeight;
        Vector3 delta = targetPos - eye;
        float dist = delta.magnitude;
        if (dist < 0.001f) return true;
        return !Physics.Raycast(eye, delta / dist, dist, obstructionMask);
    }

    // 실제 판정 로직 - 시각화와 완전히 분리
    private float ComputeWallAttenuation(Vector3 from, Vector3 to, float dist)
    {
        Vector3 dir = (to - from) / dist;
        int wallCount = Physics.RaycastNonAlloc(from, dir, hitBuffer, dist, obstructionMask);
        int floorCount = Physics.RaycastNonAlloc(from, dir, hitBuffer, dist, floorObstructionMask);
        return Mathf.Pow(defaultObstaclePassFactor, wallCount) * Mathf.Pow(floorPassFactor, floorCount);
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || !hasDebugSample) return;
        Gizmos.color = Color.Lerp(Color.red, Color.green, lastDebugIntensity);
        Gizmos.DrawLine(lastDebugFrom, lastDebugTo);
        Gizmos.DrawWireSphere(lastDebugTo, 0.3f);
    }
}
