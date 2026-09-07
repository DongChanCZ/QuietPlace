using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 맵을 교체한 뒤 실행: 층을 나누는 구조 슬래브를 FloorSlab 레이어로 재배정.
// MonsterHearing의 floorObstructionMask가 이 레이어를 참조해 바닥/천장을 벽보다 훨씬 강하게 감쇠시킨다.
//
// 이름을 "DECK"으로 부분일치시키면 안 된다. 창고 선반이 PROP_2F_Wh_W_Deck0 처럼
// 이름을 쓰고, "SLAB"은 로비 붕괴 잔해(BLOCK_1F_Lobby_Collapse_Slab_00)와
// 심지어 PartsLabel 까지 걸린다. 선반 한 칸이 층 경계로 잡히면 선반 옆에 선
// 플레이어가 몬스터에게 "한 층 너머"로 계산돼 소리가 통째로 죽는다.
// 층 슬래브는 이름이 정해져 있으므로 그것만 정확히 집는다.
public static class FloorSlabAssigner
{
    private const string FloorLayerName = "FloorSlab";

    // 층을 나누는 구조물만. 새 층이 생기면 여기에 추가한다.
    private static readonly string[] SlabMarkers = { "MAIN_DECK", "ROOF_SLAB" };

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
        var assigned = new List<string>();
        int cleared = 0;

        foreach (var r in renderers)
        {
            var go = r.gameObject;
            bool isSlab = false;
            string upper = go.name.ToUpperInvariant();
            foreach (var marker in SlabMarkers)
            {
                if (upper.Contains(marker)) { isSlab = true; break; }
            }

            if (isSlab)
            {
                if (go.layer != floorLayer)
                {
                    Undo.RecordObject(go, "Assign Floor Slab Layer");
                    go.layer = floorLayer;
                    EditorUtility.SetDirty(go);
                }
                assigned.Add(go.name);
            }
            else if (go.layer == floorLayer)
            {
                // 이전 실행이 잘못 칠한 것을 되돌린다.
                Undo.RecordObject(go, "Clear Floor Slab Layer");
                go.layer = 0;
                EditorUtility.SetDirty(go);
                cleared++;
            }
        }

        assigned.Sort();
        Debug.Log($"FloorSlab 레이어 재배정 완료: {assigned.Count}개 오브젝트\n  " + string.Join("\n  ", assigned)
                  + (cleared > 0 ? $"\n잘못 칠해져 있던 {cleared}개는 Default로 되돌림" : string.Empty));
    }
}
