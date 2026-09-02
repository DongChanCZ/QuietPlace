using UnityEngine;

public readonly struct NoiseEvent
{
    public readonly Vector3 Position;
    public readonly float Loudness;   // 0~1, 소리 자체의 크기 가중치
    public readonly float BaseRange;  // 이 거리 밖이면 벽 유무와 무관하게 안 들림
    public readonly NoiseType Type;

    public NoiseEvent(Vector3 position, float loudness, float baseRange, NoiseType type)
    {
        Position = position;
        Loudness = loudness;
        BaseRange = baseRange;
        Type = type;
    }
}
