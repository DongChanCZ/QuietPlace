using UnityEngine;

public interface IMonsterState
{
    MonsterStateType Type { get; }
    void Enter(MonsterController ctx);
    void Tick(MonsterController ctx, float dt);
    void Exit(MonsterController ctx);
}

public class MonsterIdleState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.Idle;
    public MonsterStateType SourceTier;

    private float timer;
    private float duration;

    public void Enter(MonsterController ctx)
    {
        timer = 0f;
        duration = DurationFor(SourceTier);
        ctx.Agent.ResetPath();
        ctx.SetAnimState("Shouting");
    }

    public void Tick(MonsterController ctx, float dt)
    {
        timer += dt;
        if (timer >= duration)
        {
            ctx.Downgrade(SourceTier);
        }
    }

    public void Exit(MonsterController ctx) { }

    private static float DurationFor(MonsterStateType tier)
    {
        switch (tier)
        {
            case MonsterStateType.FirstDetection: return 3f;
            case MonsterStateType.SecondDetection: return 3f;
            case MonsterStateType.FinalDetection: return 2f;
            default: return 1f;
        }
    }
}

public class MonsterPatrolState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.Patrol;
    private const float WaitAtPoint = 1f; // 지점 도착 후 잠깐 서 있는 시간 (Shouting 없음 — Idle과 별개)

    private int targetIndex = -1;
    private bool waiting;
    private float waitTimer;

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.patrolSpeed;
        waiting = false;
        MoveToNextPoint(ctx);
    }

    public void Tick(MonsterController ctx, float dt)
    {
        if (waiting)
        {
            waitTimer += dt;
            if (waitTimer >= WaitAtPoint) MoveToNextPoint(ctx);
            return;
        }

        if (!ctx.Agent.pathPending && ctx.Agent.remainingDistance <= ctx.Agent.stoppingDistance)
        {
            waiting = true;
            waitTimer = 0f;
            ctx.SetAnimState("Idle"); // 순찰 중 잠깐 대기 — 괴성/애니메이션 없이 조용히 서 있기만
        }
    }

    public void Exit(MonsterController ctx) { }

    private void MoveToNextPoint(MonsterController ctx)
    {
        if (ctx.patrolPoints == null || ctx.patrolPoints.Length == 0) return;
        waiting = false;
        targetIndex = (targetIndex + 1) % ctx.patrolPoints.Length;
        ctx.SetAnimState("Walking");
        ctx.Agent.SetDestination(ctx.patrolPoints[targetIndex].position);
    }
}

public class MonsterFirstDetectionState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.FirstDetection;

    private bool isPaused;
    private float pauseTimer;

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.firstChaseSpeed;
        BeginPause(ctx, ctx.reactionDelay); // 감지 직후 바로 움직이지 않고 "어?" 하고 멈칫
    }

    public void Tick(MonsterController ctx, float dt)
    {
        if (!ctx.IsHearingFresh)
        {
            ctx.GoToIdleFrom(MonsterStateType.FirstDetection);
            return;
        }

        if (isPaused)
        {
            pauseTimer -= dt;
            if (pauseTimer <= 0f) EndPause(ctx);
            return;
        }

        // 스냅샷 지점에 도착했는데도 여전히 들리면, 잠깐 두리번거린 후 다시 스냅샷
        if (!ctx.Agent.pathPending && ctx.Agent.remainingDistance <= ctx.Agent.stoppingDistance)
        {
            BeginPause(ctx, ctx.investigateLingerTime);
        }
    }

    public void Exit(MonsterController ctx) { isPaused = false; }

    private void BeginPause(MonsterController ctx, float duration)
    {
        isPaused = true;
        pauseTimer = duration;
        ctx.Agent.ResetPath();
        ctx.SetAnimState("Idle");
    }

    private void EndPause(MonsterController ctx)
    {
        isPaused = false;
        ctx.SetAnimState("Walking");
        ctx.Agent.SetDestination(ctx.GetHeardPositionWithError()); // 정확한 좌표가 아니라 대충 그 근처
    }
}

public class MonsterSecondDetectionState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.SecondDetection;

    private bool isPaused;
    private float pauseTimer;

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.secondChaseSpeed;
        isPaused = true;
        pauseTimer = ctx.reactionDelay; // 감지 직후 바로 움직이지 않고 "어?" 하고 멈칫 (딱 한 번만)
        ctx.Agent.ResetPath();
        ctx.SetAnimState("Idle");
    }

    public void Tick(MonsterController ctx, float dt)
    {
        float dist = Vector3.Distance(ctx.transform.position, ctx.player.transform.position);
        if (dist <= ctx.attackRange)
        {
            ctx.GoToDetection(MonsterStateType.FinalDetection);
            return;
        }
        if (!ctx.IsHearingFresh)
        {
            ctx.GoToIdleFrom(MonsterStateType.SecondDetection);
            return;
        }

        if (isPaused)
        {
            pauseTimer -= dt;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
                ctx.SetAnimState("Chasing");
            }
            return;
        }

        // 확실히 알아챈 상태 — 스냅샷이 아니라 플레이어 실시간 위치를 계속 추적 (계속 움직여도 놓치지 않음).
        // 단, 목적지가 크게 안 바뀌었으면 재지정 안 함 - 좁은 틈 너머라 도달 불가한 경우 매프레임
        // 가장 가까운 지점을 다시 계산하면서 제자리에서 떠는 것처럼 보이는 걸 방지.
        if (Vector3.Distance(ctx.Agent.destination, ctx.player.transform.position) > ctx.destinationUpdateThreshold)
            ctx.Agent.SetDestination(ctx.player.transform.position);
    }

    public void Exit(MonsterController ctx) { isPaused = false; }
}

public class MonsterFinalDetectionState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.FinalDetection;

    private bool isPaused;
    private float pauseTimer;

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.secondChaseSpeed;
        isPaused = true;
        pauseTimer = ctx.reactionDelay; // 감지 직후 바로 움직이지 않고 "어?" 하고 멈칫 (딱 한 번만)
        ctx.Agent.ResetPath();
        ctx.SetAnimState("Idle");
    }

    public void Tick(MonsterController ctx, float dt)
    {
        float dist = Vector3.Distance(ctx.transform.position, ctx.player.transform.position);
        // 터치 거리 안쪽이면 시야 체크 생략 - 문틀 상단 같은 얇은 구조물에 막혀서
        // 실제로 붙어있는데 공격을 못 하는 경우 방지. 그 밖(원거리)은 기존대로 벽 너머 공격 방지 유지.
        if (dist <= ctx.touchAttackRange || (dist <= ctx.attackRange && ctx.HasLineOfSightToPlayer()))
        {
            ctx.GoToAttack();
            return;
        }
        if (!ctx.IsHearingFresh)
        {
            ctx.GoToIdleFrom(MonsterStateType.FinalDetection);
            return;
        }

        if (isPaused)
        {
            pauseTimer -= dt;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
                ctx.SetAnimState("Chasing");
            }
            return;
        }

        // 확실히 알아챈 상태 — 스냅샷이 아니라 플레이어 실시간 위치를 계속 추적 (계속 움직여도 놓치지 않음).
        // 단, 목적지가 크게 안 바뀌었으면 재지정 안 함 - 좁은 틈 너머라 도달 불가한 경우 매프레임
        // 가장 가까운 지점을 다시 계산하면서 제자리에서 떠는 것처럼 보이는 걸 방지.
        if (Vector3.Distance(ctx.Agent.destination, ctx.player.transform.position) > ctx.destinationUpdateThreshold)
            ctx.Agent.SetDestination(ctx.player.transform.position);
    }

    public void Exit(MonsterController ctx) { isPaused = false; }
}

public class MonsterAttackState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.Attack;
    private const float Duration = 2.13f; // Death_Angel_Attack 클립 실제 길이에 맞춤 (예전엔 애니메이션이 없어서 1초 placeholder였음)
    private const float FaceDuration = 0.5f; // 이 시간 동안만 플레이어 쪽으로 계속 회전 보정
    private const float FaceTurnSpeed = 720f; // 도/초 - 0.5초면 180도 어긋나 있어도 충분히 다 돈다
    private float timer;

    public void Enter(MonsterController ctx)
    {
        timer = 0f;
        ctx.Agent.ResetPath();
        ctx.Agent.velocity = Vector3.zero; // 잔여 속도 제거 - 안 지우면 에이전트가 이 방향으로 회전을 되돌림
        ctx.Agent.updateRotation = false; // 공격 중엔 회전을 이 상태가 전적으로 담당 (에이전트가 다시 못 덮어쓰게)
        ctx.SetAnimState("Attack");
    }

    public void Tick(MonsterController ctx, float dt)
    {
        timer += dt;

        if (timer <= FaceDuration)
        {
            Vector3 toPlayer = ctx.player.transform.position - ctx.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(toPlayer);
                ctx.transform.rotation = Quaternion.RotateTowards(ctx.transform.rotation, target, FaceTurnSpeed * dt);
            }
        }

        if (timer >= Duration)
        {
            ctx.GoToIdleFrom(MonsterStateType.FinalDetection);
        }
    }

    public void Exit(MonsterController ctx)
    {
        ctx.Agent.updateRotation = true; // 다음 상태(추적/순찰)에서 다시 에이전트가 회전을 담당하도록 복구
    }
}

public class MonsterNeutralizationState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.Neutralization;

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.ResetPath();
        ctx.Agent.enabled = false;
        ctx.SetAnimState("Shouting");
    }

    public void Tick(MonsterController ctx, float dt) { }
    public void Exit(MonsterController ctx) { }
}
