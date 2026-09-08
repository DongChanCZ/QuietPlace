using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 키패드 회로 조작 - 영어 빨리치기.
///
/// 글자가 하나씩 위에서 떨어진다. 바닥선에 닿기 전에 그 글자를 쳐야 하고, 놓치거나
/// 틀리면 처음부터다. 정답 문자열은 화면에 보여주지 않는다 - 떨어지는 글자를 그 순간에
/// 읽고 치는 것이 이 미니게임의 내용이다.
///
/// 성공할수록 낙하 속도가 올라가서 뒤로 갈수록 여유가 없어진다.
/// </summary>
public class DoorHackTyping : MonoBehaviour
{
    [Header("패스워드")]
    public string targetPassword = "NCAIVibeCodingBusan";

    /// 대소문자를 구분하면 Shift 를 같이 눌러야 대문자가 입력된다. onTextInput 은
    /// 실제로 찍힌 문자를 주므로 N 키만 누르면 'n' 이 온다. 급하게 치는 게임에서
    /// Shift 까지 요구하면 그냥 짜증나는 조작이 되므로 기본은 구분하지 않는다.
    [Header("대소문자 구분 (끄면 N 키만 눌러도 N 으로 인정)")]
    public bool caseSensitive = false;

    [Header("낙하 글자")]
    public RectTransform lettersArea;
    public GameObject letterPrefab;
    public float fallSpeed = 700f;

    [Header("한 글자 맞출 때마다 빨라지는 양")]
    public float fallSpeedRampPerHit = 26f;

    public float topY = 450f;
    public float bottomY = -450f;
    public float leftX = -780f;
    public float rightX = 780f;
    public int fontSize = 96;

    [Header("아래쪽 이 비율 구간에 들어오면 붉어지며 커진다")]
    [Range(0f, 1f)] public float dangerZoneRatio = 0.4f;
    public Color safeColor = new Color(0.6f, 1f, 0.65f);
    public Color dangerColor = new Color(1f, 0.35f, 0.3f);

    [Header("진행 표시 (몇 글자째인지만 - 정답은 보여주지 않는다)")]
    public TMP_Text progressLabel;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip stage2AlertSound; // 놓치거나 틀렸을 때 나는 2단계 경보음

    public UnityEvent onHackSuccess;

    private int currentIndex;
    private RectTransform currentLetterRect;
    private TMP_Text currentLetterText;
    private bool completed;
    private float baseFallSpeed;
    private float currentFallSpeed;

    void OnEnable()
    {
        currentIndex = 0;
        completed = false;
        baseFallSpeed = fallSpeed;
        currentFallSpeed = fallSpeed;
        SpawnNextLetter();
        RefreshProgress();
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;

        // 다음에 다시 열 때 이전 글자가 남아 있으면 안 된다
        if (currentLetterRect != null) Destroy(currentLetterRect.gameObject);
        currentLetterRect = null;
        currentLetterText = null;
        fallSpeed = baseFallSpeed > 0f ? baseFallSpeed : fallSpeed;
    }

    void Update()
    {
        if (completed || currentLetterRect == null) return;

        Vector2 pos = currentLetterRect.anchoredPosition;
        pos.y -= currentFallSpeed * Time.deltaTime;
        currentLetterRect.anchoredPosition = pos;

        UpdateDangerLook(pos.y);

        if (pos.y <= bottomY) Miss();
    }

    /// 바닥에 가까워질수록 붉어지고 조금 커진다. 남은 시간을 숫자 없이 알려준다.
    void UpdateDangerLook(float y)
    {
        if (currentLetterText == null) return;

        float span = Mathf.Max(1f, topY - bottomY);
        float remaining = Mathf.Clamp01((y - bottomY) / span);          // 1 위 -> 0 바닥
        float danger = Mathf.Clamp01(1f - remaining / Mathf.Max(0.01f, dangerZoneRatio));

        currentLetterText.color = Color.Lerp(safeColor, dangerColor, danger);
        currentLetterRect.localScale = Vector3.one * (1f + danger * 0.35f);
    }

    void OnTextInput(char c)
    {
        if (completed || currentLetterRect == null) return;

        if (Matches(c, targetPassword[currentIndex]))
        {
            Destroy(currentLetterRect.gameObject);
            currentLetterRect = null;
            currentLetterText = null;
            PlaySound(hitSound);
            currentIndex++;

            // 뒤로 갈수록 빨라진다
            currentFallSpeed += fallSpeedRampPerHit;

            if (currentIndex >= targetPassword.Length)
            {
                completed = true;
                RefreshProgress();
                onHackSuccess?.Invoke();
                return;
            }

            SpawnNextLetter();
            RefreshProgress();
            return;
        }

        Miss();
    }

    bool Matches(char typed, char expected)
    {
        return caseSensitive
            ? typed == expected
            : char.ToLowerInvariant(typed) == char.ToLowerInvariant(expected);
    }

    void Miss()
    {
        if (currentLetterRect != null) Destroy(currentLetterRect.gameObject);
        currentLetterRect = null;
        currentLetterText = null;
        currentIndex = 0;
        currentFallSpeed = baseFallSpeed;   // 속도도 처음으로
        PlaySound(stage2AlertSound);
        SpawnNextLetter();
        RefreshProgress();
    }

    void SpawnNextLetter()
    {
        GameObject go = letterPrefab != null ? Instantiate(letterPrefab, lettersArea) : CreateDefaultLetterObject();

        // 템플릿은 씬에서 꺼둔 채로 쓰기 때문에 복제본도 꺼진 상태로 나온다. 켜줘야 보인다.
        go.SetActive(true);

        currentLetterRect = go.GetComponent<RectTransform>();
        currentLetterText = go.GetComponentInChildren<TMP_Text>();

        float t = targetPassword.Length <= 1 ? 0f : (float)currentIndex / (targetPassword.Length - 1);
        float x = Mathf.Lerp(leftX, rightX, t);
        currentLetterRect.anchoredPosition = new Vector2(x, topY);
        currentLetterRect.localScale = Vector3.one;

        if (currentLetterText != null)
        {
            currentLetterText.text = targetPassword[currentIndex].ToString();
            currentLetterText.color = safeColor;
        }
    }

    void RefreshProgress()
    {
        if (progressLabel == null) return;
        progressLabel.text = currentIndex + " / " + targetPassword.Length;
    }

    GameObject CreateDefaultLetterObject()
    {
        GameObject go = new GameObject("FallingLetter", typeof(RectTransform));
        go.transform.SetParent(lettersArea, false);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = safeColor;

        return go;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
