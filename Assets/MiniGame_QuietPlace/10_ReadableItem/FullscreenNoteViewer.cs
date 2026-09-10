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

    // GameScene 의 플레이어는 PlayerMovement 가 아니라 PlayerController 를 쓴다.
    // 둘 다 잡아두지 않으면 전체화면으로 글을 읽는 동안 화면이 가려진 채로 계속 걸어다닌다.
    private PlayerController playerController;

    // 연 그 프레임에 같은 E 입력으로 바로 닫히지 않게 한다
    private int openedFrame = -1;

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerMovement = playerObj.GetComponent<PlayerMovement>();
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;
        if (Keyboard.current == null) return;

        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (click || Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    public void Show(Sprite sprite)
    {
        if (displayImage != null) displayImage.sprite = sprite;
        if (panelRoot != null) panelRoot.SetActive(true);
        IsOpen = true;
        openedFrame = Time.frameCount;

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerController != null) playerController.enabled = false;
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        IsOpen = false;

        if (playerMovement != null) playerMovement.enabled = true;
        if (playerController != null) playerController.enabled = true;

        // PlayerController.OnDisable 이 커서 잠금을 풀어버린다. 다시 잠그지 않으면
        // HandleLook 이 Locked 일 때만 돌기 때문에 읽고 나온 뒤 시야가 고정된다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
