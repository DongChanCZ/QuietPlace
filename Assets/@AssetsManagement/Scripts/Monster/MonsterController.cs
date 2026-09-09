using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterController : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;
    public Animator animator;

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float firstChaseSpeed = 3f;
    public float secondChaseSpeed = 6f;
    public float attackRange = 1.5f;
    public float touchAttackRange = 1.2f;
    public float destinationUpdateThreshold = 0.8f; // 플레이어가 이만큼 움직이기 전엔 목적지 재지정 안 함 (좁은 틈에서 제자리 떨림 방지)

    [Header("Patrol")]
    public Transform[] patrolPoints;

    [Header("Hearing Memory")]
    public float hearingMemoryDuration = 2f; // 이 시간 안에 새 소리가 없으면 '안 들림' (Raycast 없이 시간 비교만)
    public float loudNoiseCommitThreshold = 8f; // NoiseLevel이 이 값 이상이면 근원지에 거의 도달할 때까지 추적 유지
    public float maxHearingMemoryDuration = 8f; // loudNoiseCommitThreshold 이상일 때 적용되는 유지 시간

    [Header("Sound Reaction")]
    public float reactionDelay = 0.4f; // 소리 감지 직후 "어?" 하고 멈칫하는 텀 (즉각 반응 방지)
    public float investigateLingerTime = 1.2f; // 소리 지점 도착 후 두리번거리는 시간
    public float firstTierMaxDuration = 7f; // First가 이 시간 넘게 지속되면 계속 들리고 있어도(격상 안 됐다면) 그냥 포기 - 스니킹 플레이어를 무한정 쫓아다니는 것 방지
    public float soundPositionError = 2f; // NoiseLevel 0 기준 오차 반경
    public float minSoundPositionError = 1.2f; // loudNoiseCommitThreshold 이상이어도 이 아래로는 안 줄어듦 - 오브젝트에 완전히 달라붙는 것 방지
    public float heardPositionSampleRadius = 3f; // 소리 위치를 NavMesh 위로 스냅할 때 허용 거리 - 벽/물체 표면에 맞아 NavMesh 밖에 찍히는 경우 대비
    public float patrolRelocateSpread = 4f; // loudNoiseCommitThreshold 이상 소리 들으면 패트롤 지점들을 그 근원지 주변 이 반경 안으로 재배치

    public NavMeshAgent Agent { get; private set; }
    public MonsterStateType CurrentStateType => currentState != null ? currentState.Type : MonsterStateType.Idle;
    public Vector3 LastHeardPosition { get; private set; }
    public float LastHeardLoudness { get; private set; }
    // 조용한 소리는 기본 hearingMemoryDuration만큼만, NoiseLevel이 클수록 근원지 근처까지 갈 시간을 더 준다.
    private float EffectiveHearingMemory => Mathf.Lerp(hearingMemoryDuration, maxHearingMemoryDuration,
        Mathf.Clamp01(LastHeardLoudness / loudNoiseCommitThreshold));
    public bool IsHearingFresh => Time.time - lastHeardTime <= EffectiveHearingMemory;

    private IMonsterState currentState;
    private float lastHeardTime = -999f;
    private MonsterHearing hearing;

    private readonly MonsterIdleState idleState = new MonsterIdleState();
    private readonly MonsterPatrolState patrolState = new MonsterPatrolState();
    private readonly MonsterFirstDetectionState firstDetectionState = new MonsterFirstDetectionState();
    private readonly MonsterSecondDetectionState secondDetectionState = new MonsterSecondDetectionState();
    private readonly MonsterFinalDetectionState finalDetectionState = new MonsterFinalDetectionState();
    private readonly MonsterAttackState attackState = new MonsterAttackState();
    private readonly MonsterNeutralizationState neutralizationState = new MonsterNeutralizationState();

    void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        hearing = GetComponent<MonsterHearing>();
        ChangeState(patrolState);
    }

    void Update()
    {
        if (player == null || currentState.Type == MonsterStateType.Neutralization) return;

        // First는 아직 확신 없는 상태라 이 즉시공격 규칙에서 제외 - 우연히 스치기만 해도 죽는 건
        // 억울함. First 중의 접촉은 MonsterFirstDetectionState 자체에서 Final 격상으로 처리한다.
        if (currentState.Type != MonsterStateType.Attack && currentState.Type != MonsterStateType.FirstDetection)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            // 터치 거리에서는 시야 체크 안 함 - 문틀 상단 같은 얇은 구조물에 레이가 걸려서
            // 실제로 닿아있는데도 공격을 못 하는 경우가 있었음. 이 정도 거리면 닿은 걸로 충분.
            if (dist <= touchAttackRange && player.stats.currentNoiseLevel > 0f)
            {
                GoToAttack();
            }
        }

        currentState.Tick(this, Time.deltaTime);
    }

    // 벽에 붙어있으면 벽 너머 플레이어를 공격하지 못하도록 하는 게이트. MonsterHearing이 없으면(설정 누락) 항상 통과.
    public bool HasLineOfSightToPlayer() => hearing == null || hearing.HasClearLineOfSight(player.transform.position);

    // 소리 난 위치를 정확히 찍지 않고 오차 반경 안에서 어긋난 지점을 돌려줌 (NavMesh 위로 보정).
    // NoiseLevel이 클수록 오차가 줄지만 minSoundPositionError 밑으로는 안 내려간다 - 오브젝트에 완전히 붙는 것 방지.
    public Vector3 GetHeardPositionWithError()
    {
        float precision = Mathf.Clamp01(LastHeardLoudness / loudNoiseCommitThreshold);
        float error = Mathf.Lerp(soundPositionError, minSoundPositionError, precision);
        Vector2 offset = Random.insideUnitCircle * error;
        Vector3 jittered = LastHeardPosition + new Vector3(offset.x, 0f, offset.y);
        if (NavMesh.SamplePosition(jittered, out NavMeshHit hit, soundPositionError + 1f, NavMesh.AllAreas))
            return hit.position;
        return LastHeardPosition;
    }

    // ponytail: 정교한 방/구역 인식 없이, 앵커 주변을 방사형으로 훑어서 서로 도달 가능한(PathComplete)
    // 지점만 채택하는 단순한 방식 - 몹이 늘어나거나 더 정교한 구역 배치가 필요해지면 교체.
    private void RelocatePatrolNear(Vector3 source)
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (!NavMesh.SamplePosition(source, out NavMeshHit anchorHit, heardPositionSampleRadius, NavMesh.AllAreas)) return;
        Vector3 anchor = anchorHit.position;

        List<Vector3> candidates = new List<Vector3> { anchor };
        int attempts = patrolPoints.Length * 4;
        for (int i = 0; i < attempts && candidates.Count < patrolPoints.Length; i++)
        {
            float angle = i * (360f / attempts) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * patrolRelocateSpread;
            if (!NavMesh.SamplePosition(anchor + offset, out NavMeshHit hit, patrolRelocateSpread * 0.6f, NavMesh.AllAreas)) continue;

            NavMeshPath path = new NavMeshPath();
            NavMesh.CalculatePath(anchor, hit.position, NavMesh.AllAreas, path);
            if (path.status == NavMeshPathStatus.PathComplete) candidates.Add(hit.position);
        }

        for (int i = 0; i < patrolPoints.Length; i++)
            if (patrolPoints[i] != null) patrolPoints[i].position = candidates[i % candidates.Count];
    }

    public void OnNoiseHeard(NoiseTier tier, Vector3 position, float loudness)
    {
        if (tier == NoiseTier.None) return;
        if (currentState.Type == MonsterStateType.Attack || currentState.Type == MonsterStateType.Neutralization) return;

        // 던진 오브젝트가 벽/물체 옆면에 맞으면 소리 위치 자체가 NavMesh 밖에 찍힌다 - Second/Final이
        // 이 좌표를 그대로 목적지로 쓰면 영원히 도달 못 해 Analyzing 전환 없이 청각 타임아웃까지 가버림.
        // 저장 시점에 NavMesh로 스냅해서 이후 모든 소비처가 항상 도달 가능한 좌표만 쓰게 한다.
        LastHeardPosition = NavMesh.SamplePosition(position, out NavMeshHit navHit, heardPositionSampleRadius, NavMesh.AllAreas)
            ? navHit.position : position;
        // Patrol(완전히 잊은 상태)에서 새로 듣기 시작하면 이번 소리 크기로 새로 잡는다.
        // 이미 추적 중이면 더 큰 값만 반영 - 큰 소리 듣고 쫓아가는 도중 플레이어의 조용한 발소리가
        // 계속 들어와서 LastHeardLoudness를 깎아버리면, 시끄러운 소리로 얻은 "끝까지 쫓아갈 시간"이
        // 몇 프레임 만에 사라져버리는 문제가 있었음.
        LastHeardLoudness = currentState == patrolState ? loudness : Mathf.Max(LastHeardLoudness, loudness);
        lastHeardTime = Time.time;

        // 8단계(loudNoiseCommitThreshold) 이상의 큰 소리는 그냥 쫓아가는 걸로 안 끝난다 - 이후
        // 순찰 구역 자체를 그 근원지 근처로 옮겨서, 놓치더라도 그 주변을 계속 맴돌게 한다.
        if (loudness >= loudNoiseCommitThreshold) RelocatePatrolNear(LastHeardPosition);

        MonsterStateType target = tier == NoiseTier.Final ? MonsterStateType.FinalDetection
                                 : tier == NoiseTier.Second ? MonsterStateType.SecondDetection
                                 : MonsterStateType.FirstDetection;

        if (Rank(target) > CurrentAlertRank()) GoToDetection(target); // 더 강한 소리로만 격상, 하향은 시간 경과로만
    }

    // Idle 쿨다운 중에는 상태 Type이 Idle이라 Rank가 0으로 보이지만,
    // 실제로는 SourceTier 등급으로 대기 중인 것 -> 그 등급 기준으로 비교해야
    // 같은 세기의 소리가 반복될 때마다 쿨다운이 계속 리셋되는 걸 막는다.
    private int CurrentAlertRank() =>
        currentState == idleState ? Rank(idleState.SourceTier) : Rank(CurrentStateType);

    private static int Rank(MonsterStateType t) =>
        t == MonsterStateType.FinalDetection ? 3 :
        t == MonsterStateType.SecondDetection ? 2 :
        t == MonsterStateType.FirstDetection ? 1 : 0;

    public void SetAnimState(string name)
    {
        if (animator == null) return;
        // 이전에 걸린 트리거가 소비 안 된 채 남아있으면(Any State 전이가 canTransitionToSelf=false라
        // 자기 자신으로는 안 넘어가서 안 지워짐) 나중에 엉뚱한 상태로 튀는 원인이 됨 - 새로 걸기 전에 나머지를 비운다.
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name != name)
                animator.ResetTrigger(p.name);
        animator.SetTrigger(name);
    }

    public void ChangeState(IMonsterState next)
    {
        currentState?.Exit(this);
        currentState = next;
        currentState.Enter(this);
    }

    public void GoToIdleFrom(MonsterStateType sourceTier)
    {
        idleState.SourceTier = sourceTier;
        ChangeState(idleState);
    }

    public void GoToDetection(MonsterStateType tier)
    {
        switch (tier)
        {
            case MonsterStateType.FirstDetection: ChangeState(firstDetectionState); break;
            case MonsterStateType.SecondDetection: ChangeState(secondDetectionState); break;
            case MonsterStateType.FinalDetection: ChangeState(finalDetectionState); break;
        }
    }

    public void Downgrade(MonsterStateType fromTier)
    {
        switch (fromTier)
        {
            case MonsterStateType.SecondDetection: ChangeState(firstDetectionState); break;
            case MonsterStateType.FinalDetection: ChangeState(secondDetectionState); break;
            default: ChangeState(patrolState); break; // Patrol, FirstDetection
        }
    }

    public void GoToAttack() => ChangeState(attackState);

    public void Neutralize() => ChangeState(neutralizationState);
}
