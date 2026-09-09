using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("씬 전환")]
    public string gameSceneName = "Intro_Cutscene";

    [Header("메인 버튼")]
    public Button playButton;
    public Button optionsButton;
    public Button difficultyButton;
    public Button exitButton;

    [Header("옵션 패널")]
    public GameObject optionsPanel;
    public Button optionsCloseButton;
    public Slider volumeSlider;
    public Slider brightnessSlider;
    public Image brightnessOverlay;

    [Header("난이도 패널 (Easy/Expert는 미구현)")]
    public GameObject difficultyPanel;
    public Button difficultyCloseButton;
    public Button easyButton;
    public Button normalButton;
    public Button expertButton;
    public TextMeshProUGUI difficultyLabel;

    void Awake()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (difficultyPanel != null) difficultyPanel.SetActive(false);

        if (playButton != null) playButton.onClick.AddListener(OnPlay);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOpenOptions);
        if (difficultyButton != null) difficultyButton.onClick.AddListener(OnOpenDifficulty);
        if (exitButton != null) exitButton.onClick.AddListener(OnExit);

        if (optionsCloseButton != null) optionsCloseButton.onClick.AddListener(() => optionsPanel.SetActive(false));
        if (difficultyCloseButton != null) difficultyCloseButton.onClick.AddListener(() => difficultyPanel.SetActive(false));

        // 보통 / 어려움 둘 다 구현됐다. 미니게임 구성은 MinigameTuning 이 난이도별로 갈라준다.
        //   보통  : 볼트 2바퀴, 색선 생략, 방전 Space 2번, 차단기 올리기만, 키패드 NCAIBusan(대소문자 무시), 컴퓨터 비밀번호 없음
        //   어려움: 볼트 3바퀴, 색선 4개, 방전 R/S/T 3번, 차단기 내리기+올리기, NCAIVibeCodingBusan(대소문자 구분), 비밀번호 있음
        // 쉬움은 아직 별도 구성이 없어 낙하 속도만 느려진다.
        if (easyButton != null) easyButton.interactable = false;
        if (normalButton != null) normalButton.onClick.AddListener(() => SetDifficulty(Difficulty.Normal));
        if (expertButton != null)
        {
            expertButton.interactable = true;
            expertButton.onClick.AddListener(() => SetDifficulty(Difficulty.Expert));
        }

        if (volumeSlider != null)
        {
            volumeSlider.value = GameSettings.MasterVolume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.value = GameSettings.Brightness;
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }

        AudioListener.volume = GameSettings.MasterVolume;
        ApplyBrightness(GameSettings.Brightness);
        UpdateDifficultyLabel();
    }

    void OnPlay()
    {
        // 새 게임이므로 지난 세이브포인트를 지운다. 이게 없으면 메뉴에서 시작해도
        // 지난 판에서 성공한 지점부터 시작한다.
        GameProgress.Clear();

        SceneManager.LoadScene(gameSceneName);
    }

    void OnOpenOptions()
    {
        if (difficultyPanel != null) difficultyPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    void OnOpenDifficulty()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (difficultyPanel != null) difficultyPanel.SetActive(true);
    }

    void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        GameSettings.MasterVolume = value;
    }

    void OnBrightnessChanged(float value)
    {
        GameSettings.Brightness = value;
        ApplyBrightness(value);
    }

    void ApplyBrightness(float value)
    {
        if (brightnessOverlay == null) return;
        float darkness = Mathf.Lerp(0.8f, 0f, value);
        Color c = brightnessOverlay.color;
        c.a = darkness;
        brightnessOverlay.color = c;
    }

    void SetDifficulty(Difficulty difficulty)
    {
        GameSettings.Difficulty = difficulty;
        UpdateDifficultyLabel();
    }

    void UpdateDifficultyLabel()
    {
        if (difficultyLabel == null) return;

        // 영어 enum 이름 대신 한글로 보여준다.
        difficultyLabel.text = "현재 난이도: " + MinigameTuning.Label;
    }

    void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
