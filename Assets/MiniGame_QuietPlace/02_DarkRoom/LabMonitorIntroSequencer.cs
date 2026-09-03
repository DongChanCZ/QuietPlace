using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class LabMonitorIntroSequencer : MonoBehaviour
{
    [Header("모니터 클로즈업 패널")]
    [SerializeField] private CanvasGroup monitorPanelGroup; // 모니터 텍스트가 있는 클로즈업 패널 전체
    [SerializeField] private TextMeshProUGUI monitorText; // 도트형 초록 CRT 폰트 할당
    [Tooltip("1번 초록 타이핑 화면처럼 한 줄씩 순서대로 타이핑됨")]
    [TextArea(1, 3)]
    [SerializeField]
    private string[] monitorLines = new string[]
    {
        "XX에 연구소가 있는데,",
        "괴물을 무력화시킬 수 있는 주파수를 연구하고 있다는",
        "소문을 들었어.",
        "성공했다는 이야기는 들었는데..."
    };

    [Header("타이핑 / 글리치 리빌 효과")]
    [Range(0.02f, 0.08f)]
    [SerializeField] private float typingInterval = 0.04f;
    [Tooltip("글자가 확정되기 전 랜덤 문자로 지지직거리는 프레임 수")]
    [Range(0, 6)]
    [SerializeField] private int glitchRevealFrames = 3;
    [SerializeField] private string glitchCharacterSet = "#$%&*+=<>?!ㅁㅇㅂㄱㅅ";
    [Tooltip("한 줄 다 찍힌 후 다음 줄로 넘어가기까지 대기 시간")]
    [Range(0.5f, 2f)]
    [SerializeField] private float waitAfterLine = 1.0f;

    [Header("모니터 전원 지지직 / 암전")]
    [SerializeField] private Graphic monitorScreenGraphic; // 화면 전체(RawImage 등) alpha 제어
    [Range(0.3f, 2f)]
    [SerializeField] private float powerFlickerDuration = 0.5f;
    [Range(0.05f, 0.3f)]
    [SerializeField] private float powerFlickerStep = 0.08f;
    [Tooltip("지지직 깜빡임의 밝기 범위 (너무 벌어지면 번쩍거림이 심해짐)")]
    [Range(0f, 1f)]
    [SerializeField] private float powerFlickerMinAlpha = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float powerFlickerMaxAlpha = 0.75f;

    [Header("와이드샷 이미지 (모니터 암전 후 방 전체 리빌)")]
    [Tooltip("어두운 방 전경 이미지 패널 (DarkRoom_WideShot 등)")]
    [SerializeField] private CanvasGroup wideShotGroup;
    [SerializeField] private RectTransform wideShotRect;
    [Range(0.5f, 3f)]
    [SerializeField] private float wideShotHoldDuration = 1.5f;
    [Tooltip("모니터에서 방 전체로 점점 멀어지는 줌아웃 연출 시간")]
    [Range(1f, 5f)]
    [SerializeField] private float zoomOutDuration = 2.5f;
    [Tooltip("줌아웃 시작 시점의 확대 배율 (모니터 화면에 가깝게 당겨진 상태)")]
    [Range(1.1f, 2.5f)]
    [SerializeField] private float zoomStartScale = 1.6f;
    [Tooltip("줌아웃 동안 모니터 클로즈업이 함께 작아지는 정도 (계속 뒤로 빠지는 느낌)")]
    [Range(0.4f, 1f)]
    [SerializeField] private float monitorEndScale = 0.6f;
    [Tooltip("줌아웃 전체 중 클로즈업->와이드샷 교차가 끝나는 지점 (0.4 = 앞쪽 40% 안에 교차 완료)")]
    [Range(0.15f, 1f)]
    [SerializeField] private float crossFadePortion = 0.42f;
    [Tooltip("줌아웃 시작 전에 클로즈업 글씨를 먼저 지우는 시간 (와이드샷에 그려진 글씨와 겹치지 않게)")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float textClearDuration = 0.45f;

    [Header("마지막 줄 깜빡임")]
    [SerializeField] private BlinkingText blinkingText;
    [Tooltip("줌아웃 시작 전에 마지막 줄이 깜빡이는 시간")]
    [Range(0f, 5f)]
    [SerializeField] private float blinkBeforeZoomDuration = 1.8f;

    [Header("방이 드러날 때 살아나는 것들")]
    [Tooltip("의자가 살짝 돌다가 멈추는 2프레임 애니메이션")]
    [SerializeField] private WideShotFrameAnimator wideShotFrameAnimator;
    [Tooltip("머그컵 김 등 방 안에서 계속 움직이는 연출 묶음")]
    [SerializeField] private GameObject roomAmbienceRoot;

    [Tooltip("모니터를 껐다가 줌아웃할지 여부. 와이드샷에 모니터가 켜져 있으면 꺼두세요.")]
    [SerializeField] private bool usePowerOffBeforeZoom = false;

    [Header("최종 페이드아웃")]
    [SerializeField] private CanvasGroup screenFadeGroup; // 검은 화면 전체 페이드용
    [Range(0.5f, 2f)]
    [SerializeField] private float fadeOutDuration = 1.2f;

    [Header("사운드")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] typingSounds;
    [SerializeField] private AudioClip[] glitchStaticSounds; // 지지직 노이즈
    [SerializeField] private AudioClip powerOffSound;

    [Header("완료 이벤트")]
    [SerializeField] private UnityEvent onIntroComplete;
    [Tooltip("이 단계가 끝나면 활성화할 다음 단계 오브젝트")]
    [SerializeField] private GameObject nextStage;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (screenFadeGroup != null) screenFadeGroup.alpha = 0f;
        if (monitorPanelGroup != null) monitorPanelGroup.alpha = 1f;

        if (wideShotGroup != null) wideShotGroup.alpha = 0f;
        if (wideShotRect != null) wideShotRect.localScale = Vector3.one;

        if (monitorPanelGroup != null) monitorPanelGroup.transform.localScale = Vector3.one;
        if (roomAmbienceRoot != null) roomAmbienceRoot.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        string accumulated = string.Empty;
        foreach (string line in monitorLines)
        {
            yield return TypeWithGlitch(accumulated, line);
            accumulated = string.IsNullOrEmpty(accumulated) ? line : accumulated + "\n" + line;
            monitorText.text = accumulated;
            yield return new WaitForSeconds(waitAfterLine);
        }

        // 다 찍힌 뒤 마지막 줄만 커서처럼 불규칙하게 깜빡임
        yield return BlinkLastLine();

        if (usePowerOffBeforeZoom)
            yield return PowerOffMonitor();

        // 검은 화면으로 끊지 않고, 모니터에서 점점 멀어지며 방 전체가 드러나는 줌아웃으로 자연스럽게 리빌
        yield return ZoomOutToRoom();

        yield return new WaitForSeconds(wideShotHoldDuration);

        yield return FadeToBlack();

        if (nextStage != null) nextStage.SetActive(true);

        yield return FadeFromBlack();

        onIntroComplete?.Invoke();
        gameObject.SetActive(false);
    }

    // prefix: 이미 화면에 쌓여있는 이전 줄들 (그대로 유지됨). line: 이번에 새로 타이핑할 줄.
    private IEnumerator TypeWithGlitch(string prefix, string line)
    {
        string prefixWithBreak = string.IsNullOrEmpty(prefix) ? string.Empty : prefix + "\n";
        var builder = new StringBuilder();

        foreach (char target in line)
        {
            if (glitchRevealFrames > 0 && !char.IsWhiteSpace(target))
            {
                for (int i = 0; i < glitchRevealFrames; i++)
                {
                    char glitchChar = glitchCharacterSet[Random.Range(0, glitchCharacterSet.Length)];
                    monitorText.text = prefixWithBreak + builder.ToString() + glitchChar;
                    yield return new WaitForSeconds(typingInterval * 0.5f);
                }
            }

            builder.Append(target);
            monitorText.text = prefixWithBreak + builder.ToString();

            if (!char.IsWhiteSpace(target))
                PlayRandomClip(typingSounds);

            yield return new WaitForSeconds(typingInterval);
        }
    }

    private IEnumerator PowerOffMonitor()
    {
        if (monitorScreenGraphic == null) yield break;

        float elapsed = 0f;
        while (elapsed < powerFlickerDuration)
        {
            monitorScreenGraphic.canvasRenderer.SetAlpha(Random.Range(powerFlickerMinAlpha, powerFlickerMaxAlpha));
            PlayRandomClip(glitchStaticSounds);
            yield return new WaitForSeconds(powerFlickerStep);
            elapsed += powerFlickerStep;
        }

        if (powerOffSound != null)
            audioSource.PlayOneShot(powerOffSound);

        monitorScreenGraphic.canvasRenderer.SetAlpha(0f);
        monitorText.text = string.Empty;
    }

    // 마지막 줄만 커서처럼 불규칙하게 깜빡이게 함
    private IEnumerator BlinkLastLine()
    {
        // blinkingText가 비어 있으면 깜빡임 없이, 다 찍힌 화면을 잠시 보여주기만 한다
        if (blinkingText != null && monitorLines != null && monitorLines.Length > 0)
        {
            int lastIndex = monitorLines.Length - 1;
            string prefix = lastIndex > 0 ? string.Join("\n", monitorLines, 0, lastIndex) : string.Empty;

            blinkingText.SetTarget(monitorText);
            blinkingText.Begin(prefix, monitorLines[lastIndex]);
        }

        yield return new WaitForSeconds(blinkBeforeZoomDuration);
    }

    private IEnumerator ZoomOutToRoom()
    {
        RectTransform monitorRect = monitorPanelGroup != null
            ? monitorPanelGroup.transform as RectTransform
            : null;

        if (wideShotGroup != null) wideShotGroup.alpha = 0f;
        if (wideShotRect != null) wideShotRect.localScale = Vector3.one * zoomStartScale;

        // 와이드샷에는 같은 글씨가 이미 그려져 있어서, 겹쳐 보이지 않도록
        // 줌아웃을 시작하기 전에 클로즈업 글씨를 먼저 완전히 지운다
        if (blinkingText != null) blinkingText.StopBlinking();
        if (monitorText != null)
        {
            float te = 0f;
            while (te < textClearDuration)
            {
                monitorText.alpha = 1f - Mathf.Clamp01(te / textClearDuration);
                te += Time.deltaTime;
                yield return null;
            }
            monitorText.alpha = 0f;
        }

        // 방이 드러나기 전부터 미리 돌려둬야 리빌되는 순간 이미 살아있는 것처럼 보임
        if (roomAmbienceRoot != null) roomAmbienceRoot.SetActive(true);
        if (wideShotFrameAnimator != null) wideShotFrameAnimator.BeginMotion();

        float elapsed = 0f;
        while (elapsed < zoomOutDuration)
        {
            float t = Mathf.Clamp01(elapsed / zoomOutDuration);

            // 뒤로 쭉 빠지다가 서서히 멈추는 카메라 느낌 (ease-out)
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            // 클로즈업 화면과 와이드샷을 같은 속도로 교차시켜 밝기가 튀지 않게 함
            float dissolve = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / crossFadePortion));

            if (monitorPanelGroup != null) monitorPanelGroup.alpha = 1f - dissolve;
            if (monitorRect != null) monitorRect.localScale = Vector3.one * Mathf.Lerp(1f, monitorEndScale, eased);
            if (wideShotGroup != null) wideShotGroup.alpha = dissolve;
            if (wideShotRect != null) wideShotRect.localScale = Vector3.one * Mathf.Lerp(zoomStartScale, 1f, eased);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (monitorPanelGroup != null) monitorPanelGroup.alpha = 0f;
        if (monitorRect != null) monitorRect.localScale = Vector3.one;
        if (monitorText != null) monitorText.alpha = 1f;
        if (wideShotGroup != null) wideShotGroup.alpha = 1f;
        if (wideShotRect != null) wideShotRect.localScale = Vector3.one;
    }

    private IEnumerator FadeToBlack()
    {
        if (screenFadeGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            screenFadeGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeOutDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        screenFadeGroup.alpha = 1f;
    }

    private IEnumerator FadeFromBlack()
    {
        if (screenFadeGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            screenFadeGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        screenFadeGroup.alpha = 0f;
    }

    private void PlayRandomClip(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) audioSource.PlayOneShot(clip);
    }
}
