using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3층 컴퓨터 UI 를 조립하고 프리팹으로 저장한다.
/// 1단계 비밀번호 입력, 2단계 주파수 맞추기(대역 4개 슬라이더 + 충전 게이지)를 만들고
/// PasswordEntry / FrequencyMatchingPuzzle / FrequencyHackManager 참조를 전부 배선한다.
/// </summary>
public static class FrequencyHackUIBuilder
{
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_FrequencyHackUI.prefab";
    const string RootName = "P_FrequencyHackUI";

    // 기본 LiberationSans 에는 한글 글리프가 없어 네모로 나온다.
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

    // 3상 + 중성선을 흉내낸 4개 대역
    static readonly string[] BandLabels = { "R", "S", "T", "N" };

    // 슬라이더 양 끝이 몇 차 화음인지. 범위가 넓으면 파형이 촘촘해져서
    // 목표와의 차이를 눈으로 가릴 수 없다.
    const float MinHarmonic = 1f;
    const float MaxHarmonic = 4f;

    // 파형 네 개를 한 화면에 겹쳐 놓는다. 진폭을 다르게 줘야 서로 포개져도 구분된다.
    const float ScopeWidth = 1080f;
    const float ScopeHeight = 400f;
    const float StringWidth = 1000f;
    static readonly float[] BandAmplitudes = { 168f, 132f, 100f, 70f };

    static readonly string[] PhaseNames = { "R 상", "S 상", "T 상", "중성선" };
    static readonly Color[] BandColors =
    {
        new Color(0.90f, 0.25f, 0.25f),
        new Color(0.30f, 0.55f, 0.95f),
        new Color(0.95f, 0.82f, 0.25f),
        new Color(0.92f, 0.92f, 0.92f),
    };

    [MenuItem("Tools/QuietPlace/컴퓨터(비밀번호+주파수) UI 만들기")]
    public static void Build()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("키패드 해킹 UI",
                    $"씬에 이미 '{RootName}' 이 있다. 지우고 새로 만들까?", "새로 만들기", "취소"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Build Frequency Hack UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = Empty(root.transform, "Panel");
        Stretch((RectTransform)panel.transform);

        Image dim = Img(panel.transform, "Dim", new Color(0f, 0.02f, 0.04f, 0.86f));
        Stretch(dim.rectTransform);

        // ---------- 1단계: 비밀번호 ----------
        GameObject passwordStage = Empty(panel.transform, "Stage_Password");
        Stretch((RectTransform)passwordStage.transform);

        Label(passwordStage.transform, "Title", "방송 제어 콘솔", 52f, new Vector2(0f, 180f), new Color(0.5f, 0.95f, 0.8f));
        Label(passwordStage.transform, "Prompt", "PASSWORD", 26f, new Vector2(0f, 110f), new Color(0.6f, 0.7f, 0.75f));

        Img(passwordStage.transform, "EntryBG", new Color(1f, 1f, 1f, 0.10f), new Vector2(0f, 40f), new Vector2(560f, 88f));
        TMP_Text entry = Label(passwordStage.transform, "Entry", "", 56f, new Vector2(0f, 40f), new Color(0.95f, 0.98f, 1f));
        TMP_Text message = Label(passwordStage.transform, "Message", "비밀번호를 입력하고 Enter", 26f,
            new Vector2(0f, -40f), new Color(0.75f, 0.85f, 0.9f));

        Label(passwordStage.transform, "Escape", "X  그만두기", 22f, new Vector2(0f, -110f), new Color(0.5f, 0.55f, 0.6f));

        PasswordEntry password = Undo.AddComponent<PasswordEntry>(passwordStage);
        password.entryLabel = entry;
        password.messageLabel = message;

        // 2층 힌트가 마지막 자리를 3인지 8인지 알기 어렵게 되어 있다. 둘 중 하나를
        // 판당 한 번 뽑으므로 플레이어는 한쪽을 걸어봐야 한다.
        password.candidates = new[] { "2023", "2028" };
        password.password = "2023";   // candidates 가 비었을 때의 대비값
        passwordStage.SetActive(false);

        // ---------- 2단계: 주파수 ----------
        GameObject puzzleStage = Empty(panel.transform, "Stage_Frequency");
        Stretch((RectTransform)puzzleStage.transform);

        Label(puzzleStage.transform, "Title", "주파수 정합", 50f, new Vector2(0f, 452f), new Color(0.5f, 0.95f, 0.8f));
        Label(puzzleStage.transform, "Hint", "네 파형을 각자의 희미한 목표선에 겹쳐라 · 넷 다 LOCK 되면 완료", 26f,
            new Vector2(0f, 404f), new Color(0.75f, 0.85f, 0.9f));

        // ---------- 파형 화면 (하나에 네 개를 겹친다) ----------
        GameObject scope = Empty(puzzleStage.transform, "Scope");
        SetBox((RectTransform)scope.transform, new Vector2(0f, 172f), new Vector2(ScopeWidth, ScopeHeight));
        Img(scope.transform, "ScopeBG", new Color(0.04f, 0.06f, 0.07f, 0.94f), Vector2.zero, new Vector2(ScopeWidth, ScopeHeight));
        Img(scope.transform, "ScopeMidLine", new Color(1f, 1f, 1f, 0.13f), Vector2.zero, new Vector2(StringWidth, 2f));

        // 줄을 물고 있는 양쪽 벽
        float wallX = StringWidth * 0.5f;
        Img(scope.transform, "ClampL", new Color(0.55f, 0.58f, 0.62f), new Vector2(-wallX, 0f), new Vector2(12f, ScopeHeight - 24f));
        Img(scope.transform, "ClampR", new Color(0.55f, 0.58f, 0.62f), new Vector2( wallX, 0f), new Vector2(12f, ScopeHeight - 24f));

        // 목표선을 전부 먼저 깔아 뒤로 보내고, 내 파형을 그 위에 얹는다.
        var targetWaves = new FrequencyWaveView[BandLabels.Length];
        for (int i = 0; i < BandLabels.Length; i++)
            targetWaves[i] = BuildWave(scope.transform, "Target_" + BandLabels[i],
                BandColors[i] * 0.34f, 0f, 3f, BandAmplitudes[i]);

        var currentWaves = new FrequencyWaveView[BandLabels.Length];
        for (int i = 0; i < BandLabels.Length; i++)
            currentWaves[i] = BuildWave(scope.transform, "Wave_" + BandLabels[i],
                BandColors[i], 2.6f, 5f, BandAmplitudes[i]);

        // ---------- 슬라이더 네 줄 ----------
        FrequencyBand[] bands = new FrequencyBand[BandLabels.Length];

        for (int i = 0; i < BandLabels.Length; i++)
        {
            float y = -60f - i * 62f;
            bands[i] = BuildBand(puzzleStage.transform, BandLabels[i], BandColors[i], y, PhaseNames[i],
                targetWaves[i], currentWaves[i]);
        }

        // ---------- 충전 게이지 ----------
        Image chargeBG = Img(puzzleStage.transform, "ChargeBG", new Color(1f, 1f, 1f, 0.14f), new Vector2(0f, -358f), new Vector2(700f, 26f));
        Image charge = Img(puzzleStage.transform, "ChargeFill", new Color(0.45f, 0.95f, 0.7f, 0.95f),
            new Vector2(0f, -358f), new Vector2(700f, 26f));
        charge.type = Image.Type.Filled;
        charge.fillMethod = Image.FillMethod.Horizontal;
        charge.fillOrigin = 0;
        charge.fillAmount = 0f;

        // 충전은 이 화면이 아니라 월드(FrequencyBroadcast)에서 숨어 기다리며 찬다.
        // 참조는 남겨두되 빈 회색 막대가 화면을 어지럽히지 않게 꺼둔다.
        chargeBG.gameObject.SetActive(false);
        charge.gameObject.SetActive(false);

        Label(puzzleStage.transform, "Escape", "X  그만두기", 22f, new Vector2(0f, -462f), new Color(0.5f, 0.55f, 0.6f));

        GameObject loading = Empty(puzzleStage.transform, "LoadingIndicator");
        SetBox((RectTransform)loading.transform, new Vector2(0f, -410f), new Vector2(700f, 40f));
        Label(loading.transform, "Text", "불러오는 중...", 30f, Vector2.zero, new Color(0.55f, 0.95f, 0.75f));
        loading.SetActive(false);

        // ---------- 퍼즐 + 매니저 ----------
        FrequencyMatchingPuzzle puzzle = Undo.AddComponent<FrequencyMatchingPuzzle>(root);
        puzzle.bands = bands;
        puzzle.chargeGaugeFill = charge;
        puzzle.loadingIndicator = loading;
        puzzle.randomizeOnStart = true;
        puzzle.targetCandidates = null;   // 정수 차수로 고정하지 않는다. 목표 모양과 겹치면 된다.
        puzzle.randomMin = 0.12f;
        puzzle.randomMax = 0.88f;

        FrequencyHackManager manager = Undo.AddComponent<FrequencyHackManager>(root);
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("minigameRoot").objectReferenceValue = panel;
        so.FindProperty("passwordRoot").objectReferenceValue = passwordStage;
        so.FindProperty("passwordEntry").objectReferenceValue = password;
        so.FindProperty("puzzleRoot").objectReferenceValue = puzzleStage;
        so.FindProperty("puzzle").objectReferenceValue = puzzle;
        so.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);

        Selection.activeGameObject = root;
        Debug.Log($"[FrequencyHackUIBuilder] 조립 완료 -> {PrefabPath}\n" +
                  $"  비밀번호 후보 {string.Join(" / ", password.candidates)} (판당 50% 무작위) -> " +
                  $"대역 {bands.Length}개 (R/S/T/N), 화음 {MinHarmonic}~{MaxHarmonic}차, 충전 {puzzle.chargeDuration}초\n" +
                  "  컴퓨터의 ComputerTerminal 이 이 매니저를 부른다.", saved);
    }

    /// 슬라이더 한 줄. 파형은 위쪽 공용 화면에 있으므로 여기서 만들지 않고 받아서 잇는다.
    static FrequencyBand BuildBand(Transform parent, string label, Color color, float y, string phaseName,
        FrequencyWaveView targetWave, FrequencyWaveView currentWave)
    {
        GameObject row = Empty(parent, "Band_" + label);
        SetBox((RectTransform)row.transform, new Vector2(0f, y), new Vector2(1080f, 54f));

        // 파형과 같은 색 점을 찍어 어느 줄이 어느 파형인지 잇는다.
        Img(row.transform, "Swatch", color, new Vector2(-500f, 0f), new Vector2(22f, 22f));
        Label(row.transform, "Label", label, 26f, new Vector2(-462f, 1f), color);
        Label(row.transform, "PhaseName", phaseName, 17f, new Vector2(-400f, 0f), color * 0.8f);

        // 트랙. Slider 가 드래그를 받으려면 그 아래에 raycast 를 받는 그래픽이 있어야 한다.
        // 채움 막대는 두지 않는다 - 파형이 이미 값을 보여주고, 굵은 색 막대는 화면만 가린다.
        Image track = Img(row.transform, "Track", new Color(1f, 1f, 1f, 0.13f), new Vector2(30f, 0f), new Vector2(660f, 12f));
        track.raycastTarget = true;

        // 슬라이더에는 목표 눈금을 두지 않는다. 어디가 정답인지 미리 알려주면
        // 파형을 보며 더듬어 찾는 재미가 없어진다 - 정답은 위쪽 그래프에만 있다.

        // Slider 는 handleRect 의 부모를 손잡이 이동 범위로 쓴다. 트랙과 같은 크기의 영역을 따로 둔다.
        GameObject handleArea = Empty(row.transform, "HandleArea");
        SetBox((RectTransform)handleArea.transform, new Vector2(30f, 0f), new Vector2(660f, 30f));

        Image handleImage = Img(handleArea.transform, "Handle", new Color(0.95f, 0.98f, 1f), Vector2.zero, new Vector2(20f, 30f));
        handleImage.raycastTarget = true;
        RectTransform handle = handleImage.rectTransform;

        TMP_Text harmonicLabel = Label(row.transform, "HarmonicValue", "1.0차", 21f, new Vector2(410f, 0f), color);

        GameObject lockLamp = Empty(row.transform, "LockLamp");
        SetBox((RectTransform)lockLamp.transform, new Vector2(498f, 0f), new Vector2(70f, 28f));
        Label(lockLamp.transform, "Text", "LOCK", 21f, Vector2.zero, new Color(0.4f, 1f, 0.55f));
        lockLamp.SetActive(false);

        Slider slider = Undo.AddComponent<Slider>(row);
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = null;   // 채움 막대 없음
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.value = 0f;

        FrequencyBand band = Undo.AddComponent<FrequencyBand>(row);
        band.bandColor = color;
        band.fillImage = null;
        band.slider = slider;
        band.tolerance = 0.04f; // 0.03 은 너무 좁아 찾기 어렵다

        FrequencyBandWave wave = Undo.AddComponent<FrequencyBandWave>(row);
        SerializedObject fso = new SerializedObject(wave);
        fso.FindProperty("targetWave").objectReferenceValue = targetWave;
        fso.FindProperty("currentWave").objectReferenceValue = currentWave;
        fso.FindProperty("matchedIndicator").objectReferenceValue = lockLamp;
        fso.FindProperty("harmonicLabel").objectReferenceValue = harmonicLabel;
        fso.ApplyModifiedPropertiesWithoutUndo();

        return band;
    }

    /// 선분 48개를 이어 굵은 곡선을 만든다. 점만 찍으면 파형이 촘촘할 때 끊겨 보인다.
    static FrequencyWaveView BuildWave(Transform parent, string name, Color color, float pulseSpeed,
        float thickness, float amplitude)
    {
        GameObject go = Empty(parent, name);
        SetBox((RectTransform)go.transform, Vector2.zero, new Vector2(StringWidth, ScopeHeight));

        const int count = 72;
        var segs = new RectTransform[count];
        for (int i = 0; i < count; i++)
        {
            Image seg = Img(go.transform, "S" + i, color, Vector2.zero, new Vector2(20f, thickness));
            seg.sprite = null;              // 단색 사각형이라야 얇은 선이 또렷하다
            seg.type = Image.Type.Simple;
            segs[i] = seg.rectTransform;
        }

        FrequencyWaveView view = Undo.AddComponent<FrequencyWaveView>(go);
        view.segments = segs;
        view.width = StringWidth;       // 양쪽 벽 사이 거리
        view.amplitude = amplitude;
        view.minHarmonic = MinHarmonic;
        view.maxHarmonic = MaxHarmonic;
        view.amplitudeFalloff = 0.28f;
        view.pulseSpeed = pulseSpeed;
        view.pulseAmount = 0.15f;   // 진동이 크면 목표와 모양을 견주기 어렵다
        return view;
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

    static TMP_FontAsset koreanFont;

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
        SetBox(tmp.rectTransform, pos, new Vector2(900f, size * 1.7f));
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
