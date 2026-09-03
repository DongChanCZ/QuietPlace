using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GlassShard : MonoBehaviour
{
    public AudioClip breakSound;
    public string triggerTag = "Player";
    public float destroyDelay = 1f;

    private AudioSource audioSource;
    private bool triggered = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag(triggerTag)) return;

        triggered = true;

        if (breakSound != null)
        {
            audioSource.PlayOneShot(breakSound);
        }

        Destroy(gameObject, destroyDelay);
    }
}
