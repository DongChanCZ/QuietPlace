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

    public UnityEvent onFullyCharged; // 충전까지 다 끝났을 때

    /// 네 대역이 처음으로 다 맞은 순간. 충전을 컴퓨터 앞에서 기다리지 않고
    /// 숨어서 기다리는 구성에서는 이 시점에 미니게임을 닫는다.
    public UnityEvent onAllBandsMatched;

    [Header("대역을 다 맞추면 그 자리에서 끝낸다 (충전은 월드에서)")]
    public bool finishOnMatch = true;

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

        // 맞춘 순간 끝내는 구성이면 충전을 돌리지 않고 바로 알린다.
        if (allMatched && finishOnMatch)
        {
            completed = true;
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            if (audioSource != null && frequencySound != null) audioSource.PlayOneShot(frequencySound);
            onAllBandsMatched?.Invoke();
            return;
        }

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
