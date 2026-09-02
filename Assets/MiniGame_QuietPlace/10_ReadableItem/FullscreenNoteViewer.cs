using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FullscreenNoteViewer : MonoBehaviour
{
    public static FullscreenNoteViewer Instance { get; private set; }

    public GameObject panelRoot;
    public Image displayImage;

    public bool IsOpen { get; private set; }

    private PlayerMovement playerMovement;

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerMovement = playerObj.GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    public void Show(Sprite sprite)
    {
        if (displayImage != null) displayImage.sprite = sprite;
        if (panelRoot != null) panelRoot.SetActive(true);
        IsOpen = true;

        if (playerMovement != null) playerMovement.enabled = false;
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        IsOpen = false;

        if (playerMovement != null) playerMovement.enabled = true;
    }
}
