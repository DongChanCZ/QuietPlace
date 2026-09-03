using UnityEngine;
using UnityEngine.Events;

public class TransformerRepairSequence : MonoBehaviour
{
    [Header("2단계: 볼트 풀기")]
    public BoltPuzzleManager boltStage;

    [Header("3단계: 모듈 안착 (볼트 다 풀리기 전엔 비활성화)")]
    public GameObject moduleBalanceStageRoot;
    public HeavyModuleBalance moduleBalanceStage;

    [Header("4단계: 배선 연결 (모듈 안착 전엔 비활성화)")]
    public GameObject wireStageRoot;
    public WirePuzzleManager wireStage;

    public UnityEvent onTransformerFixed;

    void Start()
    {
        if (moduleBalanceStageRoot != null) moduleBalanceStageRoot.SetActive(false);
        if (wireStageRoot != null) wireStageRoot.SetActive(false);

        if (boltStage != null) boltStage.onAllBoltsRemoved.AddListener(OnBoltsDone);
        if (moduleBalanceStage != null) moduleBalanceStage.onSeated.AddListener(OnModuleSeated);
        if (wireStage != null) wireStage.onAllWiresConnected.AddListener(OnWiringDone);
    }

    void OnBoltsDone()
    {
        if (moduleBalanceStageRoot != null) moduleBalanceStageRoot.SetActive(true);
    }

    void OnModuleSeated()
    {
        if (wireStageRoot != null) wireStageRoot.SetActive(true);
    }

    void OnWiringDone()
    {
        onTransformerFixed?.Invoke();
    }
}
