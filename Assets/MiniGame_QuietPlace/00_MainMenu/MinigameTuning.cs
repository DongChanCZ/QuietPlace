using UnityEngine;

/// <summary>
/// 난이도별 미니게임 설정을 한 곳에 모아둔다.
///
/// 보통(Normal)은 전체 진행 시간을 줄인 구성이고, 어려움(Expert)은 줄이기 전 원래 구성이다.
/// 값이 컴포넌트마다 흩어져 있으면 난이도를 하나 바꿀 때마다 프리팹 여러 개를 뒤져야 하므로
/// 여기서만 정하고 각 미니게임이 시작할 때 읽어간다.
///
/// 쉬움(Easy)은 보통보다 더 느슨하게 둔다.
/// </summary>
public static class MinigameTuning
{
    /// 인스펙터에서 난이도를 강제해 시험할 때 쓴다. Expert 로 켜두면 옵션과 무관하게 어려움이 된다.
    public static bool ForceOverride;
    public static Difficulty Override = Difficulty.Normal;

    public static Difficulty Current => ForceOverride ? Override : GameSettings.Difficulty;

    public static bool IsHard => Current == Difficulty.Expert;
    public static bool IsEasy => Current == Difficulty.Easy;

    public static string Label => IsHard ? "어려움" : IsEasy ? "쉬움" : "보통";

    // ---------------------------------------------------------------- 변압기 (발전기실)

    /// 몽키스패너로 볼트를 몇 바퀴 돌려야 하는가.
    public static int BoltTurns => IsHard ? 3 : 2;

    /// 볼트 한 바퀴로 셀 각도. 어려움은 더 많이 돌려야 한다.
    public static float BoltDegreesPerTurn => IsHard ? 200f : 150f;

    /// 모듈을 슬롯에서 버텨야 하는 시간(초).
    public static float BalanceHoldSeconds => IsHard ? 3f : 1.5f;

    /// 모듈 안착 허용 반경(px). 넓으면 쉽다.
    public static float BalanceTolerance => IsHard ? 40f : 55f;

    /// 색선 4개 연결 단계를 쓰는가. 보통에서는 생략한다.
    public static bool UseWireStage => IsHard;

    // ---------------------------------------------------------------- 퓨즈 (배전반실)

    /// 잔류전하 방전을 몇 번 맞춰야 하는가.
    public static int DischargeHits => IsHard ? 3 : 2;

    /// 방전에 쓰는 키. 어려움은 상마다 다른 키(R/S/T), 보통은 스페이스바만.
    public static UnityEngine.InputSystem.Key[] DischargeKeys => IsHard
        ? new[] { UnityEngine.InputSystem.Key.R, UnityEngine.InputSystem.Key.S, UnityEngine.InputSystem.Key.T }
        : new[] { UnityEngine.InputSystem.Key.Space, UnityEngine.InputSystem.Key.Space };

    /// 바늘 회전 속도(도/초).
    public static float DischargeSpeed => IsHard ? 330f : 240f;

    /// 맞춰야 하는 구간의 폭(도).
    public static float DischargeZoneWidth => IsHard ? 26f : 40f;

    /// 차단기 내리기 단계를 쓰는가. 보통에서는 올리기만 남긴다.
    public static bool UseBreakerDownStage => IsHard;

    // ---------------------------------------------------------------- 문 키패드 (빨리치기)

    public static string DoorPassword => IsHard ? "NCAIVibeCodingBusan" : "NCAIBusan";

    /// 어려움만 대소문자를 구분한다(대문자는 Shift).
    public static bool DoorCaseSensitive => IsHard;

    public static float DoorFallSpeed => IsHard ? 700f : IsEasy ? 180f : 260f;
    public static float DoorFallRamp => IsHard ? 26f : 0f;

    // ---------------------------------------------------------------- 3층 컴퓨터

    /// 비밀번호 단계를 쓰는가. 보통에서는 바로 주파수 맞추기로 간다.
    public static bool UsePasswordStage => IsHard;

    /// 주파수 송출 대기 시간(초).
    public static float BroadcastSeconds => IsHard ? 45f : 30f;

    // ---------------------------------------------------------------- 로그

    public static string Summary()
    {
        return $"[{Label}] 볼트 {BoltTurns}바퀴 / 안착 {BalanceHoldSeconds}초 / 색선 {(UseWireStage ? "있음" : "없음")}"
             + $" | 방전 {DischargeHits}번({(IsHard ? "R/S/T" : "Space")}) / 차단기 내리기 {(UseBreakerDownStage ? "있음" : "없음")}"
             + $" | 키패드 \"{DoorPassword}\" 대소문자 {(DoorCaseSensitive ? "구분" : "무시")}"
             + $" | 컴퓨터 비밀번호 {(UsePasswordStage ? "있음" : "없음")} / 송출 {BroadcastSeconds}초";
    }
}
