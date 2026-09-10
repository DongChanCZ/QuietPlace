using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Esc 로 여는 일시정지 화면. 게임을 멈추고 옵션과 메인 메뉴로 갈 수 있다.
///
/// Esc 는 이미 여러 곳이 쓰고 있다 - 전체화면 쪽지 닫기, 은신처에서 나오기,
/// 미니게임 취소. 그래서 그런 것이 열려 있는 동안에는 이 메뉴가 뜨면 안 된다.
/// Blocked 가 그 판정이다.
///
/// 설정값은 GameSettings 에 저장한다. 타이틀 메뉴와 같은 저장소라 어느 쪽에서
/// 바꾸든 같이 반영된다.
///
/// 멈추는 것은 Time.timeScale = 0 이다. Update 는 timeScale 과 무관하게 계속 돌기
/// 때문에 플레이어 조작은 따로 잠근다. 커서도 PlayerController 가 Esc 로 토글하므로
/// 열려 있는 동안 LateUpdate 에서 매 프레임 되돌려 놓는다.
/// </summary>
[DefaultExecutionOrder(-400)]
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [Header("화면")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject mainRoot;      // 계속하기 / 옵션 / 메인 메뉴
    [SerializeField] private GameObject optionsRoot;   // 감도 / 음량 / 괴물소리 / 감지범위

    [Header("옵션 - 조작")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityLabel;

    [Header("옵션 - 소리")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeLabel;
    [SerializeField] private Slider monsterVolumeSlider;
    [SerializeField] private TMP_Text monsterVolumeLabel;

    /// 괴물 소리가 달린 오브젝트(GameManager/MonsterAmbience). 그 아래 AudioSource 를
    /// 전부 훑어 원래 볼륨에 슬라이더 값을 곱한다. 추격 소리는 첫 추격 때 런타임에
    /// 만들어지므로 미리 배선할 수 없다 - 그래서 배열이 아니라 루트를 받아 열 때마다 훑는다.
    [SerializeField] private Transform monsterAudioRoot;

    [Header("옵션 - 괴물 감지범위 표시")]
    [SerializeField] private Toggle monsterRangeToggle;
    [SerializeField] private TMP_Text monsterRangeLabel;
    [SerializeField] private MonsterHearing[] monsterHearings;

    [Header("플레이어 - 멈추는 동안 조작을 잠근다")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteractor interactor;

    [Header("메인 메뉴 씬 이름")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    public bool IsOpen { get; private set; }

    /// 슬라이더는 배수로 쓴다. 원래 볼륨을 기억해둬야 몇 번을 만져도 값이 흐르지 않는다.
    private readonly System.Collections.Generic.Dictionary<AudioSource, float> monsterBaseVolume
        = new System.Collections.Generic.Dictionary<AudioSource, float>();

    /// 지금 Esc 를 다른 곳이 쓰고 있는가. 하나라도 걸리면 메뉴를 열지 않는다.
    static bool Blocked =>
        MinigameAbort.AnyRunning
        || (FullscreenNoteViewer.Instance != null && FullscreenNoteViewer.Instance.IsOpen)
        || (PlayerDeathSequence.Instance != null && PlayerDeathSequence.Instance.IsPlaying)
        || (BreakerRaiseSequence.Instance != null && BreakerRaiseSequence.Instance.IsPlaying)
        || HidingSpotQTE.AnyoneHidden;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panelRoot != null) panelRoot.SetActive(false);

        // 저장해둔 설정을 불러와 그대로 적용한다
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        if (monsterVolumeSlider != null) monsterVolumeSlider.SetValueWithoutNotify(GameSettings.MonsterVolume);
        if (monsterRangeToggle != null) monsterRangeToggle.SetIsOnWithoutNotify(GameSettings.ShowMonsterRange);

        ApplySensitivity(GameSettings.MouseSensitivity);
        ApplyVolume(GameSettings.MasterVolume);
        ApplyMonsterVolume(GameSettings.MonsterVolume);
        ApplyMonsterRange(GameSettings.ShowMonsterRange);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(ApplySensitivity);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(ApplyVolume);
        if (monsterVolumeSlider != null) monsterVolumeSlider.onValueChanged.AddListener(ApplyMonsterVolume);
        if (monsterRangeToggle != null) monsterRangeToggle.onValueChanged.AddListener(ApplyMonsterRange);
    }

    void OnDisable()
    {
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.RemoveListener(ApplySensitivity);
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(ApplyVolume);
        if (monsterVolumeSlider != null) monsterVolumeSlider.onValueChanged.RemoveListener(ApplyMonsterVolume);
        if (monsterRangeToggle != null) monsterRangeToggle.onValueChanged.RemoveListener(ApplyMonsterRange);
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (IsOpen)
        {
            // 옵션을 보고 있었으면 한 단계만 뒤로
            if (optionsRoot != null && optionsRoot.activeSelf) ShowMain();
            else Close();
            return;
        }

        if (Blocked) return;
        Open();
    }

    /// PlayerController 가 Esc 로 커서를 토글해버리므로 열려 있는 동안 계속 되돌린다.
    void LateUpdate()
    {
        if (!IsOpen) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        if (panelRoot != null) panelRoot.SetActive(true);
        ShowMain();

        // 추격 소리처럼 나중에 생긴 소스도 여기서 잡힌다
        ApplyMonsterVolume(GameSettings.MonsterVolume);

        Time.timeScale = 0f;
        if (player != null) { player.InputLocked = true; player.CameraLocked = true; }
        if (interactor != null) interactor.enabled = false;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        if (panelRoot != null) panelRoot.SetActive(false);

        Time.timeScale = 1f;
        if (player != null) { player.InputLocked = false; player.CameraLocked = false; }
        if (interactor != null) interactor.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ShowMain()
    {
        if (mainRoot != null) { mainRoot.SetActive(true); Refresh(mainRoot); }
        if (optionsRoot != null) optionsRoot.SetActive(false);
    }

    public void ShowOptions()
    {
        if (mainRoot != null) mainRoot.SetActive(false);
        if (optionsRoot != null) { optionsRoot.SetActive(true); Refresh(optionsRoot); }
    }

    /// 꺼져 있던 TMP 텍스트는 켜지는 순간 메시가 비어 있을 때가 있다. 한 번 다시 그린다.
    static void Refresh(GameObject root)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++) texts[i].ForceMeshUpdate(true);
    }

    public void GoToMainMenu()
    {
        // 멈춘 채로 씬을 넘기면 다음 씬이 정지 상태로 시작한다.
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    // ---------------------------------------------------------------- 설정 적용

    public void ApplySensitivity(float value)
    {
        if (player != null) player.mouseSensitivity = value;
        if (sensitivityLabel != null) sensitivityLabel.text = value.ToString("0.00");
        GameSettings.MouseSensitivity = value;
    }

    public void ApplyVolume(float value)
    {
        AudioListener.volume = value;
        if (volumeLabel != null) volumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        GameSettings.MasterVolume = value;
    }

    public void ApplyMonsterVolume(float value)
    {
        if (monsterAudioRoot != null)
        {
            AudioSource[] list = monsterAudioRoot.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < list.Length; i++)
            {
                float baseVol;
                if (!monsterBaseVolume.TryGetValue(list[i], out baseVol))
                {
                    baseVol = list[i].volume;               // 처음 본 소스의 원래 볼륨을 기억한다
                    monsterBaseVolume[list[i]] = baseVol;
                }
                list[i].volume = baseVol * value;
            }
        }
        if (monsterVolumeLabel != null) monsterVolumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        GameSettings.MonsterVolume = value;
    }

    public void ApplyMonsterRange(bool on)
    {
        if (monsterHearings != null)
        {
            for (int i = 0; i < monsterHearings.Length; i++)
                if (monsterHearings[i] != null) monsterHearings[i].showRangeDisc = on;
        }
        if (monsterRangeLabel != null) monsterRangeLabel.text = on ? "표시" : "숨김";
        GameSettings.ShowMonsterRange = on;
    }
}
