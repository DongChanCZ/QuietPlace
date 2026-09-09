using UnityEngine;

/// <summary>진행 단계. 순서대로 하나씩 올라간다.</summary>
public enum GameStep
{
    None = 0,
    Transformer = 1,     // 발전기실 변압기 모듈 설치
    PowerRestored = 2,   // 배전반 퓨즈 교체 + 차단기 올림 -> 건물 전원
    DoorHacked = 3,      // 3층 출입문 키패드 회로 조작
    FrequencySet = 4,    // 주파수 정합 완료 - 세이브포인트로 잡지 않는다(FrequencyHackManager 참고)
    Broadcast = 5,       // 송출 완료
}

/// <summary>
/// 미니게임을 성공한 지점을 세이브포인트로 기억한다.
///
/// 괴물에게 잡히면 GameManager.TriggerGameOver 가 GameScene 을 그대로 다시 로드한다.
/// 씬이 리로드되면 씬 안의 상태는 전부 초기값으로 돌아가므로, 어디까지 했는지는 씬 밖에
/// 남겨둬야 한다.
///
/// 이 기록은 "이번 실행 중에 죽었을 때 되살아나는 자리"일 뿐이다. 이어하기 기능은 없다.
/// 그래서 디스크(PlayerPrefs)에 적지 않고 static 으로만 들고 있는다 - static 은 씬
/// 리로드는 견디고 프로세스가 끝나면 사라지므로 딱 필요한 만큼만 살아남는다.
/// 예전에는 PlayerPrefs 에 적었는데, 그러면 게임을 껐다 켜거나 에디터에서 플레이를
/// 멈췄다 다시 켜도 지난 판의 진행이 그대로 복원돼 "처음부터 하는데 조명이 켜져 있고
/// 오브젝트가 이미 작동해 있다" 가 됐다.
///
/// 같은 실행 안에서 타이틀로 돌아가 다시 시작하는 경우는 static 이 살아 있으므로
/// MainMenuController 가 Clear 를 부른다.
/// </summary>
public static class GameProgress
{
    /// <summary>
    /// 죽는 순간 들고 있던 것들. 진행 단계와 따로 둔다 - 되살아나는 자리는 마지막
    /// 미니게임 성공 지점이지만, 소지품은 죽은 순간 그대로여야 하기 때문이다.
    /// </summary>
    public struct Inventory
    {
        public bool Valid;
        public bool HasFlashlight;
        public bool HasKeycard;
        public float Battery;

        /// 퀵슬롯 6칸. -1 은 빈 칸.
        public int[] SlotTypes;
        public string[] SlotNames;

        /// 챙겨둔 부품들. GameCheckpoint.trackedPickups 순서대로 한 비트씩.
        public int TakenPickupMask;
    }

    const int SlotCount = 6;

    /// 지금까지 통과한 마지막 단계.
    public static GameStep Step { get; private set; }

    /// 세이브포인트 위치가 기록돼 있는가.
    public static bool HasSpawn { get; private set; }

    /// 변압기보다 퓨즈를 먼저 끝낸 경우. 단계는 아직 PowerRestored 가 아니지만
    /// 퓨즈는 꽂혀 있어야 한다 - 단계 하나로는 표현할 수 없어 따로 둔다.
    public static bool FuseOnly { get; private set; }

    /// 다시 시작할 위치 / 바라볼 방향(Y축 회전).
    public static Vector3 SpawnPosition { get; private set; }
    public static float SpawnYaw { get; private set; }

    /// 복원할 것이 있는가.
    public static bool HasCheckpoint => Step > GameStep.None;

    /// 죽는 순간의 소지품. Valid 가 false 면 되살릴 게 없다.
    public static Inventory Carried { get; private set; }

    /// 진행 단계든 소지품이든 하나라도 되살릴 게 있는가.
    public static bool HasAnythingToRestore => HasCheckpoint || FuseOnly || Carried.Valid;

    /// <summary>
    /// 죽는 순간의 소지품을 남긴다. 진행 단계와 위치는 건드리지 않는다 -
    /// 되살아나는 자리는 마지막 미니게임 성공 지점이고, 손에 든 것만 그대로 가져간다.
    /// </summary>
    public static void SaveInventory(Inventory inv)
    {
        inv.Valid = true;
        if (inv.SlotTypes == null || inv.SlotTypes.Length != SlotCount) inv.SlotTypes = new int[SlotCount];
        if (inv.SlotNames == null || inv.SlotNames.Length != SlotCount) inv.SlotNames = new string[SlotCount];
        Carried = inv;

        Debug.Log($"[GameProgress] 소지품 저장: 손전등={inv.HasFlashlight} 카드={inv.HasKeycard} " +
                  $"배터리={inv.Battery:0} 부품마스크={inv.TakenPickupMask}");
    }

    /// <summary>
    /// 미니게임을 성공한 지점을 세이브포인트로 남긴다.
    /// player 를 넘기면 그 자리에서 다시 시작하고, 비우면 단계만 기억한다.
    /// </summary>
    public static void Reach(GameStep step, Transform player)
    {
        // 이미 더 진행했으면 뒤로 물러나지 않는다.
        if (step <= Step) return;

        Step = step;

        if (player != null)
        {
            HasSpawn = true;
            SpawnPosition = player.position;
            SpawnYaw = player.eulerAngles.y;
        }

        Debug.Log($"[GameProgress] 세이브포인트: {Step} @ {SpawnPosition}");
    }

    /// <summary>
    /// 발전기실보다 배전반실 퓨즈를 먼저 끝낸 경우. 아직 전원은 안 들어왔으므로 단계를
    /// PowerRestored 로 올릴 수 없다(그러면 복원할 때 변압기까지 공짜로 끝난 것이 된다).
    /// 퓨즈만 꽂혔다는 사실을 따로 남기고, 다시 시작할 자리도 여기로 옮긴다.
    /// </summary>
    public static void MarkFuseOnly(Transform player)
    {
        // 이미 전원까지 들어와 있으면 이 표시는 의미가 없다.
        if (Step >= GameStep.PowerRestored) return;

        FuseOnly = true;

        if (player != null)
        {
            HasSpawn = true;
            SpawnPosition = player.position;
            SpawnYaw = player.eulerAngles.y;
        }

        Debug.Log($"[GameProgress] 세이브포인트: 퓨즈만 교체 @ {SpawnPosition}");
    }

    /// <summary>새 게임. 기록을 전부 지운다.</summary>
    public static void Clear()
    {
        Reset();
        Debug.Log("[GameProgress] 기록을 지웠다 - 처음부터 시작한다.");
    }

    /// <summary>
    /// 게임을 켤 때(에디터에서 플레이를 누를 때도) 무조건 한 번 비운다.
    ///
    /// static 은 보통 도메인 리로드로 초기화되지만, 프로젝트 설정에서 "Reload Domain" 을
    /// 끄면 플레이를 멈췄다 다시 켜도 값이 남는다. 그러면 처음부터 시작해도 지난 판의
    /// 진행이 복원돼버리므로, 설정에 기대지 않고 여기서 직접 비운다.
    ///
    /// 씬을 다시 불러올 때(죽어서 리스폰)는 다시 호출되지 않으므로 체크포인트는 살아남는다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnLaunch()
    {
        Reset();
    }

    private static void Reset()
    {
        Step = GameStep.None;
        HasSpawn = false;
        FuseOnly = false;
        SpawnPosition = Vector3.zero;
        SpawnYaw = 0f;
        Carried = default(Inventory);
    }
}
