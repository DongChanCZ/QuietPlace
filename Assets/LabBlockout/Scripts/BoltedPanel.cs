using UnityEngine;

/// The bolted plate over the switchboard's transformer bay.
///
/// It exists so the wrench has a reason to be findable. Without it the
/// transformer goes straight into an open hole and the toolbox in the corner is
/// scenery. The whole holder - plate and bolts - is switched off in one go, so
/// the collider that catches the player's look disappears with the geometry
/// instead of leaving an invisible panel to bump into.
///
/// 볼트는 한 번에 풀리지 않는다. 몽키스패너로 requiredTurns 번 돌려야 하고, 돌릴 때마다
/// 볼트 메시가 회전하며 조금씩 빠져나온다. 마지막 회전에서 홀더 전체가 사라지므로
/// "하나를 다 풀면 나머지도 같이 풀리는" 느낌이 된다.
[DisallowMultipleComponent]
public sealed class BoltedPanel : Interactable
{
    [SerializeField] FusePickup tool;
    [SerializeField] string displayName = "고정판";
    [SerializeField] string toolName = "몽키스패너";

    [Header("몇 번 돌려야 풀리는가")]
    [SerializeField] int requiredTurns = 3;

    [Header("돌릴 때 회전시킬 볼트 메시 (비워도 동작한다)")]
    [SerializeField] Transform boltToTurn;

    /// 로컬 Z축 회전. 이 프로젝트의 규칙대로 + 값이 시계 반대 방향(= 풀리는 방향)이다.
    /// 볼트 메시의 축이 반대로 잡혀 있어 화면에서 거꾸로 돌면 이 값을 음수로 바꾸면 된다.
    [SerializeField] float rotationPerTurn = 120f;

    /// 로컬 -Z 로 이동해 카메라 쪽으로 빠져나온다(원래 BoltUnscrew 와 같은 부호 규칙).
    [SerializeField] float popOutPerTurn = 0.012f;

    [Header("사운드")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip turnSound;
    [SerializeField] AudioClip openSound;

    public bool Open { get; private set; }

    /// 지금까지 돌린 횟수. 미니게임이 중간에 취소되면 여기까지가 남는다.
    public int Turns => turns;

    /// 총 몇 바퀴가 필요한가. 미니게임이 남은 바퀴를 계산할 때 쓴다.
    public int RequiredTurns => requiredTurns;

    /// 몽키스패너를 챙겼는가.
    public bool HasTool => tool != null && tool.Taken;

    private int turns;

    void Awake()
    {
        Debug.Assert(tool, "BoltedPanel is missing the tool it waits for.");
    }

    public override string Prompt(IInteractor player)
    {
        if (Open) return null;
        if (tool == null || !tool.Taken) return displayName + " · " + toolName + " 필요";
        return $"E  볼트 풀기 ({turns}/{requiredTurns})";
    }

    public override void Interact(IInteractor player)
    {
        if (Open) return;

        if (!HasTool)
        {
            if (player != null) player.ShowNotice(toolName + "가 있어야 육각 볼트를 풀 수 있습니다.");
            return;
        }

        // 실제로 돌리는 조작은 미니게임에서 한다. 매니저가 없는 씬(프리뷰 등)에서는
        // 진행이 막히지 않게 그냥 떼어낸다.
        if (BoltRepairManager.Instance == null)
        {
            Debug.LogWarning($"[{nameof(BoltedPanel)}] BoltRepairManager 가 없어 미니게임 없이 떼어낸다.", this);
            CompleteOpen(player);
            return;
        }

        BoltRepairManager.Instance.Begin(this, player);
    }

    /// 미니게임에서 한 바퀴 돌 때마다 매니저가 부른다. 월드의 볼트 메시가 같이 돌아간다.
    public void RegisterTurn()
    {
        if (Open || turns >= requiredTurns) return;

        turns++;
        TurnBolt();
        Play(turnSound);
    }

    /// 필요한 바퀴를 다 돌렸을 때. 하나를 다 풀면 나머지도 같이 풀리는 연출이라
    /// 홀더(고정판 + 볼트 전체)를 한 번에 끈다.
    public void CompleteOpen(IInteractor player)
    {
        if (Open) return;

        while (turns < requiredTurns) RegisterTurn();

        Open = true;
        Play(openSound);
        if (player != null) player.ShowNotice(displayName + "을 떼어냈습니다.");
        gameObject.SetActive(false);
    }

    // 시계 반대 방향으로 돌면서 살짝 빠져나온다. 축은 볼트의 로컬 Z(머리가 보는 방향)다.
    private void TurnBolt()
    {
        if (boltToTurn == null) return;
        boltToTurn.Rotate(0f, 0f, rotationPerTurn, Space.Self);
        boltToTurn.Translate(0f, 0f, -popOutPerTurn, Space.Self);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
