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

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.firstChaseSpeed;
        ctx.SetAnimState("Walking");
        ctx.Agent.SetDestination(ctx.LastHeardPosition); // 실제 소리 발생 위치로 스냅샷 (매 프레임 추적 아님)
    }

    public void Tick(MonsterController ctx, float dt)
    {
        if (!ctx.IsHearingFresh)
        {
            ctx.GoToIdleFrom(MonsterStateType.FirstDetection);
            return;
        }

        // 스냅샷 지점에 도착했는데도 여전히 들리면, 그 시점 위치로 다시 한번 스냅샷
        if (!ctx.Agent.pathPending && ctx.Agent.remainingDistance <= ctx.Agent.stoppingDistance)
        {
            ctx.Agent.SetDestination(ctx.LastHeardPosition);
        }
    }

    public void Exit(MonsterController ctx) { }
}

public class MonsterSecondDetectionState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.SecondDetection;

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.secondChaseSpeed;
        ctx.SetAnimState("Chasing");
        ctx.Agent.SetDestination(ctx.LastHeardPosition); // 마지막으로 들은 위치로 스냅샷 (실시간 추적 아님)
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

        // 스냅샷 지점 도착 후에도 여전히 들리면, 그 시점 위치로 다시 스냅샷
        if (!ctx.Agent.pathPending && ctx.Agent.remainingDistance <= ctx.Agent.stoppingDistance)
        {
            ctx.Agent.SetDestination(ctx.LastHeardPosition);
        }
    }

    public void Exit(MonsterController ctx) { }
}

public class MonsterFinalDetectionState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.FinalDetection;

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.secondChaseSpeed;
        ctx.SetAnimState("Chasing");
        ctx.Agent.SetDestination(ctx.LastHeardPosition); // 마지막으로 들은 위치로 스냅샷 (실시간 추적 아님)
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

        // 스냅샷 지점 도착 후에도 여전히 들리면, 그 시점 위치로 다시 스냅샷
        if (!ctx.Agent.pathPending && ctx.Agent.remainingDistance <= ctx.Agent.stoppingDistance)
        {
            ctx.Agent.SetDestination(ctx.LastHeardPosition);
        }
    }

    public void Exit(MonsterController ctx) { }
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
