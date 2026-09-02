using UnityEngine;
using UnityEngine.UI;

public class DriftingFog : MonoBehaviour
{
    [Header("좌우 흐름")]
    public float driftSpeed = 15f;
    public float wrapMargin = 400f;

    [Header("뭉게뭉게 일렁임")]
    public float wobbleAmount = 30f;
    public float wobbleSpeed = 0.3f;

    [Header("숨쉬는 크기 변화")]
    public float scalePulseAmount = 0.15f;
    public float scalePulseSpeed = 0.4f;

    [Header("옅어졌다 진해지는 주기")]
    public float cycleDuration = 18f;
    public float minAlphaRatio = 0.3f;

    private RectTransform rt;
    private Image image;
    private float screenHalfWidth;
    private Vector2 basePos;
    private float seed;
    private float cycleTimer;
    private float baseAlpha;

    void Start()
    {
        rt = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        baseAlpha = image.color.a;

        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRt = canvas.GetComponent<RectTransform>();
        screenHalfWidth = canvasRt.rect.width * 0.5f;

        basePos = rt.anchoredPosition;
        seed = Random.Range(0f, 100f);
        cycleTimer = Random.Range(0f, cycleDuration);
    }

    void Update()
    {
        basePos.x += driftSpeed * Time.deltaTime;
        if (basePos.x > screenHalfWidth + wrapMargin)
        {
            basePos.x = -screenHalfWidth - wrapMargin;
        }

        float wobbleX = Mathf.Sin(Time.time * wobbleSpeed + seed) * wobbleAmount;
        float wobbleY = Mathf.Cos(Time.time * wobbleSpeed * 0.7f + seed) * wobbleAmount * 0.5f;
        rt.anchoredPosition = basePos + new Vector2(wobbleX, wobbleY);

        float scale = 1f + Mathf.Sin(Time.time * scalePulseSpeed + seed) * scalePulseAmount;
        rt.localScale = Vector3.one * scale;

        cycleTimer += Time.deltaTime;
        float phase = (cycleTimer % cycleDuration) / cycleDuration;
        float fade = Mathf.Sin(phase * Mathf.PI);
        Color c = image.color;
        c.a = baseAlpha * Mathf.Lerp(minAlphaRatio, 1f, fade);
        image.color = c;
    }
}
