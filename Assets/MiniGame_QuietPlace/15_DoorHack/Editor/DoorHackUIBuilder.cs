using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3층 문 키패드 회로 조작(영어 빨리치기) UI 를 조립하고 프리팹으로 저장한다.
/// 떨어지는 글자가 지나갈 영역, 받침선, 진행 표시를 만들고
/// DoorHackTyping / DoorHackManager 참조를 전부 배선한다.
/// </summary>
public static class DoorHackUIBuilder
{
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_DoorHackUI.prefab";
    const string RootName = "P_DoorHackUI";
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

    static TMP_FontAsset koreanFont;

    [MenuItem("Tools/QuietPlace/문 회로 조작(빨리치기) UI 만들기")]
    public static void Build()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject root = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Build Door Hack UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = Empty(root.transform, "Panel");
        Stretch((RectTransform)panel.transform);

        Image dim = Img(panel.transform, "Dim", new Color(0f, 0.03f, 0.02f, 0.9f), Vector2.zero, new Vector2(100f, 100f));
        Stretch(dim.rectTransform);

        Label(panel.transform, "Title", "키패드 회로 조작", 48f, new Vector2(0f, 470f), new Color(0.45f, 0.95f, 0.55f));
        Label(panel.transform, "Hint", "내려오는 글자를 바닥에 닿기 전에 받아쳐라 · 하나라도 놓치면 처음부터", 26f,
            new Vector2(0f, 418f), new Color(0.7f, 0.85f, 0.75f));

        // ---------- 글자가 떨어지는 영역 ----------
        GameObject area = Empty(panel.transform, "LettersArea");
        RectTransform areaRect = (RectTransform)area.transform;
        SetBox(areaRect, new Vector2(0f, 0f), new Vector2(1760f, 1000f));

        // 바닥선 - 여기까지 내려오면 놓친 것
        Img(area.transform, "DeadLine", new Color(0.95f, 0.3f, 0.3f, 0.55f), new Vector2(0f, -450f), new Vector2(1760f, 6f));
        Img(area.transform, "SafeLine", new Color(0.3f, 0.9f, 0.45f, 0.2f), new Vector2(0f, -330f), new Vector2(1760f, 2f));

        // ---------- 진행 표시 ----------
        // 정답 문자열은 일부러 보여주지 않는다. 떨어지는 글자를 그 순간 읽고 치는 게
        // 이 미니게임이라, 아래에 답을 적어두면 긴장이 전부 사라진다.
        TMP_Text progress = Label(panel.transform, "Progress", "0 / 19", 32f,
            new Vector2(0f, -450f), new Color(0.55f, 0.9f, 0.6f));

        // 대소문자를 구분하므로 대문자는 Shift 를 같이 눌러야 한다. 안 알려주면
        // 눌렀는데 안 넘어간다고 느낄 수밖에 없으니 화면에 못 박아둔다.
        Label(panel.transform, "CaseNotice", "대소문자 구분함 · 대문자는 Shift", 26f,
            new Vector2(0f, -495f), new Color(1f, 0.82f, 0.4f));

        Label(panel.transform, "Escape", "Esc  그만두기", 22f, new Vector2(0f, -530f), new Color(0.45f, 0.55f, 0.48f));

        // ---------- 떨어지는 글자 프리팹용 템플릿 ----------
        // DoorHackTyping 은 letterPrefab 이 비어 있으면 스스로 만들지만, 초록 모노 느낌을
        // 맞추려면 템플릿을 주는 게 낫다. 씬에서는 꺼둔 채 Instantiate 원본으로만 쓴다.
        GameObject letter = Empty(panel.transform, "LetterTemplate");
        TMP_Text letterText = Label(letter.transform, "Text", "A", 84f, Vector2.zero, new Color(0.6f, 1f, 0.65f));
        SetBox(letterText.rectTransform, Vector2.zero, new Vector2(120f, 130f));
        SetBox((RectTransform)letter.transform, Vector2.zero, new Vector2(120f, 130f));
        letter.SetActive(false);

        // ---------- 퍼즐 + 매니저 ----------
        // 퍼즐은 Panel 에 붙인다. 루트에 붙이면 씬이 로드된 순간부터 Update 가 돌아서
        // 미니게임을 열지도 않았는데 글자가 안 보이는 채로 떨어지고 실패음이 계속 난다.
        // Panel 에 있으면 SetActive 로 열 때마다 OnEnable 이 돌아 깨끗하게 다시 시작한다.
        DoorHackTyping typing = Undo.AddComponent<DoorHackTyping>(panel);
        typing.targetPassword = "NCAIVibeCodingBusan";
        typing.caseSensitive = true;    // 대문자는 Shift 를 같이 눌러야 한다
        typing.lettersArea = areaRect;
        typing.letterPrefab = letter;
        typing.fallSpeed = 700f;
        typing.fallSpeedRampPerHit = 26f;
        typing.dangerZoneRatio = 0.4f;
        typing.progressLabel = progress;
        typing.topY = 450f;
        typing.bottomY = -450f;
        typing.leftX = -780f;
        typing.rightX = 780f;
        typing.fontSize = 96;

        DoorHackManager manager = Undo.AddComponent<DoorHackManager>(root);
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("minigameRoot").objectReferenceValue = panel;
        so.FindProperty("typing").objectReferenceValue = typing;
        so.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);

        Selection.activeGameObject = root;
        Debug.Log($"[DoorHackUIBuilder] 조립 완료 -> {PrefabPath}\n" +
                  $"  \"{typing.targetPassword}\" ({typing.targetPassword.Length}글자)\n" +
                  $"  낙하속도 {typing.fallSpeed} -> 마지막엔 {typing.fallSpeed + typing.fallSpeedRampPerHit * (typing.targetPassword.Length - 1)}\n" +
                  $"  첫 글자 {(900f / typing.fallSpeed):0.00}초, 마지막 글자 {(900f / (typing.fallSpeed + typing.fallSpeedRampPerHit * (typing.targetPassword.Length - 1))):0.00}초", saved);
    }

    // ---------------------------------------------------------------- 유틸

    static GameObject Empty(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

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
        SetBox(tmp.rectTransform, pos, new Vector2(1700f, size * 1.7f));
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
