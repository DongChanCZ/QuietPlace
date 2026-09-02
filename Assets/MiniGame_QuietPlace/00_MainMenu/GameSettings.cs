using UnityEngine;

public enum Difficulty { Easy, Normal, Expert }

public static class GameSettings
{
    private const string DifficultyKey = "Difficulty";
    private const string VolumeKey = "MasterVolume";
    private const string BrightnessKey = "Brightness";

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
}
