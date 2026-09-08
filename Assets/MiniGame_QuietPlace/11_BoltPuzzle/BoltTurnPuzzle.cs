using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 몽키스패너로 녹슨 볼트를 푸는 미니게임.
///
/// 마우스 왼쪽 버튼을 누른 채로 볼트 주위를 반시계 방향으로 계속 돌린다.
/// 다만 돌린 각도가 그대로 볼트에 들어가지는 않는다. 볼트는 뻑뻑해서 이렇게 움직인다.
///
///  1. 돌리는 동작이 "힘"으로 쌓인다. 손을 멈추면 힘은 금방 빠진다.
///  2. 힘이 breakawayTorque 를 넘어야 볼트가 비로소 꿈쩍한다. 그 아래에서는
///     아무리 돌려도 볼트가 미세하게 떨기만 한다.
///  3. 넘어선 뒤에도 볼트가 도는 속도는 maxTurnRate 로 제한된다. 빨리 휘둘러도
///     볼트는 그 이상 못 돈다 - 계속 힘을 주고 있어야 조금씩 풀린다.
///
/// 진행도는 절대 되감기지 않는다. 손을 놓으면 그 자리에 멈출 뿐이다.
/// </summary>
public class BoltTurnPuzzle : MonoBehaviour
{
    [Header("돌아갈 볼트 이미지")]
    public RectTransform boltVisual;

    [Header("회전 중심 - 비우면 boltVisual 을 쓴다")]
    public RectTransform pivot;

    [Header("몇 바퀴 돌려야 풀리는가")]
    public int requiredTurns = 3;

    [Header("한 바퀴로 셀 각도 (볼트 기준)")]
    public float degreesPerTurn = 200f;

    [Header("이 힘을 넘어야 볼트가 움직인다")]
    public float breakawayTorque = 0.55f;
    public float maxTorque = 1.6f;

    [Header("마우스로 돌린 각도가 힘으로 쌓이는 비율")]
    public float torqueGainPerDegree = 0.012f;

    /// 힘은 초당 이만큼 빠진다. 즉 마우스를 초당 (이 값 / torqueGainPerDegree) 도 이상
    /// 꾸준히 돌려야 힘이 유지된다 - 기본값이면 약 142도/초. 손을 늦추면 볼트가 멈춘다.
    [Header("손을 멈추면 힘이 빠지는 속도 (초당)")]
    public float torqueDecayPerSecond = 1.7f;

    [Header("힘을 다 줘도 볼트는 이 속도를 못 넘는다 (도/초)")]
    public float maxTurnRate = 45f;

    [Header("힘이 모자랄 때 볼트가 떠는 정도(도)")]
    public float strainShakeDegrees = 2.2f;

    [Header("중심에서 이 픽셀보다 가까우면 무시한다 (각도가 튄다)")]
    public float deadZoneRadius = 30f;

    [Header("UI")]
    public Image progressFill;
    public Image torqueFill;          // 지금 주고 있는 힘
    public Image breakawayMarker;     // 볼트가 움직이기 시작하는 지점 표시
    public TMP_Text counterLabel;
    public TMP_Text hintLabel;

    [Header("바퀴 눈금 - 진행바에 한 바퀴마다 하나. 넘길 때 켜진다")]
    public Image[] turnMarkers;

    [Header("눈금 켜진 색 / 꺼진 색")]
    public Color markerOnColor = new Color(0.98f, 0.85f, 0.35f);
    public Color markerOffColor = new Color(1f, 1f, 1f, 0.22f);

    [Header("진행바 가로 폭(px) - 눈금을 균등하게 놓는 데 쓴다")]
    public float markerBarWidth = 700f;

    [Header("눈금이 켜질 때 번쩍이는 시간(초)과 배율")]
    public float markerFlashSeconds = 0.35f;
    public float markerFlashScale = 1.8f;

    [Header("사운드")]
    public AudioSource audioSource;

    /// 힘이 모자라 안 돌아갈 때 삐걱거리는 소리.
    public AudioClip strainSound;

    /// 한 바퀴 넘어갈 때. 눈금이 켜지는 순간과 맞춰 난다 - "덜컥" 소리를 넣으면 된다.
    public AudioClip turnSound;

    /// 마지막 바퀴까지 다 돌아 볼트가 빠질 때.
    public AudioClip looseSound;

    public UnityEvent onTurnCompleted;
    public UnityEvent onAllTurnsDone;

    const string HintIdle = "왼쪽 버튼을 누른 채로 반시계 방향으로 계속 돌려라";
    const string HintStrain = "볼트가 꿈쩍하지 않는다 · 멈추지 말고 계속 힘을 줘라";
    const string HintTurning = "조금씩 돌아간다";
    const string HintDone = "볼트가 풀렸다";

    private bool dragging;
    private bool completed;
    private bool turningLastFrame;
    private float lastAngle;
    private float progressDegrees;
    private float torque;
    private int turns;
    private float strainSoundCooldown;
    private float markerFlashTimer;
    private int markerFlashIndex = -1;

    public int Turns => turns;

    void OnEnable()
    {
        degreesPerTurn = MinigameTuning.BoltDegreesPerTurn;

        dragging = false;
        completed = false;
        turningLastFrame = false;
        progressDegrees = 0f;
        torque = 0f;
        turns = 0;
        strainSoundCooldown = 0f;
        markerFlashTimer = 0f;
        markerFlashIndex = -1;
        RefreshMarkers();
        if (boltVisual != null) boltVisual.localRotation = Quaternion.identity;
        Refresh(HintIdle);
    }

    void Update()
    {
        UpdateMarkerFlash();

        if (completed)
        {
            ApplyBoltRotation(0f);
            return;
        }

        // 손을 멈추면 힘은 계속 빠진다. 한 번 세게 휘두르고 손 놓는 식이 통하지 않게.
        torque = Mathf.Max(0f, torque - torqueDecayPerSecond * Time.deltaTime);

        ReadDrag();

        bool turning = torque >= breakawayTorque;
        if (turning) AdvanceBolt();

        UpdateStrainFeedback(turning);
        ApplyBoltRotation(turning ? 0f : torque / Mathf.Max(0.0001f, breakawayTorque));

        if (turning != turningLastFrame)
        {
            turningLastFrame = turning;
            Refresh(turning ? HintTurning : (dragging ? HintStrain : HintIdle));
        }
        else
        {
            Refresh(null);
        }
    }

    /// 마우스가 볼트 주위를 돈 만큼 힘으로 쌓는다. 반시계 방향만 힘이 된다.
    private void ReadDrag()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        RectTransform center = pivot != null ? pivot : boltVisual;
        if (center == null) return;

        if (!TryGetAngle(center, mouse.position.ReadValue(), out float angle))
        {
            dragging = false;
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            lastAngle = angle;
            return;
        }

        if (!mouse.leftButton.isPressed) { dragging = false; return; }
        if (!dragging) return;

        float delta = Mathf.DeltaAngle(lastAngle, angle);
        lastAngle = angle;

        // 시계 방향(조이는 쪽)은 힘이 쌓이지 않는다. 벌은 없다 - 그냥 헛수고다.
        if (delta <= 0f) return;

        torque = Mathf.Min(maxTorque, torque + delta * torqueGainPerDegree);
    }

    /// 힘이 충분한 동안만, 그리고 정해진 속도 상한까지만 볼트가 돈다.
    private void AdvanceBolt()
    {
        float headroom = Mathf.Max(0.0001f, maxTorque - breakawayTorque);
        float over = Mathf.Clamp01((torque - breakawayTorque) / headroom);
        float rate = Mathf.Lerp(maxTurnRate * 0.3f, maxTurnRate, over);

        progressDegrees += rate * Time.deltaTime;

        int reached = Mathf.Min(Mathf.FloorToInt(progressDegrees / Mathf.Max(1f, degreesPerTurn)), requiredTurns);
        if (reached <= turns) return;

        turns = reached;

        // 눈금 하나가 켜지고 번쩍인다. 소리도 이때 난다.
        markerFlashIndex = turns - 1;
        markerFlashTimer = markerFlashSeconds;
        RefreshMarkers();

        Play(turnSound);
        onTurnCompleted?.Invoke();

        if (turns < requiredTurns) return;

        completed = true;
        dragging = false;
        torque = 0f;
        Refresh(HintDone);
        Play(looseSound);
        onAllTurnsDone?.Invoke();
    }

    // 힘이 모자란 채로 버티고 있으면 삐걱거리는 소리를 간격을 두고 낸다.
    private void UpdateStrainFeedback(bool turning)
    {
        strainSoundCooldown -= Time.deltaTime;
        if (turning || !dragging || torque <= 0.05f) return;
        if (strainSoundCooldown > 0f) return;

        strainSoundCooldown = 0.45f;
        Play(strainSound);
    }

    /// 볼트는 진행도만큼 돌아간다. 힘이 모자랄 때는 그 자리에서 미세하게 떤다.
    private void ApplyBoltRotation(float strain01)
    {
        if (boltVisual == null) return;

        float shake = 0f;
        if (strain01 > 0.05f)
            shake = (Mathf.PerlinNoise(Time.time * 34f, 0f) - 0.5f) * 2f * strainShakeDegrees * Mathf.Clamp01(strain01);

        boltVisual.localRotation = Quaternion.Euler(0f, 0f, progressDegrees + shake);
    }

    /// 눈금을 바퀴 수에 맞춰 배치하고 색을 맞춘다.
    ///
    /// 눈금은 어려움 기준(3개)으로 만들어 두고, 난이도에 따라 requiredTurns 가 줄어들면
    /// 남는 눈금은 끄고 쓰는 것만 균등하게 다시 놓는다. 안 그러면 2바퀴인데 눈금이 3개
    /// 남아 있어 마지막 하나가 영원히 안 켜진다.
    private void RefreshMarkers()
    {
        if (turnMarkers == null) return;

        for (int i = 0; i < turnMarkers.Length; i++)
        {
            Image m = turnMarkers[i];
            if (m == null) continue;

            bool used = i < requiredTurns;
            m.gameObject.SetActive(used);
            if (!used) continue;

            float t = (i + 1f) / requiredTurns;                       // 1/n, 2/n, ... n/n
            float x = -markerBarWidth * 0.5f + markerBarWidth * t;
            Vector2 pos = m.rectTransform.anchoredPosition;
            m.rectTransform.anchoredPosition = new Vector2(x - m.rectTransform.sizeDelta.x * 0.5f, pos.y);

            m.color = i < turns ? markerOnColor : markerOffColor;
            m.transform.localScale = Vector3.one;
        }
    }

    /// 방금 켜진 눈금을 잠깐 크게 만들었다 되돌린다. 한 바퀴 넘어간 게 눈에 보이게.
    private void UpdateMarkerFlash()
    {
        if (markerFlashTimer <= 0f || turnMarkers == null) return;
        if (markerFlashIndex < 0 || markerFlashIndex >= turnMarkers.Length) { markerFlashTimer = 0f; return; }

        markerFlashTimer -= Time.deltaTime;

        Image m = turnMarkers[markerFlashIndex];
        if (m == null) { markerFlashTimer = 0f; return; }

        float k = Mathf.Clamp01(markerFlashTimer / Mathf.Max(0.01f, markerFlashSeconds));
        float scale = 1f + (markerFlashScale - 1f) * Mathf.Sin(k * Mathf.PI);
        m.transform.localScale = new Vector3(scale, scale, 1f);

        if (markerFlashTimer > 0f) return;

        m.transform.localScale = Vector3.one;
        markerFlashIndex = -1;
    }

    /// 마우스가 중심에서 얼마나 돌아간 위치에 있는지. 데드존 안이면 false.
    private bool TryGetAngle(RectTransform center, Vector2 screen, out float angle)
    {
        angle = 0f;

        Canvas canvas = center.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(center, screen, cam, out Vector2 local))
            return false;

        if (local.sqrMagnitude < deadZoneRadius * deadZoneRadius) return false;

        angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        return true;
    }

    /// message 가 null 이면 문구는 그대로 두고 게이지만 갱신한다.
    private void Refresh(string message)
    {
        if (progressFill != null)
        {
            float total = Mathf.Max(1f, degreesPerTurn * requiredTurns);
            progressFill.fillAmount = Mathf.Clamp01(progressDegrees / total);
        }

        if (torqueFill != null)
            torqueFill.fillAmount = Mathf.Clamp01(torque / Mathf.Max(0.0001f, maxTorque));

        if (counterLabel != null)
            counterLabel.text = "볼트 " + turns + " / " + requiredTurns + " 바퀴";

        if (hintLabel != null && message != null) hintLabel.text = message;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
