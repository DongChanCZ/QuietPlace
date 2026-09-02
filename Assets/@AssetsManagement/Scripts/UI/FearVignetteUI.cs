using UnityEngine;
using UnityEngine.UI;

public class FearVignetteUI : MonoBehaviour
{
    public PlayerController player;
    public Image vignetteImage;

    [Header("Fear range mapped to vignette darkness")]
    public float fearStart = 80f;
    public float fearFull = 100f;
    [Range(0f, 1f)] public float minAlpha = 0.35f;
    [Range(0f, 1f)] public float maxAlpha = 1f;

    [Header("Fade smoothing (alpha units per second)")]
    public float fadeInSpeed = 0.6f;
    public float fadeOutSpeed = 0.6f;

    private float currentAlpha;

    void Awake()
    {
        if (vignetteImage == null) vignetteImage = GetComponent<Image>();
    }

    void Update()
    {
        if (player == null || vignetteImage == null) return;

        float fear = player.stats.fear;
        float targetAlpha;
        if (fear <= fearStart)
        {
            targetAlpha = 0f;
        }
        else
        {
            float t = Mathf.InverseLerp(fearStart, fearFull, fear);
            targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        }

        float speed = targetAlpha >= currentAlpha ? fadeInSpeed : fadeOutSpeed;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, speed * Time.deltaTime);

        Color c = vignetteImage.color;
        c.a = currentAlpha;
        vignetteImage.color = c;
    }
}
