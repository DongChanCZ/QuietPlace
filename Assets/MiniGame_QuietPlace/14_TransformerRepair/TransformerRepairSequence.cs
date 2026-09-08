using System.Collections;
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

    /// 3D 월드에서 선을 끄는 옛 방식. 8개 오브젝트가 모듈에 가려 잡을 수 없어서 쓰지 않는다.
    /// 프리뷰 씬 호환을 위해 필드는 남겨둔다.
    public WirePuzzleManager wireStage;

    /// 화면 UI 방식. 둘 중 채워진 쪽의 완료 이벤트를 듣는다.
    public WirePuzzleUI wireStageUI;

    [Header("단계가 넘어갈 때 성공 문구를 읽을 시간(초)")]
    public float handoffDelay = 0.9f;

    public UnityEvent onTransformerFixed;

    void Start()
    {
        if (boltStage != null) boltStage.onAllBoltsRemoved.AddListener(OnBoltsDone);
        if (moduleBalanceStage != null) moduleBalanceStage.onSeated.AddListener(OnModuleSeated);
        if (moduleBalanceUI != null) moduleBalanceUI.onSeated.AddListener(OnModuleSeated);
        if (wireStage != null) wireStage.onAllWiresConnected.AddListener(OnWiringDone);
        if (wireStageUI != null) wireStageUI.onAllWiresConnected.AddListener(OnWiringDone);

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
        SetOnly(boltStage != null ? boltStage.gameObject : moduleBalanceStageRoot);
    }

    void OnBoltsDone()
    {
        StartCoroutine(Handoff(moduleBalanceStageRoot));
    }

    void OnModuleSeated()
    {
        // 보통 난이도는 색선 연결을 생략한다 - 미니게임이 너무 길어진다.
        if (!MinigameTuning.UseWireStage)
        {
            StartCoroutine(FinishWithoutWires());
            return;
        }

        StartCoroutine(Handoff(wireStageRoot));
    }

    /// 색선 단계를 건너뛰고 바로 수리 완료로 넘긴다.
    IEnumerator FinishWithoutWires()
    {
        if (handoffDelay > 0f) yield return new WaitForSeconds(handoffDelay);
        SetOnly(null);
        onTransformerFixed?.Invoke();
    }

    /// 성공 문구를 잠깐 보여준 뒤 다음 단계만 남긴다.
    ///
    /// 이전에는 다음 단계를 켜기만 하고 끝난 단계를 끄지 않았다. 모듈 안착 UI 가
    /// "모듈이 안착됐다" 상태로 화면을 덮은 채 배선 단계가 그 뒤에서 켜져서,
    /// 플레이어 눈에는 아무 일도 일어나지 않고 멈춘 것처럼 보였다.
    IEnumerator Handoff(GameObject next)
    {
        if (handoffDelay > 0f) yield return new WaitForSeconds(handoffDelay);
        SetOnly(next);
    }

    /// 넘겨준 단계 하나만 남기고 나머지는 끈다. FuseBoxRepairSequence 와 같은 방식이다.
    void SetOnly(GameObject target)
    {
        GameObject boltRoot = boltStage != null ? boltStage.gameObject : null;

        if (boltRoot != null) boltRoot.SetActive(boltRoot == target);
        if (moduleBalanceStageRoot != null) moduleBalanceStageRoot.SetActive(moduleBalanceStageRoot == target);
        if (wireStageRoot != null) wireStageRoot.SetActive(wireStageRoot == target);
    }

    void OnWiringDone()
    {
        onTransformerFixed?.Invoke();
    }
}
