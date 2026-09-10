using System.Collections;
using UnityEngine;

/// <summary>
/// 퓨즈를 꽂은 뒤의 마무리 연출.
///
///   1. 퓨즈 미니게임이 닫히고, 플레이어가 실물 차단기 레버 앞으로 저절로 걸어간다.
///   2. 레버가 내려가 있는 것이 보이고, 전용 화면에서 E 를 연타해 올린다.
///      게이지가 차는 만큼 실물 레버가 같이 올라간다.
///   3. 발전기실 변압기까지 고쳐둔 상태였다면 여기서 건물 전체에 전기가 들어온다.
///      조명이 차례로 켜지는 장면(PowerOnCutscene)을 보여준 뒤 조작을 돌려준다.
///      아직 변압기가 남았다면 불은 들어오지 않는다 - 다음 목표를 짚는 혼잣말만 띄운다.
///
/// 조작을 빼앗는 방식이 중요하다. PlayerController 를 끄면 카메라 처리(ApplyCamera)까지
/// 멈춰서 직접 카메라를 몰아야 하는데, 되돌릴 때 시선이 튄다. 대신 CharacterController 만
/// 끈다 - PlayerController.Move 는 컨트롤러가 꺼져 있으면 아무 것도 하지 않으므로,
/// 위치는 내가 직접 옮기면서 카메라는 계속 정상 처리된다.
/// </summary>
[DefaultExecutionOrder(-900)]
public class BreakerRaiseSequence : MonoBehaviour
{
    public static BreakerRaiseSequence Instance { get; private set; }

    [Header("플레이어")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteractor interactor;

    [Header("실물 차단기 - 매니저가 넣어주기도 한다")]
    [SerializeField] private BreakerLever lever;

    [Header("레버 앞에 서는 자리 (없으면 레버 앞으로 계산한다)")]
    [SerializeField] private Transform standAnchor;
    [Tooltip("standAnchor 가 없을 때 레버에서 이만큼 떨어져 선다(m)")]
    [SerializeField] private float standDistance = 1.5f;

    [Header("1. 자동으로 걸어가는 시간(초)")]
    [SerializeField] private float walkSeconds = 1.3f;
    [Tooltip("레버의 어느 높이를 보게 할지 (레버 피벗에서 위로 m)")]
    [SerializeField] private float lookAtHeight = 0f;

    [Header("2. 차단기 올리기")]
    [Tooltip("레버가 끝까지 올라가는 데 걸리는 전체 시간(초)")]
    [SerializeField] private float leverRaiseSeconds = 2.2f;

    [Tooltip("한 번에 안 올라간다. 이 지점까지 밀어올렸다가 미끄러지기를 반복한다(0~1)")]
    [SerializeField] private float[] leverStages = { 0.45f, 0.74f, 1f };

    [Tooltip("미끄러져 내려오는 양(0~1)")]
    [SerializeField] private float leverSlip = 0.13f;

    [Tooltip("밀어올리는 동안 떨리는 정도(0~1)")]
    [SerializeField] private float leverShake = 0.022f;

    // 게이지 연타 단계는 뺐다. 두 참조는 그 화면이 켜진 채 남지 않도록 꺼두는 데만 쓴다.
    [SerializeField] private GameObject minigameRoot;
    [SerializeField] private MashToComplete mash;

    [Header("3. 변압기가 아직일 때 뜨는 혼잣말")]
    [SerializeField]
    private string fuseOnlyMonologue = "전력을 복구했으니 발전기를 가동시켜야지.";
    [Tooltip("혼잣말이 화면에 머무는 시간(초)")]
    [SerializeField] private float monologueHoldSeconds = 4.2f;

    public bool IsPlaying { get; private set; }

    private IInteractor _pendingPlayer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (minigameRoot != null) minigameRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>퓨즈를 꽂은 직후 FuseRepairManager 가 부른다.</summary>
    public void Begin(IInteractor who, BreakerLever physicalLever)
    {
        if (IsPlaying) return;
        if (physicalLever != null) lever = physicalLever;

        if (player == null || lever == null)
        {
            Debug.LogError($"[{nameof(BreakerRaiseSequence)}] 참조가 비어 있어 연출을 시작할 수 없다. " +
                           $"player={player != null} lever={lever != null}", this);
            return;
        }

        IsPlaying = true;
        _pendingPlayer = who;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        TakeControl();

        // 퓨즈를 끼우려 내려둔 차단기. 올려야 할 것이 눈에 보여야 한다.
        lever.SetLeverProgress(0f);

        yield return WalkToLever();

        // ---------- 차단기 올리기 ----------
        // 게이지 연타 화면 없이 레버가 그대로 올라간다. 올라가는 것만 보이면 되는 단계라
        // 판정을 붙일 이유가 없었다.
        if (minigameRoot != null) minigameRoot.SetActive(false);

        yield return RaiseLever();
        lever.ForceOn();                   // 끝까지 올라간 자세로 확정

        yield return Finale();

        ReturnControl();
        IsPlaying = false;
        _pendingPlayer = null;
    }

    /// <summary>
    /// 뻑뻑한 차단기를 밀어올리는 동작. 한 번에 안 올라간다 -
    /// 단계까지 밀어올렸다가 조금 미끄러지기를 반복하고, 미는 동안엔 손이 떨린다.
    /// 마지막 단계에서만 끝까지 올라간다.
    /// </summary>
    private IEnumerator RaiseLever()
    {
        int n = (leverStages != null && leverStages.Length > 0) ? leverStages.Length : 1;
        float pushTime = leverRaiseSeconds * 0.75f / n;
        float slipTime = leverRaiseSeconds * 0.25f / Mathf.Max(1, n - 1);
        float cur = 0f;

        for (int i = 0; i < n; i++)
        {
            float goal = (leverStages != null && i < leverStages.Length) ? Mathf.Clamp01(leverStages[i]) : 1f;

            float t = 0f;
            while (t < pushTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Lerp(cur, goal, Smooth(t / pushTime));
                // 끝에 다다를수록 떨림을 줄여야 '겨우 걸렸다' 로 읽힌다
                float fade = 1f - Smooth(t / pushTime) * 0.6f;
                float shake = (Mathf.PerlinNoise(Time.time * 26f, i * 7.3f) - 0.5f) * 2f * leverShake * fade;
                lever.SetLeverProgress(Mathf.Clamp01(k + shake));
                yield return null;
            }
            cur = goal;
            lever.SetLeverProgress(cur);

            if (i == n - 1) break;          // 마지막엔 미끄러지지 않는다

            float back = Mathf.Max(0f, cur - leverSlip);
            t = 0f;
            while (t < slipTime)
            {
                t += Time.deltaTime;
                lever.SetLeverProgress(Mathf.Lerp(cur, back, Smooth(t / slipTime)));
                yield return null;
            }
            cur = back;
        }

        lever.SetLeverProgress(1f);
    }

    /// 레버 앞까지 저절로 걸어간다. 시선도 레버로 돌아간다.
    private IEnumerator WalkToLever()
    {
        Vector3 target = StandPosition();
        Vector3 lookAt = lever.transform.position + Vector3.up * lookAtHeight;

        Vector3 fromPos = player.transform.position;
        Quaternion fromRot = player.transform.rotation;
        float fromPitch = player.LookPitch;

        // 목표 자리에서 레버를 봤을 때의 좌우/위아래 각도
        Vector3 flat = lookAt - target;
        flat.y = 0f;
        Quaternion toRot = flat.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(flat.normalized, Vector3.up)
            : fromRot;

        // 카메라는 눈높이에 있으므로 그 높이에서 내려다보는 각도를 구한다
        float eyeY = player.playerCamera != null
            ? target.y + (player.playerCamera.transform.position.y - fromPos.y)
            : target.y + 1.6f;
        float horizontal = new Vector2(lookAt.x - target.x, lookAt.z - target.z).magnitude;
        float toPitch = -Mathf.Atan2(lookAt.y - eyeY, Mathf.Max(0.01f, horizontal)) * Mathf.Rad2Deg;

        float t = 0f;
        while (t < walkSeconds)
        {
            t += Time.deltaTime;
            float k = Smooth(t / walkSeconds);

            player.transform.position = Vector3.Lerp(fromPos, target, k);
            player.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
            player.LookPitch = Mathf.Lerp(fromPitch, toPitch, k);
            yield return null;
        }

        player.transform.position = target;
        player.transform.rotation = toRot;
        player.LookPitch = toPitch;
    }

    /// <summary>
    /// 차단기를 올린 뒤. 발전기실(변압기)까지 끝나 있었다면 여기서 전원이 들어오므로
    /// 불이 켜지는 장면을 보여준다. 아직이라면 불은 안 들어온다 - 사운드와 카메라 연출
    /// 대신 다음 목표를 짚어주는 혼잣말만 띄운다.
    /// </summary>
    private IEnumerator Finale()
    {
        // 퓨즈 쪽이 끝났다고 알린다. 변압기까지 끝나 있으면 여기서 전원이 들어온다.
        if (BuildingPower.Instance != null) BuildingPower.Instance.MarkFuseInstalled();

        bool powered = BuildingPower.Instance != null && BuildingPower.Instance.Powered;

        if (powered)
        {
            if (PowerOnCutscene.Instance != null) yield return PowerOnCutscene.Instance.Play();
            else Debug.LogWarning($"[{nameof(BreakerRaiseSequence)}] PowerOnCutscene 이 없어 전원 연출을 건너뛴다.", this);

            GameCheckpoint.Save(GameStep.PowerRestored, _pendingPlayer);
            Debug.Log("[BreakerRaiseSequence] 차단기 올림 -> 전원 복구 (변압기도 끝나 있었다)");
            yield break;
        }

        // 변압기가 남았다. 단계를 PowerRestored 로 올리면 복원할 때 변압기까지 끝난 것이
        // 되어버리므로, 퓨즈만 꽂았다는 사실만 따로 남긴다.
        if (MonologueOverlay.Instance != null)
            yield return MonologueOverlay.Instance.Show(fuseOnlyMonologue, monologueHoldSeconds);
        else if (_pendingPlayer != null)
            _pendingPlayer.ShowNotice(fuseOnlyMonologue);

        GameProgress.MarkFuseOnly(player != null ? player.transform : null);
        if (_pendingPlayer != null) _pendingPlayer.ShowNotice("◆ 기록됨 — 죽어도 이 지점부터 다시 시작한다.");

        Debug.Log("[BreakerRaiseSequence] 차단기 올림 -> 전원 아직 (발전기 변압기가 남았다)");
    }

    private Vector3 StandPosition()
    {
        if (standAnchor != null) return standAnchor.position;

        // 앵커가 없으면 레버 앞쪽 바닥을 찾아 선다.
        Vector3 front = lever.transform.position + lever.transform.forward * standDistance;
        RaycastHit hit;
        if (Physics.Raycast(front + Vector3.up * 1.5f, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.05f;

        return new Vector3(front.x, player.transform.position.y, front.z);
    }

    // ---------------------------------------------------------------- 조작 잠금

    private void TakeControl()
    {
        if (interactor != null) interactor.enabled = false;

        player.InputLocked = true;
        player.CameraLocked = true;

        // Move 는 컨트롤러가 꺼져 있으면 그냥 돌아간다. 그래서 위치를 직접 옮길 수 있고,
        // PlayerController 자체는 켜져 있으니 카메라는 계속 눈높이에 붙어 있는다.
        if (player.Controller != null) player.Controller.enabled = false;
    }

    private void ReturnControl()
    {
        if (player.Controller != null) player.Controller.enabled = true;

        player.InputLocked = false;
        player.CameraLocked = false;

        if (interactor != null) interactor.enabled = true;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
