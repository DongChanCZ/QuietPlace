using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class FrequencyBand : MonoBehaviour
{
    [Header("색상")]
    public Color bandColor = Color.red;
    public Image fillImage;

    [Header("슬라이더")]
    public Slider slider;
    public float targetValue = 0.5f;
    public float tolerance = 0.03f;

    public bool IsMatched { get; private set; }
    public UnityEvent onMatched;
    public UnityEvent onUnmatched;

    void Start()
    {
        if (fillImage != null) fillImage.color = bandColor;
        if (slider != null) slider.onValueChanged.AddListener(CheckMatch);
        CheckMatch(slider != null ? slider.value : 0f);
    }

    void CheckMatch(float value)
    {
        bool matched = Mathf.Abs(value - targetValue) <= tolerance;
        if (matched == IsMatched) return;

        IsMatched = matched;
        if (matched) onMatched?.Invoke();
        else onUnmatched?.Invoke();
    }

    public void Randomize(float min, float max)
    {
        targetValue = Random.Range(min, max);
        if (slider != null) CheckMatch(slider.value);
    }
}
