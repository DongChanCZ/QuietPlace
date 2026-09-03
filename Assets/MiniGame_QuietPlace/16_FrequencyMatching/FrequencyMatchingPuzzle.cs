using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class FrequencyMatchingPuzzle : MonoBehaviour
{
    public FrequencyBand[] bands;

    [Header("랜덤 목표값 (시작 시)")]
    public bool randomizeOnStart = true;
    public float randomMin = 0.1f;
    public float randomMax = 0.9f;

    [Header("충전 게이지")]
    public Image chargeGaugeFill;
    public float chargeDuration = 5f;
    public GameObject loadingIndicator; // "불러오는 중" 표시 UI

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip frequencySound; // 확성기에서 나가는 주파수 소리

    public UnityEvent onFullyCharged; // 몬스터 무력화 트리거용

    private float chargeTimer;
    private bool completed;

    void Start()
    {
        if (randomizeOnStart)
        {
            foreach (FrequencyBand band in bands)
            {
                if (band != null) band.Randomize(randomMin, randomMax);
            }
        }

        if (loadingIndicator != null) loadingIndicator.SetActive(false);
        if (chargeGaugeFill != null) chargeGaugeFill.fillAmount = 0f;
    }

    void Update()
    {
        if (completed) return;

        bool allMatched = AllBandsMatched();

        if (loadingIndicator != null) loadingIndicator.SetActive(allMatched);

        if (allMatched)
        {
            chargeTimer += Time.deltaTime;
            if (chargeGaugeFill != null) chargeGaugeFill.fillAmount = chargeTimer / chargeDuration;

            if (chargeTimer >= chargeDuration)
            {
                Complete();
            }
        }
        else
        {
            chargeTimer = 0f;
            if (chargeGaugeFill != null) chargeGaugeFill.fillAmount = 0f;
        }
    }

    bool AllBandsMatched()
    {
        foreach (FrequencyBand band in bands)
        {
            if (band == null || !band.IsMatched) return false;
        }
        return true;
    }

    void Complete()
    {
        completed = true;
        if (loadingIndicator != null) loadingIndicator.SetActive(false);
        if (audioSource != null && frequencySound != null) audioSource.PlayOneShot(frequencySound);
        onFullyCharged?.Invoke();
    }
}
