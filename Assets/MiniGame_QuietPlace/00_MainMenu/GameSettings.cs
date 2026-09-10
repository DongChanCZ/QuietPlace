using UnityEngine;

public enum Difficulty { Easy, Normal, Expert }

public static class GameSettings
{
    private const string DifficultyKey = "Difficulty";
    private const string VolumeKey = "MasterVolume";
    private const string BrightnessKey = "Brightness";
    private const string MonsterVolumeKey = "MonsterVolume";
    private const string MonsterRangeKey = "ShowMonsterRange";
    private const string SensitivityKey = "MouseSensitivity";

    public static Difficulty Difficulty
    {
        get => (Difficulty)PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal);
        set
        {
            PlayerPrefs.SetInt(DifficultyKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, value);
            PlayerPrefs.Save();
        }
    }

    public static float Brightness
    {
        get => PlayerPrefs.GetFloat(BrightnessKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(BrightnessKey, value);
            PlayerPrefs.Save();
        }
    }

    /// 괴물 소리만 따로. 전체 음량(MasterVolume)에 곱해져서 들린다.
    public static float MonsterVolume
    {
        get => PlayerPrefs.GetFloat(MonsterVolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(MonsterVolumeKey, value);
            PlayerPrefs.Save();
        }
    }

    /// 괴물 감지범위 원판을 바닥에 보여줄지. 디버그용이라 기본은 꺼둔다.
    public static bool ShowMonsterRange
    {
        get => PlayerPrefs.GetInt(MonsterRangeKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(MonsterRangeKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(SensitivityKey, 0.12f);
        set
        {
            PlayerPrefs.SetFloat(SensitivityKey, value);
            PlayerPrefs.Save();
        }
    }
}
