using UnityEngine;

public interface IHidingSpot
{
    string HideAnimationName { get; }
    float NoiseLevelBump { get; }
    float NoiseRangeBump { get; }
    Transform HiddenViewPoint { get; }
}

public interface IHoldable
{
    string PickupAnimationName { get; }
    float NoiseLevelBump { get; }
    float NoiseRangeBump { get; }
    Transform HoldPoint { get; }

    /// 들고 있을 때의 이동 속도 배율. 1 = 평소 속도, 0.5 = 절반.
    /// 변압기 모듈처럼 무거운 물건은 낮게 준다.
    float MoveSpeedMultiplier { get; }

    /// 들고 있을 때 고개를 숙이는 정도(도). 무거울수록 크게.
    float CarryLookDownPitch { get; }

    /// 들고 있을 때 시점이 내려가는 정도(m). 무릎을 굽힌 느낌을 준다.
    float CarryEyeDrop { get; }

    void OnPickedUp(PlayerController player);
    void OnThrown(Vector3 force);
    void OnDropped();
}
