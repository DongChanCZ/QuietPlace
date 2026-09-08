using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// P_TransformerRepair 안의 4단계(색선 연결)를 화면 UI 방식으로 다시 조립한다.
///
/// 3D 선/단자(Wire_*, Term_*)는 지우지 않고 끄기만 한다.
/// </summary>
public static class WirePuzzleUIBuilder
{
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_TransformerRepair.prefab";
    const string StageName = "Stage_Wires";
    const string UIName = "WireUI";
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

    static TMP_FontAsset koreanFont;

    // 단자는 선과 다른 순서로 놓아야 색을 보고 찾는 의미가 생긴다.
    static readonly WireColor[] WireOrder = { WireColor.R_Red, WireColor.S_Blue, WireColor.T_Yellow, WireColor.N_White };
    static readonly int[] TerminalSlot = { 2, 0, 3, 1 };   // WireOrder[i] 단자가 놓일 자리

    static readonly Color[] Colors =
    {
        new Color(0.90f, 0.25f, 0.25f),   // R 빨강
        new Color(0.30f, 0.55f, 0.95f),   // S 파랑
        new Color(0.95f, 0.82f, 0.25f),   // T 노랑
        new Color(0.92f, 0.92f, 0.92f),   // N 흰색
    };
    static readonly string[] Labels = { "R", "S", "T", "N" };

    [MenuItem("Tools/QuietPlace/변압기 색선 연결 UI 만들기")]
    public static void Build()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (contents == null) { Debug.LogError($"[WirePuzzleUIBuilder] {PrefabPath} 를 못 열었다."); return; }

        try
        {
            Transform stage = null;
            foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                if (t.name == StageName) stage = t;
            if (stage == null) { Debug.LogError($"[WirePuzzleUIBuilder] {StageName} 이 없다."); return; }

            // 옛 3D 선/단자는 끄기만 한다
            int turnedOff = 0;
            foreach (Transform child in stage)
            {
                if (!child.name.StartsWith("Wire_") && !child.name.StartsWith("Term_")) continue;
                if (!child.gameObject.activeSelf) continue;
                child.gameObject.SetActive(false);
                turnedOff++;
            }

            Transform old = stage.Find(UIName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject uiRoot = new GameObject(UIName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot.transform.SetParent(stage, false);

            Canvas canvas = uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Image dim = Img(uiRoot.transform, "Dim", new Color(0.02f, 0.02f, 0.03f, 0.9f), Vector2.zero, Vector2.one * 100f);
            Stretch(dim.rectTransform);

            Label(uiRoot.transform, "Title", "배선 연결", 48f, new Vector2(0f, 400f), new Color(0.6f, 0.85f, 1f));
            TMP_Text hint = Label(uiRoot.transform, "Hint", "왼쪽 선을 잡아 같은 색 단자에 연결하라", 28f,
                new Vector2(0f, 348f), new Color(0.78f, 0.82f, 0.86f));

            GameObject areaGo = Empty(uiRoot.transform, "Area");
            RectTransform area = (RectTransform)areaGo.transform;
            SetBox(area, new Vector2(0f, 10f), new Vector2(1300f, 640f));
            Img(area, "AreaBG", new Color(1f, 1f, 1f, 0.05f), Vector2.zero, new Vector2(1300f, 640f));

            // 왼쪽 벽(선이 나오는 곳) / 오른쪽 단자판
            Img(area, "LeftPanel", new Color(0.14f, 0.15f, 0.17f), new Vector2(-560f, 0f), new Vector2(120f, 600f));
            Img(area, "RightPanel", new Color(0.14f, 0.15f, 0.17f), new Vector2(560f, 0f), new Vector2(120f, 600f));

            float[] y = { 225f, 75f, -75f, -225f };
            var wireList = new WirePuzzleUI.Wire[WireOrder.Length];

            for (int i = 0; i < WireOrder.Length; i++)
            {
                Color c = Colors[i];
                string tag = Labels[i];

                // ---- 단자 (오른쪽, 순서를 섞어 놓는다) ----
                GameObject termGo = Empty(area, "Term_" + tag);
                RectTransform term = (RectTransform)termGo.transform;
                SetBox(term, new Vector2(560f, y[TerminalSlot[i]]), new Vector2(96f, 96f));
                Img(term, "Socket", new Color(0.06f, 0.07f, 0.08f), Vector2.zero, new Vector2(96f, 96f));
                Image termRing = Img(term, "Ring", c, Vector2.zero, new Vector2(112f, 112f));
                termRing.transform.SetAsFirstSibling();
                Label(term, "Tag", tag, 30f, new Vector2(78f, 0f), c);

                // ---- 선 몸통 + 끝 (왼쪽) ----
                Vector2 home = new Vector2(-500f, y[i]);

                GameObject cableGo = Empty(area, "Cable_" + tag);
                RectTransform cable = (RectTransform)cableGo.transform;
                cable.anchorMin = cable.anchorMax = new Vector2(0.5f, 0.5f);
                cable.pivot = new Vector2(0.5f, 0.5f);
                cable.sizeDelta = new Vector2(1f, 14f);
                Image cableImg = cableGo.AddComponent<Image>();
                cableImg.color = c * 0.75f;
                cableImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                cableImg.type = Image.Type.Sliced;
                cableImg.raycastTarget = false;

                GameObject plugGo = Empty(area, "Plug_" + tag);
                RectTransform plug = (RectTransform)plugGo.transform;
                SetBox(plug, home, new Vector2(84f, 84f));
                Image plugImg = Img(plug, "Head", c, Vector2.zero, new Vector2(84f, 84f));
                Img(plug, "Tip", new Color(0.85f, 0.86f, 0.88f), new Vector2(28f, 0f), new Vector2(26f, 34f));
                Label(plug, "Tag", tag, 28f, new Vector2(-64f, 0f), c);

                wireList[i] = new WirePuzzleUI.Wire
                {
                    color = WireOrder[i],
                    plug = plug,
                    terminal = term,
                    cable = cable,
                    plugImage = plugImg,
                    terminalImage = termRing,
                };
            }

            TMP_Text counter = Label(uiRoot.transform, "Counter", "연결 0 / 4", 32f,
                new Vector2(0f, -390f), new Color(0.95f, 0.95f, 0.95f));
            Label(uiRoot.transform, "Escape", "X  그만두기", 22f, new Vector2(0f, -440f), new Color(0.55f, 0.58f, 0.62f));

            WirePuzzleUI puzzle = stage.GetComponent<WirePuzzleUI>();
            if (puzzle == null) puzzle = stage.gameObject.AddComponent<WirePuzzleUI>();
            puzzle.wires = wireList;
            puzzle.bounds = area;
            puzzle.grabRadius = 70f;
            puzzle.snapRadius = 80f;
            puzzle.hintLabel = hint;
            puzzle.counterLabel = counter;

            TransformerRepairSequence seq = contents.GetComponent<TransformerRepairSequence>();
            if (seq != null)
            {
                seq.wireStageUI = puzzle;
                seq.wireStage = null;   // 3D 방식은 더 이상 듣지 않는다
            }

            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            Debug.Log($"[WirePuzzleUIBuilder] 조립 완료 -> {PrefabPath}\n" +
                      $"  옛 3D 선/단자 {turnedOff}개 비활성 (지우지 않음)\n" +
                      $"  선 {wireList.Length}개, 단자 순서 섞음, 스냅 {puzzle.snapRadius}px");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.Refresh();
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

        if (koreanFont == null) koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (koreanFont != null) tmp.font = koreanFont;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        SetBox(tmp.rectTransform, pos, new Vector2(size * 6f, size * 1.7f));
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
