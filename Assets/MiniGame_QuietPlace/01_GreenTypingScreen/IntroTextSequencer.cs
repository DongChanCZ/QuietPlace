using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro; // TextMeshPro 패키지가 없다면 이 줄과 아래 TMP 참조 부분을 지워주세요

[RequireComponent(typeof(AudioSource))]
public class IntroTextSequencer : MonoBehaviour
{
    [Header("텍스트 컴포넌트 (TMP 우선, 없으면 Legacy Text 사용)")]
    [SerializeField] private TextMeshProUGUI tmpText;
    [SerializeField] private Text legacyText;

    [Header("페이드 처리용 CanvasGroup")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("출력 문구 (순서대로)")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] lines = new string[]
    {
        "20XX년, 소리를 감지하는 괴물에게 인류는 멸망 직전까지 내몰렸다...",
        "살아남기 위해...",
        "침묵해야만 한다....."
    };

    [Header("타이밍 설정")]
    [Tooltip("글자 한 개당 출력 간격")]
    [Range(0.03f, 0.08f)]
    [SerializeField] private float typingInterval = 0.05f;

    [Tooltip("한 줄 다 찍힌 후 대기 시간")]
    [Range(0.8f, 1.5f)]
    [SerializeField] private float waitAfterLine = 1.2f;

    [Header("글리치 리빌 효과 (2번 모니터 화면과 동일한 느낌)")]
    [Tooltip("글자가 확정되기 전 랜덤 문자로 지지직거리는 프레임 수")]
    [Range(0, 6)]
    [SerializeField] private int glitchRevealFrames = 3;
    [SerializeField] private string glitchCharacterSet = "#$%&*+=<>?!ㅁㅇㅂㄱㅅ";

    [Tooltip("CanvasGroup 페이드 인/아웃 시간")]
    [Range(0.5f, 1f)]
    [SerializeField] private float fadeDuration = 0.75f;

    [Header("타건 사운드")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("1개만 넣어도 되고, 여러 개 넣으면 매 글자마다 랜덤 재생")]
    [SerializeField] private AudioClip[] typingSounds;
    [Range(0f, 1f)]
    [SerializeField] private float typingSoundVolume = 1f;

    [Header("스킵 기능 (선택)")]
    [SerializeField] private bool allowSkip = true;

    [Header("인트로 종료 후 처리")]
    [SerializeField] private UnityEvent onIntroComplete;
    [Tooltip("이 단계가 끝나면 활성화할 다음 단계 오브젝트")]
    [SerializeField] private GameObject nextStage;

    private bool _skipRequested;
    private bool _introFinished;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        SetDisplayText(string.Empty);
    }

    private void Start()
    {
        StartCoroutine(PlaySequence());
    }

    private void Update()
    {
        if (!allowSkip || _introFinished) return;

        if (AnyInputThisFrame())
            _skipRequested = true;
    }

    // 새 Input System 기준 "아무 키/클릭" 판정 (팀 프로젝트 병합 시 Player Settings에 의존하지 않도록)
    private static bool AnyInputThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            return true;

        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
            return true;

        return false;
    }

    private IEnumerator PlaySequence()
    {
        foreach (string line in lines)
        {
            _skipRequested = false;
            SetDisplayText(string.Empty);

            yield return Fade(0f, 1f, fadeDuration);

            _skipRequested = false;
            yield return TypeLine(line);

            _skipRequested = false;
            yield return WaitWithSkip(waitAfterLine);

            yield return Fade(1f, 0f, fadeDuration);
        }

        _introFinished = true;
        onIntroComplete?.Invoke();

        if (nextStage != null) nextStage.SetActive(true);
        gameObject.SetActive(false);
    }

    private IEnumerator TypeLine(string line)
    {
        var builder = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            if (_skipRequested)
            {
                SetDisplayText(line);
                yield break;
            }

            char c = line[i];

            if (glitchRevealFrames > 0 && !char.IsWhiteSpace(c))
            {
                for (int g = 0; g < glitchRevealFrames; g++)
                {
                    if (_skipRequested)
                    {
                        SetDisplayText(line);
                        yield break;
                    }

                    char glitchChar = glitchCharacterSet[Random.Range(0, glitchCharacterSet.Length)];
                    SetDisplayText(builder.ToString() + glitchChar);
                    yield return new WaitForSeconds(typingInterval * 0.5f);
                }
            }

            builder.Append(c);
            SetDisplayText(builder.ToString());

            if (!char.IsWhiteSpace(c))
                PlayTypingSound();

            yield return new WaitForSeconds(typingInterval);
        }
    }

    private IEnumerator WaitWithSkip(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_skipRequested)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null)
            yield break;

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            // 페이드 중에는 스킵으로 끊지 않고 자연스럽게 완료시킴
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void PlayTypingSound()
    {
        if (audioSource == null || typingSounds == null || typingSounds.Length == 0)
            return;

        AudioClip clip = typingSounds[Random.Range(0, typingSounds.Length)];
        if (clip != null)
            audioSource.PlayOneShot(clip, typingSoundVolume);
    }

    private void SetDisplayText(string value)
    {
        if (tmpText != null)
            tmpText.text = value;
        else if (legacyText != null)
            legacyText.text = value;
    }
}
