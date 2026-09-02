using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)] // 다른 스크립트의 OnEnable이 구독을 시도하기 전에 Instance가 항상 준비되어 있어야 함
public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance { get; private set; }
    public event Action<NoiseEvent> OnNoiseEmitted;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Emit(NoiseEvent evt) => OnNoiseEmitted?.Invoke(evt);
}
