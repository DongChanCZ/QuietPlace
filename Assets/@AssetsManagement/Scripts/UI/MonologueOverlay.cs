using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 아래에 혼잣말 한 줄을 띄운다.
///
/// 연출마다 자막 UI 를 따로 만들면 폰트/위치/페이드가 조금씩 어긋난다. 자막은 한 곳에서만
/// 관리하고, 대사를 넘기는 쪽(변압기 완료, 퓨즈만 완료 등)은 문장만 넘긴다.
///
/// Time.timeScale 을 건드리지 않는다 - 연출 중에도 괴물은 계속 돌아다녀야 하고,
/// timeScale 이 0 이면 이 코루틴도 같이 멈춘다.
/// </summary>
[DefaultExecutionOrder(-900)]
public class MonologueOverlay : MonoBehaviour
{
    public static MonologueOverlay Instance { get; private set; }

    [Header("자막")]
    [SerializeField] private TMP_Text line;
    [SerializeField] private CanvasGroup group;

    [Header("페이드 시간(초)")]
    [SerializeField] private float fadeSeconds = 0.45f;

    [Header("따로 지정하지 않았을 때 머무는 시간(초)")]
    [SerializeField] private float defaultHoldSeconds = 4.2f;

    public bool IsShowing { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Hide()
    {
        if (group == null) return;
        group.alpha = 0f;
        group.gameObject.SetActive(false);
    }

    /// <summary>
    /// 한 줄 띄우고, 다 사라질 때까지 기다린다. 부르는 쪽에서 yield return 으로 쓴다.
    /// </summary>
    /// <param name="text">띄울 문장</param>
    /// <param name="holdSeconds">머무는 시간. 0 이하면 기본값을 쓴다.</param>
    public IEnumerator Show(string text, float holdSeconds = 0f)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        float hold = holdSeconds > 0f ? holdSeconds : defaultHoldSeconds;

        IsShowing = true;
        if (line != null) line.text = text;

        if (group != null)
        {
            group.gameObject.SetActive(true);
            yield return Fade(0f, 1f);
        }

        if (hold > 0f) yield return new WaitForSeconds(hold);

        if (group != null)
        {
            yield return Fade(1f, 0f);
            group.gameObject.SetActive(false);
        }

        IsShowing = false;
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
