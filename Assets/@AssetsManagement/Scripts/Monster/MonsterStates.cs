using UnityEngine;
using UnityEngine.AI;

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
    private float totalTimer; // 계속 들려서 IsHearingFresh가 안 풀려도, 이거 하나로 결국 포기하게 만든다
    private bool targetUnreachable; // 감지 지점이 방 너머 등 도달 불가라 판정되면, 그 뒤로 재시도 자체를 멈춘다

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.firstChaseSpeed;
        totalTimer = 0f;
        targetUnreachable = false;
        BeginPause(ctx, ctx.reactionDelay, "Idle"); // 감지 직후 바로 움직이지 않고 "어?" 하고 멈칫 - 아직 도착 전이라 Idle
    }

    public void Tick(MonsterController ctx, float dt)
    {
        // 닿았는데 완전히 숨죽인(Stopping) 게 아니라 소리까지 내고 있으면 확실한 증거 - Final로 즉시 격상.
        // 반대로 닿아도 숨죽이고 있으면(currentNoiseLevel<=0) 넘어가준다 - 완벽히 숨는 걸 보상해줌.
        float touchDist = Vector3.Distance(ctx.transform.position, ctx.player.transform.position);
        if (touchDist <= ctx.touchAttackRange && ctx.player.stats.currentNoiseLevel > 0f)
        {
            ctx.GoToDetection(MonsterStateType.FinalDetection);
            return;
        }

        totalTimer += dt;
        // Second/Final로 못 올라간 애매한 소리가 계속 들리기만 하면 무한정 따라다니게 되므로,
        // 격상 없이 이 시간을 넘기면 그냥 포기한다 (스니킹 플레이어를 영원히 쫓아다니는 것 방지).
        if (totalTimer >= ctx.firstTierMaxDuration)
        {
            ctx.GoToIdleFrom(MonsterStateType.FirstDetection);
            return;
        }

        if (!ctx.IsHearingFresh)
        {
            ctx.GoToIdleFrom(MonsterStateType.FirstDetection);
            return;
        }

        if (isPaused)
        {
            // 도달 불가로 판정된 뒤에는 새 지점을 다시 흔들어봐야 똑같이 못 갈 뿐이라, 그냥 이 자세로
            // 버틴다 - 위쪽의 타임아웃/청각 만료로만 빠져나간다 (한쪽만 보고 반복 재생되는 것 방지).
            if (targetUnreachable) return;
            pauseTimer -= dt;
            if (pauseTimer <= 0f) EndPause(ctx);
            return;
        }

        // 감지된 위치에 도착하면 무조건 두리번(Scan) - 여전히 들리면 이 상태에서 잠깐 있다가 다시 스냅샷
        if (!ctx.Agent.pathPending && ctx.Agent.remainingDistance <= ctx.Agent.stoppingDistance)
        {
            targetUnreachable = ctx.Agent.pathStatus != NavMeshPathStatus.PathComplete;
            BeginPause(ctx, ctx.investigateLingerTime, "Analyzing");
        }
    }

    public void Exit(MonsterController ctx) { isPaused = false; targetUnreachable = false; }

    private void BeginPause(MonsterController ctx, float duration, string animState)
    {
        isPaused = true;
        pauseTimer = duration;
        ctx.Agent.ResetPath();
        ctx.SetAnimState(animState);
    }

    private void EndPause(MonsterController ctx)
    {
        isPaused = false;
        ctx.SetAnimState("Chasing");
        ctx.Agent.SetDestination(ctx.GetHeardPositionWithError()); // 정확한 좌표가 아니라 대충 그 근처
    }
}

public class MonsterSecondDetectionState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.SecondDetection;

    private bool isPaused;
    private float pauseTimer;
    private bool arrivedIdle; // 쫓아갈 곳에 도착해서 더 갱신될 목적지가 없어 두리번거리는 중인지
    private bool targetUnreachable; // LastHeardPosition이 방 너머 등 도달 불가라 판정되면, 그 뒤로 재시도 자체를 멈춘다

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.secondChaseSpeed;
        isPaused = true;
        pauseTimer = ctx.reactionDelay; // 감지 직후 바로 움직이지 않고 "어?" 하고 멈칫 (딱 한 번만)
        arrivedIdle = false;
        targetUnreachable = false;
        ctx.Agent.ResetPath();
        ctx.SetAnimState("Idle");
    }

    public void Tick(MonsterController ctx, float dt)
    {
        // 멈칫(reactionDelay) 동안은 격상 판정 자체를 안 한다 - 락온 직후 실제로 반응할 시간을 준다.
        // 이 텀이 없으면 "어?" 하고 멈칫하는 연출만 있고 실제로는 그 순간 바로 다음 단계로 넘어가버림.
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

        float dist = Vector3.Distance(ctx.transform.position, ctx.player.transform.position);
        // IsHearingFresh도 같이 봐야 한다 - 안 그러면 청각이 이미 만료됐는데도 실좌표 거리만으로
        // Final을 계속 재격상해서, 숨어서 조용해진 플레이어 옆에 우연히 서 있기만 해도
        // Final<->Idle<->Second를 무한 반복하며 절대 포기하지 않는 버그가 생긴다.
        if (dist <= ctx.attackRange && ctx.IsHearingFresh)
        {
            ctx.GoToDetection(MonsterStateType.FinalDetection);
            return;
        }
        if (!ctx.IsHearingFresh)
        {
            ctx.GoToIdleFrom(MonsterStateType.SecondDetection);
            return;
        }

        // 도달 불가로 판정된 뒤에는 재시도 자체를 멈추고 그 자세로 버틴다 - 공격판정/청각만료 체크는
        // 위에서 계속 돌고 있으니 자연스럽게 빠져나간다 (한쪽만 보고 반복 재생되는 것 방지).
        if (targetUnreachable) return;

        // 확실히 알아챈 상태지만, 실좌표가 아니라 LastHeardPosition(마지막으로 실제 소리가 들린 지점)을
        // 쫓는다 - 플레이어가 계속 소리를 내는 한(PlayerNoiseEmitter가 0.3초마다 갱신) 사실상 실시간
        // 추적과 다름없지만, 조용해지면 그 지점에서 멈춰서 실제로 놓칠 수 있다 - "위치를 보고 있는"
        // 것이 아니라 "소리를 쫓는" 느낌을 위함. 목적지가 크게 안 바뀌었으면 재지정 안 함 - 좁은 틈
        // 너머라 도달 불가한 경우 매프레임 가장 가까운 지점을 다시 계산하면서 제자리에서 떠는 것 방지.
        if (Vector3.Distance(ctx.Agent.destination, ctx.LastHeardPosition) > ctx.destinationUpdateThreshold)
        {
            ctx.Agent.SetDestination(ctx.LastHeardPosition);
            if (arrivedIdle)
            {
                arrivedIdle = false;
                ctx.SetAnimState("Chasing"); // 새 소리가 갱신돼서 다시 쫓아감
            }
        }
        else if (!arrivedIdle && !ctx.Agent.pathPending && ctx.Agent.remainingDistance <= ctx.Agent.stoppingDistance)
        {
            // 도착했는데 더 갈 곳이 없음 - 청각 상태(타임아웃/공격 판정)는 그대로 유지한 채
            // 계속 뛰는 모션 대신 두리번거리는 모션으로 - 제자리에서 계속 달리는 것처럼 보이는 것 방지.
            targetUnreachable = ctx.Agent.pathStatus != NavMeshPathStatus.PathComplete;
            arrivedIdle = true;
            ctx.SetAnimState("Analyzing");
        }
    }

    public void Exit(MonsterController ctx) { isPaused = false; arrivedIdle = false; targetUnreachable = false; }
}

public class MonsterFinalDetectionState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.FinalDetection;

    private bool isPaused;
    private float pauseTimer;
    private bool arrivedIdle; // 쫓아갈 곳에 도착해서 더 갱신될 목적지가 없어 두리번거리는 중인지
    private bool targetUnreachable; // LastHeardPosition이 방 너머 등 도달 불가라 판정되면, 그 뒤로 재시도 자체를 멈춘다

    public void Enter(MonsterController ctx)
    {
        ctx.Agent.speed = ctx.secondChaseSpeed;
        isPaused = true;
        pauseTimer = ctx.reactionDelay; // 감지 직후 바로 움직이지 않고 "어?" 하고 멈칫 (딱 한 번만)
        arrivedIdle = false;
        targetUnreachable = false;
        ctx.Agent.ResetPath();
        ctx.SetAnimState("Idle");
    }

    public void Tick(MonsterController ctx, float dt)
    {
        // 멈칫(reactionDelay) 동안은 공격 판정 자체를 안 한다 - 락온 직후 실제로 반응할 시간을 준다.
        // 이 텀이 없으면 "어?" 하고 멈칫하는 연출만 있고 실제로는 그 순간 바로 공격이 나가버림.
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

        float dist = Vector3.Distance(ctx.transform.position, ctx.player.transform.position);
        // 완전히 조용한(Stopping/Hiding, currentNoiseLevel<=0) 상태면 거리만으로 공격하지 않는다 -
        // MonsterController의 범용 즉시공격 체크와 동일한 조건을 여기도 걸어야 한다. Final은 그
        // 체크를 안 거치고 자체 판정으로 공격하기 때문에, 안 걸면 숨어도 이미 락온된 상태에서
        // 다가오면 그대로 공격당한다.
        // 터치 거리 안쪽이면 시야 체크 생략 - 문틀 상단 같은 얇은 구조물에 막혀서
        // 실제로 붙어있는데 공격을 못 하는 경우 방지. 그 밖(원거리)은 기존대로 벽 너머 공격 방지 유지.
        if (ctx.player.stats.currentNoiseLevel > 0f &&
            (dist <= ctx.touchAttackRange || (dist <= ctx.attackRange && ctx.HasLineOfSightToPlayer())))
        {
            ctx.GoToAttack();
            return;
        }
        if (!ctx.IsHearingFresh)
        {
            ctx.GoToIdleFrom(MonsterStateType.FinalDetection);
            return;
        }

        // 도달 불가로 판정된 뒤에는 재시도 자체를 멈추고 그 자세로 버틴다 - 공격판정/청각만료 체크는
        // 위에서 계속 돌고 있으니 자연스럽게 빠져나간다 (한쪽만 보고 반복 재생되는 것 방지).
        if (targetUnreachable) return;

        // 확실히 알아챈 상태지만, 실좌표가 아니라 LastHeardPosition(마지막으로 실제 소리가 들린 지점)을
        // 쫓는다 - 플레이어가 계속 소리를 내는 한(PlayerNoiseEmitter가 0.3초마다 갱신) 사실상 실시간
        // 추적과 다름없지만, 조용해지면 그 지점에서 멈춰서 실제로 놓칠 수 있다 - "위치를 보고 있는"
        // 것이 아니라 "소리를 쫓는" 느낌을 위함. 목적지가 크게 안 바뀌었으면 재지정 안 함 - 좁은 틈
        // 너머라 도달 불가한 경우 매프레임 가장 가까운 지점을 다시 계산하면서 제자리에서 떠는 것 방지.
        if (Vector3.Distance(ctx.Agent.destination, ctx.LastHeardPosition) > ctx.destinationUpdateThreshold)
        {
            ctx.Agent.SetDestination(ctx.LastHeardPosition);
            if (arrivedIdle)
            {
                arrivedIdle = false;
                ctx.SetAnimState("Chasing"); // 새 소리가 갱신돼서 다시 쫓아감
            }
        }
        else if (!arrivedIdle && !ctx.Agent.pathPending && ctx.Agent.remainingDistance <= ctx.Agent.stoppingDistance)
        {
            // 도착했는데 더 갈 곳이 없음 - 청각 상태(타임아웃/공격 판정)는 그대로 유지한 채
            // 계속 뛰는 모션 대신 두리번거리는 모션으로 - 제자리에서 계속 달리는 것처럼 보이는 것 방지.
            targetUnreachable = ctx.Agent.pathStatus != NavMeshPathStatus.PathComplete;
            arrivedIdle = true;
            ctx.SetAnimState("Analyzing");
        }
    }

    public void Exit(MonsterController ctx) { isPaused = false; arrivedIdle = false; targetUnreachable = false; }
}

public class MonsterAttackState : IMonsterState
{
    public MonsterStateType Type => MonsterStateType.Attack;
    private const float Duration = 2.13f; // Death_Angel_Attack 클립 실제 길이에 맞춤 (예전엔 애니메이션이 없어서 1초 placeholder였음)
    private const float FaceDuration = 0.5f; // 이 시간 동안만 플레이어 쪽으로 계속 회전 보정
    private const float FaceTurnSpeed = 720f; // 도/초 - 0.5초면 180도 어긋나 있어도 충분히 다 돈다
    private float timer;

    /// 죽음 연출이 뒤를 맡았는가. 맡았으면 여기서 게임오버를 부르지 않는다 -
    /// 연출이 비명/벽 충돌/회색 페이드를 다 보여준 뒤 스스로 부른다.
    private bool deathSequenceTookOver;

    public void Enter(MonsterController ctx)
    {
        timer = 0f;
        ctx.Agent.ResetPath();
        ctx.Agent.velocity = Vector3.zero; // 잔여 속도 제거 - 안 지우면 에이전트가 이 방향으로 회전을 되돌림
        ctx.Agent.updateRotation = false; // 공격 중엔 회전을 이 상태가 전적으로 담당 (에이전트가 다시 못 덮어쓰게)
        ctx.SetAnimState("Attack");

        // 팔을 휘두르는 동작에 맞춰 연출이 흘러가야 하므로 공격이 끝난 뒤가 아니라
        // 시작할 때 넘긴다.
        deathSequenceTookOver = PlayerDeathSequence.Begin(ctx.transform);
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

        if (timer >= Duration && !deathSequenceTookOver)
        {
            // 연출이 없는 씬(프리뷰 등)에서는 예전처럼 바로 게임오버.
            // 살아서 순찰/추적으로 돌아가지는 않는다.
            GameManager.Instance?.TriggerGameOver();
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
