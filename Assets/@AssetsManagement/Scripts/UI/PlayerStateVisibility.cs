using UnityEngine;
using UnityEngine.UI;

// 플레이어가 특정 상태일 때만 이 UI를 표시. 상태 전용 아이콘 표시에 재사용.
// GameObject 자체는 항상 켜둔 채 Graphic만 토글한다 — SetActive(false)로 자기 자신을 끄면
// 그 순간부터 Update()가 다시 호출되지 않아 영영 못 켜지는 문제가 있었음.
public class PlayerStateVisibility : MonoBehaviour
{
    public PlayerController player;
    public PlayerStateType visibleDuringState;

    private Graphic[] graphics;

    void Awake()
    {
        graphics = GetComponentsInChildren<Graphic>(true);
    }

    void Update()
    {
        if (player == null) return;
        bool visible = player.CurrentStateType == visibleDuringState;
        foreach (var g in graphics) g.enabled = visible;
    }
}
