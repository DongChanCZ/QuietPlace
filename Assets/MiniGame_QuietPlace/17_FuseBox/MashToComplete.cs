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
        if (progressFill != null) progressFill.fillAmount = progress / requiredPresses;
    }
}
