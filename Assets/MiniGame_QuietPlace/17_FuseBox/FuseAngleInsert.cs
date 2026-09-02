using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class FuseAngleInsert : MonoBehaviour
{
    [Header("퓨즈 회전 (A/D)")]
    public RectTransform fuseVisual;
    public float rotateSpeed = 90f;
    public float targetAngle = 45f;
    public float tolerance = 8f;
    public bool randomizeTargetOnEnable = true;

    [Header("목표 각도를 보여주는 투명 가이드선")]
    public RectTransform targetGuideLine;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip insertSound;
    public AudioClip wrongClunkSound; // 각도 틀렸을 때 철컥 소리
    public AudioClip wrongAngleSound; // 2단계 경보음

    public UnityEvent onInserted;

    private float currentAngle;
    private bool completed;

    void OnEnable()
    {
        completed = false;
        currentAngle = 0f;
        if (randomizeTargetOnEnable) targetAngle = Random.Range(0f, 360f);
        if (targetGuideLine != null) targetGuideLine.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        UpdateVisual();
    }

    void Update()
    {
        if (completed) return;

        float input = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) input -= 1f;
            if (Keyboard.current.dKey.isPressed) input += 1f;
        }

        if (input != 0f)
        {
            currentAngle = (currentAngle + input * rotateSpeed * Time.deltaTime + 360f) % 360f;
            UpdateVisual();
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInsert();
        }
    }

    void TryInsert()
    {
        float diff = Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle));

        if (diff <= tolerance)
        {
            completed = true;
            if (fuseVisual != null) fuseVisual.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            PlaySound(insertSound);
            onInserted?.Invoke();
        }
        else
        {
            PlaySound(wrongClunkSound);
            PlaySound(wrongAngleSound);
        }
    }

    void UpdateVisual()
    {
        if (fuseVisual != null) fuseVisual.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
