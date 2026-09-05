using UnityEngine;
using UnityEngine.UI;

// 커서가 잠겨 실제로 조작 중일 때만 화면 중앙에 보여준다.
// 프리뷰 씬(BlockoutWalkPlayer)의 크로스헤어와 같은 표시 조건.
public class CrosshairUI : MonoBehaviour
{
    Text label;

    void Awake()
    {
        label = GetComponent<Text>();
    }

    void Update()
    {
        label.enabled = Cursor.lockState == CursorLockMode.Locked;
    }
}
