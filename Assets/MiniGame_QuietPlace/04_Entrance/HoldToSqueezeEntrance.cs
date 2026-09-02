using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HoldToSqueezeEntrance : MonoBehaviour
{
    public float holdDuration = 2f;
    public Transform exitPoint;
    public GameObject gapRubble;
    public GameObject collapsedRubble;
    public Slider holdProgressUI;

    public AudioSource audioSource;
    public AudioClip squeezeSound;
    public AudioClip collapseSound;

    private Transform player;
    private bool playerInRange;
    private float holdTimer;
    private bool completed;

    void Start()
    {
        if (holdProgressUI != null) holdProgressUI.gameObject.SetActive(false);
        if (collapsedRubble != null) collapsedRubble.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        player = other.transform;
        if (holdProgressUI != null) holdProgressUI.gameObject.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        holdTimer = 0f;
        if (holdProgressUI != null)
        {
            holdProgressUI.value = 0f;
            holdProgressUI.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (completed || !playerInRange || Keyboard.current == null) return;

        if (Keyboard.current.eKey.isPressed)
        {
            holdTimer += Time.deltaTime;
            if (holdProgressUI != null) holdProgressUI.value = holdTimer / holdDuration;

            if (holdTimer >= holdDuration)
            {
                Squeeze();
            }
        }
        else
        {
            holdTimer = 0f;
            if (holdProgressUI != null) holdProgressUI.value = 0f;
        }
    }

    void Squeeze()
    {
        completed = true;

        if (holdProgressUI != null) holdProgressUI.gameObject.SetActive(false);

        if (exitPoint != null && player != null)
        {
            player.position = exitPoint.position;
        }

        if (audioSource != null && squeezeSound != null)
        {
            audioSource.PlayOneShot(squeezeSound);
        }

        Collapse();
    }

    void Collapse()
    {
        if (gapRubble != null) gapRubble.SetActive(false);
        if (collapsedRubble != null) collapsedRubble.SetActive(true);

        if (audioSource != null && collapseSound != null)
        {
            audioSource.PlayOneShot(collapseSound);
        }
    }
}
