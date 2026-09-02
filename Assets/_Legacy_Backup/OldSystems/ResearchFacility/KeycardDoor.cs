using UnityEngine;

public sealed class KeycardDoor : MonoBehaviour
{
    [SerializeField] Transform movingPanel;
    [SerializeField] Vector3 openOffset = new Vector3(0f, 0f, 1.5f);
    [SerializeField] float slideSpeed = 2.2f;
    Vector3 closedPosition;
    bool open;

    void Awake()
    {
        if (movingPanel == null) movingPanel = transform;
        closedPosition = movingPanel.localPosition;
    }

    public void Configure(Transform panel, Vector3 offset)
    {
        movingPanel = panel;
        openOffset = offset;
        closedPosition = panel.localPosition;
    }

    public string Prompt(FacilityPlayerController player)
        => player != null && player.HasKeycard ? (open ? "문 닫기" : "카드키로 문 열기") : "잠김 - 카드키 필요";

    public void Interact(FacilityPlayerController player)
    {
        if (player == null || !player.HasKeycard) return;
        open = !open;
    }

    void Update()
    {
        if (movingPanel == null) return;
        Vector3 target = closedPosition + (open ? openOffset : Vector3.zero);
        movingPanel.localPosition = Vector3.MoveTowards(movingPanel.localPosition, target, slideSpeed * Time.deltaTime);
    }
}
