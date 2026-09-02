using UnityEngine;

public interface IPlayerState
{
    PlayerStateType Type { get; }
    void Enter(PlayerController ctx);
    void Tick(PlayerController ctx, float dt);
    void Exit(PlayerController ctx);
}

public class IdleState : IPlayerState
{
    public PlayerStateType Type => PlayerStateType.Idle;

    public void Enter(PlayerController ctx)
    {
        var s = ctx.stats;
        s.currentNoiseLevel = s.noiseLevelBase;
        s.currentNoiseRange = s.noiseRangeBase;
        s.currentMoveSpeed = 0f;
        ctx.SetAnimState("Idle");
    }

    public void Tick(PlayerController ctx, float dt)
    {
        if (ctx.StopHeld) { ctx.GoToStopping(); return; }
        if (ctx.SitTogglePressed) { ctx.GoToSitting(); return; }
        if (ctx.HasMoveInput) { ctx.GoToWalking(); return; }

        ctx.Move(Vector3.zero);
    }

    public void Exit(PlayerController ctx) { }
}

public class WalkingState : IPlayerState
{
    public PlayerStateType Type => PlayerStateType.Walking;

    public void Enter(PlayerController ctx)
    {
        var s = ctx.stats;
        s.currentNoiseLevel = s.ClampNoiseTier(s.noiseLevelBase + s.noiseTierStep);
        s.currentNoiseRange = s.ClampNoiseTier(s.noiseRangeBase + s.noiseTierStep + 1f);
        s.currentMoveSpeed = s.moveSpeed;
        ctx.SetAnimState("Walking");
    }

    public void Tick(PlayerController ctx, float dt)
    {
        if (ctx.StopHeld) { ctx.GoToStopping(); return; }
        if (ctx.SitTogglePressed) { ctx.GoToSitting(); return; }
        if (!ctx.HasMoveInput) { ctx.GoToIdle(); return; }

        ctx.Move(ctx.MoveDirectionWorld() * ctx.stats.currentMoveSpeed);
    }

    public void Exit(PlayerController ctx) { }
}

public class SittingState : IPlayerState
{
    public PlayerStateType Type => PlayerStateType.Sitting;

    public void Enter(PlayerController ctx)
    {
        var s = ctx.stats;
        s.currentNoiseLevel = s.ClampNoiseTier(s.noiseLevelBase - s.noiseTierStep);
        s.currentNoiseRange = s.ClampNoiseTier(s.noiseRangeBase - s.noiseTierStep);
        s.currentMoveSpeed = s.sittingMoveSpeed;
        ctx.SetAnimState("Sitting");
    }

    public void Tick(PlayerController ctx, float dt)
    {
        if (ctx.StopHeld) { ctx.GoToStopping(); return; }
        if (ctx.SitTogglePressed) { ctx.ClearSittingIntent(); ctx.GoToIdleOrWalking(); return; }

        Vector3 dir = ctx.HasMoveInput ? ctx.MoveDirectionWorld() * ctx.stats.currentMoveSpeed : Vector3.zero;
        ctx.Move(dir);
    }

    public void Exit(PlayerController ctx) { }
}

public class StoppingState : IPlayerState
{
    public PlayerStateType Type => PlayerStateType.Stopping;

    public void Enter(PlayerController ctx)
    {
        var s = ctx.stats;
        s.currentNoiseLevel = 0f;
        s.currentNoiseRange = 0f;
        s.currentMoveSpeed = 0f;
        ctx.SetAnimState("Idle"); // Stopping 전용 애니메이션이 아직 없어 Idle로 대체
    }

    public void Tick(PlayerController ctx, float dt)
    {
        ctx.stats.DrainOxygen(dt);
        ctx.Move(Vector3.zero);

        if (ctx.stats.oxygen <= 0f)
        {
            ctx.EnterGasping();
            return;
        }

        if (!ctx.StopHeld)
        {
            if (ctx.stats.oxygen < ctx.stats.gaspingThreshold) ctx.EnterGasping();
            else ctx.GoToIdleOrWalking();
        }
    }

    public void Exit(PlayerController ctx) { }
}

public class GaspingState : IPlayerState
{
    public PlayerStateType Type => PlayerStateType.Gasping;

    private float timer;
    private float duration;

    public void Enter(PlayerController ctx)
    {
        var s = ctx.stats;
        duration = Mathf.Lerp(3f, 1f, Mathf.Clamp01(s.oxygen / s.gaspingThreshold));
        timer = 0f;

        s.currentNoiseLevel = s.ClampNoiseTier(s.noiseLevelBase + s.noiseTierStep * 3f);
        s.currentNoiseRange = s.ClampNoiseTier(s.noiseRangeBase + s.noiseTierStep * 2f);
        s.currentMoveSpeed = 0f;
        ctx.SetAnimState("Gasping");
        ctx.SetLowerBodyIdleOverride(true); // 하체는 Gasping 대신 Idle 모션 유지 (PlayerAvatarMask)
        ctx.SetGaspingModelTilt(true); // Gasping 동안만 모델을 X축으로 살짝 틀어서 재생
    }

    public void Tick(PlayerController ctx, float dt)
    {
        ctx.Move(Vector3.zero);
        timer += dt;
        if (timer >= duration)
        {
            ctx.stats.oxygen = Mathf.Min(ctx.stats.oxygenMax, ctx.stats.oxygen + 1f);
            ctx.GoToIdleOrWalking(); // Sitting 중이었으면(wantsSitting) 그대로 복귀
        }
    }

    public void Exit(PlayerController ctx)
    {
        ctx.SetLowerBodyIdleOverride(false);
        ctx.SetGaspingModelTilt(false);
    }
}

public class FearingState : IPlayerState
{
    public PlayerStateType Type => PlayerStateType.Fearing;

    private const float Duration = 6f;
    private float timer;

    public void Enter(PlayerController ctx)
    {
        var s = ctx.stats;
        s.currentNoiseLevel = s.noiseMaxTier;
        s.currentNoiseRange = s.noiseMaxTier;
        s.currentMoveSpeed = 0f;
        timer = 0f;
        ctx.CameraLocked = true;
        ctx.SetAnimState("Fearing");
    }

    public void Tick(PlayerController ctx, float dt)
    {
        ctx.Move(Vector3.zero);
        timer += dt;
        if (timer >= Duration)
        {
            ctx.stats.fear = 0f;
            ctx.GoToIdle();
        }
    }

    public void Exit(PlayerController ctx)
    {
        ctx.CameraLocked = false;
    }
}

public class HidingState : IPlayerState
{
    public PlayerStateType Type => PlayerStateType.Hiding;

    private enum Phase { Entering, Hidden, Exiting }
    private Phase phase;
    private float timer;

    public void Enter(PlayerController ctx)
    {
        phase = Phase.Entering;
        timer = 0f;
        ApplyNoiseBump(ctx);
        ctx.stats.currentMoveSpeed = 0f;
        if (ctx.ActiveHidingSpot != null) ctx.SetAnimState(ctx.ActiveHidingSpot.HideAnimationName);
    }

    public void Tick(PlayerController ctx, float dt)
    {
        ctx.Move(Vector3.zero);

        switch (phase)
        {
            case Phase.Entering:
                timer += dt;
                if (timer >= ctx.hideTransitionDuration)
                {
                    phase = Phase.Hidden;
                    RestoreQuietNoise(ctx);
                }
                break;

            case Phase.Hidden:
                if (ctx.HasMoveInput)
                {
                    phase = Phase.Exiting;
                    timer = 0f;
                    ApplyNoiseBump(ctx);
                }
                break;

            case Phase.Exiting:
                timer += dt;
                if (timer >= ctx.hideTransitionDuration)
                {
                    ctx.GoToIdleOrWalking();
                }
                break;
        }
    }

    public void Exit(PlayerController ctx)
    {
        ctx.ExitHidingInternal();
    }

    private void ApplyNoiseBump(PlayerController ctx)
    {
        var spot = ctx.ActiveHidingSpot;
        var s = ctx.stats;
        s.currentNoiseLevel = spot != null ? spot.NoiseLevelBump : s.noiseLevelBase;
        s.currentNoiseRange = spot != null ? spot.NoiseRangeBump : s.noiseRangeBase;
    }

    private void RestoreQuietNoise(PlayerController ctx)
    {
        // 스펙에 "숨어있는 동안"의 소음 수치가 명시되어 있지 않아 Sitting과 동일 수준(기본값 -1단계)으로 가정
        var s = ctx.stats;
        s.currentNoiseLevel = s.ClampNoiseTier(s.noiseLevelBase - s.noiseTierStep);
        s.currentNoiseRange = s.ClampNoiseTier(s.noiseRangeBase - s.noiseTierStep);
    }
}

public class HoldingState : IPlayerState
{
    public PlayerStateType Type => PlayerStateType.Holding;

    private bool animating;
    private float timer;

    public void Enter(PlayerController ctx)
    {
        animating = true;
        timer = 0f;
        ApplyNoiseBump(ctx);
        ctx.stats.currentMoveSpeed = 0f;
        if (ctx.HeldItem != null) ctx.SetAnimState(ctx.HeldItem.PickupAnimationName);
    }

    public void Tick(PlayerController ctx, float dt)
    {
        if (animating)
        {
            ctx.Move(Vector3.zero);
            timer += dt;
            if (timer >= ctx.pickupAnimationDuration)
            {
                animating = false;
                RestoreNormalNoise(ctx);
                ctx.stats.currentMoveSpeed = ctx.stats.moveSpeed;
            }
            return;
        }

        if (ctx.ThrowPressed)
        {
            ctx.ThrowHeldItem(ctx.transform.forward * 10f);
            return;
        }
        if (ctx.DropPressed)
        {
            ctx.DropHeldItem();
            return;
        }

        Vector3 dir = ctx.HasMoveInput ? ctx.MoveDirectionWorld() * ctx.stats.moveSpeed : Vector3.zero;
        ctx.Move(dir);
    }

    public void Exit(PlayerController ctx) { }

    private void ApplyNoiseBump(PlayerController ctx)
    {
        var item = ctx.HeldItem;
        var s = ctx.stats;
        s.currentNoiseLevel = item != null ? item.NoiseLevelBump : s.noiseLevelBase;
        s.currentNoiseRange = item != null ? item.NoiseRangeBump : s.noiseRangeBase;
    }

    private void RestoreNormalNoise(PlayerController ctx)
    {
        var s = ctx.stats;
        s.currentNoiseLevel = s.noiseLevelBase;
        s.currentNoiseRange = s.noiseRangeBase;
    }
}
