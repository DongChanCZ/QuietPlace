using UnityEngine;

public class ChairCreakSettle : MonoBehaviour
{
    [Header("회전 감쇠")]
    public float maxAngularSpeed = 15f;
    public float deceleration = 4f;
    public Vector3 rotationAxis = Vector3.up;

    [Header("끼이익 소리")]
    public AudioSource audioSource;
    public AudioClip creakSound;

    private float currentSpeed;
    private bool active;

    public void TriggerCreak()
    {
        if (active) return;
        active = true;
        currentSpeed = maxAngularSpeed;

        if (audioSource != null && creakSound != null)
        {
            audioSource.PlayOneShot(creakSound);
        }
    }

    void Update()
    {
        if (!active || currentSpeed <= 0f) return;

        transform.Rotate(rotationAxis, currentSpeed * Time.deltaTime, Space.World);
        currentSpeed = Mathf.Max(0f, currentSpeed - deceleration * Time.deltaTime);
    }
}
