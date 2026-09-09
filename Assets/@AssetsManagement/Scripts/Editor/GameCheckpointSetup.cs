using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GameCheckpoint 를 GameManager 에 붙이고 참조를 전부 꽂는다.
///
/// 런타임에는 Find 를 쓰지 않기로 했으므로(CLAUDE.md) 여기서 에디터 시점에 한 번 찾아
/// Inspector 값으로 박아둔다. 맵을 고쳐 오브젝트가 바뀌면 이 메뉴를 다시 실행하면 된다.
/// </summary>
public static class GameCheckpointSetup
{
    [MenuItem("Tools/QuietPlace/세이브포인트 연결")]
    public static void Wire()
    {
        GameObject host = GameObject.Find("GameManager");
        if (host == null)
        {
            Debug.LogError("[GameCheckpointSetup] 씬에 GameManager 오브젝트가 없다.");
            return;
        }

        GameCheckpoint cp = host.GetComponent<GameCheckpoint>();
        if (cp == null) cp = Undo.AddComponent<GameCheckpoint>(host);

        PlayerController player = FindOne<PlayerController>();
        PlayerInteractor interactor = FindOne<PlayerInteractor>();
        BoltedPanel cover = FindOne<BoltedPanel>();
        KeypadTerminal keypad = FindOne<KeypadTerminal>();
        ComputerTerminal computer = FindOne<ComputerTerminal>();

        // 슬롯은 두 개인데 오브젝트 이름과 실제 역할이 서로 반대다
        // (PROP_2F_Generator 가 퓨즈 슬롯, PROP_2F_Switchboard 가 변압기 슬롯).
        // 이름을 믿으면 거꾸로 꽂히므로 requiresRepair 값으로 가른다.
        FuseSlot transformerSlot = null, fuseSlot = null;
        foreach (FuseSlot slot in Object.FindObjectsByType<FuseSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedProperty kind = new SerializedObject(slot).FindProperty("requiresRepair");
            string name = kind.enumNames[kind.enumValueIndex];

            if (name == "TransformerModule") transformerSlot = slot;
            else if (name == "SwitchboardFuse") fuseSlot = slot;
        }

        // 죽어도 들고 가는 부품들. 배열 순서가 저장 키(비트 위치)이므로 이름으로
        // 정렬해 둔다 - FindObjectsByType 의 순서는 실행마다 달라질 수 있어서,
        // 그대로 쓰면 다시 배선할 때마다 지난 기록의 비트가 다른 부품을 가리킨다.
        var pickups = new List<FusePickup>(
            Object.FindObjectsByType<FusePickup>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        pickups.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        var monsters = new List<MonsterController>(
            Object.FindObjectsByType<MonsterController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        monsters.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        FlashlightPickup worldFlashlight = FindOne<FlashlightPickup>();

        SerializedObject so = new SerializedObject(cp);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("interactor").objectReferenceValue = interactor;
        so.FindProperty("bayCover").objectReferenceValue = cover;
        so.FindProperty("transformerSlot").objectReferenceValue = transformerSlot;
        so.FindProperty("fuseSlot").objectReferenceValue = fuseSlot;
        so.FindProperty("keypad").objectReferenceValue = keypad;
        so.FindProperty("computer").objectReferenceValue = computer;
        so.FindProperty("worldFlashlight").objectReferenceValue = worldFlashlight;

        SerializedProperty picks = so.FindProperty("trackedPickups");
        picks.arraySize = pickups.Count;
        for (int i = 0; i < pickups.Count; i++)
            picks.GetArrayElementAtIndex(i).objectReferenceValue = pickups[i];

        SerializedProperty mons = so.FindProperty("monsters");
        mons.arraySize = monsters.Count;
        for (int i = 0; i < monsters.Count; i++)
            mons.GetArrayElementAtIndex(i).objectReferenceValue = monsters[i];

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(cp);

        Debug.Log("[GameCheckpointSetup] 배선 완료\n" +
                  $"  player={Name(player)} interactor={Name(interactor)}\n" +
                  $"  bayCover={Name(cover)}\n" +
                  $"  transformerSlot={Name(transformerSlot)} fuseSlot={Name(fuseSlot)}\n" +
                  $"  keypad={Name(keypad)} computer={Name(computer)}\n" +
                  $"  worldFlashlight={Name(worldFlashlight)}\n" +
                  $"  trackedPickups {pickups.Count}개 (이름순): " +
                  string.Join(", ", pickups.ConvertAll(p => p.gameObject.name).ToArray()) + "\n" +
                  $"  monsters {monsters.Count}마리 (부활 유예 대상)", cp);
    }

    [MenuItem("Tools/QuietPlace/세이브포인트 기록 지우기")]
    public static void ClearProgress()
    {
        GameProgress.Clear();
        Debug.Log("[GameCheckpointSetup] 세이브포인트 기록을 지웠다. 다음 플레이는 처음부터다.");
    }

    static T FindOne<T>() where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found.Length == 0)
        {
            Debug.LogWarning($"[GameCheckpointSetup] {typeof(T).Name} 을 씬에서 못 찾았다.");
            return null;
        }
        if (found.Length > 1)
            Debug.LogWarning($"[GameCheckpointSetup] {typeof(T).Name} 이 {found.Length}개다. 첫 번째를 쓴다: {found[0].name}");
        return found[0];
    }

    static string Name(Component c) => c == null ? "없음" : c.gameObject.name;
}
