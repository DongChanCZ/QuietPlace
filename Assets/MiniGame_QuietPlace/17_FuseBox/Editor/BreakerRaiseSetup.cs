using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 차단기 올리기 연출을 씬에 조립한다 - 전용 미니게임 UI, 레버 앞에 설 자리 앵커,
/// 그리고 참조 배선까지.
///
/// 화면에는 문장을 넣지 않는다(요청). 게이지와 눌러야 하는 키(E)만 보여준다.
/// </summary>
public static class BreakerRaiseSetup
{
    const string HostName = "GameManager";
    const string RootName = "BreakerRaise";
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

    [MenuItem("Tools/QuietPlace/차단기 올리기 연출 만들기")]
    public static void Build()
    {
        GameObject host = GameObject.Find(HostName);
        if (host == null)
        {
            Debug.LogError($"[BreakerRaiseSetup] 씬에 {HostName} 오브젝트가 없다.");
            return;
        }

        Transform old = host.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Breaker Raise");
        root.transform.SetParent(host.transform, false);

        // ---------- 미니게임 화면 ----------
        GameObject canvasGo = new GameObject("BreakerRaiseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(root.transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;   // 다른 미니게임 UI(100) 위, 죽음 화면(500) 아래

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = Empty(canvasGo.transform, "Panel");
        Stretch((RectTransform)panel.transform);

        // 화면을 덜 가린다 - 실물 레버가 올라가는 걸 봐야 하므로 아래쪽만 어둡게
        Image dim = Img(panel.transform, "Dim", new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -300f), new Vector2(4000f, 440f));

        // 게이지
        Img(panel.transform, "BarBG", new Color(1f, 1f, 1f, 0.16f), new Vector2(0f, -250f), new Vector2(660f, 40f));
        Image barFill = Img(panel.transform, "BarFill", new Color(1f, 0.78f, 0.28f, 0.95f), new Vector2(0f, -250f), new Vector2(660f, 40f));
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = 0;
        barFill.fillAmount = 0f;

        // 눌러야 하는 키. 문장 대신 키캡만 둔다.
        Img(panel.transform, "KeyCapRim", new Color(0.55f, 0.50f, 0.38f), new Vector2(0f, -180f), new Vector2(84f, 84f));
        Img(panel.transform, "KeyCap", new Color(0.13f, 0.13f, 0.12f), new Vector2(0f, -180f), new Vector2(74f, 74f));
        Label(panel.transform, "KeyLetter", "E", 46f, new Vector2(0f, -180f), new Color(0.94f, 0.90f, 0.76f));

        MashToComplete mash = panel.AddComponent<MashToComplete>();
        mash.requiredPresses = 14;
        mash.progressPerPress = 1f;
        mash.decayPerSecond = 0.6f;
        mash.progressFill = barFill;
        mash.leverGoesUp = true;

        panel.SetActive(false);   // 매니저가 켠다

        // ---------- 레버 앞에 설 자리 ----------
        BreakerLever lever = FindOne<BreakerLever>();
        GameObject anchor = new GameObject("StandAnchor");
        anchor.transform.SetParent(root.transform, false);

        string anchorNote = "레버를 못 찾아 앵커를 원점에 뒀다";
        if (lever != null)
        {
            Vector3 spot = FindFloorSpot(lever.transform, 1.5f);
            anchor.transform.position = spot;
            anchorNote = "레버 앞 " + Vector3.Distance(spot, lever.transform.position).ToString("0.00") + "m";
        }

        // ---------- 배선 ----------
        BreakerRaiseSequence seq = host.GetComponent<BreakerRaiseSequence>();
        if (seq == null) seq = Undo.AddComponent<BreakerRaiseSequence>(host);

        PlayerController player = FindOne<PlayerController>();
        PlayerInteractor pi = FindOne<PlayerInteractor>();

        SerializedObject so = new SerializedObject(seq);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("interactor").objectReferenceValue = pi;
        so.FindProperty("lever").objectReferenceValue = lever;
        so.FindProperty("standAnchor").objectReferenceValue = anchor.transform;
        so.FindProperty("minigameRoot").objectReferenceValue = panel;
        so.FindProperty("mash").objectReferenceValue = mash;
        so.FindProperty("standDistance").floatValue = 1.5f;
        so.FindProperty("walkSeconds").floatValue = 1.3f;
        so.FindProperty("lookAtHeight").floatValue = 0f;
        so.FindProperty("cutsceneLookUpPitch").floatValue = -32f;
        so.FindProperty("cutscenePanSeconds").floatValue = 1.2f;
        so.FindProperty("cutsceneHoldSeconds").floatValue = 2.6f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(seq);

        // 퓨즈 시퀀서가 이제 퓨즈를 꽂는 순간 화면을 닫도록 맞춘다.
        FuseBoxRepairSequence fuseSeq = FindOne<FuseBoxRepairSequence>();
        if (fuseSeq != null)
        {
            fuseSeq.raiseBreakerInThisScreen = false;
            EditorUtility.SetDirty(fuseSeq);
        }

        Debug.Log("[BreakerRaiseSetup] 조립 완료\n" +
                  $"  player={Name(player)} lever={Name(lever)}\n" +
                  $"  설 자리 앵커 = {anchor.transform.position.ToString("0.00")} ({anchorNote})\n" +
                  $"  연타 {mash.requiredPresses}번, 게이지에 맞춰 실물 레버가 올라간다\n" +
                  $"  퓨즈 시퀀서 raiseBreakerInThisScreen={(fuseSeq == null ? "시퀀서 못 찾음" : "false (밖으로 나감)")}\n" +
                  "  화면에 문장 없음 - 게이지 + E 키캡만", seq);
    }

    /// 레버 앞쪽에서 바닥을 찾는다. 앞이 막혀 있으면 좌우/뒤도 훑는다.
    static Vector3 FindFloorSpot(Transform lever, float distance)
    {
        Vector3[] dirs =
        {
            lever.forward, -lever.forward, lever.right, -lever.right,
            (lever.forward + lever.right).normalized, (lever.forward - lever.right).normalized,
        };

        foreach (Vector3 d in dirs)
        {
            Vector3 probe = lever.position + d * distance;

            // 그 방향이 벽으로 막혀 있으면 건너뛴다
            if (Physics.Raycast(lever.position, d, distance + 0.2f, ~0, QueryTriggerInteraction.Ignore)) continue;

            RaycastHit hit;
            if (!Physics.Raycast(probe + Vector3.up * 1.2f, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;

            return hit.point + Vector3.up * 0.05f;
        }

        return lever.position + lever.forward * distance;
    }

    // ---------------------------------------------------------------- 유틸

    static GameObject Empty(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image Img(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = Empty(parent, name);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        SetBox(img.rectTransform, pos, size);
        return img;
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Vector2 pos, Color color)
    {
        GameObject go = Empty(parent, name);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (font != null) tmp.font = font;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        SetBox(tmp.rectTransform, pos, new Vector2(size * 4f, size * 1.6f));
        return tmp;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetBox(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static T FindOne<T>() where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found.Length == 0)
        {
            Debug.LogWarning($"[BreakerRaiseSetup] {typeof(T).Name} 을 씬에서 못 찾았다.");
            return null;
        }
        return found[0];
    }

    static string Name(Component c) => c == null ? "없음" : c.gameObject.name;
}
