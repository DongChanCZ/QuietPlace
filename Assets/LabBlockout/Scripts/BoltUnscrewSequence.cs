using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 고정판 볼트를 화면 UI 없이 그 자리에서 푸는 연출. 차단기 올리기(BreakerRaiseSequence)와
/// 같은 방식이다.
///
///   1. 볼트 앞으로 저절로 걸어가 시선이 볼트에 고정된다.
///   2. 손에 든 몽키스패너가 화면에 나와 볼트를 문다.
///   3. 마우스 왼쪽 버튼을 누른 채 반시계로 돌린다. strokeDegrees 만큼 돌면 더 못 돌리니
///      스패너를 빼서 처음 자리에 다시 물고(짧게 작아졌다 커진다) 또 돌린다.
///      한 획마다 실제 볼트 메시가 BoltedPanel.RegisterTurn() 으로 돌아간다.
///   4. 필요한 획을 다 채우면 고정판이 떨어지고 조작이 돌아온다.
///
/// 조작은 BreakerRaiseSequence 와 같은 이유로 CharacterController 만 끈다 -
/// PlayerController 를 끄면 카메라 처리까지 멈춰서 시선이 튄다.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class BoltUnscrewSequence : MonoBehaviour
{
    public static BoltUnscrewSequence Instance { get; private set; }

    [Header("플레이어")]
    [SerializeField] PlayerController player;
    [SerializeField] PlayerInteractor interactor;

    [Header("손에 드는 몽키스패너 - 카메라 자식. 비워도 볼트는 돌아간다")]
    [SerializeField] Transform wrenchProp;

    [Header("스패너가 물릴 때 카메라 기준 위치")]
    [SerializeField] Vector3 wrenchLocalPos = new Vector3(0.22f, -0.20f, 0.55f);

    /// 모델마다 손잡이가 향한 쪽이 다르다. 이 값을 더해 손잡이가 0도(3시)를 보게 맞춘 뒤,
    /// 거기에 아래 시작 각도와 돌린 각도를 얹는다. 새 스패너 모델을 넣으면 이 값만 다시 잡으면 된다.
    [Header("모델 보정 각도 - 손잡이가 3시를 보게 하는 값")]
    [SerializeField] Vector3 wrenchLocalEuler = new Vector3(0f, 0f, 106f);

    /// 11시 = 120도. 왼쪽 위에서 물어야 반시계로 당길 수 있다.
    [Header("무는 자리 (11시 = 120도)")]
    [SerializeField] float startClockDegrees = 120f;

    [Header("볼트 앞에 서는 거리(m)")]
    [SerializeField] float standDistance = 1.1f;
    [SerializeField] float walkSeconds = 1.0f;

    /// 11시에서 물어 7시까지 = 120도. 그 이상은 손목이 안 돌아가니 빼서 다시 문다.
    [Header("한 획으로 돌리는 각도 (11시 -> 7시 = 120도)")]
    [SerializeField] float strokeDegrees = 120f;

    [Header("다시 물 때 작아지는 정도(0~1)와 시간(초)")]
    [SerializeField] float reseatDip = 0.18f;
    [SerializeField] float reseatSeconds = 0.14f;

    [Header("중심에서 이 픽셀보다 가까우면 각도를 무시한다")]
    [SerializeField] float deadZonePixels = 40f;

    [Header("마우스를 움직인 만큼 손이 도는 비율")]
    [SerializeField] float dragSensitivity = 1.0f;

    public bool IsPlaying { get; private set; }

    BoltedPanel target;
    IInteractor pending;

    /// 볼트 중심을 도는 '손'의 가상 위치(픽셀). 커서가 잠겨 있어 델타로 굴린다.
    Vector2 hand;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (wrenchProp != null) wrenchProp.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>BoltedPanel 이 E 를 받았을 때 부른다. 맡았으면 true.</summary>
    public static bool Begin(BoltedPanel panel, IInteractor who)
    {
        if (Instance == null || Instance.IsPlaying) return false;
        if (Instance.player == null || panel == null) return false;

        Instance.IsPlaying = true;
        Instance.target = panel;
        Instance.pending = who;
        Instance.StartCoroutine(Instance.Run());
        return true;
    }

    IEnumerator Run()
    {
        TakeControl();

        Transform bolt = target.BoltTransform != null ? target.BoltTransform : target.transform;
        yield return WalkTo(bolt);

        if (wrenchProp != null)
        {
            wrenchProp.gameObject.SetActive(true);
            wrenchProp.localPosition = wrenchLocalPos;
            wrenchProp.localEulerAngles = wrenchLocalEuler + new Vector3(0f, 0f, startClockDegrees);
            wrenchProp.localScale = Vector3.one;
        }

        int need = Mathf.Max(1, target.RequiredTurns - target.Turns);
        int done = 0;
        float swept = 0f;          // 이번 획에서 돌린 각도
        float reseat = 0f;
        bool dragging = false;
        float lastAngle = 0f;
        // 스패너를 무는 자리(11시)에서 시작한다
        float rad = startClockDegrees * Mathf.Deg2Rad;
        hand = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Max(deadZonePixels, 1f);

        if (pending != null) pending.ShowNotice("마우스 왼쪽 버튼을 누른 채 반시계로 돌리세요.");

        while (done < need)
        {
            Mouse mouse = Mouse.current;

            if (mouse != null)
            {
                // 커서가 Locked 라 mouse.position 은 화면 한가운데에 붙어 움직이지 않는다.
                // 그래서 델타를 쌓아 '손이 볼트 둘레를 도는' 가상 위치를 직접 굴린다.
                hand += mouse.delta.ReadValue() * dragSensitivity;
                float reach = Mathf.Max(deadZonePixels, 1f);
                if (hand.magnitude < reach) hand = (hand.sqrMagnitude > 0.0001f ? hand.normalized : Vector2.left) * reach;

                float angle = Mathf.Atan2(hand.y, hand.x) * Mathf.Rad2Deg;

                if (mouse.leftButton.wasPressedThisFrame) { dragging = true; lastAngle = angle; }
                else if (!mouse.leftButton.isPressed) dragging = false;
                else if (dragging)
                {
                    float delta = Mathf.DeltaAngle(lastAngle, angle);
                    lastAngle = angle;
                    if (delta > 0f) swept += delta;      // 반시계만 센다
                }
            }

            // 한 획을 다 돌렸다 - 볼트가 그만큼 돌고, 스패너는 빼서 다시 문다
            if (swept >= strokeDegrees)
            {
                swept = 0f;
                reseat = reseatSeconds;
                done++;
                target.RegisterTurn();
                dragging = false;
            }

            if (wrenchProp != null)
            {
                float scale = 1f;
                if (reseat > 0f)
                {
                    reseat -= Time.deltaTime;
                    scale = 1f - reseatDip * Mathf.Clamp01(reseat / Mathf.Max(0.01f, reseatSeconds));
                }
                // 보정각 + 무는 자리(11시) + 이번 획에서 돌린 만큼. 120도를 채우면 7시가 된다.
                wrenchProp.localEulerAngles = wrenchLocalEuler + new Vector3(0f, 0f, startClockDegrees + swept);
                wrenchProp.localScale = new Vector3(scale, scale, scale);
            }

            yield return null;
        }

        if (wrenchProp != null) wrenchProp.gameObject.SetActive(false);
        target.CompleteOpen(pending);

        ReturnControl();
        IsPlaying = false;
        target = null;
        pending = null;
    }

    /// 볼트 앞으로 걸어가며 시선을 볼트에 맞춘다. BreakerRaiseSequence 와 같은 방식.
    IEnumerator WalkTo(Transform bolt)
    {
        Vector3 lookAt = bolt.position;
        Vector3 fromPos = player.transform.position;

        Vector3 back = player.transform.position - lookAt;
        back.y = 0f;
        if (back.sqrMagnitude < 0.01f) back = -player.transform.forward;
        Vector3 target2 = lookAt + back.normalized * standDistance;

        RaycastHit floor;
        target2 = Physics.Raycast(target2 + Vector3.up * 1.5f, Vector3.down, out floor, 4f, ~0, QueryTriggerInteraction.Ignore)
            ? floor.point + Vector3.up * 0.05f
            : new Vector3(target2.x, fromPos.y, target2.z);

        Quaternion fromRot = player.transform.rotation;
        float fromPitch = player.LookPitch;

        Vector3 flat = lookAt - target2;
        flat.y = 0f;
        Quaternion toRot = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : fromRot;

        float eyeY = player.playerCamera != null
            ? target2.y + (player.playerCamera.transform.position.y - fromPos.y)
            : target2.y + 1.6f;
        float horiz = new Vector2(lookAt.x - target2.x, lookAt.z - target2.z).magnitude;
        float toPitch = -Mathf.Atan2(lookAt.y - eyeY, Mathf.Max(0.01f, horiz)) * Mathf.Rad2Deg;

        float t = 0f;
        while (t < walkSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / walkSeconds);
            player.transform.position = Vector3.Lerp(fromPos, target2, k);
            player.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
            player.LookPitch = Mathf.Lerp(fromPitch, toPitch, k);
            yield return null;
        }
        player.transform.position = target2;
        player.transform.rotation = toRot;
        player.LookPitch = toPitch;
    }

    void TakeControl()
    {
        if (interactor != null) interactor.enabled = false;
        player.InputLocked = true;
        player.CameraLocked = true;
        if (player.Controller != null) player.Controller.enabled = false;
    }

    void ReturnControl()
    {
        if (player.Controller != null) player.Controller.enabled = true;
        player.InputLocked = false;
        player.CameraLocked = false;
        if (interactor != null) interactor.enabled = true;
    }
}
