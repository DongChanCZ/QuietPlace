using UnityEngine;

/// <summary>
/// 괴물을 한 지점으로 확실히 불러들인다.
///
/// 소음(NoiseManager)만으로는 "무조건" 을 보장할 수 없다. 벽 하나당 0.6, 층 하나당
/// 0.15 로 세기가 깎여서, 층이 다르거나 벽이 여러 겹이면 아무리 큰 소리도 최종 등급에
/// 못 미친다. 또 이미 같은 등급으로 경계 중인 괴물은 등급이 올라가지 않아 반응하지 않는다
/// (MonsterController 의 Rank 비교). 반드시 달려와야 하는 사건은 이걸로 직접 부른다.
///
/// 소음도 같이 내야 다른 판정(순찰 구역 재배치 등)이 함께 걸리므로, 부르는 쪽에서
/// NoiseManager.Emit 과 이 호출을 나란히 쓴다.
/// </summary>
[DefaultExecutionOrder(-900)]
public class MonsterAlarm : MonoBehaviour
{
    public static MonsterAlarm Instance { get; private set; }

    [Header("불러들일 괴물들")]
    [SerializeField] private MonsterController[] monsters;

    [Header("이 거리 안의 괴물만 부른다(m). 0 이면 전부.")]
    [SerializeField] private float summonRange;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 그 자리로 괴물을 전력으로 보낸다. 경계 등급 비교를 건너뛰므로 반드시 반응한다.
    /// </summary>
    public static void Summon(Vector3 at, float loudness)
    {
        if (Instance == null) return;
        Instance.SummonInternal(at, loudness);
    }

    private void SummonInternal(Vector3 at, float loudness)
    {
        if (monsters == null) return;

        int called = 0;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterController m = monsters[i];
            if (m == null || !m.gameObject.activeInHierarchy) continue;

            // 이미 덮치는 중이거나 무력화된 놈은 건드리지 않는다.
            if (m.CurrentStateType == MonsterStateType.Attack) continue;
            if (m.CurrentStateType == MonsterStateType.Neutralization) continue;

            if (summonRange > 0f && Vector3.Distance(m.transform.position, at) > summonRange) continue;

            // 먼저 소리를 들은 것으로 처리해 목표 지점과 세기를 채우고,
            // 그다음 등급 비교를 건너뛰고 최종 추적으로 밀어넣는다.
            m.OnNoiseHeard(NoiseTier.Final, at, loudness);
            m.GoToDetection(MonsterStateType.FinalDetection);
            called++;
        }

        Debug.Log($"[MonsterAlarm] {at} 로 괴물 {called}마리 호출 (세기 {loudness:0})");
    }
}
