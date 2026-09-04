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

        // 확실히 알아챈 상태 — 스냅샷이 아니라 플레이어 실시간 위치를 계속 추적 (계속 움직여도 놓치지 않음)
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
        if (dist <= ctx.attackRange && ctx.HasLineOfSightToPlayer())
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

        // 확실히 알아챈 상태 — 스냅샷이 아니라 플레이어 실시간 위치를 계속 추적 (계속 움직여도 놓치지 않음)
        ctx.Agent.SetDestination(ctx.player.transform.position);
    }

    public void Exit(MonsterController ctx) { isPaused = false; }
}

public class MonsterAttackState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.Attack;
    private const float Duration = 1f; // no anim yet; placeholder length
    private float timer;

    public void Enter(MonsterController ctx)
    {
        timer = 0f;
        ctx.Agent.ResetPath();
        ctx.SetAnimState("Attack");
    }

    public void Tick(MonsterController ctx, float dt)
    {
        timer += dt;
        if (timer >= Duration)
        {
            ctx.GoToIdleFrom(MonsterStateType.FinalDetection);
        }
    }

    public void Exit(MonsterController ctx) { }
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
