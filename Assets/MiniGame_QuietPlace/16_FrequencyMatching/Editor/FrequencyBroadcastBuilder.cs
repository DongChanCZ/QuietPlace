using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주파수 송출 대기 구간을 씬에 조립한다.
///
///  - 화면 아래에 송출 진행 HUD (미니게임 UI 가 아니라 평소 화면 위에 뜬다)
///  - 송출 지점(3층 방송실 콘솔 근처)에서 계속 소음이 나가 괴물을 부른다
///  - 방 안 소품을 하나씩 넘기는 RoomRampage
/// </summary>
public static class FrequencyBroadcastBuilder
{
    const string RootName = "P_FrequencyBroadcast";
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_FrequencyBroadcast.prefab";
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

    [MenuItem("Tools/QuietPlace/주파수 송출(숨어서 버티기) 만들기")]
    public static void Build()
    {
        foreach (FrequencyBroadcast b in Object.FindObjectsByType<FrequencyBroadcast>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(b.gameObject);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Frequency Broadcast");

        // ---------- HUD ----------
        GameObject canvasGo = new GameObject("BroadcastHUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root.transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;   // 미니게임(100) 보다 아래, 일반 HUD 위

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);

        Image bg = Img(canvasGo.transform, "BroadcastBarBG", new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -430f), new Vector2(620f, 26f));
        Image fill = Img(canvasGo.transform, "BroadcastBarFill", new Color(0.45f, 0.9f, 0.7f, 0.95f), new Vector2(0f, -430f), new Vector2(620f, 26f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;

        GameObject labelGo = new GameObject("BroadcastLabel", typeof(RectTransform));
        labelGo.transform.SetParent(canvasGo.transform, false);
        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = "주파수 송출 중";
        label.fontSize = 26f;
        label.color = new Color(0.7f, 0.95f, 0.85f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        SetBox(label.rectTransform, new Vector2(0f, -396f), new Vector2(700f, 44f));

        // ---------- 송출 지점 ----------
        // 3층 방송실 콘솔 앞. 여기서 소음이 나가 괴물이 찾아온다.
        GameObject emit = new GameObject("EmitPoint");
        emit.transform.SetParent(root.transform, false);
        emit.transform.position = new Vector3(6.1f, 9.8f, -7.6f);

        // ---------- 부술 소품 ----------
        GameObject rampGo = new GameObject("RoomRampage");
        rampGo.transform.SetParent(root.transform, false);
        RoomRampage rampage = rampGo.AddComponent<RoomRampage>();

        List<Transform> props = CollectBroadcastRoomProps();
        SerializedObject rso = new SerializedObject(rampage);
        SerializedProperty arr = rso.FindProperty("props");
        arr.arraySize = props.Count;
        for (int i = 0; i < props.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = props[i];
        rso.ApplyModifiedPropertiesWithoutUndo();

        // ---------- 매니저 ----------
        FrequencyBroadcast broadcast = root.AddComponent<FrequencyBroadcast>();
        SerializedObject so = new SerializedObject(broadcast);
        so.FindProperty("duration").floatValue = 45f;
        so.FindProperty("hudRoot").objectReferenceValue = canvasGo;
        so.FindProperty("progressFill").objectReferenceValue = fill;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("emitPoint").objectReferenceValue = emit.transform;
        so.FindProperty("rampage").objectReferenceValue = rampage;
        so.ApplyModifiedPropertiesWithoutUndo();

        canvasGo.SetActive(false);

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);

        Selection.activeGameObject = root;
        Debug.Log($"[FrequencyBroadcastBuilder] 조립 완료\n" +
                  $"  송출 {broadcast.duration}초, 소음 1.5초마다 (범위 30m)\n" +
                  $"  부술 소품 {props.Count}개 (3층 방송실에서 골라 담았다)\n" +
                  $"  프리팹 = {PrefabPath}");
    }

    /// 3층 방송실 안의 작고 넘어뜨릴 만한 소품을 고른다.
    /// 벽/바닥/문/천장 같은 구조물과 콘솔 본체는 넘어지면 안 되므로 제외한다.
    static List<Transform> CollectBroadcastRoomProps()
    {
        var picked = new List<Transform>();

        string[] exclude = { "Wall", "Floor", "Ceil", "Slab", "Door", "Frame", "Stair", "Rail",
                             "Keypad", "Pillar", "Column", "Beam", "Window", "Light", "Sparkle" };

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // 3층 방송실 범위
            Vector3 p = t.position;
            if (p.y < 8.4f || p.y > 11.5f) continue;
            if (p.x < -3f || p.x > 15.5f) continue;
            if (p.z < -10.5f || p.z > -3.5f) continue;

            Renderer r = t.GetComponent<Renderer>();
            if (r == null) continue;

            // 너무 큰 것은 구조물이다
            Vector3 size = r.bounds.size;
            if (size.x > 1.2f || size.y > 1.2f || size.z > 1.2f) continue;
            if (size.magnitude < 0.08f) continue;   // 너무 작은 장식은 넘어져도 안 보인다

            bool skip = false;
            foreach (string e in exclude)
                if (t.name.IndexOf(e, System.StringComparison.OrdinalIgnoreCase) >= 0) { skip = true; break; }
            if (skip) continue;

            picked.Add(t);
            if (picked.Count >= 14) break;   // 너무 많으면 방이 폭발한다
        }

        return picked;
    }

    // ---------------------------------------------------------------- 유틸

    static Image Img(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        SetBox(img.rectTransform, pos, size);
        return img;
    }

    static void SetBox(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
