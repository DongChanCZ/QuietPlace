using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 죽음 연출을 씬에 조립한다 - 회색 오버레이 캔버스, 전용 AudioSource 두 개,
/// 그리고 참조 배선까지.
///
/// 런타임에 Find 를 쓰지 않기로 했으므로(CLAUDE.md) 여기서 한 번 찾아 박아둔다.
/// 사운드도 SoundManager 의 이름 목록을 거치지 않는다 - 심장 모니터를 5초 지점부터
/// 틀어야 해서 재생 위치를 직접 만질 수 있는 AudioSource 가 필요하다.
/// </summary>
public static class PlayerDeathSequenceSetup
{
    const string HostName = "GameManager";
    const string RootName = "DeathSequence";
    const string ScreamPath = "Assets/MiniGame_QuietPlace/Audio/Crush_malescream.mp3";
    const string MonitorPath = "Assets/MiniGame_QuietPlace/Audio/heart_monitor_beep_Die.mp3";

    [MenuItem("Tools/QuietPlace/죽음 연출 만들기")]
    public static void Build()
    {
        GameObject host = GameObject.Find(HostName);
        if (host == null)
        {
            Debug.LogError($"[PlayerDeathSequenceSetup] 씬에 {HostName} 오브젝트가 없다.");
            return;
        }

        // 다시 실행해도 같은 결과가 되도록 지우고 새로 만든다.
        Transform old = host.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Death Sequence");
        root.transform.SetParent(host.transform, false);

        // ---------- 회색 오버레이 ----------
        // 미니게임 UI(sortingOrder 100)보다 위에 와야 죽는 화면이 그 위를 덮는다.
        GameObject canvasGo = new GameObject("DeathOverlayCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root.transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject greyGo = new GameObject("GreyWash", typeof(RectTransform));
        greyGo.transform.SetParent(canvasGo.transform, false);
        Image grey = greyGo.AddComponent<Image>();
        grey.color = new Color(0.62f, 0.62f, 0.63f, 0f);
        grey.sprite = null;
        grey.raycastTarget = false;
        Stretch(grey.rectTransform);
        greyGo.SetActive(false);

        // ---------- 사운드 ----------
        AudioClip scream = AssetDatabase.LoadAssetAtPath<AudioClip>(ScreamPath);
        AudioClip monitor = AssetDatabase.LoadAssetAtPath<AudioClip>(MonitorPath);
        if (scream == null) Debug.LogWarning("[PlayerDeathSequenceSetup] " + ScreamPath + " 를 못 찾았다.");
        if (monitor == null) Debug.LogWarning("[PlayerDeathSequenceSetup] " + MonitorPath + " 를 못 찾았다.");

        AudioSource screamSrc = MakeSource(root.transform, "ScreamSource", scream, 1f);
        AudioSource monitorSrc = MakeSource(root.transform, "HeartMonitorSource", monitor, 0.85f);

        // ---------- 배선 ----------
        PlayerDeathSequence seq = host.GetComponent<PlayerDeathSequence>();
        if (seq == null) seq = Undo.AddComponent<PlayerDeathSequence>(host);

        PlayerController player = FindOne<PlayerController>();
        PlayerInteractor interactor = FindOne<PlayerInteractor>();
        Camera cam = player != null ? player.playerCamera : null;
        if (cam == null) cam = Camera.main;

        SerializedObject so = new SerializedObject(seq);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("interactor").objectReferenceValue = interactor;
        so.FindProperty("cam").objectReferenceValue = cam;
        so.FindProperty("screamClip").objectReferenceValue = scream;
        so.FindProperty("heartMonitorClip").objectReferenceValue = monitor;
        so.FindProperty("screamSource").objectReferenceValue = screamSrc;
        so.FindProperty("monitorSource").objectReferenceValue = monitorSrc;
        so.FindProperty("greyOverlay").objectReferenceValue = grey;

        // 수치도 여기서 정한다. 스크립트 기본값만 바꾸면 이미 씬에 붙어 있는
        // 컴포넌트는 옛 값을 그대로 들고 있어서(직렬화된 값이 이긴다) 안 먹는다.
        SetFloat(so, "faceSeconds", 0.4f);
        SetFloat(so, "lookAtBodyHeight", 0.45f);
        SetFloat(so, "impactSeconds", 0.95f);
        SetFloat(so, "heartMonitorStartTime", 5f);

        SetFloat(so, "slamSeconds", 0.28f);
        SetFloat(so, "kickDistance", 0.45f);
        SetFloat(so, "kickRollDegrees", 26f);
        SetFloat(so, "kickPitchDegrees", 12f);

        SetFloat(so, "wallProbeDistance", 1.4f);
        SetFloat(so, "wallStandoff", 0.3f);
        SetFloat(so, "wallRollDegrees", 58f);
        SetFloat(so, "wallPitchDegrees", 22f);
        SetFloat(so, "wallRecoilDistance", 0.14f);
        SetFloat(so, "wallRecoilSeconds", 0.13f);
        SetFloat(so, "wallShakeMultiplier", 2.4f);

        SetFloat(so, "shakeAmount", 0.035f);
        SetFloat(so, "shakeSeconds", 0.5f);
        SetFloat(so, "greyFadeSeconds", 1.6f);
        SetFloat(so, "holdSeconds", 1.8f);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(seq);

        float monitorStart = so.FindProperty("heartMonitorStartTime").floatValue;
        Debug.Log("[PlayerDeathSequenceSetup] 조립 완료\n" +
                  $"  player={Name(player)} interactor={Name(interactor)} cam={Name(cam)}\n" +
                  $"  비명={(scream == null ? "없음" : scream.name + " (" + scream.length.ToString("0.0") + "초)")}\n" +
                  $"  심장모니터={(monitor == null ? "없음" : monitor.name + " (" + monitor.length.ToString("0.0") + "초)")}" +
                  $" / {monitorStart:0.#}초 지점부터 재생\n" +
                  "  회색 오버레이 캔버스 sortingOrder=500 (미니게임 UI 위)", seq);
    }

    static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[PlayerDeathSequenceSetup] {field} 필드를 못 찾았다."); return; }
        p.floatValue = value;
    }

    static AudioSource MakeSource(Transform parent, string name, AudioClip clip, float volume)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.playOnAwake = false;
        src.loop = false;
        src.volume = volume;
        src.spatialBlend = 0f;   // 화면 연출이므로 2D 로 - 위치에 따라 작아지면 안 된다
        return src;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static T FindOne<T>() where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found.Length == 0)
        {
            Debug.LogWarning($"[PlayerDeathSequenceSetup] {typeof(T).Name} 을 씬에서 못 찾았다.");
            return null;
        }
        return found[0];
    }

    static string Name(Component c) => c == null ? "없음" : c.gameObject.name;
}
