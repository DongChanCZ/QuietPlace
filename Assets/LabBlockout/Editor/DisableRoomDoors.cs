using UnityEditor;
using UnityEngine;

/// <summary>
/// 2층 창고 / 배전반실 / 발전기실 문의 열고닫기를 없앤다.
///
/// 문 오브젝트를 통째로 끄면 문틀(Frame_*)까지 사라져 벽에 구멍이 생기므로,
/// 움직이는 부분(SlideLeaf / SlideStripe)만 끄고 SlidingDoor 컴포넌트를 비활성화한다.
/// 결과: 문틀은 남고, 통로는 항상 열려 있고, E 를 눌러도 아무 일도 없다.
///
/// GameScene 의 프리팹 인스턴스 오버라이드로만 남으므로 맵 프리팹 원본은 그대로다.
/// 되돌리려면 하이어라키에서 해당 오브젝트 체크박스를 다시 켜면 된다.
/// </summary>
public static class DisableRoomDoors
{
    static readonly string[] DoorNames =
    {
        "DOOR_2F_Warehouse_OPEN",              // 창고
        "DOOR_2F_CORRIDOR_NORTH_HEADER_03",    // 배전반실
        "DOOR_2F_Generator_OPEN",              // 발전기실
    };

    // 이 조각들만 끈다. Frame_* 과 SlideTrack 은 문틀/레일이라 남긴다.
    static readonly string[] MovingPartMarkers = { "SlideLeaf", "SlideStripe", "Leaf_", "SwingLeaf" };

    [MenuItem("Tools/QuietPlace/2층 방 문 3개 열고닫기 없애기")]
    public static void Disable()
    {
        int doorsTouched = 0, partsHidden = 0, componentsDisabled = 0;

        foreach (string name in DoorNames)
        {
            GameObject door = FindInScene(name);
            if (door == null)
            {
                Debug.LogWarning($"[DisableRoomDoors] '{name}' 을 씬에서 찾지 못했다.");
                continue;
            }

            doorsTouched++;

            SlidingDoor slider = door.GetComponent<SlidingDoor>();
            if (slider != null && slider.enabled)
            {
                Undo.RecordObject(slider, "Disable SlidingDoor");
                slider.enabled = false;
                EditorUtility.SetDirty(slider);
                componentsDisabled++;
            }

            foreach (Transform child in door.transform)
            {
                if (!IsMovingPart(child.name)) continue;
                if (!child.gameObject.activeSelf) continue;

                Undo.RecordObject(child.gameObject, "Hide Door Leaf");
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
                partsHidden++;
            }
        }

        Debug.Log($"[DisableRoomDoors] 문 {doorsTouched}개 처리: " +
                  $"SlidingDoor {componentsDisabled}개 비활성화, 문짝 {partsHidden}개 숨김.\n" +
                  "문틀(Frame_*)과 레일(SlideTrack)은 그대로 남겼다.");
    }

    static bool IsMovingPart(string objectName)
    {
        foreach (string marker in MovingPartMarkers)
            if (objectName.Contains(marker)) return true;
        return false;
    }

    /// 이름으로 씬 전체를 훑는다. 비활성 오브젝트도 찾아야 해서 GameObject.Find 는 못 쓴다.
    /// 에디터에서 한 번만 도는 코드라 전수 검사로 충분하다.
    static GameObject FindInScene(string name)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
            if (t.name == name) return t.gameObject;
        return null;
    }
}
