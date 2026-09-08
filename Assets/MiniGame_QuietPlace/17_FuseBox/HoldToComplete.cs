using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 차단기 레버를 꾹 눌러 내리거나 올리는 단계.
///
/// 연타(MashToComplete) 가 아니라 계속 누르고 있는 방식이다. 처음에는 뻣뻣해서 느리게
/// 움직이고, 누르고 있는 시간이 쌓이면서 속도가 붙는다. 손을 떼면 게이지가 되감긴다.
///
/// 화면 게이지만 차오르면 무엇을 하는지 알 수 없으므로, 실물 레버(PIVOT_2F_BreakerLever)를
/// 게이지와 같은 비율로 함께 움직인다.
/// </summary>
public class HoldToComplete : MonoBehaviour
{
    [Header("누르고 있어야 하는 키")]
    public Key holdKey = Key.E;

    [Header("최고 속도로 계속 눌렀을 때 걸리는 시간(초)")]
    public float requiredSeconds = 2.4f;

    [Header("처음 속도 배율 - 낮으면 초반이 더 뻣뻣하다")]
    [Range(0.05f, 1f)] public float startSpeed = 0.22f;

    [Header("이 시간 동안 눌러야 최고 속도가 된다(초)")]
    public float accelSeconds = 1.3f;

    [Header("손을 떼면 초당 이 비율로 되감긴다")]
    public float decayPerSecond = 0.5f;

    [Header("UI")]
    public Image progressFill;
    public TMP_Text hintLabel;

    [Header("실물 차단기 레버 - 매니저가 넣어준다")]
    public BreakerLever lever;

    /// false = 내리기(레버가 1 -> 0), true = 올리기(0 -> 1)
    public bool leverGoesUp;

    public UnityEvent onComplete;

    private float progress01;
    private float heldSeconds;
    private bool completed;

    void OnEnable()
    {
        progress01 = 0f;
        heldSeconds = 0f;
        completed = false;
        ApplyLever();
        Refresh();
    }

    void Update()
    {
        if (completed) return;

        Keyboard kb = Keyboard.current;
        bool holding = kb != null && kb[holdKey].isPressed;

        if (holding)
        {
            heldSeconds += Time.deltaTime;

            // 처음엔 startSpeed 배, accelSeconds 동안 눌러야 1배가 된다
            float speed = Mathf.Lerp(startSpeed, 1f, Mathf.Clamp01(heldSeconds / Mathf.Max(0.01f, accelSeconds)));
            progress01 += speed * Time.deltaTime / Mathf.Max(0.01f, requiredSeconds);
        }
        else
        {
            heldSeconds = 0f;   // 떼면 가속도 처음부터
            progress01 -= decayPerSecond * Time.deltaTime;
        }

        progress01 = Mathf.Clamp01(progress01);
        ApplyLever();
        Refresh();

        if (progress01 < 1f) return;

        completed = true;
        onComplete?.Invoke();
    }

    private void ApplyLever()
    {
        if (lever == null) return;
        lever.SetLeverProgress(leverGoesUp ? progress01 : 1f - progress01);
    }

    private void Refresh()
    {
        if (progressFill != null) progressFill.fillAmount = progress01;

        if (hintLabel == null) return;

        if (completed) hintLabel.text = leverGoesUp ? "차단기를 올렸다" : "차단기를 내렸다";
        else if (heldSeconds <= 0f) hintLabel.text = "E 를 꾹 누른다";
        else hintLabel.text = heldSeconds < accelSeconds ? "뻣뻣하다 · 계속 눌러라" : "움직인다 · 놓지 마라";
    }
}
