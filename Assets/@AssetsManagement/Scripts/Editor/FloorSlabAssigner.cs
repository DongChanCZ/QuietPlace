using UnityEditor;
using UnityEngine;

// 맵을 교체한 뒤 실행: 이름에 "DECK"이 포함된 오브젝트를 전부 FloorSlab 레이어로 재배정.
// MonsterHearing의 floorObstructionMask가 이 레이어를 참조해 바닥/천장을 벽보다 훨씬 강하게 감쇠시킨다.
public static class FloorSlabAssigner
{
    private const string FloorLayerName = "FloorSlab";
    private const string FloorNameMarker = "DECK";

    [MenuItem("Tools/QuietPlace/Assign Floor Slab Layer")]
    public static void AssignFloorSlabLayer()
    {
        int floorLayer = LayerMask.NameToLayer(FloorLayerName);
        if (floorLayer < 0)
        {
            Debug.LogError($"'{FloorLayerName}' 레이어가 없습니다. Edit > Project Settings > Tags and Layers에서 먼저 추가하세요.");
            return;
        }

        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (var r in renderers)
        {
            if (r.gameObject.name.ToUpperInvariant().Contains(FloorNameMarker))
            {
                r.gameObject.layer = floorLayer;
                count++;
            }
        }
        Debug.Log($"FloorSlab 레이어 재배정 완료: {count}개 오브젝트");
    }
}
