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

        // Easy / Expert는 아직 미구현 - Normal만 선택 가능
        if (easyButton != null) easyButton.interactable = false;
        if (expertButton != null) expertButton.interactable = false;
        if (normalButton != null) normalButton.onClick.AddListener(() => SetDifficulty(Difficulty.Normal));

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
        if (difficultyLabel != null) difficultyLabel.text = "현재 난이도: " + GameSettings.Difficulty;
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
