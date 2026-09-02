using UnityEngine;

public class AmbientAudioLayer : MonoBehaviour
{
    [Header("초반: 모니터 고주파 소음")]
    public AudioSource monitorNoiseSource;
    public float minMonitorNoiseVolume = 0.1f;

    [Header("중반: 줌아웃되며 커지는 3D 실내 환경음")]
    public AudioSource roomAmbienceSource;
    public float maxRoomAmbienceVolume = 0.4f;

    void Start()
    {
        if (monitorNoiseSource != null)
        {
            monitorNoiseSource.volume = 1f;
            monitorNoiseSource.Play();
        }

        if (roomAmbienceSource != null)
        {
            roomAmbienceSource.volume = 0f;
            roomAmbienceSource.Play();
        }
    }

    // CutsceneCameraDolly.onDollyProgress 에 연결
    public void OnDollyProgress(float t)
    {
        if (roomAmbienceSource != null) roomAmbienceSource.volume = Mathf.Lerp(0f, maxRoomAmbienceVolume, t);
        if (monitorNoiseSource != null) monitorNoiseSource.volume = Mathf.Lerp(1f, minMonitorNoiseVolume, t);
    }
}
