using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 세이브포인트를 씬에 되살린다.
///
/// GameProgress 는 "어디까지 했는가"만 기억한다. 그 단계까지의 결과를 실제 씬에 다시
/// 만들어주는 게 이 컴포넌트다. 저장 파일에 씬 상태를 통째로 담는 대신, 이미 있는
/// 성공 처리 메서드들을 순서대로 다시 불러 같은 결과를 만든다 - 미니게임이 바뀌어도
/// 복원 쪽을 따로 고칠 일이 없다.
///
/// 참조는 전부 Inspector 로 꽂는다(에디터 스크립트가 자동으로 꽂아준다).
/// 런타임에 Find 로 찾지 않는다.
/// </summary>
[DefaultExecutionOrder(500)]   // 다른 것들이 자리를 잡은 뒤에 덮어쓴다
public class GameCheckpoint : MonoBehaviour
{
    public static GameCheckpoint Instance { get; private set; }

    [Header("플레이어")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteractor interactor;

    // 주의: 오브젝트 이름과 실제 역할이 반대다. PROP_2F_Switchboard 가 변압기 슬롯이고
    // PROP_2F_Generator 가 퓨즈 슬롯이다. 이름 대신 역할로 이름을 붙여 둔다.
    [Header("1단계 - 변압기 모듈")]
    [SerializeField] private BoltedPanel bayCover;      // 볼트로 잠긴 고정판
    [SerializeField] private FuseSlot transformerSlot;  // 변압기 자리 (고장난 구 변압기가 박혀 있다)

    [Header("2단계 - 퓨즈 + 건물 전원")]
    [SerializeField] private FuseSlot fuseSlot;         // 퓨즈 자리

    [Header("3단계 - 3층 출입문")]
    [SerializeField] private KeypadTerminal keypad;

    [Header("4~5단계 - 방송 콘솔")]
    [SerializeField] private ComputerTerminal computer;

    [Header("월드에 놓인 손전등 - 되살릴 때 같이 치운다")]
    [SerializeField] private FlashlightPickup worldFlashlight;

    [Header("죽어도 들고 가는 부품들 - 순서가 저장 키다. 순서를 바꾸면 지난 기록이 어긋난다")]
    [SerializeField] private FusePickup[] trackedPickups;

    // 주머니에 넣는 테스트 아이템(약/유리병/카드 등). 종류 단위로만 가르므로(QuickSlotManager.HasItem)
    // 순서는 상관없다 - 슬롯에 그 종류가 있으면 같은 종류의 월드 오브젝트를 전부 치운다.
    [Header("주머니 아이템 (약/유리병/카드 등) - 슬롯에 있으면 월드에서도 사라져 있어야 한다")]
    [SerializeField] private TestItemPickup[] trackedItemPickups;

    [Header("되살아난 직후 청각을 잠시 막을 괴물들")]
    [SerializeField] private MonsterController[] monsters;

    [Header("되살아난 직후 몇 초 동안 소리를 못 듣게 할지")]
    [SerializeField] private float respawnGraceSeconds = 5f;

    [Header("복원 후 안내문을 띄울지")]
    [SerializeField] private bool showNotice = true;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // 세이브포인트가 없으면 되살릴 것이 없다. 소지품이 남아 있어도 마찬가지다 -
        // 첫 세이브 전에 죽었다면 처음부터, 빈손으로 시작해야 한다.
        if (!GameProgress.HasAnythingToRestore) return;
        StartCoroutine(RestoreNextFrame());
    }

    // 한 프레임 기다린다. BuildingPower 의 조명 수집이나 BreakerLever 의 초기 자세처럼
    // 다른 Start 에서 정해지는 것들을 덮어쓰려면 그것들이 먼저 끝나 있어야 한다.
    private IEnumerator RestoreNextFrame()
    {
        yield return null;

        GameStep step = GameProgress.Step;

        if (step >= GameStep.Transformer) RestoreTransformer();
        if (step >= GameStep.PowerRestored) RestorePower();
        else if (GameProgress.FuseOnly) RestoreFuseOnly();   // 퓨즈만 먼저 갈아둔 경우
        if (step >= GameStep.DoorHacked) RestoreDoor();

        // 주파수 정합은 세이브하지 않는다(FrequencyHackManager 참고). 송출까지 끝낸
        // 뒤에 죽은 경우만 되살린다.
        if (step >= GameStep.Broadcast) RestoreBroadcastDone();

        MovePlayerToCheckpoint();
        RestoreInventory();

        // 되살아난 자리 바로 옆에 괴물이 있으면, 상태가 초기화돼도 발소리 한 번에
        // 다시 들켜서 "죽었는데 또 붙어 있다"가 된다. 잠깐 귀를 막아 도망칠 틈을 준다.
        StartCoroutine(RespawnGrace());

        if (showNotice && interactor != null)
            interactor.ShowNotice("마지막으로 성공한 지점에서 다시 시작한다.");

        Debug.Log($"[GameCheckpoint] {step} 까지 복원했다.");
    }

    // ---------------------------------------------------------------- 소지품

    /// <summary>
    /// 죽는 순간 들고 있던 것을 기억한다. GameManager.TriggerGameOver 가 씬을
    /// 다시 불러오기 직전에 부른다.
    /// </summary>
    public static void CaptureOnDeath()
    {
        if (Instance == null) return;
        Instance.Capture();
    }

    private void Capture()
    {
        var inv = new GameProgress.Inventory
        {
            SlotTypes = new int[6],
            SlotNames = new string[6],
        };

        if (interactor != null)
        {
            inv.HasFlashlight = interactor.HasFlashlight;
            inv.HasKeycard = interactor.HasKeycard;
            inv.Battery = interactor.BatteryLevel;
        }

        QuickSlotManager slots = QuickSlotManager.Instance;
        for (int i = 0; i < inv.SlotTypes.Length; i++)
        {
            inv.SlotTypes[i] = -1;
            inv.SlotNames[i] = "";

            if (slots == null) continue;
            ItemType item;
            if (!slots.TryGetItem(i, out item)) continue;
            inv.SlotTypes[i] = (int)item;
        }

        // 챙겨둔 부품. 이미 꽂아버린 것은 진행 단계가 되살리므로 여기서 제외한다.
        if (trackedPickups != null)
        {
            for (int i = 0; i < trackedPickups.Length && i < 32; i++)
            {
                FusePickup p = trackedPickups[i];
                if (p == null || !p.Taken || p.Installed) continue;
                inv.TakenPickupMask |= 1 << i;
            }
        }

        GameProgress.SaveInventory(inv);
    }

    private void RestoreInventory()
    {
        GameProgress.Inventory inv = GameProgress.Carried;
        if (!inv.Valid) return;

        // 퀵슬롯부터, 죽기 전 있던 자리 그대로 복원한다. 아래 손전등/부품 복원이 각자
        // AddItem 을 다시 부르더라도 QuickSlotManager 가 이미 있는 종류는 알아서
        // 건너뛰므로 순서와 상관없이 안전하다 - 대신 이걸 먼저 해야 "빈 자리 아무데나"가
        // 아니라 정확한 인덱스에 꽂힌다(RestoreSlot 은 AddItem 과 달리 인덱스를 그대로 쓴다).
        QuickSlotManager slots = QuickSlotManager.Instance;
        if (slots != null && inv.SlotTypes != null)
        {
            for (int i = 0; i < inv.SlotTypes.Length; i++)
            {
                if (inv.SlotTypes[i] < 0) continue;
                slots.RestoreSlot(i, (ItemType)inv.SlotTypes[i]);
            }
        }

        // 손전등 - 월드에 놓인 것도 같이 치운다. 안 그러면 하나를 두 번 줍는다.
        if (inv.HasFlashlight && interactor != null && !interactor.HasFlashlight)
        {
            if (worldFlashlight != null) worldFlashlight.Collect(interactor);   // 안에서 GiveFlashlight 까지 한다
            else interactor.GiveFlashlight();
        }

        if (inv.HasKeycard && interactor != null) interactor.GiveKeycard();
        if (interactor != null) interactor.batteryLevel = inv.Battery;

        HideTakenWorldItems(inv.SlotTypes);

        // 챙겨둔 부품을 다시 챙긴 상태로 만든다.
        if (trackedPickups == null) return;

        for (int i = 0; i < trackedPickups.Length && i < 32; i++)
        {
            if ((inv.TakenPickupMask & (1 << i)) == 0) continue;

            FusePickup p = trackedPickups[i];
            if (p == null || p.Taken || p.Installed) continue;

            // 주머니에 넣는 물건은 조용히 치우고, 두 손으로 드는 물건은 실제로 들려준다.
            // AllowedWhileHandsFull 이 두 손 물건에서만 false 라 그것으로 가른다.
            //
            // 주머니 물건은 Interact 가 아니라 ForceConsumed 를 쓴다 - 슬롯은 위 SlotTypes
            // 루프가 이미 복원해서 QuickSlotManager.HasItem 이 true 인 상태라, Interact 를
            // 부르면 "이미 들고 있음" 가드에 막혀 정작 이 오브젝트(월드에 남은 원본)를
            // 숨기는 쪽은 실행되지 않는다 - 슬롯엔 있는데 월드에도 그대로 남는 버그가 된다.
            bool twoHanded = !p.AllowedWhileHandsFull;
            if (twoHanded) p.Interact(interactor);
            else p.ForceConsumed();
        }
    }

    /// QuickSlotManager 는 표시 이름을 들고 있지 않고 획득 알림에만 쓴다.
    /// 되살릴 때도 한글 이름이 뜨도록 여기서 붙여준다.
    private static string KoreanName(ItemType item)
    {
        switch (item)
        {
            case ItemType.Flashlight: return "손전등";
            case ItemType.Brick: return "벽돌";
            case ItemType.Wrench: return "몽키스패너";
            case ItemType.Fuse: return "퓨즈";
            case ItemType.Bottle: return "병";
            case ItemType.Medicine: return "진정제";
            default: return item.ToString();
        }
    }

    /// <summary>
    /// 슬롯에 남아 있던 종류는 월드의 같은 종류 오브젝트에서도 사라져 있어야 한다.
    /// 안 그러면 씬이 리로드될 때 오브젝트는 저장된 그대로(활성) 돌아오는데 슬롯에는
    /// 이미 들어있어서, 이미 챙긴 물건이 월드에 그대로 남아있는 것처럼 보인다.
    /// </summary>
    private void HideTakenWorldItems(int[] slotTypes)
    {
        if (trackedItemPickups == null || slotTypes == null) return;

        for (int i = 0; i < slotTypes.Length; i++)
        {
            if (slotTypes[i] < 0) continue;
            ItemType item = (ItemType)slotTypes[i];

            for (int j = 0; j < trackedItemPickups.Length; j++)
            {
                TestItemPickup pickup = trackedItemPickups[j];
                if (pickup != null && pickup.ItemType == item) pickup.gameObject.SetActive(false);
            }
        }
    }

    // ---------------------------------------------------------------- 부활 유예

    private IEnumerator RespawnGrace()
    {
        if (respawnGraceSeconds <= 0f || monsters == null) yield break;

        var muted = new List<MonsterHearing>();

        foreach (MonsterController m in monsters)
        {
            if (m == null) continue;

            // 씬을 다시 불러오면 상태는 이미 순찰로 돌아가 있다. 그래도 명시해 둔다 -
            // 나중에 괴물이 씬을 넘어 살아남게 바뀌어도 이 줄이 지켜준다.
            m.GoToIdleFrom(MonsterStateType.FinalDetection);

            MonsterHearing ear = m.GetComponent<MonsterHearing>();
            if (ear == null || !ear.enabled) continue;

            ear.enabled = false;   // OnDisable 에서 소음 구독을 끊는다
            muted.Add(ear);
        }

        if (muted.Count == 0) yield break;

        Debug.Log($"[GameCheckpoint] 부활 유예 {respawnGraceSeconds:0.#}초 - 괴물 {muted.Count}마리 청각 정지");
        yield return new WaitForSeconds(respawnGraceSeconds);

        for (int i = 0; i < muted.Count; i++)
            if (muted[i] != null) muted[i].enabled = true;   // OnEnable 에서 다시 구독한다

        Debug.Log("[GameCheckpoint] 부활 유예 끝 - 괴물 청각 복구");
    }

    // ---------------------------------------------------------------- 단계별 복원

    private void RestoreTransformer()
    {
        // 볼트를 다 풀어 고정판을 떼어낸 상태로 만든다.
        if (bayCover != null) bayCover.CompleteOpen(null);

        if (transformerSlot != null)
        {
            transformerSlot.ForceUnblock();   // 고장난 변압기를 들어냈다
            transformerSlot.ForceInstall();   // 새 변압기를 꽂았다
        }

        if (BuildingPower.Instance != null) BuildingPower.Instance.MarkTransformerFixed();
    }

    private void RestorePower()
    {
        if (fuseSlot != null) fuseSlot.ForceInstall();

        // 변압기까지 끝나 있어야 전원이 들어온다. 위에서 이미 표시했다.
        // MarkFuseInstalled 안의 TryRestore 가 차단기 레버까지 올려준다.
        if (BuildingPower.Instance != null) BuildingPower.Instance.MarkFuseInstalled();
    }

    /// <summary>
    /// 발전기실보다 퓨즈를 먼저 끝냈던 경우. 퓨즈만 꽂아 두고 전원은 넣지 않는다 -
    /// 변압기가 아직이라 BuildingPower 가 스스로 판단해 불을 켜지 않는다.
    /// </summary>
    private void RestoreFuseOnly()
    {
        if (fuseSlot != null) fuseSlot.ForceInstall();
        if (BuildingPower.Instance != null) BuildingPower.Instance.MarkFuseInstalled();
    }

    private void RestoreDoor()
    {
        if (keypad != null) keypad.OnHacked();   // 잠금 해제 + 문 열기
    }

    private void RestoreBroadcastDone()
    {
        if (computer == null) return;

        computer.MarkPasswordSolved();
        computer.OnFrequencySet(null);   // 주파수도 맞춰 놓은 상태여야 콘솔 안내문이 맞다
        computer.OnSolved(null);
    }

    private void MovePlayerToCheckpoint()
    {
        if (!GameProgress.HasSpawn || player == null) return;

        player.TeleportTo(GameProgress.SpawnPosition, Quaternion.Euler(0f, GameProgress.SpawnYaw, 0f));
    }

    // ---------------------------------------------------------------- 저장

    /// <summary>
    /// 미니게임 성공 지점을 세이브포인트로 남긴다. 각 미니게임 매니저가 부른다.
    /// 씬에 이 컴포넌트가 없어도(프리뷰 씬 등) 단계는 남도록 static 으로 둔다.
    /// </summary>
    public static void Save(GameStep step, IInteractor who)
    {
        Transform at = (who as MonoBehaviour) != null ? (who as MonoBehaviour).transform : null;
        if (at == null && Instance != null && Instance.player != null) at = Instance.player.transform;

        GameProgress.Reach(step, at);

        if (who != null) who.ShowNotice("◆ 기록됨 — 죽어도 이 지점부터 다시 시작한다.");
    }
}
