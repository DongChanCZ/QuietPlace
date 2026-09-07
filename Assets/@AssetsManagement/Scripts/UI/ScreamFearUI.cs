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
            // 반올림하면 99.6도 100으로 보여서 Fearing 진입 문턱(fear>=fearMax)과 화면 표기가 어긋난다 - 버림으로 통일.
            fearText.text = "Fear " + Mathf.FloorToInt(player.stats.fear);
        }

        bool shaking = player.stats.fear >= fearShakeThreshold;
        rt.anchoredPosition = shaking ? basePosition + Random.insideUnitCircle * shakeIntensity : basePosition;
    }
}
