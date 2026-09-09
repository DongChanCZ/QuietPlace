using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 변압기 수리 직후 연출을 조립한다 - 혼잣말 자막 UI, 바라볼 표시등 배선,
/// 그리고 변압기를 떨어뜨렸을 때의 소리/소음 설정까지 한 번에.
/// </summary>
public static class TransformerFixedCutsceneSetup
{
    const string HostName = "GameManager";
    const string RootName = "TransformerFixed";
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";
    const string DropSoundPath = "Assets/MiniGame_QuietPlace/Audio/collapse.mp3";

    [MenuItem("Tools/QuietPlace/변압기 완료 연출 + 낙하음 만들기")]
    public static void Build()
    {
        GameObject host = GameObject.Find(HostName);
        if (host == null)
        {
            Debug.LogError($"[TransformerFixedCutsceneSetup] 씬에 {HostName} 오브젝트가 없다.");
            return;
        }

        Transform old = host.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Transformer Fixed Cutscene");
        root.transform.SetParent(host.transform, false);

        // ---------- 혼잣말 자막 ----------
        GameObject canvasGo = new GameObject("MonologueCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root.transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130;   // 미니게임 UI 위, 죽음 화면(500) 아래

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject group = new GameObject("Monologue", typeof(RectTransform), typeof(CanvasGroup));
        group.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)group.transform);
        CanvasGroup cg = group.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // 자막 뒤에 살짝 어두운 띠 - 밝은 벽 앞에서도 글이 읽히게
        Img(group.transform, "Backing", new Color(0f, 0f, 0f, 0.5f), new Vector2(0f, -330f), new Vector2(1300f, 92f));

        TMP_Text line = Label(group.transform, "Line",
            "발전기를 가동시켰는데 전원이 안 들어온다면... 배전반을 봐야겠군.",
            36f, new Vector2(0f, -330f), new Color(0.93f, 0.92f, 0.88f));
        line.fontStyle = FontStyles.Italic;   // 혼잣말이라 기울여 둔다

        group.SetActive(false);

        // ---------- 바라볼 표시등 ----------
        IndicatorLed led = FindLed();

        // ---------- 배선 ----------
        TransformerFixedCutscene seq = host.GetComponent<TransformerFixedCutscene>();
        if (seq == null) seq = Undo.AddComponent<TransformerFixedCutscene>(host);

        PlayerController player = FindOne<PlayerController>();
        PlayerInteractor pi = FindOne<PlayerInteractor>();

        SerializedObject so = new SerializedObject(seq);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("interactor").objectReferenceValue = pi;
        so.FindProperty("lookAt").objectReferenceValue = led != null ? led.transform : null;
        so.FindProperty("line").objectReferenceValue = line;
        so.FindProperty("lineGroup").objectReferenceValue = cg;
        so.FindProperty("turnSeconds").floatValue = 0.7f;
        so.FindProperty("fadeSeconds").floatValue = 0.45f;
        so.FindProperty("holdSeconds").floatValue = 4.2f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(seq);

        // ---------- 변압기 낙하음 ----------
        AudioClip drop = AssetDatabase.LoadAssetAtPath<AudioClip>(DropSoundPath);
        if (drop == null) Debug.LogWarning("[TransformerFixedCutsceneSetup] " + DropSoundPath + " 를 못 찾았다.");

        int wired = 0;
        var names = new System.Collections.Generic.List<string>();

        foreach (FusePickup pick in Object.FindObjectsByType<FusePickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject pso = new SerializedObject(pick);

            // 두 손으로 드는 무거운 물건만. 주머니에 넣는 퓨즈가 쿵 소리를 낼 이유가 없다.
            if (!pso.FindProperty("carryInHands").boolValue) continue;

            pso.FindProperty("dropSound").objectReferenceValue = drop;
            pso.FindProperty("minImpactSpeed").floatValue = 1.6f;
            pso.FindProperty("dropNoiseLoudness").floatValue = 60f;
            pso.FindProperty("dropNoiseRange").floatValue = 70f;
            pso.FindProperty("impactCooldown").floatValue = 0.6f;
            pso.ApplyModifiedProperties();
            EditorUtility.SetDirty(pick);

            wired++;
            names.Add(pick.gameObject.name);
        }

        Debug.Log("[TransformerFixedCutsceneSetup] 조립 완료\n" +
                  $"  player={Name(player)} / 바라볼 표시등={(led == null ? "없음 ★" : led.gameObject.name)}\n" +
                  $"  혼잣말 자막: sortingOrder 130, 0.45초 페이드 + 4.2초 유지\n" +
                  $"  낙하음={(drop == null ? "없음" : drop.name)} / 소음 60, 범위 70m (같은 층 괴물은 전력질주)\n" +
                  $"  무거운 물건 {wired}개에 적용: " + string.Join(", ", names.ToArray()), seq);
    }

    /// 변압기 수리 매니저가 쓰는 그 표시등을 그대로 쓴다. 없으면 씬에서 아무 것이나 찾는다.
    static IndicatorLed FindLed()
    {
        TransformerRepairManager mgr = FindOne<TransformerRepairManager>();
        if (mgr != null)
        {
            Object led = new SerializedObject(mgr).FindProperty("successLed").objectReferenceValue;
            if (led is IndicatorLed) return (IndicatorLed)led;
        }

        Debug.LogWarning("[TransformerFixedCutsceneSetup] TransformerRepairManager.successLed 가 비어 있다. 씬의 IndicatorLed 를 찾는다.");
        return FindOne<IndicatorLed>();
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

    static TMP_Text Label(Transform parent, string name, string text, float size, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (font != null) tmp.font = font;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        SetBox(tmp.rectTransform, pos, new Vector2(1260f, 84f));
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
        return found.Length == 0 ? null : found[0];
    }

    static string Name(Component c) => c == null ? "없음" : c.gameObject.name;
}
