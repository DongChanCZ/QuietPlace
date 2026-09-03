using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FusePuzzleTest : MonoBehaviour
{
    public Transform player;
    public GameObject looseFuse;
    public Transform installAnchor;
    public float useDistance = 1.8f;

    bool carrying;
    bool installed;
    string prompt;

    void Awake()
    {
        Debug.Assert(player && looseFuse && installAnchor && useDistance > 0,
            "FusePuzzleTest setup is incomplete.");
    }

    void Update()
    {
        if (!player || !looseFuse || !installAnchor) return;

        if (installed)
        {
            prompt = "퓨즈 설치 완료";
            return;
        }

        var ePressed = Keyboard.current?.eKey.wasPressedThisFrame == true;
        if (!carrying)
        {
            prompt = HorizontalDistance(player, looseFuse.transform) <= useDistance
                ? "E : 퓨즈 줍기"
                : "";
            if (prompt.Length > 0 && ePressed) PickUp();
            return;
        }

        if (HorizontalDistance(player, installAnchor) > useDistance)
        {
            prompt = "퓨즈 보유 중 · 빈 슬롯으로 이동";
            return;
        }

        prompt = "E : 퓨즈 설치";
        if (ePressed) Install();
    }

    public bool Carrying => carrying;
    public bool Installed => installed;

    public void PickUp()
    {
        if (installed || !looseFuse) return;
        carrying = true;
        looseFuse.SetActive(false);
    }

    public void Install()
    {
        if (!carrying || !looseFuse || !installAnchor) return;
        looseFuse.transform.SetPositionAndRotation(installAnchor.position, installAnchor.rotation);
        looseFuse.SetActive(true);
        carrying = false;
        installed = true;
    }

    static float HorizontalDistance(Transform a, Transform b)
    {
        var delta = a.position - b.position;
        delta.y = 0;
        return delta.magnitude;
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(prompt)) return;
        GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height - 64, 360, 36), prompt);
    }
}
