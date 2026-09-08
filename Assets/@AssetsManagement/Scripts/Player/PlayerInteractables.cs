using UnityEngine;

public interface IHidingSpot
{
    string HideAnimationName { get; }
    float NoiseLevelBump { get; }
    float NoiseRangeBump { get; }
    Transform HiddenViewPoint { get; }

    /// 숨기 시작할 때. 플레이어를 안으로 옮기고 문을 닫는 등의 처리를 여기서 한다.
    void OnHideEnter(PlayerController player);

    /// 나올 때. 플레이어를 원래 서 있던 자리로 돌려놓는다.
    void OnHideExit(PlayerController player);
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
