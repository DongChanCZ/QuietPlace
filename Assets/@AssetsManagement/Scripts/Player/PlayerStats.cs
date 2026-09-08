using UnityEngine;

[System.Serializable]
public class PlayerStats
{
    [Header("Fear")]
    public float fear = 0f;
    public float fearMax = 100f;
    public float fearIncreaseRate = 1f;

    [Header("Oxygen")]
    public float oxygen = 100f;
    public float oxygenMax = 100f;
    public float oxygenDrainRate = 10f;
    public float oxygenRegenRate = 10f;
    public float gaspingThreshold = 50f;
    public float criticalOxygenThreshold = 15f;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sittingMoveSpeed = 2f;

    [Header("Noise")]
    public float noiseLevelBase = 2f;
    public float noiseRangeBase = 2f;
    public float noiseTierStep = 1f;
    public float noiseMaxTier = 8f;

    [System.NonSerialized] public float currentNoiseLevel;
    [System.NonSerialized] public float currentNoiseRange;
    [System.NonSerialized] public float currentMoveSpeed;

    public bool IsFearMaxed => fear >= fearMax;

    public void TickFear(float dt)
    {
        fear = Mathf.Min(fear + fearIncreaseRate * dt, fearMax);
    }

    public void UseSedative()
    {
        fear = 0f;
    }

    public void DrainOxygen(float dt)
    {
        oxygen = Mathf.Max(0f, oxygen - oxygenDrainRate * dt);
    }

    public void RegenOxygen(float dt)
    {
        oxygen = Mathf.Min(oxygenMax, oxygen + oxygenRegenRate * dt);
    }

    public float ClampNoiseTier(float value)
    {
        return Mathf.Clamp(value, 0f, noiseMaxTier);
    }

    public float ActiveNoiseRangeFor(PlayerStateType type)
    {
        switch (type)
        {
            case PlayerStateType.Walking: return ClampNoiseTier(noiseRangeBase + noiseTierStep + 1f);
            case PlayerStateType.Sitting: return ClampNoiseTier(noiseRangeBase - noiseTierStep);
            default: return noiseRangeBase; // Idle
        }
    }
}
