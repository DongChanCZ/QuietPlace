using UnityEngine;
using UnityEngine.InputSystem;

public class ReadableNote : MonoBehaviour
{
    public Sprite fullscreenImage;
    public float interactRange = 2.5f;
    public GameObject interactPrompt;

    private Transform player;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    void Update()
    {
        if (player == null || FullscreenNoteViewer.Instance == null) return;

        bool playerInRange = Vector3.Distance(transform.position, player.position) <= interactRange;
        bool viewerOpen = FullscreenNoteViewer.Instance.IsOpen;

        if (interactPrompt != null) interactPrompt.SetActive(playerInRange && !viewerOpen);

        if (playerInRange && !viewerOpen &&
            Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            FullscreenNoteViewer.Instance.Show(fullscreenImage);
        }
    }
}
