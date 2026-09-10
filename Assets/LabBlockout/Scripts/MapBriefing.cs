using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 1층 안내도를 펼쳤을 때 다음에 갈 곳을 짚어 주는 짧은 브리핑.
///
/// 지도가 뜨고 잠시 뒤 자막이 흐르면서 발전기실과 방송통제실에 빨간 동그라미가
/// 차례로 그려지고, 곧바로 지도가 닫힌다. 쫓기는 상황이라 길게 붙잡지 않는다.
///
/// 동그라미는 지도 이미지(NoteImage)의 자식이고 앵커로 자리를 잡는다.
/// 화면 크기나 지도 크기가 바뀌어도 방 위에 그대로 붙어 있다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MapBriefing : MonoBehaviour
{
    public static MapBriefing Instance { get; private set; }

    [Header("지도 위에 그릴 동그라미 (순서대로 뜬다)")]
    [SerializeField] RectTransform[] circles;

    [Header("자막")]
    [SerializeField] TMP_Text subtitle;

    /// 검은 자막이 보이려면 밝은 판이 뒤에 있어야 한다. 자막과 같이 켜고 끈다.
    [SerializeField] GameObject subtitlePlate;
    [SerializeField, TextArea] string line =
        "2층 발전기실에서 발전기를 켜고, 3층 방송통제실에서 주파수를 얻으면 밖으로 신호를 보낼 수 있다.";

    [Header("속도 - 긴박한 장면이라 짧게 끊는다 (초)")]
    [SerializeField] float startDelay = 0.35f;
    [SerializeField] float circleGap = 0.28f;
    [SerializeField] float circlePopSeconds = 0.18f;
    [SerializeField] float holdSeconds = 1.9f;

    [Tooltip("브리핑이 끝나면 지도를 저절로 닫는다")]
    [SerializeField] bool closeWhenDone = true;

    bool playing;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        HideAll();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void HideAll()
    {
        if (circles != null)
            foreach (RectTransform c in circles)
                if (c != null) c.gameObject.SetActive(false);
        if (subtitle != null) subtitle.gameObject.SetActive(false);
        if (subtitlePlate != null) subtitlePlate.SetActive(false);
    }

    /// 지도를 펼친 NoteBoard 가 부른다.
    public void Play()
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        HideAll();
        yield return WaitUnscaled(startDelay);

        if (subtitlePlate != null) subtitlePlate.SetActive(true);
        if (subtitle != null)
        {
            subtitle.text = line;
            subtitle.gameObject.SetActive(true);
        }

        if (circles != null)
        {
            foreach (RectTransform c in circles)
            {
                if (c == null) continue;
                c.gameObject.SetActive(true);

                // 툭 던져 그린 것처럼 크게 시작해 제자리로 줄어든다
                float t = 0f;
                while (t < circlePopSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / circlePopSeconds);
                    float s = Mathf.Lerp(1.6f, 1f, k * k);
                    c.localScale = new Vector3(s, s, 1f);
                    yield return null;
                }
                c.localScale = Vector3.one;

                yield return WaitUnscaled(circleGap);
            }
        }

        playing = false;

        // 자막과 동그라미는 지도가 닫힐 때까지 그대로 둔다. 지도는 클릭이나 E 로 닫는다.
        // (자동으로 넘기고 싶으면 closeWhenDone 을 켠다)
        if (!closeWhenDone) yield break;

        yield return WaitUnscaled(holdSeconds);
        HideAll();
        if (FullscreenNoteViewer.Instance != null && FullscreenNoteViewer.Instance.IsOpen)
            FullscreenNoteViewer.Instance.Close();
    }

    /// 일시정지(timeScale 0) 중에도 흘러야 하므로 unscaled 로 센다.
    static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }
}
