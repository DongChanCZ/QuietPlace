using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using TMPro;

public class DoorHackTyping : MonoBehaviour
{
    [Header("패스워드 (대소문자 구분)")]
    public string targetPassword = "NCAIVibeCodingBusan";

    [Header("낙하 글자")]
    public RectTransform lettersArea;
    public GameObject letterPrefab;
    public float fallSpeed = 800f;
    public float topY = 450f;
    public float bottomY = -450f;
    public float leftX = -800f;
    public float rightX = 800f;
    public int fontSize = 60;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip stage2AlertSound; // 놓치거나 틀렸을 때 나는 2단계 경보음

    public UnityEvent onHackSuccess;

    private int currentIndex;
    private RectTransform currentLetterRect;
    private TMP_Text currentLetterText;
    private bool completed;

    void OnEnable()
    {
        currentIndex = 0;
        completed = false;
        SpawnNextLetter();
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
    }

    void Update()
    {
        if (completed || currentLetterRect == null) return;

        Vector2 pos = currentLetterRect.anchoredPosition;
        pos.y -= fallSpeed * Time.deltaTime;
        currentLetterRect.anchoredPosition = pos;

        if (pos.y <= bottomY)
        {
            Miss();
        }
    }

    void OnTextInput(char c)
    {
        if (completed || currentLetterRect == null) return;

        if (c == targetPassword[currentIndex])
        {
            Destroy(currentLetterRect.gameObject);
            currentLetterRect = null;
            PlaySound(hitSound);
            currentIndex++;

            if (currentIndex >= targetPassword.Length)
            {
                completed = true;
                onHackSuccess?.Invoke();
            }
            else
            {
                SpawnNextLetter();
            }
        }
        else
        {
            Miss();
        }
    }

    void Miss()
    {
        if (currentLetterRect != null) Destroy(currentLetterRect.gameObject);
        currentLetterRect = null;
        currentIndex = 0;
        PlaySound(stage2AlertSound);
        SpawnNextLetter();
    }

    void SpawnNextLetter()
    {
        GameObject go = letterPrefab != null ? Instantiate(letterPrefab, lettersArea) : CreateDefaultLetterObject();
        currentLetterRect = go.GetComponent<RectTransform>();
        currentLetterText = go.GetComponentInChildren<TMP_Text>();

        float t = targetPassword.Length <= 1 ? 0f : (float)currentIndex / (targetPassword.Length - 1);
        float x = Mathf.Lerp(leftX, rightX, t);
        currentLetterRect.anchoredPosition = new Vector2(x, topY);

        if (currentLetterText != null) currentLetterText.text = targetPassword[currentIndex].ToString();
    }

    GameObject CreateDefaultLetterObject()
    {
        GameObject go = new GameObject("FallingLetter", typeof(RectTransform));
        go.transform.SetParent(lettersArea, false);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        return go;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
