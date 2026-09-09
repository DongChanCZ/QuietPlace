using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 변압기 수리를 끝낸 직후의 짧은 연출.
///
/// 조작만 잠깐 멈추고(시간은 그대로 흐른다) 카메라를 오른쪽 표시등으로 돌린다.
/// 빨강이던 등이 초록으로 바뀐 것을 보면서 혼잣말이 한 줄 뜨고, 조작이 돌아온다.
/// "고쳤는데 왜 불이 안 들어오지" 를 플레이어가 스스로 묻게 만들어 배전반으로 보내는 장면이다.
///
/// 시간을 멈추지 않는 이유는 두 가지다. 괴물이 계속 돌아다녀야 긴장이 유지되고,
/// Time.timeScale 을 0 으로 만들면 이 연출의 코루틴도 같이 멈춘다.
/// </summary>
[DefaultExecutionOrder(-900)]
public class TransformerFixedCutscene : MonoBehaviour
{
    public static TransformerFixedCutscene Instance { get; private set; }

    [Header("플레이어")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteractor interactor;

    [Header("바라볼 표시등 (초록으로 바뀌는 그 등)")]
    [SerializeField] private Transform lookAt;

    [Header("시선을 돌리는 시간(초)")]
    [SerializeField] private float turnSeconds = 0.7f;

    [Header("혼잣말")]
    [SerializeField] private TMP_Text line;
    [SerializeField] private CanvasGroup lineGroup;
    [SerializeField]
    private string monologue = "발전기를 가동시켰는데 전원이 안 들어온다면... 배전반을 봐야겠군.";
    [SerializeField] private float fadeSeconds = 0.45f;
    [Tooltip("혼잣말이 화면에 머무는 시간(초)")]
    [SerializeField] private float holdSeconds = 4.2f;

    public bool IsPlaying { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (lineGroup != null)
        {
            lineGroup.alpha = 0f;
            lineGroup.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>변압기 수리가 끝난 직후 TransformerRepairManager 가 부른다.</summary>
    public void Begin()
    {
        if (IsPlaying || player == null) return;

        IsPlaying = true;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        TakeControl();

        yield return TurnToLamp();

        // ---------- 혼잣말 ----------
        if (line != null) line.text = monologue;

        if (lineGroup != null)
        {
            lineGroup.gameObject.SetActive(true);
            yield return Fade(0f, 1f, fadeSeconds);
        }

        if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);

        if (lineGroup != null)
        {
            yield return Fade(1f, 0f, fadeSeconds);
            lineGroup.gameObject.SetActive(false);
        }

        ReturnControl();
        IsPlaying = false;

        Debug.Log("[TransformerFixedCutscene] 표시등 확인 + 혼잣말 끝 - 조작 복귀");
    }

    /// 표시등으로 시선을 돌린다. 좌우는 몸을 돌려서, 위아래는 LookPitch 로.
    private IEnumerator TurnToLamp()
    {
        if (lookAt == null) yield break;

        Vector3 eye = player.playerCamera != null
            ? player.playerCamera.transform.position
            : player.transform.position + Vector3.up * 1.6f;

        Vector3 flat = lookAt.position - player.transform.position;
        flat.y = 0f;

        Quaternion fromRot = player.transform.rotation;
        Quaternion toRot = flat.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(flat.normalized, Vector3.up)
            : fromRot;

        float horizontal = new Vector2(lookAt.position.x - eye.x, lookAt.position.z - eye.z).magnitude;
        float fromPitch = player.LookPitch;
        float toPitch = -Mathf.Atan2(lookAt.position.y - eye.y, Mathf.Max(0.01f, horizontal)) * Mathf.Rad2Deg;

        float t = 0f;
        while (t < turnSeconds)
        {
            t += Time.deltaTime;
            float k = Smooth(t / turnSeconds);
            player.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
            player.LookPitch = Mathf.Lerp(fromPitch, toPitch, k);
            yield return null;
        }

        player.transform.rotation = toRot;
        player.LookPitch = toPitch;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (lineGroup == null || seconds <= 0f)
        {
            if (lineGroup != null) lineGroup.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            lineGroup.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        lineGroup.alpha = to;
    }

    // ---------------------------------------------------------------- 조작 잠금

    private void TakeControl()
    {
        if (interactor != null) interactor.enabled = false;

        player.InputLocked = true;
        player.CameraLocked = true;

        // Move 는 컨트롤러가 꺼져 있으면 그냥 돌아간다. 몸을 돌려도 밀려나지 않는다.
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
