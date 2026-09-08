using UnityEngine;

/// The main breaker beside the switchboard. Down is off, up is on.
///
/// Throwing it into an empty bay trips it straight back down. That trip is the
/// point: a lever that simply refused to move would read as broken scenery,
/// whereas one that swings up, drops, and says why sends the player back for
/// the transformer.
///
/// The swing runs off Update rather than a coroutine so the editor walk checks,
/// which pump frames by hand, can still drive it.
[DisallowMultipleComponent]
public sealed class BreakerLever : Interactable
{
    [SerializeField] Transform lever;
    [SerializeField] FuseSlot bay;
    [SerializeField] string displayName = "차단기";
    [SerializeField] Vector3 downEuler = new Vector3(52f, 0f, 0f);
    [SerializeField] Vector3 upEuler = new Vector3(-22f, 0f, 0f);
    [SerializeField] float swingSeconds = 0.22f;
    [SerializeField] float tripSeconds = 0.7f;

    [Header("처음부터 올라가 있는가 - 플레이어가 내리고 시작한다")]
    [SerializeField] bool startOn = true;

    public bool On { get; private set; }

    Quaternion from;
    Quaternion to;
    float t = -1f;
    float tripAt = -1f;

    void Awake()
    {
        Debug.Assert(lever, "BreakerLever has no lever to swing.");

        // 정전 전까지 돌아가던 설비라서 레버는 올라간 채로 발견된다. 퓨즈를 갈려면
        // 플레이어가 먼저 이걸 내려야 한다 - 미니게임 1단계가 그 동작이다.
        On = startOn;
        if (lever != null) lever.localRotation = Quaternion.Euler(startOn ? upEuler : downEuler);
    }

    // 레버는 팔꿈치로도 내릴 수 있다
    public override bool AllowedWhileHandsFull => true;

    public override string Prompt(IInteractor player)
    {
        return "E  " + displayName + " " + (On ? "내리기" : "올리기");
    }

    public override void Interact(IInteractor player)
    {
        if (t >= 0f) return;                       // mid-swing
        if (On) { Swing(downEuler); On = false; if (player != null) player.ShowNotice(displayName + "를 내렸습니다."); return; }

        Swing(upEuler);
        if (bay != null && !bay.Filled)
        {
            tripAt = tripSeconds;                  // springs back on its own
            if (player != null) player.ShowNotice("차단기가 튕겨 내려옵니다. 배전반이 비어 있습니다.");
            return;
        }
        On = true;
        if (player != null) player.ShowNotice("전원이 들어왔습니다.");
    }

    /// 미니게임(퓨즈 교체)을 통과해 전원이 복구됐을 때 외부에서 레버를 올린다.
    /// 플레이어가 직접 누른 것이 아니므로 빈 슬롯 검사와 튕겨 내려오기는 건너뛴다.
    public void ForceOn()
    {
        if (On) return;

        tripAt = -1f;
        On = true;
        Swing(upEuler);
    }

    void Swing(Vector3 target)
    {
        if (lever == null) return;
        from = lever.localRotation;
        to = Quaternion.Euler(target);
        t = 0f;
    }

    void Update()
    {
        if (t >= 0f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, swingSeconds);
            if (t >= 1f) { t = -1f; lever.localRotation = to; }
            else lever.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            return;
        }
        if (tripAt < 0f) return;
        tripAt -= Time.deltaTime;
        if (tripAt <= 0f) { tripAt = -1f; Swing(downEuler); }
    }
}
