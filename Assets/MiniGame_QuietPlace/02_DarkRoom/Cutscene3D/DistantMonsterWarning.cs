using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class DistantMonsterWarning : MonoBehaviour
{
    public AudioClip warningSound;
    public AudioLowPassFilter lowPassFilter;
    public float lowPassCutoff = 500f;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (lowPassFilter != null) lowPassFilter.cutoffFrequency = lowPassCutoff;
    }

    // CutsceneCameraDolly.onDollyComplete 에 연결
    public void PlayWarning()
    {
        if (warningSound != null) audioSource.PlayOneShot(warningSound);
    }
}
