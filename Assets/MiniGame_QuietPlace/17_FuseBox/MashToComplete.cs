using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.UI;

public class MashToComplete : MonoBehaviour
{
    [Header("연타(E) 설정")]
    public int requiredPresses = 15;
    public float progressPerPress = 1f;
    public float decayPerSecond = 0.6f;
    public Image progressFill;

    [Header("실물 차단기 레버 - 매니저가 넣어준다. 게이지와 같은 비율로 움직인다.")]
    public BreakerLever lever;

    /// false = 내리기(레버가 1 -> 0), true = 올리기(0 -> 1)
    public bool leverGoesUp;

    public UnityEvent onComplete;

    private float progress;
    private bool completed;

    void OnEnable()
    {
        progress = 0f;
        completed = false;
        UpdateFill();
    }

    void Update()
    {
        if (completed) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            progress += progressPerPress;
        }

        progress = Mathf.Max(0f, progress - decayPerSecond * Time.deltaTime);
        UpdateFill();

        if (progress >= requiredPresses)
        {
            completed = true;
            onComplete?.Invoke();
        }
    }

    void UpdateFill()
    {
        float t = requiredPresses <= 0 ? 1f : Mathf.Clamp01(progress / requiredPresses);

        if (progressFill != null) progressFill.fillAmount = t;

        // 화면 게이지만 차오르면 무엇을 하는지 알 수 없으므로 실물 레버를 같이 움직인다.
        if (lever != null) lever.SetLeverProgress(leverGoesUp ? t : 1f - t);
    }
}
