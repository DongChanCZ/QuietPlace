using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 비밀번호 입력. 컴퓨터를 켜면 먼저 이걸 통과해야 다음 단계(주파수 맞추기)로 넘어간다.
///
/// candidates 를 채우면 그 중 하나를 무작위로 뽑는다. 2층 힌트가 "202?" 처럼 마지막 자리를
/// 읽기 어렵게 되어 있어서, 플레이어가 두 후보 중 어느 쪽인지 한 번 걸어보게 만드는 구성이다.
/// 뽑기는 Awake 에서 딱 한 번만 한다 - OnEnable 에서 뽑으면 틀리고 다시 열 때마다 정답이
/// 바뀌어서 힌트가 아무 의미도 없어진다.
///
/// 입력은 Keyboard.onTextInput 으로 받는다. InputField 를 쓰면 EventSystem 포커스를
/// 따로 관리해야 하는데, 미니게임 중에는 플레이어 입력을 이미 잠가둔 상태라 그럴 필요가 없다.
/// </summary>
public class PasswordEntry : MonoBehaviour
{
    [Header("정답 (candidates 가 비어 있을 때만 쓰인다)")]
    public string password = "2023";

    [Header("이 중 하나를 무작위로 뽑는다 (판당 한 번, 고정)")]
    public string[] candidates;

    [Header("한 번에 입력할 수 있는 최대 길이 - 0이면 정답 길이")]
    public int maxLength;

    [Header("틀렸을 때 이만큼 입력을 막는다(초)")]
    public float lockoutSeconds = 1.2f;

    [Header("UI")]
    public TMP_Text entryLabel;      // 입력한 값이 보이는 곳
    public TMP_Text messageLabel;    // 안내 / 오류 문구
    public string maskCharacter = ""; // 비우면 그대로 보여준다

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip keySound;
    public AudioClip wrongSound;
    public AudioClip correctSound;

    public UnityEvent onCorrect;

    /// 이번 판에 실제로 맞춰야 하는 값.
    public string ActivePassword => string.IsNullOrEmpty(active) ? password : active;

    private string active;
    private string typed = "";
    private bool solved;
    private float lockoutTimer;

    int Limit => maxLength > 0 ? maxLength : ActivePassword.Length;

    void Awake()
    {
        active = candidates != null && candidates.Length > 0
            ? candidates[Random.Range(0, candidates.Length)]
            : password;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[{nameof(PasswordEntry)}] 이번 판 비밀번호: {active}" +
                  (candidates != null && candidates.Length > 1 ? $" (후보 {candidates.Length}개 중)" : ""), this);
#endif
    }

    void OnEnable()
    {
        typed = "";
        solved = false;
        lockoutTimer = 0f;
        Refresh("비밀번호를 입력하고 Enter");
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
    }

    void Update()
    {
        if (solved) return;

        if (lockoutTimer > 0f)
        {
            lockoutTimer -= Time.deltaTime;
            if (lockoutTimer <= 0f) Refresh("비밀번호를 입력하고 Enter");
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.backspaceKey.wasPressedThisFrame && typed.Length > 0)
        {
            typed = typed.Substring(0, typed.Length - 1);
            Refresh(null);
            return;
        }

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Submit();
    }

    void OnTextInput(char c)
    {
        if (solved || lockoutTimer > 0f) return;
        if (c == '\n' || c == '\r' || c == '\b') return;   // Enter/Backspace 는 Update 에서 처리
        if (char.IsControl(c)) return;
        if (typed.Length >= Limit) return;

        typed += c;
        Play(keySound);
        Refresh(null);

        // 길이가 다 차면 Enter 없이도 판정한다 - 숫자 코드는 그게 자연스럽다
        if (typed.Length >= Limit) Submit();
    }

    void Submit()
    {
        if (solved || typed.Length == 0) return;

        if (typed == ActivePassword)
        {
            solved = true;
            Play(correctSound);
            Refresh("접속 허가");
            onCorrect?.Invoke();
            return;
        }

        typed = "";
        lockoutTimer = lockoutSeconds;
        Play(wrongSound);
        Refresh("비밀번호가 틀렸습니다");
    }

    void Refresh(string message)
    {
        if (entryLabel != null)
        {
            entryLabel.text = string.IsNullOrEmpty(maskCharacter)
                ? typed
                : new string(maskCharacter[0], typed.Length);
        }

        if (messageLabel != null && message != null) messageLabel.text = message;
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
