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
        ctx.SetSittingCollider(true);
    }

    public void Tick(PlayerController ctx, float dt)
    {
        if (ctx.StopHeld) { ctx.GoToStopping(); return; }
        if (ctx.SitTogglePressed) { ctx.ClearSittingIntent(); ctx.GoToIdleOrWalking(); return; }

        Vector3 dir = ctx.HasMoveInput ? ctx.MoveDirectionWorld() * ctx.stats.currentMoveSpeed : Vector3.zero;
        ctx.Move(dir);
    }

    public void Exit(PlayerController ctx)
    {
        ctx.SetSittingCollider(false);
    }
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

    /// 공포가 100% 가 된 순간의 비명. 이름은 코드에 둔다 - 인스펙터 참조는 병합 때
    /// 빠지지만 이 문자열은 남고, SoundManager 가 못 찾으면 사운드 목록에서 찾아준다.
    private const string ScreamName = "malescreamHigh";

    /// 범위는 레벨(noiseMaxTier)과 따로 조절할 수 있어야 해서 별도 상수로 둔다.
    private const float NoiseRange = 14f;

    public void Enter(PlayerController ctx)
    {
        var s = ctx.stats;
        s.currentNoiseLevel = s.noiseMaxTier;
        s.currentNoiseRange = NoiseRange;
        s.currentMoveSpeed = 0f;
        timer = 0f;
        ctx.CameraLocked = true;
        ctx.SetAnimState("Fearing");

        // 여기가 공포 100% 다. 6초 뒤 공포가 0 으로 돌아가므로 연달아 나지 않는다.
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(ScreamName);
        Debug.Log("[FearingState] 공포 100% - 비명 " + ScreamName);
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
        // Stopping(숨 참기)과 동일한 소음 수준 - 완전히 숨은 상태는 사실상 무음으로 취급
        ctx.stats.currentNoiseLevel = 0f;
        ctx.stats.currentNoiseRange = 0f;
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
                // 무거운 물건일수록 느리게 걷는다. 배율은 물건이 들고 있다(IHoldable).
                float weightFactor = ctx.HeldItem != null ? ctx.HeldItem.MoveSpeedMultiplier : 1f;
                ctx.stats.currentMoveSpeed = ctx.stats.moveSpeed * weightFactor;
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

        // currentMoveSpeed 를 쓴다. moveSpeed 를 직접 쓰면 위에서 계산한 무게 배율이 무시된다.
        Vector3 dir = ctx.HasMoveInput ? ctx.MoveDirectionWorld() * ctx.stats.currentMoveSpeed : Vector3.zero;
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
