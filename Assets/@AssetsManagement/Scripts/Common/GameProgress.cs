using UnityEngine;

/// <summary>진행 단계. 순서대로 하나씩 올라간다.</summary>
public enum GameStep
{
    None = 0,
    Transformer = 1,     // 발전기실 변압기 모듈 설치
    PowerRestored = 2,   // 배전반 퓨즈 교체 + 차단기 올림 -> 건물 전원
    DoorHacked = 3,      // 3층 출입문 키패드 회로 조작
    FrequencySet = 4,    // 주파수 정합 완료 (송출 시작)
    Broadcast = 5,       // 송출 완료
}

/// <summary>
/// 미니게임을 성공한 지점을 세이브포인트로 기억한다.
///
/// 괴물에게 잡히면 GameManager.TriggerGameOver 가 GameScene 을 그대로 다시 로드한다.
/// 씬이 리로드되면 씬 안의 상태는 전부 초기값으로 돌아가므로, 어디까지 했는지는 씬 밖에
/// 남겨둬야 한다. static 만으로도 씬 리로드는 견디지만, 에디터에서 플레이를 멈췄다
/// 다시 켜거나 게임을 껐다 켜도 이어지도록 PlayerPrefs 에도 같이 적는다.
///
/// 되돌아가는 일은 없다 - 더 앞선 단계를 이미 지났으면 뒤 단계 저장은 무시한다.
/// 새 게임을 시작할 때는 MainMenuController 가 Clear 를 부른다.
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

    const string StepKey = "QP_Progress_Step";
    const string PosXKey = "QP_Progress_X";
    const string PosYKey = "QP_Progress_Y";
    const string PosZKey = "QP_Progress_Z";
    const string YawKey = "QP_Progress_Yaw";
    const string HasSpawnKey = "QP_Progress_HasSpawn";

    const string InvValidKey = "QP_Inv_Valid";
    const string InvFlashKey = "QP_Inv_Flashlight";
    const string InvCardKey = "QP_Inv_Keycard";
    const string InvBatteryKey = "QP_Inv_Battery";
    const string InvPickupKey = "QP_Inv_Pickups";
    const string InvSlotTypeKey = "QP_Inv_SlotType";   // + 칸 번호
    const string InvSlotNameKey = "QP_Inv_SlotName";   // + 칸 번호

    /// 지금까지 통과한 마지막 단계.
    public static GameStep Step { get; private set; }

    /// 세이브포인트 위치가 기록돼 있는가.
    public static bool HasSpawn { get; private set; }

    /// 다시 시작할 위치 / 바라볼 방향(Y축 회전).
    public static Vector3 SpawnPosition { get; private set; }
    public static float SpawnYaw { get; private set; }

    /// 복원할 것이 있는가.
    public static bool HasCheckpoint => Step > GameStep.None;

    /// 죽는 순간의 소지품. Valid 가 false 면 되살릴 게 없다.
    public static Inventory Carried { get; private set; }

    /// 진행 단계든 소지품이든 하나라도 되살릴 게 있는가.
    public static bool HasAnythingToRestore => HasCheckpoint || Carried.Valid;

    static bool _loaded;

    /// <summary>PlayerPrefs 에 적힌 값을 한 번만 읽어온다.</summary>
    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        Step = (GameStep)PlayerPrefs.GetInt(StepKey, 0);
        HasSpawn = PlayerPrefs.GetInt(HasSpawnKey, 0) != 0;
        SpawnPosition = new Vector3(
            PlayerPrefs.GetFloat(PosXKey, 0f),
            PlayerPrefs.GetFloat(PosYKey, 0f),
            PlayerPrefs.GetFloat(PosZKey, 0f));
        SpawnYaw = PlayerPrefs.GetFloat(YawKey, 0f);

        var inv = new Inventory
        {
            Valid = PlayerPrefs.GetInt(InvValidKey, 0) != 0,
            HasFlashlight = PlayerPrefs.GetInt(InvFlashKey, 0) != 0,
            HasKeycard = PlayerPrefs.GetInt(InvCardKey, 0) != 0,
            Battery = PlayerPrefs.GetFloat(InvBatteryKey, 100f),
            TakenPickupMask = PlayerPrefs.GetInt(InvPickupKey, 0),
            SlotTypes = new int[SlotCount],
            SlotNames = new string[SlotCount],
        };
        for (int i = 0; i < SlotCount; i++)
        {
            inv.SlotTypes[i] = PlayerPrefs.GetInt(InvSlotTypeKey + i, -1);
            inv.SlotNames[i] = PlayerPrefs.GetString(InvSlotNameKey + i, "");
        }
        Carried = inv;
    }

    /// <summary>
    /// 죽는 순간의 소지품을 남긴다. 진행 단계와 위치는 건드리지 않는다 -
    /// 되살아나는 자리는 마지막 미니게임 성공 지점이고, 손에 든 것만 그대로 가져간다.
    /// </summary>
    public static void SaveInventory(Inventory inv)
    {
        EnsureLoaded();

        inv.Valid = true;
        if (inv.SlotTypes == null || inv.SlotTypes.Length != SlotCount) inv.SlotTypes = new int[SlotCount];
        if (inv.SlotNames == null || inv.SlotNames.Length != SlotCount) inv.SlotNames = new string[SlotCount];
        Carried = inv;

        PlayerPrefs.SetInt(InvValidKey, 1);
        PlayerPrefs.SetInt(InvFlashKey, inv.HasFlashlight ? 1 : 0);
        PlayerPrefs.SetInt(InvCardKey, inv.HasKeycard ? 1 : 0);
        PlayerPrefs.SetFloat(InvBatteryKey, inv.Battery);
        PlayerPrefs.SetInt(InvPickupKey, inv.TakenPickupMask);
        for (int i = 0; i < SlotCount; i++)
        {
            PlayerPrefs.SetInt(InvSlotTypeKey + i, inv.SlotTypes[i]);
            PlayerPrefs.SetString(InvSlotNameKey + i, inv.SlotNames[i] ?? "");
        }
        PlayerPrefs.Save();

        Debug.Log($"[GameProgress] 소지품 저장: 손전등={inv.HasFlashlight} 카드={inv.HasKeycard} " +
                  $"배터리={inv.Battery:0} 부품마스크={inv.TakenPickupMask}");
    }

    /// <summary>
    /// 미니게임을 성공한 지점을 세이브포인트로 남긴다.
    /// player 를 넘기면 그 자리에서 다시 시작하고, 비우면 단계만 기억한다.
    /// </summary>
    public static void Reach(GameStep step, Transform player)
    {
        EnsureLoaded();

        // 이미 더 진행했으면 뒤로 물러나지 않는다.
        if (step <= Step) return;

        Step = step;

        if (player != null)
        {
            HasSpawn = true;
            SpawnPosition = player.position;
            SpawnYaw = player.eulerAngles.y;
        }

        PlayerPrefs.SetInt(StepKey, (int)Step);
        PlayerPrefs.SetInt(HasSpawnKey, HasSpawn ? 1 : 0);
        PlayerPrefs.SetFloat(PosXKey, SpawnPosition.x);
        PlayerPrefs.SetFloat(PosYKey, SpawnPosition.y);
        PlayerPrefs.SetFloat(PosZKey, SpawnPosition.z);
        PlayerPrefs.SetFloat(YawKey, SpawnYaw);
        PlayerPrefs.Save();

        Debug.Log($"[GameProgress] 세이브포인트: {Step} @ {SpawnPosition}");
    }

    /// <summary>새 게임. 기록을 전부 지운다.</summary>
    public static void Clear()
    {
        _loaded = true;
        Step = GameStep.None;
        HasSpawn = false;
        SpawnPosition = Vector3.zero;
        SpawnYaw = 0f;

        PlayerPrefs.DeleteKey(StepKey);
        PlayerPrefs.DeleteKey(HasSpawnKey);
        PlayerPrefs.DeleteKey(PosXKey);
        PlayerPrefs.DeleteKey(PosYKey);
        PlayerPrefs.DeleteKey(PosZKey);
        PlayerPrefs.DeleteKey(YawKey);

        Carried = default(Inventory);
        PlayerPrefs.DeleteKey(InvValidKey);
        PlayerPrefs.DeleteKey(InvFlashKey);
        PlayerPrefs.DeleteKey(InvCardKey);
        PlayerPrefs.DeleteKey(InvBatteryKey);
        PlayerPrefs.DeleteKey(InvPickupKey);
        for (int i = 0; i < SlotCount; i++)
        {
            PlayerPrefs.DeleteKey(InvSlotTypeKey + i);
            PlayerPrefs.DeleteKey(InvSlotNameKey + i);
        }

        PlayerPrefs.Save();
    }
}
