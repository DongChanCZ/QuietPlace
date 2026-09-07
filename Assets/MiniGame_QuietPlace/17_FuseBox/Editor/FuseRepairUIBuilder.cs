using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배전반 수리 미니게임 UI 를 한 번에 조립하고 프리팹으로 저장한다.
/// 17_FuseBox 의 4개 스크립트가 요구하는 Image / RectTransform 참조를 전부 배선해준다.
///
/// 손으로 만들면 오브젝트 20개 이상에 참조 10개를 일일이 끼워야 해서 빠뜨리기 쉽다.
/// 여러 번 실행해도 같은 결과가 나오도록 매번 새로 조립한다.
/// </summary>
public static class FuseRepairUIBuilder
{
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_FuseRepairUI.prefab";
    const string RootName = "P_FuseRepairUI";

    // 다이얼 반지름. 바늘/히트존 길이의 기준이 된다.
    const float DialRadius = 140f;

    [MenuItem("Tools/QuietPlace/배전반 미니게임 UI 만들기")]
    public static void Build()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("배전반 미니게임 UI",
                    $"씬에 이미 '{RootName}' 이 있다. 지우고 새로 만들까?", "새로 만들기", "취소"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Build Fuse Repair UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 다른 UI 위에 뜨도록

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 껐다 켜는 대상은 이 Panel 이다. 매니저를 루트에 두고 Panel 만 토글하면
        // 매니저가 자기 자신을 끄는 일이 없어서 Instance 와 호출 경로가 항상 살아있다.
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panel, "Create Panel");
        panel.transform.SetParent(root.transform, false);
        Stretch((RectTransform)panel.transform);

        // 뒤 화면을 살짝 어둡게 — 미니게임에 집중되도록
        Image dim = MakeImage(panel.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);

        MashToComplete breakerDown = BuildMashStage(panel.transform, "Stage1_BreakerDown",
            "차단기를 내린다", "E 연타");

        SkillCheckMinigame discharge = BuildSkillCheckStage(panel.transform, "Stage2_Discharge",
            "잔류전하 방전 (R / S / T)", "바늘이 밝은 구간에 올 때 Space · 3회");

        FuseAngleInsert fuseInsert = BuildAngleStage(panel.transform, "Stage3_FuseInsert",
            "각도를 맞춰 삽입", "A / D 회전 · E 삽입");

        MashToComplete breakerUp = BuildMashStage(panel.transform, "Stage4_BreakerUp",
            "차단기를 올린다", "E 연타");

        // 시퀀서 배선
        FuseBoxRepairSequence sequence = Undo.AddComponent<FuseBoxRepairSequence>(root);
        sequence.breakerDown = breakerDown;
        sequence.breakerDownRoot = breakerDown.gameObject;
        sequence.dischargeCheck = discharge;
        sequence.dischargeRoot = discharge.gameObject;
        sequence.fuseInsert = fuseInsert;
        sequence.fuseRoot = fuseInsert.gameObject;
        sequence.breakerUp = breakerUp;
        sequence.breakerUpRoot = breakerUp.gameObject;

        // 매니저 배선 (private 필드라 SerializedObject 로 넣는다)
        FuseRepairManager manager = Undo.AddComponent<FuseRepairManager>(root);
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("minigameRoot").objectReferenceValue = panel;
        so.FindProperty("sequence").objectReferenceValue = sequence;
        so.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);

        Selection.activeGameObject = root;
        Debug.Log($"[FuseRepairUIBuilder] 조립 완료 -> {PrefabPath}\n" +
                  $"  Stage1 {breakerDown.name} / Stage2 {discharge.name} / Stage3 {fuseInsert.name} / Stage4 {breakerUp.name}\n" +
                  $"  이 오브젝트를 GameScene 에 두고, 맵의 FuseSlot 에서 Requires Repair 를 켜면 연결된다.", saved);
    }

    // ---------------------------------------------------------------- 단계 조립

    static MashToComplete BuildMashStage(Transform parent, string name, string title, string hint)
    {
        GameObject stage = MakeStage(parent, name, title, hint);

        // 게이지 배경 + 채워지는 부분
        Image bg = MakeImage(stage.transform, "BarBG", new Color(1f, 1f, 1f, 0.15f));
        SetBox(bg.rectTransform, new Vector2(0f, -40f), new Vector2(640f, 46f));

        Image fill = MakeImage(stage.transform, "BarFill", new Color(1f, 0.72f, 0.2f, 0.95f));
        SetBox(fill.rectTransform, new Vector2(0f, -40f), new Vector2(640f, 46f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;

        MashToComplete mash = Undo.AddComponent<MashToComplete>(stage);
        mash.progressFill = fill;
        return mash;
    }

    static SkillCheckMinigame BuildSkillCheckStage(Transform parent, string name, string title, string hint)
    {
        GameObject stage = MakeStage(parent, name, title, hint);

        // 원형 다이얼
        Image dial = MakeImage(stage.transform, "Dial", new Color(1f, 1f, 1f, 0.10f));
        dial.sprite = BuiltinSprite("UI/Skin/Knob.psd");
        SetBox(dial.rectTransform, new Vector2(0f, -60f), new Vector2(DialRadius * 2f, DialRadius * 2f));

        // 히트존과 바늘은 다이얼 중심을 축으로 회전한다.
        // 스크립트가 localRotation 의 Z 만 돌리므로 피벗을 아래쪽 끝에 두고 위로 뻗게 만든다.
        RectTransform hitZone = MakeSpoke(dial.transform, "HitZone",
            new Color(0.35f, 1f, 0.45f, 0.55f), 34f);
        RectTransform needle = MakeSpoke(dial.transform, "Needle",
            new Color(1f, 0.95f, 0.9f, 1f), 6f);

        SkillCheckMinigame check = Undo.AddComponent<SkillCheckMinigame>(stage);
        check.needle = needle;
        check.hitZoneVisual = hitZone;
        return check;
    }

    static FuseAngleInsert BuildAngleStage(Transform parent, string name, string title, string hint)
    {
        GameObject stage = MakeStage(parent, name, title, hint);

        GameObject pivot = new GameObject("Pivot", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(pivot, "Create Pivot");
        pivot.transform.SetParent(stage.transform, false);
        SetBox((RectTransform)pivot.transform, new Vector2(0f, -60f), new Vector2(1f, 1f));

        // 목표 각도 가이드 (흐릿한 선)
        RectTransform guide = MakeSpoke(pivot.transform, "TargetGuideLine",
            new Color(1f, 1f, 1f, 0.28f), 10f);

        // 실제 퓨즈 — 가이드보다 두껍고 진하게
        RectTransform fuse = MakeSpoke(pivot.transform, "FuseVisual",
            new Color(0.55f, 0.78f, 1f, 1f), 26f);

        FuseAngleInsert insert = Undo.AddComponent<FuseAngleInsert>(stage);
        insert.targetGuideLine = guide;
        insert.fuseVisual = fuse;
        return insert;
    }

    // ---------------------------------------------------------------- 유틸

    /// 다이얼 중심에서 밖으로 뻗는 막대. 피벗이 아래 끝이라 Z 회전이 곧 각도가 된다.
    static RectTransform MakeSpoke(Transform parent, string name, Color color, float width)
    {
        Image img = MakeImage(parent, name, color);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(width, DialRadius);
        return rt;
    }

    static GameObject MakeStage(Transform parent, string name, string title, string hint)
    {
        GameObject stage = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(stage, "Create " + name);
        stage.transform.SetParent(parent, false);
        Stretch((RectTransform)stage.transform);

        MakeLabel(stage.transform, "Title", title, 54f, new Vector2(0f, 250f), Color.white);
        MakeLabel(stage.transform, "Hint", hint, 30f, new Vector2(0f, 190f), new Color(1f, 0.78f, 0.3f, 1f));

        stage.SetActive(false); // 시퀀서가 필요할 때 켠다
        return stage;
    }

    static void MakeLabel(Transform parent, string name, string text, float size, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        SetBox(tmp.rectTransform, pos, new Vector2(1200f, size * 1.6f));
    }

    static Image MakeImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        Image img = Undo.AddComponent<Image>(go);
        img.color = color;
        img.sprite = BuiltinSprite("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return img;
    }

    static Sprite BuiltinSprite(string path)
    {
        Sprite s = AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        if (s == null) Debug.LogWarning($"[FuseRepairUIBuilder] 내장 스프라이트 '{path}' 를 찾지 못했다. 흰 사각형으로 보인다.");
        return s;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetBox(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }
}
