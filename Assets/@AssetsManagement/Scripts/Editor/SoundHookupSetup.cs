using UnityEditor;
using UnityEngine;

/// <summary>
/// 요청받은 사운드들을 각자 자리에 꽂는다.
///
///   - 전원 복구 컷신 : Eerie void ambience_2 + Far_creepy_monster_growling
///   - 볼트 한 바퀴   : Wrench_Bolt
///   - 변압기 낙하    : Metal_drop_1 (+ 괴물 강제 호출)
///   - 쓰레기통 밀기  : Dumpster dragging wheels_2 (앞 1초만)
///
/// 사운드는 SoundManager 의 이름 목록을 거치지 않는다. 재생 지점을 잘라 쓰거나
/// 늦게 틀어야 하는 것들이라 AudioSource 를 직접 들고 있어야 한다.
/// </summary>
public static class SoundHookupSetup
{
    const string HostName = "GameManager";
    const string RootName = "PowerOnSounds";
    const string AudioDir = "Assets/MiniGame_QuietPlace/Audio";

    [MenuItem("Tools/QuietPlace/사운드 연결 (전원복구/볼트/낙하/쓰레기통)")]
    public static void Wire()
    {
        var report = new System.Text.StringBuilder("[SoundHookupSetup]\n");

        report.Append(WirePowerOn());
        report.Append(WireBolt());
        report.Append(WireDrop());
        report.Append(WireDumpster());
        report.Append(WireThrow());
        report.Append(WireAlarm());

        Debug.Log(report.ToString());
        AssetDatabase.Refresh();
    }

    // ---------------------------------------------------------------- 전원 복구 컷신

    static string WirePowerOn()
    {
        GameObject host = GameObject.Find(HostName);
        if (host == null) return "  ★ GameManager 오브젝트가 없다\n";

        BreakerRaiseSequence seq = host.GetComponent<BreakerRaiseSequence>();
        if (seq == null) return "  ★ BreakerRaiseSequence 가 없다. '차단기 올리기 연출 만들기' 를 먼저 실행할 것\n";

        Transform old = host.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Power On Sounds");
        root.transform.SetParent(host.transform, false);

        AudioClip ambience = Find("Eerie void ambience_2");
        AudioClip growl = Find("Far_creepy_monster_growling");

        // 둘 다 화면 연출이라 2D. 위치에 따라 작아지면 안 된다.
        AudioSource ambSrc = MakeSource(root.transform, "AmbienceSource", 0.7f);
        AudioSource growlSrc = MakeSource(root.transform, "GrowlSource", 0.85f);

        SerializedObject so = new SerializedObject(seq);
        so.FindProperty("ambienceClip").objectReferenceValue = ambience;
        so.FindProperty("growlClip").objectReferenceValue = growl;
        so.FindProperty("ambienceSource").objectReferenceValue = ambSrc;
        so.FindProperty("growlSource").objectReferenceValue = growlSrc;
        so.FindProperty("growlDelay").floatValue = 1.1f;

        // 카메라를 더 크게 들어 "전기가 다 들어왔다" 가 보이게
        so.FindProperty("cutsceneLookUpPitch").floatValue = -32f;
        so.FindProperty("cutsceneTurnToOpenSpace").boolValue = true;
        so.FindProperty("cutsceneProbeCount").intValue = 12;
        so.FindProperty("cutsceneProbeDistance").floatValue = 14f;
        so.FindProperty("cutscenePanSeconds").floatValue = 1.8f;
        so.FindProperty("cutsceneHoldSeconds").floatValue = 3.2f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(seq);

        return $"  전원복구 컷신: 공간음={Name(ambience)} / 포효={Name(growl)} (1.1초 뒤)\n" +
               "    가장 트인 쪽으로 몸을 돌리고 위로 32도, 1.8초 팬 + 3.2초 유지\n";
    }

    // ---------------------------------------------------------------- 볼트 한 바퀴

    static string WireBolt()
    {
        const string path = "Assets/@Resources/Prefabs/Manager/P_BoltRepairUI.prefab";
        AudioClip clip = Find("Wrench_Bolt");

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        if (contents == null) return "  ★ P_BoltRepairUI 를 못 열었다\n";

        string note;
        try
        {
            BoltTurnPuzzle puzzle = contents.GetComponentInChildren<BoltTurnPuzzle>(true);
            if (puzzle == null) return "  ★ BoltTurnPuzzle 을 못 찾았다\n";

            // 볼트 퍼즐은 바퀴를 채울 때마다 turnSound 를 이미 재생한다. 클립만 꽂으면 된다.
            puzzle.turnSound = clip;

            if (puzzle.audioSource == null)
            {
                AudioSource src = puzzle.gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = 0.9f;
                puzzle.audioSource = src;
                note = " (AudioSource 새로 만듦)";
            }
            else note = "";

            EditorUtility.SetDirty(puzzle);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        return $"  볼트 한 바퀴: turnSound={Name(clip)}{note} - 보통 난이도 2바퀴이므로 2번 난다\n";
    }

    // ---------------------------------------------------------------- 변압기 낙하

    static string WireDrop()
    {
        AudioClip clip = Find("Metal_drop_1");
        int n = 0;
        var names = new System.Collections.Generic.List<string>();

        foreach (FusePickup pick in Object.FindObjectsByType<FusePickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(pick);
            if (!so.FindProperty("carryInHands").boolValue) continue;   // 무거운 물건만

            so.FindProperty("dropSound").objectReferenceValue = clip;
            so.FindProperty("summonMonsters").boolValue = true;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(pick);

            n++;
            names.Add(pick.gameObject.name);
        }

        return $"  변압기 낙하: {Name(clip)} / 괴물 강제 호출 켬 - {n}개에 적용 " +
               string.Join(", ", names.ToArray()) + "\n";
    }

    // ---------------------------------------------------------------- 쓰레기통

    static string WireDumpster()
    {
        AudioClip clip = Find("Dumpster dragging wheels_2");
        PushProp[] props = Object.FindObjectsByType<PushProp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (props.Length == 0) return "  ★ PushProp(쓰레기통) 을 씬에서 못 찾았다\n";

        var names = new System.Collections.Generic.List<string>();

        foreach (PushProp prop in props)
        {
            SerializedObject so = new SerializedObject(prop);

            AudioSource src = prop.GetComponent<AudioSource>();
            if (src == null)
            {
                src = Undo.AddComponent<AudioSource>(prop.gameObject);
                src.playOnAwake = false;
                src.spatialBlend = 0.85f;   // 물건에서 나는 소리라 3D
                src.minDistance = 2f;
                src.maxDistance = 25f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.volume = 0.9f;
            }

            so.FindProperty("dragSound").objectReferenceValue = clip;
            so.FindProperty("dragSource").objectReferenceValue = src;
            so.FindProperty("dragSoundSeconds").floatValue = 1f;   // 앞 1초만
            so.FindProperty("dragSoundDelay").floatValue = 0f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop);

            names.Add(prop.gameObject.name);
        }

        return $"  쓰레기통 밀기: {Name(clip)} 앞 1초만 - " + string.Join(", ", names.ToArray()) + "\n";
    }

    // ---------------------------------------------------------------- 던진 물건

    static string WireThrow()
    {
        PlayerInteractor pi = Object.FindFirstObjectByType<PlayerInteractor>();
        if (pi == null) return "  ★ PlayerInteractor 를 못 찾았다\n";

        AudioClip bottle = Find("bottle_break");
        AudioClip brick = Find("brick_throw");

        // 떨어진 자리로 옮겨 쓸 3D 소스. 던진 물건은 파괴되므로 플레이어가 들고 있어야 한다.
        Transform holder = pi.transform.Find("ThrowImpactSource");
        if (holder == null)
        {
            GameObject go = new GameObject("ThrowImpactSource");
            Undo.RegisterCreatedObjectUndo(go, "Create ThrowImpactSource");
            go.transform.SetParent(pi.transform, false);
            holder = go.transform;
        }

        AudioSource src = holder.GetComponent<AudioSource>();
        if (src == null) src = Undo.AddComponent<AudioSource>(holder.gameObject);
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 1f;            // 떨어진 방향이 들려야 미끼로 쓸 수 있다
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 45f;
        src.volume = 1f;

        SerializedObject so = new SerializedObject(pi);
        so.FindProperty("bottleBreakSound").objectReferenceValue = bottle;
        so.FindProperty("brickThrowSound").objectReferenceValue = brick;
        so.FindProperty("throwImpactSource").objectReferenceValue = src;
        so.FindProperty("throwImpactSeconds").floatValue = 1.5f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(pi);

        return $"  던진 유리병: {Name(bottle)}\n  던진 벽돌: {Name(brick)}\n" +
               "    떨어진 자리에서 3D 로 울린다 (앞 1.5초만 - 벽돌 클립이 14초라 잘라야 한다)\n";
    }

    // ---------------------------------------------------------------- 괴물 호출기

    static string WireAlarm()
    {
        GameObject host = GameObject.Find(HostName);
        if (host == null) return "  ★ GameManager 오브젝트가 없다\n";

        MonsterAlarm alarm = host.GetComponent<MonsterAlarm>();
        if (alarm == null) alarm = Undo.AddComponent<MonsterAlarm>(host);

        var mons = new System.Collections.Generic.List<MonsterController>(
            Object.FindObjectsByType<MonsterController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        mons.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        SerializedObject so = new SerializedObject(alarm);
        SerializedProperty list = so.FindProperty("monsters");
        list.arraySize = mons.Count;
        for (int i = 0; i < mons.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = mons[i];
        so.FindProperty("summonRange").floatValue = 0f;   // 0 = 전부 (무조건)
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(alarm);

        return $"  괴물 호출기: {mons.Count}마리 등록, 범위 제한 없음(무조건)\n";
    }

    // ---------------------------------------------------------------- 유틸

    static AudioClip Find(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != fileName) continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;
        }

        Debug.LogWarning($"[SoundHookupSetup] '{fileName}' 오디오를 {AudioDir} 에서 못 찾았다.");
        return null;
    }

    static AudioSource MakeSource(Transform parent, string name, float volume)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.volume = volume;
        return src;
    }

    static string Name(AudioClip c) => c == null ? "없음 ★" : c.name + $" ({c.length:0.0}초)";
}
