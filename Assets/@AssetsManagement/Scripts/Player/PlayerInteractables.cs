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

    void OnPickedUp(PlayerController player);
    void OnThrown(Vector3 force);
    void OnDropped();
}
