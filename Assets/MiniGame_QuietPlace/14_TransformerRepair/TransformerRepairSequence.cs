using UnityEngine;
using UnityEngine.Events;

public class TransformerRepairSequence : MonoBehaviour
{
    [Header("2단계: 볼트 풀기")]
    public BoltPuzzleManager boltStage;

    [Header("3단계: 모듈 안착 (볼트 다 풀리기 전엔 비활성화)")]
    public GameObject moduleBalanceStageRoot;

    /// 3D 월드에서 큐브를 끄는 옛 방식. 어두운 방에서 카메라 각도에 따라 보이지 않는
    /// 문제가 있어 지금은 쓰지 않는다. 프리뷰 씬 호환을 위해 필드는 남겨둔다.
    public HeavyModuleBalance moduleBalanceStage;

    /// 화면 UI 방식. 둘 중 채워진 쪽의 onSeated 를 듣는다.
    public ModuleBalanceUI moduleBalanceUI;

    [Header("4단계: 배선 연결 (모듈 안착 전엔 비활성화)")]
    public GameObject wireStageRoot;
    public WirePuzzleManager wireStage;

    public UnityEvent onTransformerFixed;

    void Start()
    {
        if (boltStage != null) boltStage.onAllBoltsRemoved.AddListener(OnBoltsDone);
        if (moduleBalanceStage != null) moduleBalanceStage.onSeated.AddListener(OnModuleSeated);
        if (moduleBalanceUI != null) moduleBalanceUI.onSeated.AddListener(OnModuleSeated);
        if (wireStage != null) wireStage.onAllWiresConnected.AddListener(OnWiringDone);

        Begin();
    }

    /// <summary>
    /// 첫 단계부터 다시 시작한다. 재시도할 때 Start 가 두 번 돌지 않으므로 외부에서 불러준다.
    ///
    /// boltStage 가 비어 있으면 볼트를 월드에서 따로 처리하는 구성(BoltedPanel 이 고정판을
    /// 떼는 방식)이므로 모듈 안착부터 시작한다.
    /// </summary>
    public void Begin()
    {
        if (wireStageRoot != null) wireStageRoot.SetActive(false);
        if (moduleBalanceStageRoot != null) moduleBalanceStageRoot.SetActive(boltStage == null);
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
