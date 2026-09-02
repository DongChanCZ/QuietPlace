using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class LoadingBridgeSequencer : MonoBehaviour
{
    [Header("브릿지 이미지 (숲 속 연구소, 빛바랜/흑백)")]
    [SerializeField] private CanvasGroup imageGroup;

    [Header("타이밍")]
    [Range(0.5f, 2f)]
    [SerializeField] private float fadeInDuration = 1f;
    [Range(1f, 5f)]
    [SerializeField] private float holdDuration = 2.5f;
    [Range(0.5f, 2f)]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("사운드 (숲을 헤쳐 지나가는 소리 등)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip ambientForestSound;
    [Tooltip("클립이 화면 시간보다 짧아도 끊기지 않도록 반복 재생")]
    [SerializeField] private bool loopAmbient = true;
    [Range(0f, 1f)]
    [SerializeField] private float ambientVolume = 0.7f;

    [Header("완료 이벤트")]
    [SerializeField] private UnityEvent onBridgeComplete;
    [Tooltip("이 단계가 끝나면 활성화할 다음 단계 오브젝트")]
    [SerializeField] private GameObject nextStage;
    [Tooltip("체크하면 이 화면이 사라지는 동안 다음 화면이 동시에 나타남 (빈 화면 없이 자연스럽게 전환)")]
    [SerializeField] private bool crossfadeToNextStage = true;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (imageGroup != null) imageGroup.alpha = 0f;
    }

    private void Start()
    {
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        if (ambientForestSound != null && audioSource != null)
        {
            audioSource.clip = ambientForestSound;
            audioSource.loop = loopAmbient;
            audioSource.volume = ambientVolume;
            audioSource.Play();
        }

        yield return Fade(0f, 1f, fadeInDuration);
        yield return new WaitForSeconds(holdDuration);

        // 이 화면이 사라지기 전에 다음 화면을 먼저 켜서, 두 페이드가 겹치게 함 (빈 화면 방지)
        if (crossfadeToNextStage && nextStage != null) nextStage.SetActive(true);

        // 화면이 사라지는 동안 소리도 같이 잦아들게 함
        StartCoroutine(FadeOutAudio(fadeOutDuration));

        yield return Fade(1f, 0f, fadeOutDuration);

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.volume = ambientVolume;
        }

        onBridgeComplete?.Invoke();

        if (!crossfadeToNextStage && nextStage != null) nextStage.SetActive(true);
        gameObject.SetActive(false);
    }

    private IEnumerator FadeOutAudio(float duration)
    {
        if (audioSource == null || duration <= 0f) yield break;

        float startVolume = audioSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        audioSource.volume = 0f;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (imageGroup == null) yield break;

        float elapsed = 0f;
        imageGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            imageGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        imageGroup.alpha = to;
    }
}
