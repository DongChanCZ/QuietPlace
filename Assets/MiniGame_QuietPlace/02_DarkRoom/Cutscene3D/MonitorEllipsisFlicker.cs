using System.Collections;
using UnityEngine;
using TMPro;

public class MonitorEllipsisFlicker : MonoBehaviour
{
    [Header("깜빡일 텍스트 (마지막 줄 '...' 등)")]
    public TMP_Text targetText;

    [Header("불규칙한 켜짐/꺼짐 시간 범위")]
    public float minOnDuration = 0.1f;
    public float maxOnDuration = 0.5f;
    public float minOffDuration = 0.05f;
    public float maxOffDuration = 0.3f;

    [Header("지직거리는 전자 노이즈")]
    public AudioSource audioSource;
    public AudioClip[] crackleSounds;

    void OnEnable()
    {
        StartCoroutine(FlickerLoop());
    }

    IEnumerator FlickerLoop()
    {
        while (true)
        {
            targetText.enabled = true;
            PlayCrackle();
            yield return new WaitForSeconds(Random.Range(minOnDuration, maxOnDuration));

            targetText.enabled = false;
            PlayCrackle();
            yield return new WaitForSeconds(Random.Range(minOffDuration, maxOffDuration));
        }
    }

    void PlayCrackle()
    {
        if (audioSource == null || crackleSounds == null || crackleSounds.Length == 0) return;
        audioSource.PlayOneShot(crackleSounds[Random.Range(0, crackleSounds.Length)]);
    }
}
