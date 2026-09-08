using UnityEngine;

/// <summary>
/// 진행 중인 미니게임을 강제로 중단시킨다.
///
/// 공포가 최대치에 닿으면 몸이 굳는다(FearingState). 그 순간 볼트를 돌리거나 선을 잡고
/// 있었다면 손을 놓치는 게 맞다. X 로 나가는 것(Cancel)과 달리 진행도까지 되돌려서
/// 처음부터 다시 하게 만든다 - 그래서 Cancel 이 아니라 Abort 다.
///
/// 매니저마다 싱글톤이라 여기서 한 번에 훑는다. 새 미니게임을 추가하면 이 목록에 넣으면 된다.
/// </summary>
public static class MinigameAbort
{
    /// <summary>지금 열려 있는 미니게임이 있는가.</summary>
    public static bool AnyRunning =>
        (BoltRepairManager.Instance != null && BoltRepairManager.Instance.IsRunning) ||
        (TransformerRepairManager.Instance != null && TransformerRepairManager.Instance.IsRunning) ||
        (FuseRepairManager.Instance != null && FuseRepairManager.Instance.IsRunning) ||
        (DoorHackManager.Instance != null && DoorHackManager.Instance.IsRunning) ||
        (FrequencyHackManager.Instance != null && FrequencyHackManager.Instance.IsRunning);

    /// <summary>
    /// 열려 있는 미니게임을 전부 중단하고 진행도를 되돌린다.
    /// 중단한 게 하나라도 있으면 true.
    /// </summary>
    public static bool AbortAll(string notice = null)
    {
        bool any = false;

        if (BoltRepairManager.Instance != null && BoltRepairManager.Instance.IsRunning)
        { BoltRepairManager.Instance.Abort(); any = true; }

        if (TransformerRepairManager.Instance != null && TransformerRepairManager.Instance.IsRunning)
        { TransformerRepairManager.Instance.Abort(); any = true; }

        if (FuseRepairManager.Instance != null && FuseRepairManager.Instance.IsRunning)
        { FuseRepairManager.Instance.Abort(); any = true; }

        if (DoorHackManager.Instance != null && DoorHackManager.Instance.IsRunning)
        { DoorHackManager.Instance.Cancel(); any = true; }   // 타이핑은 열 때마다 처음부터라 별도 되돌림이 없다

        if (FrequencyHackManager.Instance != null && FrequencyHackManager.Instance.IsRunning)
        { FrequencyHackManager.Instance.Cancel(); any = true; }

        if (any && !string.IsNullOrEmpty(notice)) Debug.Log("[MinigameAbort] " + notice);
        return any;
    }
}
