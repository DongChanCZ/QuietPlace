using UnityEngine;
using UnityEngine.Events;

public enum MonsterAlertState { Idle, Patrol, Approach, Chase }

public class MonsterNoiseReactor : MonoBehaviour
{
    [Header("소음 등급 기준 (참고용)")]
    [TextArea(3, 5)]
    public string tierGuide =
        "1 = 걷기\n2 = 문 열기\n3 = 물건 던짐\n4 = (미정)\n5 = 무겁고 파괴적인 소리";

    [Header("현재 상태")]
    public MonsterAlertState currentState = MonsterAlertState.Idle;
    public Vector3 lastNoisePosition;

    public UnityEvent onStop;
    public UnityEvent onPatrol;
    public UnityEvent onApproach;
    public UnityEvent onChase;

    // 어디서든 소음이 발생하면 이 함수를 호출 (예: monsterNoiseReactor.ReportNoise(2, transform.position))
    public void ReportNoise(int tier, Vector3 sourcePosition)
    {
        lastNoisePosition = sourcePosition;
        SetState(DecideReaction(tier));
    }

    MonsterAlertState DecideReaction(int tier)
    {
        if (tier >= 5) return MonsterAlertState.Chase;
        if (tier == 3) return MonsterAlertState.Approach;

        // 경계 등급: 2는 1(대기)과 3(접근) 사이, 4는 3(접근)과 5(추격) 사이에서 50% 확률로 갈림
        if (tier == 2)
        {
            return Random.value < 0.5f ? MonsterAlertState.Idle : MonsterAlertState.Approach;
        }
        if (tier == 4)
        {
            return Random.value < 0.5f ? MonsterAlertState.Approach : MonsterAlertState.Chase;
        }

        // tier 1 이하
        return MonsterAlertState.Idle;
    }

    void SetState(MonsterAlertState state)
    {
        currentState = state;
        switch (state)
        {
            case MonsterAlertState.Idle: onStop?.Invoke(); break;
            case MonsterAlertState.Patrol: onPatrol?.Invoke(); break;
            case MonsterAlertState.Approach: onApproach?.Invoke(); break;
            case MonsterAlertState.Chase: onChase?.Invoke(); break;
        }
    }
}
