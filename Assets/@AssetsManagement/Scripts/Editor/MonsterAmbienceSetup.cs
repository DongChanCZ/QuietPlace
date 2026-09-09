using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 괴물 분위기 사운드를 씬에 조립한다 - AudioSource 두 개를 만들고 참조를 꽂는다.
///
/// 클립은 파일 이름으로 찾는다. 아직 프로젝트에 없는 파일은 빈 칸으로 남기고 경고만
/// 남긴다 - 나중에 파일을 넣고 이 메뉴를 다시 실행하면 그때 꽂힌다.
/// </summary>
public static class MonsterAmbienceSetup
{
    const string HostName = "GameManager";
    const string RootName = "MonsterAmbience";
    const string AudioDir = "Assets/MiniGame_QuietPlace/Audio";

    // 요청받은 이름 그대로. 오타/변형이 있어도 잡히게 후보를 몇 개 둔다.
    static readonly string[] GrowlNames = { "Far_creepy_monster_growling" };
    static readonly string[] FootstepNames = { "Far_footstep", "Far_footsteps", "Far_Footstep" };
    static readonly string[] ScanNames = { "Creature_jaw_clicks_seach", "Creature_jaw_clicks_search", "Creature_jaw_clicks" };
    static readonly string[] NearGrowlNames = { "Near_growl", "Near_growling", "near_growl" };

    [MenuItem("Tools/QuietPlace/괴물 분위기 사운드 만들기")]
    public static void Build()
    {
        GameObject host = GameObject.Find(HostName);
        if (host == null)
        {
            Debug.LogError($"[MonsterAmbienceSetup] 씬에 {HostName} 오브젝트가 없다.");
            return;
        }

        Transform old = host.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Monster Ambience");
        root.transform.SetParent(host.transform, false);

        // 먼 소리 - 방향은 느껴지되 멀어도 들려야 하므로 3D 를 약하게 섞고 범위를 넓게 잡는다.
        AudioSource farSrc = MakeSource(root.transform, "FarSource",
            spatialBlend: 0.7f, minDistance: 15f, maxDistance: 90f, volume: 0.55f);

        // 살피는 소리 - 가까이서 나는 소리라 방향감이 뚜렷해야 한다.
        AudioSource scanSrc = MakeSource(root.transform, "ScanSource",
            spatialBlend: 0.85f, minDistance: 2f, maxDistance: 30f, volume: 0.8f);

        // 포기할 때 내는 포효는 가까이서 나므로 방향감이 뚜렷해야 한다.
        AudioSource nearSrc = MakeSource(root.transform, "NearGrowlSource",
            spatialBlend: 0.9f, minDistance: 3f, maxDistance: 35f, volume: 0.9f);

        AudioClip growl = FindClip(GrowlNames);
        AudioClip footstep = FindClip(FootstepNames);
        AudioClip scan = FindClip(ScanNames);
        AudioClip nearGrowl = FindClip(NearGrowlNames);

        PlayerController player = FindOne<PlayerController>();
        MonsterController[] monsters = Object.FindObjectsByType<MonsterController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(monsters, (a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        MonsterAmbience amb = host.GetComponent<MonsterAmbience>();
        if (amb == null) amb = Undo.AddComponent<MonsterAmbience>(host);

        SerializedObject so = new SerializedObject(amb);
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;

        SerializedProperty list = so.FindProperty("monsters");
        list.arraySize = monsters.Length;
        for (int i = 0; i < monsters.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = monsters[i];

        so.FindProperty("growlClip").objectReferenceValue = growl;
        so.FindProperty("farFootstepClip").objectReferenceValue = footstep;
        so.FindProperty("scanClip").objectReferenceValue = scan;
        so.FindProperty("nearGrowlClip").objectReferenceValue = nearGrowl;
        so.FindProperty("farSource").objectReferenceValue = farSrc;
        so.FindProperty("scanSource").objectReferenceValue = scanSrc;
        so.FindProperty("nearGrowlSource").objectReferenceValue = nearSrc;

        // 괴물 근처에서 심장 소리를 조금 더 크게
        SetFloat(so, "calmMusicVolume", 0.55f);
        SetFloat(so, "stressedMusicVolume", 0.78f);

        // 긴장 배경음은 "눈으로 봤을 때"만 뜨므로 플레이어 카메라가 필요하다.
        Camera eyes = player != null ? player.playerCamera : null;
        if (eyes == null) eyes = Camera.main;
        so.FindProperty("eyes").objectReferenceValue = eyes;

        // 수치도 여기서 정한다 - 스크립트 기본값만 바꾸면 이미 씬에 붙은
        // 컴포넌트는 직렬화된 옛 값을 그대로 들고 있어서 안 먹는다.
        SetFloat(so, "closeDistance", 18f);
        SetFloat(so, "calmDownSeconds", 6f);
        SetFloat(so, "calmDistance", 26f);
        SetFloat(so, "sightHeight", 0.6f);
        SetFloat(so, "farDistance", 14f);
        SetFloat(so, "growlChance", 0.45f);
        SetFloat(so, "checkInterval", 0.25f);
        so.FindProperty("farInterval").vector2Value = new Vector2(11f, 22f);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(amb);

        var missing = new System.Collections.Generic.List<string>();
        if (growl == null) missing.Add(GrowlNames[0]);
        if (footstep == null) missing.Add(FootstepNames[0]);
        if (scan == null) missing.Add(ScanNames[0]);
        if (nearGrowl == null) missing.Add(NearGrowlNames[0]);

        Debug.Log("[MonsterAmbienceSetup] 조립 완료\n" +
                  $"  player={(player == null ? "없음" : player.gameObject.name)} / 괴물 {monsters.Length}마리\n" +
                  $"  먼 포효={ClipName(growl)}\n" +
                  $"  먼 발소리={ClipName(footstep)}\n" +
                  $"  살피는 소리={ClipName(scan)}\n" +
                  $"  포기할 때 포효={ClipName(nearGrowl)}\n" +
                  "  심장 소리 볼륨: 평온 0.55 -> 긴장 0.78\n" +
                  (missing.Count == 0
                      ? "  클립 전부 연결됨"
                      : $"  ※ 없는 파일 {missing.Count}개: {string.Join(", ", missing.ToArray())}\n" +
                        $"    {AudioDir} 에 넣고 이 메뉴를 다시 실행하면 꽂힌다."), amb);
    }

    static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[MonsterAmbienceSetup] {field} 필드를 못 찾았다."); return; }
        p.floatValue = value;
    }

    static AudioClip FindClip(string[] names)
    {
        foreach (string name in names)
        {
            // 확장자를 모르므로 폴더 안에서 이름으로 찾는다.
            string[] guids = AssetDatabase.FindAssets(name + " t:AudioClip", new[] { AudioDir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name) continue;

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) return clip;
            }
        }
        return null;
    }

    static AudioSource MakeSource(Transform parent, string name,
        float spatialBlend, float minDistance, float maxDistance, float volume)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.volume = volume;
        src.spatialBlend = spatialBlend;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        return src;
    }

    static T FindOne<T>() where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return found.Length == 0 ? null : found[0];
    }

    static string ClipName(AudioClip c) => c == null ? "없음 (파일 미존재)" : c.name + $" ({c.length:0.0}초)";
}
