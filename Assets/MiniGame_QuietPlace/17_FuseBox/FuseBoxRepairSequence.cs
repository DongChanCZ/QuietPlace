using UnityEngine;
using UnityEngine.Events;

public class FuseBoxRepairSequence : MonoBehaviour
{
    [Header("1. 차단기 내리기 (E 꾹 누르기)")]
    public GameObject breakerDownRoot;

    /// 옛 연타 방식. 지금은 꾹 누르기(HoldToComplete)를 쓴다. 필드는 호환용으로 남긴다.
    public MashToComplete breakerDown;
    public HoldToComplete breakerDownHold;

    [Header("2. 잔류전하 방전 (R/S/T 스킬체크 3번)")]
    public SkillCheckMinigame dischargeCheck;
    public GameObject dischargeRoot;

    [Header("3. 퓨즈 각도 맞춰 삽입")]
    public FuseAngleInsert fuseInsert;
    public GameObject fuseRoot;

    [Header("4. 차단기 올리기 (E 꾹 누르기)")]
    public GameObject breakerUpRoot;

    public MashToComplete breakerUp;
    public HoldToComplete breakerUpHold;

    public UnityEvent onPowerRestored; // 건물 전원 들어옴(불 켜짐 등)에 연결

    void Start()
    {
        if (breakerDown != null) breakerDown.onComplete.AddListener(OnBreakerDownDone);
        if (breakerDownHold != null) breakerDownHold.onComplete.AddListener(OnBreakerDownDone);
        if (dischargeCheck != null) dischargeCheck.onAllHits.AddListener(OnDischargeDone);
        if (fuseInsert != null) fuseInsert.onInserted.AddListener(OnFuseDone);
        if (breakerUp != null) breakerUp.onComplete.AddListener(OnBreakerUpDone);
        if (breakerUpHold != null) breakerUpHold.onComplete.AddListener(OnBreakerUpDone);

        Begin();
    }

    /// <summary>1단계부터 다시 시작한다. 재시도할 때 Start 가 두 번 돌지 않으므로 외부에서 불러준다.</summary>
    public void Begin()
    {
        // 보통 난이도는 차단기 내리기를 생략하고 방전부터 시작한다.
        SetOnly(MinigameTuning.UseBreakerDownStage ? breakerDownRoot : dischargeRoot);
    }

    void OnBreakerDownDone()
    {
        SetOnly(dischargeRoot);
    }

    void OnDischargeDone()
    {
        SetOnly(fuseRoot);
    }

    void OnFuseDone()
    {
        SetOnly(breakerUpRoot);
    }

    void OnBreakerUpDone()
    {
        SetOnly(null);
        onPowerRestored?.Invoke();
    }

    void SetOnly(GameObject target)
    {
        if (breakerDownRoot != null) breakerDownRoot.SetActive(breakerDownRoot == target);
        if (dischargeRoot != null) dischargeRoot.SetActive(dischargeRoot == target);
        if (fuseRoot != null) fuseRoot.SetActive(fuseRoot == target);
        if (breakerUpRoot != null) breakerUpRoot.SetActive(breakerUpRoot == target);
    }
}
