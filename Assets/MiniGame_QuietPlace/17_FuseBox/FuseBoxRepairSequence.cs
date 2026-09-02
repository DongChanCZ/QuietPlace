using UnityEngine;
using UnityEngine.Events;

public class FuseBoxRepairSequence : MonoBehaviour
{
    [Header("1. 차단기 내리기 (E 연타)")]
    public MashToComplete breakerDown;
    public GameObject breakerDownRoot;

    [Header("2. 잔류전하 방전 (R/S/T 스킬체크 3번)")]
    public SkillCheckMinigame dischargeCheck;
    public GameObject dischargeRoot;

    [Header("3. 퓨즈 각도 맞춰 삽입")]
    public FuseAngleInsert fuseInsert;
    public GameObject fuseRoot;

    [Header("4. 차단기 올리기 (E 연타)")]
    public MashToComplete breakerUp;
    public GameObject breakerUpRoot;

    public UnityEvent onPowerRestored; // 건물 전원 들어옴(불 켜짐 등)에 연결

    void Start()
    {
        if (breakerDown != null) breakerDown.onComplete.AddListener(OnBreakerDownDone);
        if (dischargeCheck != null) dischargeCheck.onAllHits.AddListener(OnDischargeDone);
        if (fuseInsert != null) fuseInsert.onInserted.AddListener(OnFuseDone);
        if (breakerUp != null) breakerUp.onComplete.AddListener(OnBreakerUpDone);

        SetOnly(breakerDownRoot);
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
