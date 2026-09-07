using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 판을 고정한 볼트들을 관리한다.
///
/// 기본 동작은 "볼트 하나를 다 풀면 나머지도 함께 풀린다" 다. 볼트마다 3번씩 돌리게 하면
/// 같은 조작을 여러 번 반복하는 지루한 작업이 되므로, 첫 볼트만 플레이어가 풀고
/// 나머지는 약간의 간격을 두고 따라 빠지게 해서 판이 풀려나가는 느낌을 준다.
/// </summary>
public class BoltPuzzleManager : MonoBehaviour
{
    public BoltUnscrew[] bolts;

    [Header("첫 볼트가 빠지면 나머지도 따라 빠진다")]
    public bool chainRemainingBolts = true;

    [Header("따라 빠질 때 볼트 사이 간격(초) - 0이면 전부 동시에")]
    public float chainInterval = 0.12f;

    public UnityEvent onAllBoltsRemoved;

    private int removedCount;
    private bool chaining;
    private bool finished;

    void Start()
    {
        foreach (BoltUnscrew bolt in bolts)
        {
            if (bolt != null) bolt.onRemoved.AddListener(OnBoltRemoved);
        }
    }

    void OnBoltRemoved()
    {
        removedCount++;

        // 첫 볼트가 빠진 순간 나머지를 연쇄로 풀기 시작한다.
        // 연쇄 중에 들어오는 onRemoved 는 카운트만 올리고 다시 시작하지 않는다.
        if (chainRemainingBolts && !chaining && removedCount < bolts.Length)
        {
            chaining = true;
            StartCoroutine(ChainRemaining());
            return;
        }

        TryFinish();
    }

    private IEnumerator ChainRemaining()
    {
        foreach (BoltUnscrew bolt in bolts)
        {
            if (bolt == null || bolt.Removed) continue;

            if (chainInterval > 0f) yield return new WaitForSeconds(chainInterval);
            bolt.ForcePopOut(); // onRemoved 를 발생시켜 removedCount 가 올라간다
        }

        TryFinish();
    }

    private void TryFinish()
    {
        if (finished) return;
        if (removedCount < bolts.Length) return;

        finished = true;
        onAllBoltsRemoved?.Invoke();
    }
}
