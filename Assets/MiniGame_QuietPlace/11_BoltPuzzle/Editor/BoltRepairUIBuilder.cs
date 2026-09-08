using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 볼트 풀기 미니게임 UI 를 조립하고 프리팹으로 저장한다.
/// 육각 볼트 그림(6방향 플랜지 + 머리)과 진행 게이지, 안내문을 만들고
/// BoltTurnPuzzle / BoltRepairManager 참조를 전부 배선한다.
/// </summary>
public static class BoltRepairUIBuilder
{
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_BoltRepairUI.prefab";
    const string RootName = "P_BoltRepairUI";
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

    static TMP_FontAsset koreanFont;

    [MenuItem("Tools/QuietPlace/볼트 풀기 UI 만들기")]
    public static void Build()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject root = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Build Bolt Repair UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = Empty(root.transform, "Panel");
        Stretch((RectTransform)panel.transform);

        Image dim = Img(panel.transform, "Dim", new Color(0.02f, 0.02f, 0.03f, 0.88f));
        Stretch(dim.rectTransform);

        Label(panel.transform, "Title", "육각 볼트 풀기", 52f, new Vector2(0f, 340f), new Color(0.95f, 0.85f, 0.5f));
        TMP_Text hint = Label(panel.transform, "Hint", "왼쪽 버튼을 누른 채로 반시계 방향으로 계속 돌려라", 28f,
            new Vector2(0f, 280f), new Color(0.78f, 0.82f, 0.86f));

        // ---------- 볼트 ----------
        // 회전 중심을 따로 둔다. BoltTurnPuzzle 이 마우스 각도를 이 사각형 기준으로 잰다.
        GameObject pivotGo = Empty(panel.transform, "BoltPivot");
        RectTransform pivot = (RectTransform)pivotGo.transform;
        SetBox(pivot, new Vector2(0f, 10f), new Vector2(360f, 360f));

        Img(pivot, "Socket", new Color(0.10f, 0.11f, 0.13f), Vector2.zero, new Vector2(340f, 340f));

        GameObject boltGo = Empty(pivot, "Bolt");
        RectTransform bolt = (RectTransform)boltGo.transform;
        SetBox(bolt, Vector2.zero, new Vector2(300f, 300f));

        // 육각 머리를 흉내낸다: 60도 간격 플랜지 6장 + 가운데 머리 + 방향 표시 홈
        for (int i = 0; i < 6; i++)
        {
            Image flange = Img(bolt, "Flange_" + i, new Color(0.62f, 0.64f, 0.68f), Vector2.zero, new Vector2(230f, 133f));
            flange.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
        }
        Img(bolt, "Head", new Color(0.72f, 0.74f, 0.78f), Vector2.zero, new Vector2(150f, 150f));
        Img(bolt, "Notch", new Color(0.24f, 0.25f, 0.28f), new Vector2(0f, 52f), new Vector2(26f, 96f));

        // 반시계가 어느 쪽인지 알려주는 표시.
        // 화살표 글리프(U+21BA)는 맑은고딕에 없어서 네모로 나온다. 한글로 적는다.
        Label(pivot, "TurnArrow", "반시계\n방향", 34f, new Vector2(-255f, 0f), new Color(0.55f, 0.9f, 0.7f));

        TMP_Text counter = Label(panel.transform, "Counter", "볼트 0 / 3 바퀴", 38f,
            new Vector2(0f, -240f), new Color(0.95f, 0.95f, 0.95f));

        // ---------- 힘 게이지 ----------
        // 지금 얼마나 힘을 주고 있는지, 그리고 어디를 넘겨야 볼트가 움직이는지 보여준다.
        Label(panel.transform, "TorqueLabel", "주는 힘", 22f, new Vector2(-420f, -290f), new Color(0.7f, 0.72f, 0.75f));
        Img(panel.transform, "TorqueBG", new Color(1f, 1f, 1f, 0.14f), new Vector2(0f, -290f), new Vector2(700f, 26f));
        Image torque = Img(panel.transform, "TorqueFill", new Color(0.85f, 0.35f, 0.3f, 0.95f),
            new Vector2(0f, -290f), new Vector2(700f, 26f));
        torque.type = Image.Type.Filled;
        torque.fillMethod = Image.FillMethod.Horizontal;
        torque.fillOrigin = 0;
        torque.fillAmount = 0f;

        // breakawayTorque / maxTorque = 0.45 / 1.6 지점에 문턱 표시
        Image marker = Img(panel.transform, "BreakawayMarker", new Color(0.95f, 0.95f, 0.6f, 0.9f),
            new Vector2(-350f + 700f * (0.45f / 1.6f), -290f), new Vector2(4f, 40f));

        // ---------- 진행 게이지 ----------
        Label(panel.transform, "ProgressLabel", "풀린 정도", 22f, new Vector2(-420f, -340f), new Color(0.7f, 0.72f, 0.75f));
        Img(panel.transform, "ProgressBG", new Color(1f, 1f, 1f, 0.14f), new Vector2(0f, -340f), new Vector2(700f, 26f));
        Image progress = Img(panel.transform, "ProgressFill", new Color(0.95f, 0.78f, 0.35f, 0.95f),
            new Vector2(0f, -340f), new Vector2(700f, 26f));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = 0;
        progress.fillAmount = 0f;

        Label(panel.transform, "Escape", "Esc  그만두기", 22f, new Vector2(0f, -400f), new Color(0.55f, 0.58f, 0.62f));

        // ---------- 퍼즐 + 매니저 ----------
        BoltTurnPuzzle puzzle = Undo.AddComponent<BoltTurnPuzzle>(root);
        puzzle.boltVisual = bolt;
        puzzle.pivot = pivot;
        puzzle.requiredTurns = 3;
        puzzle.degreesPerTurn = 200f;

        // 뻑뻑함: 마우스를 초당 약 142도(1.7 / 0.012) 이상 꾸준히 돌려야 힘이 유지되고,
        // 문턱 0.55 를 넘겨야 볼트가 비로소 움직인다. 넘겨도 초당 45도가 상한이라
        // 3바퀴 x 200도 = 600도를 푸는 데 최선으로 붙잡아도 13초 넘게 걸린다.
        puzzle.breakawayTorque = 0.55f;
        puzzle.maxTorque = 1.6f;
        puzzle.torqueGainPerDegree = 0.012f;
        puzzle.torqueDecayPerSecond = 1.7f;
        puzzle.maxTurnRate = 45f;
        puzzle.strainShakeDegrees = 2.2f;

        puzzle.progressFill = progress;
        puzzle.torqueFill = torque;
        puzzle.breakawayMarker = marker;
        puzzle.counterLabel = counter;
        puzzle.hintLabel = hint;

        BoltRepairManager manager = Undo.AddComponent<BoltRepairManager>(root);
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("minigameRoot").objectReferenceValue = panel;
        so.FindProperty("puzzle").objectReferenceValue = puzzle;
        so.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);

        Selection.activeGameObject = root;
        Debug.Log($"[BoltRepairUIBuilder] 조립 완료 -> {PrefabPath}\n" +
                  $"  {puzzle.requiredTurns}바퀴 x {puzzle.degreesPerTurn}도, 문턱 {puzzle.breakawayTorque}, " +
                  $"최대 회전속도 {puzzle.maxTurnRate}도/초 (최선으로 붙잡아도 약 " +
                  $"{(puzzle.requiredTurns * puzzle.degreesPerTurn / puzzle.maxTurnRate):0.0}초)", saved);
    }

    // ---------------------------------------------------------------- 유틸

    static GameObject Empty(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image Img(Transform parent, string name, Color color) => Img(parent, name, color, Vector2.zero, new Vector2(100f, 100f));

    static Image Img(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = Empty(parent, name);
        Image img = Undo.AddComponent<Image>(go);
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
        TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);

        if (koreanFont == null) koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (koreanFont != null) tmp.font = koreanFont;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        SetBox(tmp.rectTransform, pos, new Vector2(1000f, size * 1.7f));
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
}
