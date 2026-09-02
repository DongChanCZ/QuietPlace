using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class SkillCheckMinigame : MonoBehaviour
{
    [Header("회전 바늘 (데바데식 스킬체크)")]
    public RectTransform needle;
    public RectTransform hitZoneVisual;
    public float rotationSpeed = 220f;
    public float hitZoneWidth = 30f;

    [Header("R/S/T 3번 성공해야 완료")]
    public int requiredHits = 3;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip missSound; // 실패 시 2단계 경보음

    public UnityEvent onAllHits;

    private float currentAngle;
    private float hitZoneCenter;
    private int hitCount;
    private bool completed;

    void OnEnable()
    {
        hitCount = 0;
        completed = false;
        currentAngle = 0f;
        RandomizeHitZone();
    }

    void Update()
    {
        if (completed) return;

        currentAngle = (currentAngle + rotationSpeed * Time.deltaTime) % 360f;
        if (needle != null) needle.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);
        if (hitZoneVisual != null) hitZoneVisual.localRotation = Quaternion.Euler(0f, 0f, -hitZoneCenter);

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TryHit();
        }
    }

    void TryHit()
    {
        float diff = Mathf.Abs(Mathf.DeltaAngle(currentAngle, hitZoneCenter));

        if (diff <= hitZoneWidth * 0.5f)
        {
            hitCount++;
            PlaySound(hitSound);

            if (hitCount >= requiredHits)
            {
                completed = true;
                onAllHits?.Invoke();
            }
            else
            {
                RandomizeHitZone();
            }
        }
        else
        {
            PlaySound(missSound);
        }
    }

    void RandomizeHitZone()
    {
        hitZoneCenter = Random.Range(0f, 360f);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
