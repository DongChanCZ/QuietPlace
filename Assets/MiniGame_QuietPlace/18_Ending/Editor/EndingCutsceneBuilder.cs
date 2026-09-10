using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔딩 컷신을 씬에 조립한다 - 전용 카메라, 7개 컷의 카메라 앵커, USB 문서 화면,
/// 암전 화면, 그리고 지하 1층의 박쥐 그림자 세트까지.
///
/// 앵커 위치는 맵의 실제 좌표에서 계산한다(방송 콘솔, 3층 창문, 알 방). 좌표를 코드에
/// 박아두면 팀원이 맵을 옮겼을 때 조용히 엉뚱한 곳을 찍는다. 못 찾은 것만 기본값으로 둔다.
///
/// 맵 프리팹 안에는 아무 것도 넣지 않는다(CLAUDE.md). 세트는 전부 씬 루트에 만든다.
/// </summary>
public static class EndingCutsceneBuilder
{
    const string HostName = "GameManager";
    const string RootName = "Ending";
    const string SetRootName = "ENDING_B1_RevealSet";
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

    [MenuItem("Tools/QuietPlace/엔딩 컷신 만들기")]
    public static void Build()
    {
        GameObject host = GameObject.Find(HostName);
        if (host == null)
        {
            Debug.LogError($"[EndingCutsceneBuilder] 씬에 {HostName} 오브젝트가 없다. GameScene 을 열고 실행할 것.");
            return;
        }

        // ---------- 기준 좌표 수집 ----------
        ComputerTerminal console = FindConsole();
        Transform monitor = FindMonitor();
        FrequencyBroadcast broadcast = FindOne<FrequencyBroadcast>();
        Transform emit = broadcast != null
            ? new SerializedObject(broadcast).FindProperty("emitPoint").objectReferenceValue as Transform
            : null;
        Transform window = FindByName("WIN_3F_North_West_Glass");
        Transform egg = FindByName("MESH_EggPod");

        Vector3 consoleAt = console != null ? console.transform.position : new Vector3(6.83f, 8.34f, -10.00f);
        Vector3 monitorAt = monitor != null ? monitor.position : new Vector3(5.45f, 9.64f, -8.48f);
        Vector3 emitAt = emit != null ? emit.position : consoleAt + Vector3.up * 1.6f;
        Vector3 windowAt = window != null ? window.position : new Vector3(10.50f, 10.53f, -10.50f);
        Vector3 eggAt = egg != null ? egg.position : new Vector3(-16.00f, -2.86f, 2.99f);

        // ---------- 루트 ----------
        Transform old = host.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Ending Cutscene");
        root.transform.SetParent(host.transform, false);

        // ---------- 컷신 카메라 ----------
        GameObject camGo = new GameObject("EndingCamera");
        camGo.transform.SetParent(root.transform, false);
        Camera cam = camGo.AddComponent<Camera>();
        cam.depth = 10f;                 // 플레이어 카메라 위에 그린다
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.05f;
        cam.enabled = false;

        // ---------- 카메라 앵커 ----------
        GameObject anchors = new GameObject("Anchors");
        anchors.transform.SetParent(root.transform, false);

        // 컷1 - 콘솔 모니터를 타이트하게. 모니터 정면 0.9m.
        Transform shot1 = LookAnchor(anchors, "Shot1_Monitor", monitorAt + new Vector3(0.15f, 0.25f, 0.9f), monitorAt);

        // 컷2 - 확성기 익스트림 클로즈업. 0.55m.
        Transform shot2 = LookAnchor(anchors, "Shot2_Speaker", emitAt + new Vector3(0.4f, 0.1f, 0.55f), emitAt);

        // 컷3 - 방 안의 괴물. 방송실 가운데를 기준으로 넓게 / 가깝게.
        Vector3 roomCenter = consoleAt + new Vector3(-1.5f, 0.9f, 2.6f);
        Transform shot3Wide = LookAnchor(anchors, "Shot3_MonsterWide", roomCenter + new Vector3(0f, 0.9f, 3.4f), roomCenter);
        Transform shot3Close = LookAnchor(anchors, "Shot3_MonsterClose", roomCenter + new Vector3(0.5f, 0.5f, 1.5f), roomCenter);

        // 컷4 - 숄더샷. 콘솔 뒤 오른쪽 위에서 모니터를 함께 본다.
        Transform shot4 = LookAnchor(anchors, "Shot4_OverShoulder",
            monitorAt + new Vector3(0.75f, 0.65f, 1.9f), monitorAt + new Vector3(0f, -0.1f, 0f));

        // 컷5 - 완강기. 건물 밖(북쪽 z-)에서 창문 높이 -> 지면까지 같이 내려온다.
        float outZ = windowAt.z - 3.4f;
        Transform shot5Top = LookAnchor(anchors, "Shot5_RappelTop",
            new Vector3(windowAt.x, windowAt.y - 0.3f, outZ), new Vector3(windowAt.x, windowAt.y - 0.6f, windowAt.z));
        Transform shot5Bottom = LookAnchor(anchors, "Shot5_Ground",
            new Vector3(windowAt.x, 1.9f, outZ), new Vector3(windowAt.x, 1.2f, windowAt.z));

        // 컷6 - 플레이어를 떠나 외벽을 타고 내려가 지하로 스며든다.
        Transform shot6Start = LookAnchor(anchors, "Shot6_LookBack",
            new Vector3(windowAt.x, 1.75f, outZ - 0.4f), new Vector3(windowAt.x, 3.5f, windowAt.z));
        Transform shot6Mid = LookAnchor(anchors, "Shot6_WallDown",
            new Vector3(windowAt.x, 0.35f, windowAt.z - 1.6f), new Vector3(windowAt.x, -1.5f, windowAt.z + 1.0f));
        Transform shot6End = LookAnchor(anchors, "Shot6_IntoB1",
            new Vector3(-11.0f, -2.2f, 5.6f), new Vector3(-14.6f, -2.6f, 1.2f));

        // 컷7 - 알과 그림자. 알을 앞에 두고 북쪽 벽(z=0)을 정면에 잡는다.
        Transform shot7 = LookAnchor(anchors, "Shot7_Reveal",
            new Vector3(-13.2f, -2.35f, 5.2f), new Vector3(-15.3f, -2.55f, 0.4f));

        // ---------- 플레이어 연기 자리 ----------
        // 3층 바닥 높이를 실제로 재서 쓴다. 콘솔 피벗이 바닥에 있다는 보장이 없다.
        float floor3F = FloorUnder(monitorAt, consoleAt.y);

        GameObject poses = new GameObject("Poses");
        poses.transform.SetParent(root.transform, false);

        // 콘솔 앞에서 모니터를 마주보고 타이핑
        Transform poseTyping = LookAnchor(poses, "Pose_Typing",
            new Vector3(monitorAt.x + 0.1f, floor3F, monitorAt.z + 1.05f), new Vector3(monitorAt.x, floor3F, monitorAt.z));

        // 창문 밖 로프 시작 - 벽을 마주본다
        Transform poseRopeTop = LookAnchor(poses, "Pose_RopeTop",
            new Vector3(windowAt.x, windowAt.y - 1.1f, windowAt.z - 0.75f), new Vector3(windowAt.x, windowAt.y - 1.1f, windowAt.z));

        // 착지 지점 - 건물을 마주본다
        Transform poseLanded = LookAnchor(poses, "Pose_Landed",
            new Vector3(windowAt.x, 0f, windowAt.z - 1.3f), new Vector3(windowAt.x, 1f, windowAt.z));

        // 돌아서 걸어간 끝
        Transform poseWalkEnd = LookAnchor(poses, "Pose_WalkEnd",
            new Vector3(windowAt.x, 0f, windowAt.z - 4.4f), new Vector3(windowAt.x, 1f, windowAt.z - 6f));

        // ---------- 문서 화면 ----------
        EndingDocumentScreen doc = BuildDocumentScreen(root.transform);

        // ---------- 암전 화면 ----------
        CanvasGroup fade;
        TMP_Text endTitle;
        BuildFade(root.transform, out fade, out endTitle);

        // ---------- 완강기 더미 ----------
        GameObject dummy = BuildRappelDummy(root.transform, windowAt);

        // ---------- 지하 1층 그림자 세트 ----------
        BatShadowReveal reveal = BuildRevealSet(eggAt);

        // ---------- BuildingPower 가 이 세트 조명을 끄지 않게 한다 ----------
        // BuildingPower 는 시작할 때 씬의 모든 비-Directional 조명을 모아 꺼버린다.
        // 엔딩 세트 조명은 건물 전원과 무관하므로 이름으로 빼둔다.
        ExcludeFromBuildingPower("ENDING");

        // ---------- 배선 ----------
        EndingCutscene seq = host.GetComponent<EndingCutscene>();
        if (seq == null) seq = Undo.AddComponent<EndingCutscene>(host);

        PlayerController pc = FindOne<PlayerController>();
        PlayerInteractor pi = FindOne<PlayerInteractor>();

        GameObject hud = null; Image hudFill = null; TMP_Text hudLabel = null;
        if (broadcast != null)
        {
            SerializedObject bso = new SerializedObject(broadcast);
            hud = bso.FindProperty("hudRoot").objectReferenceValue as GameObject;
            hudFill = bso.FindProperty("progressFill").objectReferenceValue as Image;
            hudLabel = bso.FindProperty("label").objectReferenceValue as TMP_Text;
        }

        MonsterController[] monsters = Object.FindObjectsByType<MonsterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        SerializedObject so = new SerializedObject(seq);
        so.FindProperty("player").objectReferenceValue = pc;
        so.FindProperty("interactor").objectReferenceValue = pi;
        so.FindProperty("cutsceneCamera").objectReferenceValue = cam;

        // 컷신 동안 내릴 인게임 HUD. 위의 hud(방송 진행 표시)와는 다른 것이다.
        List<GameObject> gameplayHud = CollectGameplayHud(root.transform);
        SerializedProperty hudArr = so.FindProperty("hudToHide");
        hudArr.arraySize = gameplayHud.Count;
        for (int i = 0; i < gameplayHud.Count; i++)
            hudArr.GetArrayElementAtIndex(i).objectReferenceValue = gameplayHud[i];
        so.FindProperty("fadeGroup").objectReferenceValue = fade;
        so.FindProperty("endTitle").objectReferenceValue = endTitle;

        so.FindProperty("shot1").objectReferenceValue = shot1;
        so.FindProperty("broadcastHud").objectReferenceValue = hud;
        so.FindProperty("broadcastFill").objectReferenceValue = hudFill;
        so.FindProperty("broadcastLabel").objectReferenceValue = hudLabel;

        so.FindProperty("shot2").objectReferenceValue = shot2;
        so.FindProperty("shot3Wide").objectReferenceValue = shot3Wide;
        so.FindProperty("shot3Close").objectReferenceValue = shot3Close;

        SerializedProperty arr = so.FindProperty("monsters");
        arr.arraySize = monsters.Length;
        for (int i = 0; i < monsters.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = monsters[i];

        so.FindProperty("poseTyping").objectReferenceValue = poseTyping;
        so.FindProperty("poseRopeTop").objectReferenceValue = poseRopeTop;
        so.FindProperty("poseLanded").objectReferenceValue = poseLanded;
        so.FindProperty("poseWalkEnd").objectReferenceValue = poseWalkEnd;
        so.FindProperty("hidePlayerModel").boolValue = false;   // 연기 애니메이션이 들어와 있다

        so.FindProperty("shot4").objectReferenceValue = shot4;
        so.FindProperty("documentScreen").objectReferenceValue = doc;

        so.FindProperty("shot5Top").objectReferenceValue = shot5Top;
        so.FindProperty("shot5Bottom").objectReferenceValue = shot5Bottom;
        so.FindProperty("rappelDummy").objectReferenceValue = dummy;

        so.FindProperty("shot6Start").objectReferenceValue = shot6Start;
        so.FindProperty("shot6Mid").objectReferenceValue = shot6Mid;
        so.FindProperty("shot6End").objectReferenceValue = shot6End;

        so.FindProperty("shot7").objectReferenceValue = shot7;
        so.FindProperty("reveal").objectReferenceValue = reveal;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(seq);

        Debug.Log("[EndingCutsceneBuilder] 조립 완료\n" +
                  $"  기준: 콘솔={Name(console)} 모니터={Name(monitor)} 확성기={Name(emit)} 창문={Name(window)} 알={Name(egg)}\n" +
                  $"  카메라 앵커 11개 / 괴물 {monsters.Length}마리 등록\n" +
                  $"  송출 HUD={(hud == null ? "못 찾음 ★" : hud.name)}\n" +
                  $"  문서 화면={(doc == null ? "실패 ★" : "OK")} / 암전={(fade == null ? "실패 ★" : "OK")}\n" +
                  $"  지하 그림자 세트={(reveal == null ? "실패 ★" : reveal.gameObject.name)} (알 {eggAt.ToString("F1")} 기준)\n" +
                  "  송출이 끝나면 FrequencyBroadcast 가 자동으로 시작한다", seq);
    }

    // ---------------------------------------------------------------- 문서 화면

    static EndingDocumentScreen BuildDocumentScreen(Transform parent)
    {
        GameObject canvasGo = new GameObject("DocumentCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(parent, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;   // 미니게임(100)/컷신 자막(130) 위, 암전(600) 아래

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject group = new GameObject("Document", typeof(RectTransform), typeof(CanvasGroup));
        group.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)group.transform);
        CanvasGroup cg = group.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // 모니터를 들여다보는 느낌 - 화면 가운데를 어두운 판으로 덮고 그 위에 글을 올린다
        Img(group.transform, "Backing", new Color(0.02f, 0.05f, 0.06f, 0.93f), new Vector2(0f, 20f), new Vector2(1180f, 720f));
        Img(group.transform, "HeaderBar", new Color(0.55f, 0.14f, 0.14f, 0.85f), new Vector2(0f, 348f), new Vector2(1180f, 64f));

        TMP_Text header = Label(group.transform, "Header", "", 26f, new Vector2(0f, 348f),
            new Color(1f, 0.92f, 0.9f), new Vector2(1120f, 60f), TextAlignmentOptions.Center);

        TMP_Text body = Label(group.transform, "Body", "", 24f, new Vector2(0f, 10f),
            new Color(0.62f, 0.95f, 0.72f), new Vector2(1100f, 600f), TextAlignmentOptions.TopLeft);
        body.lineSpacing = 12f;

        Img(group.transform, "BarBG", new Color(1f, 1f, 1f, 0.14f), new Vector2(0f, -336f), new Vector2(1100f, 22f));
        Image barFill = Img(group.transform, "BarFill", new Color(0.45f, 0.9f, 0.7f, 0.95f), new Vector2(0f, -336f), new Vector2(1100f, 22f));
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = 0;
        barFill.fillAmount = 0f;

        TMP_Text progressLabel = Label(group.transform, "ProgressLabel", "", 22f, new Vector2(0f, -370f),
            new Color(0.85f, 0.9f, 0.9f), new Vector2(1100f, 34f), TextAlignmentOptions.Center);

        group.SetActive(false);

        // 얼굴에 반사되는 파란 불빛. 모니터 자리에 두고 플레이어 쪽을 비춘다.
        GameObject lightGo = new GameObject("ScreenLight");
        lightGo.transform.SetParent(parent, false);
        Transform monitor = FindMonitor();
        lightGo.transform.position = (monitor != null ? monitor.position : new Vector3(5.45f, 9.64f, -8.48f)) + new Vector3(0f, 0.1f, 0.35f);
        Light screenLight = lightGo.AddComponent<Light>();
        screenLight.type = LightType.Point;
        screenLight.range = 3.2f;
        screenLight.intensity = 0f;
        screenLight.color = new Color(0.42f, 0.62f, 1f);

        EndingDocumentScreen doc = canvasGo.AddComponent<EndingDocumentScreen>();
        SerializedObject so = new SerializedObject(doc);
        so.FindProperty("root").objectReferenceValue = group;
        so.FindProperty("group").objectReferenceValue = cg;
        so.FindProperty("header").objectReferenceValue = header;
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("progressFill").objectReferenceValue = barFill;
        so.FindProperty("progressLabel").objectReferenceValue = progressLabel;
        so.FindProperty("screenLight").objectReferenceValue = screenLight;
        so.ApplyModifiedProperties();

        return doc;
    }

    // ---------------------------------------------------------------- 암전

    static void BuildFade(Transform parent, out CanvasGroup group, out TMP_Text title)
    {
        GameObject canvasGo = new GameObject("EndingFadeCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(parent, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;   // 무엇보다 위

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject go = new GameObject("Black", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)go.transform);
        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image black = go.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        title = Label(go.transform, "EndTitle", "", 54f, Vector2.zero,
            new Color(0.9f, 0.9f, 0.9f), new Vector2(1400f, 120f), TextAlignmentOptions.Center);
        title.characterSpacing = 14f;

        go.SetActive(false);
    }

    // ---------------------------------------------------------------- 완강기 더미

    /// <summary>
    /// 로프에 매달려 내려오는 플레이어 자리. 전용 애니메이션(Climbing A Rope)이 아직
    /// 프로젝트에 없어서, 우선 로프 선과 실루엣만 세워 컷이 비지 않게 한다.
    /// 애니메이션이 들어오면 이 더미를 그 모델로 갈아끼우면 된다.
    /// </summary>
    static GameObject BuildRappelDummy(Transform parent, Vector3 windowAt)
    {
        GameObject dummy = new GameObject("RappelDummy");
        dummy.transform.SetParent(parent, false);
        dummy.transform.position = new Vector3(windowAt.x, 0f, windowAt.z - 0.5f);

        // 로프 - 창문에서 지면까지 늘어진 선
        GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rope.name = "Rope";
        rope.transform.SetParent(dummy.transform, true);
        rope.transform.position = new Vector3(windowAt.x, windowAt.y * 0.5f, windowAt.z - 0.45f);
        rope.transform.localScale = new Vector3(0.035f, windowAt.y * 0.5f, 0.035f);
        Object.DestroyImmediate(rope.GetComponent<Collider>());

        // 사람은 실제 플레이어가 연기한다(Climbing A Rope). 여기서는 로프만 세운다.
        Material dark = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        dark.color = new Color(0.06f, 0.06f, 0.07f);
        rope.GetComponent<Renderer>().sharedMaterial = dark;

        dummy.SetActive(false);
        return dummy;
    }

    // ---------------------------------------------------------------- 지하 그림자 세트

    /// <summary>
    /// 알 방의 북쪽 벽(z=0)에 박쥐 실루엣을 드리운다.
    ///
    /// 3D 모델을 만들지 않는다. 귀와 날개 모양 판을 벽 앞에 세우고 스포트라이트를 쏴서
    /// 그림자만 남긴다. 판 자체는 ShadowsOnly 라 화면에 보이지 않는다.
    /// 빛에서 판까지 1.4m, 판에서 벽까지 2.0m 라 그림자가 2.4배쯤 커진다.
    /// </summary>
    static BatShadowReveal BuildRevealSet(Vector3 eggAt)
    {
        GameObject old = GameObject.Find(SetRootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        // 맵 프리팹 안에 넣지 않는다 - 씬 루트에 둔다(CLAUDE.md).
        GameObject set = new GameObject(SetRootName);
        Undo.RegisterCreatedObjectUndo(set, "Build B1 Reveal Set");
        set.transform.position = new Vector3(-14.4f, -4.16f, 2.0f);

        // 그림자를 만드는 실루엣. 알을 피해 방의 동쪽(x 큰 쪽)에 세운다.
        Mesh headMesh, wingMesh;
        BatSilhouetteMesh.Get(0.05f, out headMesh, out wingMesh);

        GameObject caster = new GameObject("ShadowCaster");
        caster.transform.SetParent(set.transform, false);
        caster.transform.localPosition = new Vector3(0.05f, 1.85f, 0f);
        // 0.7 배 - 이보다 크면 날개 끝 손가락이 벽을 벗어나 삼각형으로 뭉쳐 보인다.
        caster.transform.localScale = Vector3.one * 0.7f;

        // 머리 + 뾰족한 귀
        Silhouette(caster.transform, "Head", headMesh, Vector3.zero, Vector3.one, 0f);

        // 접혔다 펴지는 날개 - 회전축을 어깨에 두려고 부모를 따로 만든다.
        // 오른쪽 날개 메시를 왼쪽에는 x 를 뒤집어 쓴다.
        GameObject wl = new GameObject("Wing_L");
        wl.transform.SetParent(caster.transform, false);
        wl.transform.localPosition = new Vector3(-0.28f, 0.02f, 0f);
        Silhouette(wl.transform, "Blade", wingMesh, Vector3.zero, new Vector3(-1f, 1f, 1f), 0f);

        GameObject wr = new GameObject("Wing_R");
        wr.transform.SetParent(caster.transform, false);
        wr.transform.localPosition = new Vector3(0.28f, 0.02f, 0f);
        Silhouette(wr.transform, "Blade", wingMesh, Vector3.zero, Vector3.one, 0f);

        // 빛 - 판 뒤에서 벽을 향해 쏜다
        GameObject spotGo = new GameObject("ShadowSpot");
        spotGo.transform.SetParent(set.transform, false);
        spotGo.transform.localPosition = new Vector3(0.1f, 1.85f, 1.6f);
        spotGo.transform.rotation = Quaternion.LookRotation(new Vector3(-0.12f, -0.06f, -1f).normalized, Vector3.up);
        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.spotAngle = 84f;
        spot.range = 12f;
        spot.intensity = 0f;
        spot.color = new Color(0.86f, 0.9f, 1f);
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 1f;

        // 붉은 경고등
        GameObject warnGo = new GameObject("WarningLight");
        warnGo.transform.SetParent(set.transform, false);
        warnGo.transform.localPosition = new Vector3(1.4f, 2.9f, 3.2f);
        Light warn = warnGo.AddComponent<Light>();
        warn.type = LightType.Point;
        warn.range = 9f;
        warn.intensity = 0f;
        warn.color = new Color(1f, 0.15f, 0.12f);

        // 알에서 흘러나온 배양액 - 얇은 판을 바닥에 깔고 발광 재질을 준다
        GameObject goo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        goo.name = "Goo";
        goo.transform.SetParent(set.transform, false);
        goo.transform.localPosition = new Vector3(eggAt.x - set.transform.position.x + 0.6f, 0.02f, eggAt.z - set.transform.position.z);
        goo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        goo.transform.localScale = new Vector3(3.4f, 2.8f, 1f);
        Object.DestroyImmediate(goo.GetComponent<Collider>());

        Material gooMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        gooMat.color = new Color(0.16f, 0.55f, 0.28f);
        gooMat.EnableKeyword("_EMISSION");
        gooMat.SetColor("_EmissionColor", new Color(0.16f, 0.62f, 0.30f) * 0.7f);
        gooMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        goo.GetComponent<Renderer>().sharedMaterial = gooMat;

        // 방에 이미 있는 조명 - 이 컷 동안 꺼야 그림자가 보인다
        var dim = new List<Light>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l == spot || l == warn) continue;
            if (l.type == LightType.Directional) continue;
            if (Vector3.Distance(l.transform.position, set.transform.position) > 9f) continue;
            dim.Add(l);
        }

        BatShadowReveal reveal = set.AddComponent<BatShadowReveal>();
        SerializedObject so = new SerializedObject(reveal);
        so.FindProperty("spot").objectReferenceValue = spot;
        so.FindProperty("wingLeft").objectReferenceValue = wl.transform;
        so.FindProperty("wingRight").objectReferenceValue = wr.transform;
        so.FindProperty("warningLight").objectReferenceValue = warn;
        // URP 는 점/스포트 조명 세기 단위가 빌트인과 다르다. 6 으로는 벽이 거의 안 밝아져
        // 그림자가 보이지 않는다. 화면에서 맞춰본 값을 그대로 넣는다.
        so.FindProperty("litIntensity").floatValue = 40f;
        so.FindProperty("warningIntensity").floatValue = 1.4f;

        SerializedProperty arr = so.FindProperty("dimLights");
        arr.arraySize = dim.Count;
        for (int i = 0; i < dim.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = dim[i];
        so.ApplyModifiedProperties();

        Debug.Log($"[EndingCutsceneBuilder] 지하 그림자 세트: 실루엣 3개(머리+날개2) / 끌 조명 {dim.Count}개");
        return reveal;
    }

    /// <summary>
    /// 그림자만 만드는 실루엣. 화면에는 보이지 않는다(ShadowsOnly).
    ///
    /// 단면 판(Quad)은 그림자를 아예 만들지 못한다 - 실제로 확인했다. 그래서 두께가 있는
    /// 닫힌 메시를 쓴다. 자세한 이유는 BatSilhouetteMesh 참고.
    /// </summary>
    static void Silhouette(Transform parent, string name, Mesh mesh, Vector3 localPos, Vector3 localScale, float roll)
    {
        GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        go.transform.localScale = localScale;

        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        r.receiveShadows = false;
    }

    // ---------------------------------------------------------------- 유틸

    /// 자리와 바라볼 곳을 주면 그 방향을 보는 앵커를 만든다.
    static Transform LookAnchor(GameObject parent, string name, Vector3 at, Vector3 lookAt)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = at;

        Vector3 dir = lookAt - at;
        if (dir.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        return go.transform;
    }

    /// <summary>
    /// 컷신 동안 내릴 인게임 HUD 를 모은다. 공포/산소 표시와 퀵슬롯이 붙어 있는
    /// 캔버스를 찾아 그 루트를 담는다. 이름으로 찾지 않고 컴포넌트로 찾는다 -
    /// 팀원이 오브젝트 이름을 바꿔도 따라간다.
    /// </summary>
    static List<GameObject> CollectGameplayHud(Transform endingRoot)
    {
        var found = new List<GameObject>();

        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;

            string type = mb.GetType().Name;
            if (type != "ScreamFearUI" && type != "FearVignetteUI" && type != "QuickSlotUI" && type != "CrosshairUI") continue;

            Canvas canvas = mb.GetComponentInParent<Canvas>();
            GameObject go = canvas != null ? canvas.gameObject : mb.gameObject;

            // 엔딩이 만든 캔버스는 내리면 안 된다
            if (endingRoot != null && go.transform.IsChildOf(endingRoot)) continue;
            if (!found.Contains(go)) found.Add(go);
        }
        return found;
    }

    /// BuildingPower 의 '자동 수집에서 제외할 이름 조각' 목록에 한 조각을 넣는다.
    static void ExcludeFromBuildingPower(string part)
    {
        BuildingPower bp = FindOne<BuildingPower>();
        if (bp == null) { Debug.LogWarning("[EndingCutsceneBuilder] BuildingPower 를 못 찾았다. 엔딩 세트 조명이 꺼질 수 있다."); return; }

        SerializedObject so = new SerializedObject(bp);
        SerializedProperty arr = so.FindProperty("ignoredNameParts");

        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).stringValue == part) return;   // 이미 있다

        arr.arraySize++;
        arr.GetArrayElementAtIndex(arr.arraySize - 1).stringValue = part;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(bp);
    }

    /// 위에서 아래로 쏴 바닥 높이를 잰다. 못 맞히면 넘겨받은 기본값을 쓴다.
    static float FloorUnder(Vector3 at, float fallback)
    {
        RaycastHit hit;
        if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return fallback;
    }

    static ComputerTerminal FindConsole()
    {
        // 콜라이더가 없는 쪽이 논리용 콘솔이다. 겨눌 수 있는 모니터는 따로 있다.
        ComputerTerminal fallback = null;
        foreach (ComputerTerminal c in Object.FindObjectsByType<ComputerTerminal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            fallback = c;
            if (c.GetComponent<Collider>() == null) return c;
        }
        return fallback;
    }

    static Transform FindMonitor()
    {
        foreach (ComputerTerminal c in Object.FindObjectsByType<ComputerTerminal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.GetComponent<Collider>() != null) return c.transform;
        return null;
    }

    static Transform FindByName(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }

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

    static TMP_Text Label(Transform parent, string name, string text, float size, Vector2 pos,
        Color color, Vector2 box, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (font != null) tmp.font = font;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        SetBox(tmp.rectTransform, pos, box);
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

    static string Name(Component c) => c == null ? "못 찾음 ★" : c.gameObject.name;
    static string Name(Object o) => o == null ? "못 찾음 ★" : o.name;
}
