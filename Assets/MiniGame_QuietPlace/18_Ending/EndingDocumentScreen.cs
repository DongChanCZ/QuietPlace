using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// USB 로 자료를 받는 동안 모니터에 뜨는 기밀문서 화면.
///
/// 플레이어가 "여기가 괴물을 만든 곳이었다"를 스스로 읽어내는 구간이라, 한 번에 다 띄우지
/// 않고 한 줄씩 출력한다. 다 읽을 시간을 주는 것이 목적이므로 글자 단위가 아니라 줄 단위로
/// 넘긴다 - 글자 단위 타이핑은 읽는 속도를 오히려 방해한다.
///
/// 화면 자체는 스크린 스페이스 캔버스다. 모니터 표면에 월드 캔버스로 붙이면 각도에 따라
/// 글씨가 안 읽히고, 이 구간은 "플레이어가 화면을 들여다보는" 컷이라 화면을 채우는 편이 맞다.
/// </summary>
[DisallowMultipleComponent]
public class EndingDocumentScreen : MonoBehaviour
{
    [Header("화면")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup group;

    [Header("글")]
    [SerializeField] private TMP_Text header;
    [SerializeField] private TMP_Text body;

    [Header("다운로드 게이지")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressLabel;

    [Header("모니터 불빛 - 플레이어 얼굴에 반사되는 빛")]
    [SerializeField] private Light screenLight;
    [SerializeField] private float screenLightIntensity = 2.4f;
    [SerializeField] private Color screenLightColor = new Color(0.42f, 0.62f, 1f);

    [Header("타이핑 소리")]
    [SerializeField] private AudioClip typingClip;
    [SerializeField] private AudioSource typingSource;

    [Header("문서 제목")]
    [SerializeField] private string headerText = "CLASSIFIED  //  LEVEL 4  —  NCAI BUSAN BIOLAB";

    [Header("한 줄씩 출력할 내용")]
    [SerializeField]
    private string[] lines =
    {
        "> USB 연결됨.  자료 전송 시작...",
        "",
        "[프로젝트: 데스 엔젤(DEATH ANGEL)]",
        "외계 생명체 무기화 연구 보고서",
        "",
        "표본 출처 : 낙하 운석 내부 회수 개체 (B1 격리)",
        "감각 구조 : 시각 없음 / 청각 의존 — 반경 8km 진동 탐지",
        "취약점   : 고주파 음역 노출 시 감각기관 마비",
        "",
        "> 배양 3세대 개체 12기 중 11기 폐기.",
        "> 잔존 1기 : B1 격리실. 상태 — 부화 대기.",
        "",
        "> 전송 완료. 이 시설의 모든 개체는 이곳에서 만들어졌다.",
    };

    [Tooltip("줄 사이 간격(초)")]
    [SerializeField] private float lineInterval = 0.42f;
    [SerializeField] private float fadeSeconds = 0.35f;

    public bool IsShowing { get; private set; }

    // 클립 이름은 코드에 둔다 - 병합으로 사라지지 않는다.
    const string TypingName = "Typing_LabMonitor";

    void Awake()
    {
        typingClip = AudioLibrary.Resolve(typingClip, TypingName);
        typingSource = AudioLibrary.EnsureSource(typingSource, transform, "DocTypingSource", 0f, 0f, 0f, 0.55f);
        if (typingSource != null) typingSource.loop = true;

        Hide();
    }

    private void Hide()
    {
        IsShowing = false;
        if (group != null) group.alpha = 0f;
        if (root != null) root.SetActive(false);
        if (screenLight != null) screenLight.intensity = 0f;
    }

    /// <summary>
    /// 자료를 받는 동안 문서를 한 줄씩 띄운다. 게이지는 seconds 동안 0 에서 100% 로 찬다.
    /// </summary>
    public IEnumerator Play(float seconds)
    {
        IsShowing = true;

        if (header != null) header.text = headerText;
        if (body != null) body.text = "";
        if (progressFill != null) progressFill.fillAmount = 0f;
        if (progressLabel != null) progressLabel.text = "USB TRANSFER   0%";

        if (screenLight != null)
        {
            screenLight.color = screenLightColor;
            screenLight.intensity = screenLightIntensity;
        }

        if (root != null) root.SetActive(true);
        yield return Fade(0f, 1f);

        if (typingSource != null && typingClip != null)
        {
            typingSource.clip = typingClip;
            typingSource.Play();
        }

        // 줄이 다 나오는 데 걸리는 시간이 게이지가 차는 시간을 넘지 않게 맞춘다.
        float perLine = lines.Length > 0 ? Mathf.Min(lineInterval, seconds / lines.Length) : lineInterval;

        var text = new System.Text.StringBuilder();
        float elapsed = 0f;
        int shown = 0;

        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;

            int want = Mathf.Min(lines.Length, Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, perLine)));
            while (shown < want)
            {
                text.AppendLine(lines[shown]);
                shown++;
            }
            if (body != null && shown > 0) body.text = text.ToString();

            float p = Mathf.Clamp01(elapsed / seconds);
            if (progressFill != null) progressFill.fillAmount = p;
            if (progressLabel != null) progressLabel.text = "USB TRANSFER   " + Mathf.FloorToInt(p * 100f) + "%";

            yield return null;
        }

        // 남은 줄은 마지막에 몰아서라도 다 보여준다
        while (shown < lines.Length) { text.AppendLine(lines[shown]); shown++; }
        if (body != null) body.text = text.ToString();
        if (progressFill != null) progressFill.fillAmount = 1f;
        if (progressLabel != null) progressLabel.text = "USB TRANSFER   COMPLETE";

        if (typingSource != null) typingSource.Stop();
    }

    /// <summary>화면을 닫는다.</summary>
    public IEnumerator Close()
    {
        yield return Fade(1f, 0f);
        Hide();
    }

    private IEnumerator Fade(float from, float to)
    {
        if (group == null || fadeSeconds <= 0f)
        {
            if (group != null) group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }
        group.alpha = to;
    }
}
