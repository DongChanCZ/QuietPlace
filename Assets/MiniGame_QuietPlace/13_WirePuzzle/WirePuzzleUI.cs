using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 같은 색 선 4개를 단자에 연결하는 단계 - 화면 UI 방식.
///
/// 원래는 3D 월드의 선/단자 오브젝트를 OnMouseDown 으로 끄는 DraggableWire 였다. 그런데
/// 이 단계가 열릴 때 8개 오브젝트가 전부 방금 빼낸 변압기 모듈에 가려지고 시야 맨 위
/// 구석에 몰려서, 선을 하나도 잡을 수 없었다. 모듈 안착 단계와 같은 이유로 화면으로 옮겼다.
///
/// 조작: 왼쪽 선 끝을 왼쪽 버튼으로 잡아 오른쪽 단자까지 끌고 가서 놓는다.
/// 색이 맞으면 붙고, 틀리면 제자리로 튕긴다.
/// </summary>
public class WirePuzzleUI : MonoBehaviour
{
    [System.Serializable]
    public class Wire
    {
        public WireColor color;
        public RectTransform plug;        // 끌고 다니는 선 끝
        public RectTransform terminal;    // 꽂아야 하는 단자
        public RectTransform cable;       // 선 몸통 (plug 를 따라 늘어난다)
        public Image plugImage;
        public Image terminalImage;

        [HideInInspector] public Vector2 home;
        [HideInInspector] public bool connected;
    }

    [Header("선 4개")]
    public Wire[] wires;

    [Header("드래그 영역")]
    public RectTransform bounds;

    [Header("이 픽셀 안을 눌러야 선을 잡는다")]
    public float grabRadius = 70f;

    [Header("단자 중심에서 이 픽셀 안에서 놓으면 연결된다")]
    public float snapRadius = 80f;

    [Header("UI")]
    public TMP_Text hintLabel;
    public TMP_Text counterLabel;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip connectSound;
    public AudioClip wrongSound;

    public UnityEvent onAllWiresConnected;

    const string HintIdle = "왼쪽 선을 잡아 같은 색 단자에 연결하라";
    const string HintWrong = "색이 맞지 않는다";
    const string HintDone = "배선 완료";

    private Wire held;
    private Vector2 grabOffset;
    private bool completed;

    void OnEnable()
    {
        completed = false;
        held = null;

        if (wires != null)
        {
            foreach (Wire w in wires)
            {
                if (w == null || w.plug == null) continue;
                w.home = w.plug.anchoredPosition;
                w.connected = false;
                UpdateCable(w);
            }
        }

        Refresh(HintIdle);
    }

    void Update()
    {
        if (completed || wires == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 cursor;
        bool valid = TryGetLocalCursor(mouse.position.ReadValue(), out cursor);

        if (mouse.leftButton.wasPressedThisFrame && valid) TryGrab(cursor);

        if (held != null && valid)
        {
            held.plug.anchoredPosition = cursor + grabOffset;
            UpdateCable(held);
        }

        if (held != null && !mouse.leftButton.isPressed) Release();
    }

    private void TryGrab(Vector2 cursor)
    {
        Wire nearest = null;
        float nearestDist = float.PositiveInfinity;

        foreach (Wire w in wires)
        {
            if (w == null || w.connected || w.plug == null) continue;
            float d = Vector2.Distance(w.plug.anchoredPosition, cursor);
            if (d > grabRadius || d >= nearestDist) continue;
            nearest = w;
            nearestDist = d;
        }

        if (nearest == null) return;

        held = nearest;
        grabOffset = held.plug.anchoredPosition - cursor;
        held.plug.SetAsLastSibling();   // 잡은 선이 다른 선 위로 올라온다
    }

    private void Release()
    {
        Wire w = held;
        held = null;

        // 놓은 자리에서 가장 가까운 단자를 찾는다. 아직 비어 있는 단자만 후보다.
        Wire target = null;
        float best = snapRadius;
        foreach (Wire other in wires)
        {
            if (other == null || other.terminal == null || other.connected) continue;
            float d = Vector2.Distance(w.plug.anchoredPosition, other.terminal.anchoredPosition);
            if (d > best) continue;
            target = other;
            best = d;
        }

        // 색이 같은 단자여야 붙는다
        if (target != null && target.color == w.color)
        {
            w.connected = true;
            w.plug.anchoredPosition = target.terminal.anchoredPosition;
            UpdateCable(w);
            Play(connectSound);

            int done = CountConnected();
            if (done >= wires.Length)
            {
                completed = true;
                Refresh(HintDone);
                onAllWiresConnected?.Invoke();
                return;
            }

            Refresh(HintIdle);
            return;
        }

        // 틀렸거나 아무 단자도 없으면 제자리로
        w.plug.anchoredPosition = w.home;
        UpdateCable(w);
        Play(wrongSound);
        Refresh(target != null ? HintWrong : HintIdle);
    }

    /// 선 몸통을 시작점에서 지금 잡은 끝까지 늘리고 그 방향으로 회전시킨다.
    private void UpdateCable(Wire w)
    {
        if (w.cable == null || w.plug == null) return;

        Vector2 from = w.home;
        Vector2 to = w.plug.anchoredPosition;
        Vector2 mid = (from + to) * 0.5f;
        Vector2 delta = to - from;

        w.cable.anchoredPosition = mid;
        w.cable.sizeDelta = new Vector2(delta.magnitude, w.cable.sizeDelta.y);
        w.cable.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private int CountConnected()
    {
        int n = 0;
        foreach (Wire w in wires) if (w != null && w.connected) n++;
        return n;
    }

    private bool TryGetLocalCursor(Vector2 screen, out Vector2 local)
    {
        local = Vector2.zero;

        RectTransform space = bounds != null ? bounds : (RectTransform)transform;
        Canvas canvas = space.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, cam, out local);
    }

    private void Refresh(string message)
    {
        if (counterLabel != null && wires != null)
            counterLabel.text = "연결 " + CountConnected() + " / " + wires.Length;

        if (hintLabel != null && message != null) hintLabel.text = message;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
