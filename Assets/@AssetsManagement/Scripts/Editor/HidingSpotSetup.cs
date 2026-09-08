using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 하이어라키에서 고른 오브젝트를 "숨을 수 있는 곳"으로 만든다.
///
/// 붙는 것들
///  - HidingSpot (IHidingSpot 구현체)
///  - 겨눌 수 있게 트리거 BoxCollider (없으면)
///  - 숨었을 때 플레이어가 있을 자리 HiddenViewPoint (자식으로 하나)
///
/// 반짝이도 같이 붙여준다. 숨을 곳인지 눈에 보이지 않으면 있는지도 모른다.
/// </summary>
public static class HidingSpotSetup
{
    [MenuItem("Tools/QuietPlace/숨을 곳 - 고른 오브젝트를 숨을 곳으로 만들기")]
    public static void MakeHidingSpots()
    {
        GameObject[] selection = Selection.gameObjects;
        if (selection == null || selection.Length == 0)
        {
            Debug.LogWarning("[HidingSpotSetup] 하이어라키에서 숨을 오브젝트를 먼저 고를 것.");
            return;
        }

        var report = new List<string>();
        int made = 0, already = 0;

        foreach (GameObject go in selection)
        {
            if (go.GetComponent<HidingSpot>() != null) { already++; continue; }

            // 캐비닛처럼 부모에 렌더러가 없고 조각들로 나뉜 경우가 많다. 전부 합쳐서 잰다.
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                report.Add("  렌더러 없음(건너뜀): " + go.name);
                continue;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            HidingSpot spot = Undo.AddComponent<HidingSpot>(go);

            // 겨눌 수 있어야 프롬프트가 뜬다. 트리거라 플레이어를 막지 않는다.
            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(go);
                box.center = go.transform.InverseTransformPoint(bounds.center);
                Vector3 lossy = go.transform.lossyScale;
                box.size = new Vector3(
                    lossy.x != 0f ? bounds.size.x / lossy.x : bounds.size.x,
                    lossy.y != 0f ? bounds.size.y / lossy.y : bounds.size.y,
                    lossy.z != 0f ? bounds.size.z / lossy.z : bounds.size.z);
            }
            box.isTrigger = true;

            // 숨었을 때 서 있을 자리. 오브젝트 바닥 근처 중앙에 둔다.
            Transform view = go.transform.Find("HiddenViewPoint");
            if (view == null)
            {
                GameObject vp = new GameObject("HiddenViewPoint");
                Undo.RegisterCreatedObjectUndo(vp, "Create HiddenViewPoint");
                vp.transform.SetParent(go.transform, true);
                vp.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
                vp.transform.rotation = go.transform.rotation;
                view = vp.transform;
            }

            SerializedObject so = new SerializedObject(spot);
            so.FindProperty("hiddenViewPoint").objectReferenceValue = view;
            so.FindProperty("displayName").stringValue = GuessName(go.name);
            so.ApplyModifiedProperties();

            // 숨을 곳이라는 걸 알려주는 반짝임도 같이
            if (go.GetComponent<InteractableGlow>() == null) Undo.AddComponent<InteractableGlow>(go);

            made++;
            report.Add($"  숨을 곳: {go.name}  (이름 \"{GuessName(go.name)}\", 자리 {view.position.ToString("F2")}, 크기 {bounds.size.ToString("F2")})");
        }

        Debug.Log($"[HidingSpotSetup] {made}개 생성 / 이미 있음 {already}개\n"
                  + string.Join("\n", report.ToArray())
                  + "\n  겨누고 Q -> 숨기, 숨은 상태에서 Q 또는 이동키 -> 나오기");
    }

    /// 오브젝트 이름으로 한글 표시명을 추측한다. 맞지 않으면 인스펙터에서 바꾸면 된다.
    static string GuessName(string objectName)
    {
        string n = objectName.ToLowerInvariant();
        if (n.Contains("locker") || n.Contains("cabinet")) return "캐비닛";
        if (n.Contains("desk") || n.Contains("table")) return "책상 밑";
        if (n.Contains("shelf") || n.Contains("rack")) return "선반 뒤";
        if (n.Contains("crate") || n.Contains("box")) return "상자 뒤";
        if (n.Contains("bed")) return "침대 밑";
        return "숨을 곳";
    }

    [MenuItem("Tools/QuietPlace/숨을 곳 - 씬의 숨을 곳 목록 보기")]
    public static void ListSpots()
    {
        HidingSpot[] all = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length == 0) { Debug.Log("[HidingSpotSetup] 씬에 숨을 곳이 없다."); return; }

        var lines = new List<string>();
        foreach (HidingSpot s in all)
        {
            SerializedObject so = new SerializedObject(s);
            Object vp = so.FindProperty("hiddenViewPoint").objectReferenceValue;
            lines.Add($"  {s.name}  \"{so.FindProperty("displayName").stringValue}\"  pos={s.transform.position.ToString("F1")}"
                      + $"  자리={(vp == null ? "<없음>" : ((Transform)vp).position.ToString("F1"))}");
        }

        Debug.Log($"[HidingSpotSetup] 숨을 곳 {all.Length}개\n" + string.Join("\n", lines.ToArray()));
    }
}
