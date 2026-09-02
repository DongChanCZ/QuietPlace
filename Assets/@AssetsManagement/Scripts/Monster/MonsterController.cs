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

    [Header("Patrol")]
    public Transform[] patrolPoints;

    [Header("Hearing Memory")]
    public float hearingMemoryDuration = 2f; // 이 시간 안에 새 소리가 없으면 '안 들림' (Raycast 없이 시간 비교만)

    public NavMeshAgent Agent { get; private set; }
    public MonsterStateType CurrentStateType => currentState != null ? currentState.Type : MonsterStateType.Idle;
    public Vector3 LastHeardPosition { get; private set; }
    public bool IsHearingFresh => Time.time - lastHeardTime <= hearingMemoryDuration;

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

        if (currentState.Type != MonsterStateType.Attack)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist <= touchAttackRange && player.stats.currentNoiseLevel > 0f && HasLineOfSightToPlayer())
            {
                GoToAttack();
            }
        }

        currentState.Tick(this, Time.deltaTime);
    }

    // 벽에 붙어있으면 벽 너머 플레이어를 공격하지 못하도록 하는 게이트. MonsterHearing이 없으면(설정 누락) 항상 통과.
    public bool HasLineOfSightToPlayer() => hearing == null || hearing.HasClearLineOfSight(player.transform.position);

    public void OnNoiseHeard(NoiseTier tier, Vector3 position)
    {
        if (tier == NoiseTier.None) return;
        if (currentState.Type == MonsterStateType.Attack || currentState.Type == MonsterStateType.Neutralization) return;

        LastHeardPosition = position;
        lastHeardTime = Time.time;

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
