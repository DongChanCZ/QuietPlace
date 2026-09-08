using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 건물 천장등 리그를 만든다.
///
/// 전원 복구(BuildingPower)는 잘 돌지만 정작 플레이어가 미니게임을 끝내는 2층 기계실과
/// 3층에는 조명이 하나도 없어서 "불이 들어왔다"가 눈에 보이지 않았다. 방마다 천장등을
/// 깔아 그 순간이 실제로 보이게 한다.
///
/// 조명은 맵 프리팹 안이 아니라 씬 오브젝트로 만든다. CLAUDE.md 대로 맵 프리팹에는
/// 스크립트로 오브젝트를 넣지 않는다(중첩 프리팹 트랜스폼이 깨진다).
///
/// 그림자는 끈다. 20개 남짓을 그림자까지 켜면 URP 섀도 아틀라스가 부족하다는 경고가
/// 계속 뜨고 성능도 떨어진다. 채움광 용도라 그림자가 없어도 충분하다.
/// </summary>
public static class BuildingLightRigBuilder
{
    const string RootName = "BuildingLights";

    struct Room
    {
        public string tag;
        public Vector3 center;   // 방 중심 (x, 천장높이, z)
        public Vector2 span;     // x, z 로 퍼뜨릴 범위
        public int countX;
        public int countZ;
        public float range;
        public float intensity;
    }

    [MenuItem("Tools/QuietPlace/건물 천장등 리그 만들기")]
    public static void Build()
    {
        // 방 경계는 맵의 프롭 그룹 렌더러 바운즈에서 잰 값이다.
        //   2층 바닥 4.2 / 천장 8.0  -> 등은 7.4
        //   3층 바닥 8.3 / 천장 12.2 -> 등은 11.3
        Room[] rooms =
        {
            new Room { tag = "2F_Generator",  center = new Vector3( 1.0f, 7.4f, -7.4f), span = new Vector2(5.0f, 3.0f), countX = 2, countZ = 1, range = 7.5f, intensity = 2.6f },
            new Room { tag = "2F_Switchboard",center = new Vector3(-7.5f, 7.4f, -7.3f), span = new Vector2(4.5f, 3.0f), countX = 2, countZ = 1, range = 7.5f, intensity = 2.6f },
            new Room { tag = "2F_Warehouse",  center = new Vector3(-5.0f, 7.4f,  0.3f), span = new Vector2(8.0f,12.0f), countX = 2, countZ = 3, range = 8.0f, intensity = 2.3f },
            new Room { tag = "2F_Corridor",   center = new Vector3(-1.0f, 7.4f, -0.7f), span = new Vector2(26.0f,0f),   countX = 6, countZ = 1, range = 7.0f, intensity = 2.2f },
            new Room { tag = "3F_Broadcast",  center = new Vector3( 6.1f,11.3f, -7.0f), span = new Vector2(12.0f,3.0f), countX = 4, countZ = 1, range = 7.5f, intensity = 2.5f },
        };

        GameObject root = GameObject.Find(RootName);
        if (root != null) Undo.DestroyObjectImmediate(root);

        root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Building Light Rig");

        var report = new List<string>();
        int total = 0;

        foreach (Room r in rooms)
        {
            GameObject group = new GameObject(r.tag);
            group.transform.SetParent(root.transform, false);

            for (int ix = 0; ix < r.countX; ix++)
            {
                for (int iz = 0; iz < r.countZ; iz++)
                {
                    float fx = r.countX <= 1 ? 0.5f : (float)ix / (r.countX - 1);
                    float fz = r.countZ <= 1 ? 0.5f : (float)iz / (r.countZ - 1);

                    Vector3 pos = r.center + new Vector3(
                        Mathf.Lerp(-r.span.x * 0.5f, r.span.x * 0.5f, fx),
                        0f,
                        Mathf.Lerp(-r.span.y * 0.5f, r.span.y * 0.5f, fz));

                    GameObject go = new GameObject($"LIGHT_{r.tag}_{ix}{iz}");
                    go.transform.SetParent(group.transform, false);
                    go.transform.position = pos;

                    Light l = go.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = r.range;
                    l.intensity = r.intensity;
                    l.color = new Color(1f, 0.96f, 0.90f);   // 형광등보다 살짝 따뜻하게
                    l.shadows = LightShadows.None;           // 섀도 아틀라스 아끼기
                    l.renderMode = LightRenderMode.Auto;

                    total++;
                }
            }

            report.Add($"  {r.tag}: {r.countX * r.countZ}개 (범위 {r.range}m, 세기 {r.intensity})");
        }

        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);

        Debug.Log($"[BuildingLightRigBuilder] 천장등 {total}개 생성 (씬 오브젝트, 그림자 없음)\n"
                  + string.Join("\n", report.ToArray())
                  + "\n  BuildingPower 의 자동 수집이 이 조명들을 잡아 전원 복구 때 켠다.");
    }
}
