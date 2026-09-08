using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 변압기 모듈 수리 미니게임의 3D 오브젝트를 조립하고 프리팹으로 저장한다.
/// 볼트 3개 / 모듈+안착목표 / 전선 4개+터미널 4개 / 밸런스 게이지 UI 를 만들고
/// 스크립트 참조를 전부 배선한다.
///
/// 로컬 XY 평면에 z=0 기준으로 눕혀서 만든다. 배전반 벽에 붙일 때 프리팹을 회전시켜
/// 플레이어를 향하게 하면 된다(드래그가 화면 평면 기준이라 카메라를 정면으로 봐야 자연스럽다).
/// </summary>
public static class TransformerRepairBuilder
{
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_TransformerRepair.prefab";
    const string MaterialDir = "Assets/@Resources/Prefabs/Manager/TransformerRepairMaterials";
    const string RootName = "P_TransformerRepair";

    [MenuItem("Tools/QuietPlace/변압기 수리 미니게임 만들기")]
    public static void Build()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("변압기 수리 미니게임",
                    $"씬에 이미 '{RootName}' 이 있다. 지우고 새로 만들까?", "새로 만들기", "취소"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        System.IO.Directory.CreateDirectory(MaterialDir);
        AssetDatabase.Refresh();

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Transformer Repair");

        // ---------- 2단계: 볼트 ----------
        GameObject boltStage = Child(root.transform, "Stage_Bolts", Vector3.zero);

        Solid(boltStage.transform, "Plate", new Vector3(0f, 0f, 0f),
            new Vector3(0.72f, 0.52f, 0.04f), Mat("M_Repair_Plate", new Color(0.34f, 0.36f, 0.38f)), false);

        GameObject wrench = Solid(boltStage.transform, "WrenchIcon", new Vector3(0.46f, 0.30f, -0.04f),
            new Vector3(0.20f, 0.045f, 0.02f), Mat("M_Repair_Wrench", new Color(0.85f, 0.78f, 0.35f)), false);
        wrench.SetActive(false); // BoltUnscrew 가 첫 클릭에 켠다

        Vector3[] boltPos =
        {
            new Vector3(-0.28f,  0.19f, -0.035f),
            new Vector3( 0.28f,  0.19f, -0.035f),
            new Vector3( 0.00f, -0.19f, -0.035f),
        };

        BoltUnscrew[] bolts = new BoltUnscrew[boltPos.Length];
        Material boltMat = Mat("M_Repair_Bolt", new Color(0.62f, 0.63f, 0.66f));

        for (int i = 0; i < boltPos.Length; i++)
        {
            GameObject go = Solid(boltStage.transform, $"Bolt_{i}", boltPos[i],
                new Vector3(0.055f, 0.055f, 0.035f), boltMat, true);
            BoltUnscrew bolt = Undo.AddComponent<BoltUnscrew>(go);
            bolt.wrenchIcon = wrench;
            bolts[i] = bolt;
        }

        BoltPuzzleManager boltManager = Undo.AddComponent<BoltPuzzleManager>(boltStage);
        boltManager.bolts = bolts;

        // ---------- 3단계: 모듈 안착 ----------
        GameObject balanceStage = Child(root.transform, "Stage_ModuleBalance", Vector3.zero);

        GameObject slotTarget = Child(balanceStage.transform, "SlotTarget", new Vector3(0f, 0f, -0.12f));

        GameObject moduleGo = Solid(balanceStage.transform, "Module", new Vector3(0.62f, 0.34f, -0.30f),
            new Vector3(0.30f, 0.24f, 0.16f), Mat("M_Repair_Module", new Color(0.45f, 0.5f, 0.6f)), true);
        Rigidbody moduleBody = Undo.AddComponent<Rigidbody>(moduleGo);
        moduleBody.isKinematic = true; // 드래그로만 움직인다

        HeavyModuleBalance balance = Undo.AddComponent<HeavyModuleBalance>(moduleGo);
        balance.slotTarget = slotTarget.transform;

        BuildGauge(balanceStage.transform, balance);

        // ---------- 4단계: 배선 ----------
        GameObject wireStage = Child(root.transform, "Stage_Wires", Vector3.zero);

        WireColor[] colors = { WireColor.R_Red, WireColor.S_Blue, WireColor.T_Yellow, WireColor.N_White };
        Color[] rgb =
        {
            new Color(0.85f, 0.20f, 0.20f),
            new Color(0.25f, 0.45f, 0.90f),
            new Color(0.90f, 0.80f, 0.20f),
            new Color(0.92f, 0.92f, 0.92f),
        };

        DraggableWire[] wires = new DraggableWire[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            float x = -0.27f + i * 0.18f;
            Material m = Mat("M_Repair_Wire_" + colors[i], rgb[i]);

            GameObject wireGo = Solid(wireStage.transform, "Wire_" + colors[i], new Vector3(x, -0.34f, -0.05f),
                new Vector3(0.07f, 0.07f, 0.05f), m, true);
            DraggableWire wire = Undo.AddComponent<DraggableWire>(wireGo);
            wire.wireColor = colors[i];
            wires[i] = wire;

            // 터미널은 색만 같고 위치는 섞어서 배치한다 (그냥 위로 끌면 맞아버리면 재미가 없다)
            int slot = (i * 3 + 1) % colors.Length;
            float tx = -0.27f + slot * 0.18f;
            GameObject termGo = Solid(wireStage.transform, "Term_" + colors[i], new Vector3(tx, 0.30f, -0.05f),
                new Vector3(0.09f, 0.09f, 0.03f), m, false);
            WireTerminal term = Undo.AddComponent<WireTerminal>(termGo);
            term.terminalColor = colors[i];
        }

        WirePuzzleManager wireManager = Undo.AddComponent<WirePuzzleManager>(wireStage);
        wireManager.wires = wires;

        // ---------- 시퀀서 + 매니저 ----------
        TransformerRepairSequence sequence = Undo.AddComponent<TransformerRepairSequence>(root);
        sequence.boltStage = boltManager;
        sequence.moduleBalanceStageRoot = balanceStage;
        sequence.moduleBalanceStage = balance;
        sequence.wireStageRoot = wireStage;
        sequence.wireStage = wireManager;

        TransformerRepairManager manager = Undo.AddComponent<TransformerRepairManager>(root);
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("minigameRoot").objectReferenceValue = root;
        so.FindProperty("sequence").objectReferenceValue = sequence;
        so.FindProperty("moduleBalance").objectReferenceValue = balance;
        SerializedProperty wireArray = so.FindProperty("wires");
        wireArray.arraySize = wires.Length;
        for (int i = 0; i < wires.Length; i++)
            wireArray.GetArrayElementAtIndex(i).objectReferenceValue = wires[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        Selection.activeGameObject = root;

        Debug.Log($"[TransformerRepairBuilder] 조립 완료 -> {PrefabPath}\n" +
                  $"  볼트 {bolts.Length}개 / 전선·터미널 {wires.Length}쌍\n" +
                  $"  배전반 앞에 배치하고 플레이어를 향하도록 회전할 것.\n" +
                  $"  FuseSlot 의 Requires Repair 를 TransformerModule 로 두면 연결된다.", saved);
    }

    // ---------------------------------------------------------------- 조각

    static void BuildGauge(Transform parent, HeavyModuleBalance balance)
    {
        GameObject canvasGo = new GameObject("GaugeUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create GaugeUI");
        canvasGo.transform.SetParent(parent, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image bg = UiImage(canvasGo.transform, "GaugeBG", new Color(1f, 1f, 1f, 0.15f),
            new Vector2(0f, -320f), new Vector2(520f, 34f));

        Image fill = UiImage(canvasGo.transform, "GaugeFill", new Color(0.3f, 0.95f, 0.45f, 0.95f),
            new Vector2(0f, -320f), new Vector2(520f, 34f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;

        // 목표 반경 안/밖을 색으로 알려주는 점
        Image dot = UiImage(canvasGo.transform, "DriftIndicator", Color.red,
            new Vector2(0f, -270f), new Vector2(26f, 26f));

        balance.balanceGaugeFill = fill;
        balance.driftIndicator = dot;
    }

    static Image UiImage(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        Image img = Undo.AddComponent<Image>(go);
        img.color = color;
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;

        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    static GameObject Child(Transform parent, string name, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go;
    }

    /// 큐브 하나. keepCollider=false 면 콜라이더를 지운다(장식용이라 클릭을 가로채면 안 된다).
    static GameObject Solid(Transform parent, string name, Vector3 localPos, Vector3 scale,
        Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        Renderer r = go.GetComponent<Renderer>();
        if (r != null && material != null) r.sharedMaterial = material;

        Collider col = go.GetComponent<Collider>();
        if (col != null && !keepCollider) Object.DestroyImmediate(col);

        return go;
    }

    static Material Mat(string name, Color color)
    {
        string path = $"{MaterialDir}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { existing.color = color; return existing; }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material m = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
