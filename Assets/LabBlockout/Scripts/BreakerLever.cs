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

    public bool On { get; private set; }

    Quaternion from;
    Quaternion to;
    float t = -1f;
    float tripAt = -1f;

    void Awake()
    {
        Debug.Assert(lever, "BreakerLever has no lever to swing.");
        if (lever != null) lever.localRotation = Quaternion.Euler(downEuler);
    }

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
