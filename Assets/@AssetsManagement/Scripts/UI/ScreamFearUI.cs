using UnityEngine;
using UnityEngine.UI;

public class ScreamFearUI : MonoBehaviour
{
    public PlayerController player;
    public Image screamImage;
    public Text fearText;

    [Header("Shake (Fear >= threshold)")]
    public float fearShakeThreshold = 95f;
    public float shakeIntensity = 8f;

    private RectTransform rt;
    private Vector2 basePosition;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        basePosition = rt.anchoredPosition;
    }

    void Update()
    {
        if (player == null) return;

        if (fearText != null)
        {
            fearText.text = "Fear: " + Mathf.RoundToInt(player.stats.fear);
        }

        bool shaking = player.stats.fear >= fearShakeThreshold;
        rt.anchoredPosition = shaking ? basePosition + Random.insideUnitCircle * shakeIntensity : basePosition;
    }
}
