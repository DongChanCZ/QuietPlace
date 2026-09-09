using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이미 만들어져 있는 미니게임 UI 프리팹에 아날로그 계기판을 입힌다.
///
/// 프리팹을 처음부터 다시 만들지 않는다 - 손으로 고쳐둔 배선(HoldToComplete 로 바뀐
/// 차단기 단계 등)을 날리지 않으려면 있는 것 위에 얹는 게 안전하다. 기존 막대 게이지는
/// 지우지 않고 끄기만 해서, 되돌리고 싶으면 다시 켜면 된다.
/// </summary>
public static class GaugeSkinInstaller
{
    const string FusePath = "Assets/@Resources/Prefabs/Manager/P_FuseRepairUI.prefab";
    const string BoltPath = "Assets/@Resources/Prefabs/Manager/P_BoltRepairUI.prefab";

    [MenuItem("Tools/QuietPlace/미니게임 UI - 아날로그 계기판 입히기")]
    public static void InstallAll()
    {
        string report = InstallFuse() + "\n" + InstallBolt();
        Debug.Log("[GaugeSkinInstaller]\n" + report);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/QuietPlace/미니게임 UI - 아날로그 계기판 빼기")]
    public static void UninstallAll()
    {
        string report = Uninstall(FusePath, "Stage4_BreakerUp",
                            new[] { "BarBG", "BarFill" })
                      + "\n"
                      + Uninstall(BoltPath, null,
                            new[] { "TorqueBG", "TorqueFill", "TorqueLabel", "BreakawayMarker",
                                    "ProgressBG", "ProgressFill", "ProgressLabel",
                                    "TurnMarker_0", "TurnMarker_1", "TurnMarker_2" });

        Debug.Log("[GaugeSkinInstaller] 계기판 제거\n" + report);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 계기판을 걷어내고 원래 막대 게이지를 되살린다.
    /// 차단기 단계의 Title/Hint 는 따로 지우기로 한 것이라 계속 꺼둔다.
    /// </summary>
    static string Uninstall(string path, string stageName, string[] restore)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        if (contents == null) return path + " 를 못 열었다";

        var log = new System.Text.StringBuilder("== " + System.IO.Path.GetFileName(path) + "\n");
        try
        {
            // 참조를 먼저 끊는다. 안 끊으면 게이지를 지운 뒤 깨진 참조가 남는다.
            foreach (HoldToComplete h in contents.GetComponentsInChildren<HoldToComplete>(true))
            {
                if (h.gauge == null) continue;
                h.gauge = null;
                EditorUtility.SetDirty(h);
                log.AppendLine("  HoldToComplete.gauge 참조 해제 (" + h.gameObject.name + ")");
            }
            foreach (BoltTurnPuzzle b in contents.GetComponentsInChildren<BoltTurnPuzzle>(true))
            {
                if (b.torqueGauge == null && b.progressGauge == null) continue;
                b.torqueGauge = null;
                b.progressGauge = null;
                EditorUtility.SetDirty(b);
                log.AppendLine("  BoltTurnPuzzle 게이지 참조 해제");
            }

            int removed = 0;
            foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "GaugePanel") continue;
                Object.DestroyImmediate(t.gameObject);
                removed++;
                break;   // 지우면 목록이 흐트러지므로 한 번에 하나씩
            }
            // 프리팹마다 하나뿐이지만, 남아 있으면 다시 훑는다.
            while (true)
            {
                Transform found = null;
                foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                    if (t.name == "GaugePanel") { found = t; break; }
                if (found == null) break;
                Object.DestroyImmediate(found.gameObject);
                removed++;
            }
            log.AppendLine("  GaugePanel " + removed + "개 삭제");

            Transform host = stageName == null ? contents.transform : Find(contents, stageName);
            if (host != null) ShowNamed(host, log, restore);

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            log.AppendLine("  저장 완료");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        return log.ToString();
    }

    // ---------------------------------------------------------------- 배전반

    static string InstallFuse()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(FusePath);
        if (contents == null) return "P_FuseRepairUI 를 못 열었다";

        var log = new System.Text.StringBuilder("== P_FuseRepairUI\n");
        try
        {
            Transform stage = Find(contents, "Stage4_BreakerUp");
            if (stage == null) return "Stage4_BreakerUp 이 없다";

            // 이미 입혔으면 지우고 다시 만든다(값을 바꿔가며 다시 실행할 수 있게).
            Wipe(stage, "GaugePanel");

            HideNamed(stage, log, "BarBG", "BarFill");

            GameObject panel = AnalogGaugeBuilder.MakePanel(stage, "GaugePanel", new Vector2(0f, 20f), new Vector2(760f, 600f));
            AnalogGaugeBuilder.Label(panel.transform, "PlateName", "MAIN BREAKER", 26f, new Vector2(0f, 232f),
                new Color(0.78f, 0.74f, 0.62f));
            AnalogGaugeBuilder.Label(panel.transform, "PlateSub", "LINE VOLTAGE", 18f, new Vector2(0f, 204f),
                new Color(0.58f, 0.55f, 0.46f));

            AnalogGauge gauge = AnalogGaugeBuilder.MakeGauge(panel.transform, "VoltGauge",
                new Vector2(0f, 20f), 380f, "VOLTS", 380, "V");

            // 계기 밑에 눌러야 하는 키를 새긴 명판
            AnalogGaugeBuilder.Img(panel.transform, "KeyPlate", new Color(0.17f, 0.16f, 0.14f),
                new Vector2(0f, -218f), new Vector2(200f, 54f));
            AnalogGaugeBuilder.Img(panel.transform, "KeyPlateRim", new Color(0.45f, 0.41f, 0.33f),
                new Vector2(0f, -218f), new Vector2(208f, 62f)).transform.SetSiblingIndex(
                    panel.transform.childCount - 2);
            AnalogGaugeBuilder.Label(panel.transform, "KeyCap", "E", 34f, new Vector2(0f, -218f),
                new Color(0.88f, 0.84f, 0.72f));

            // 올리기 단계의 HoldToComplete 에 계기를 물린다. 프리팹에는 내리기/올리기
            // 두 개가 있으므로 leverGoesUp 으로 가른다.
            int wired = 0;
            foreach (HoldToComplete h in contents.GetComponentsInChildren<HoldToComplete>(true))
            {
                if (!h.leverGoesUp) continue;
                h.gauge = gauge;
                EditorUtility.SetDirty(h);
                wired++;
            }
            log.AppendLine(wired > 0 ? "  HoldToComplete.gauge 배선 " + wired + "개" : "  경고: 올리기 단계의 HoldToComplete 를 못 찾았다");

            PrefabUtility.SaveAsPrefabAsset(contents, FusePath);
            log.AppendLine("  저장 완료");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        return log.ToString();
    }

    // ---------------------------------------------------------------- 볼트

    static string InstallBolt()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(BoltPath);
        if (contents == null) return "P_BoltRepairUI 를 못 열었다";

        var log = new System.Text.StringBuilder("== P_BoltRepairUI\n");
        try
        {
            BoltTurnPuzzle puzzle = contents.GetComponentInChildren<BoltTurnPuzzle>(true);
            if (puzzle == null) return "BoltTurnPuzzle 을 못 찾았다";

            Transform host = puzzle.transform;
            Wipe(host, "GaugePanel");

            // 토크 막대만 계기로 바꾼다. 풀린 정도 막대는 그대로 둔다 - 그 위에 얹힌
            // 바퀴 표시(TurnMarker)가 막대를 밑바탕으로 쓰기 때문에, 막대를 끄면
            // 표시가 허공에 떠버린다.
            HideNamed(host, log, "TorqueBG", "TorqueFill", "TorqueLabel", "BreakawayMarker");

            // 예전 판에서 껐을 수 있으니 되살린다. 몇 번 실행해도 같은 결과가 되게.
            ShowNamed(host, log, "ProgressBG", "ProgressFill", "ProgressLabel",
                "TurnMarker_0", "TurnMarker_1", "TurnMarker_2");

            // 볼트 그림은 가운데 그대로 두고, 계기는 왼쪽 빈 자리에 세운다.
            GameObject panel = AnalogGaugeBuilder.MakePanel(host, "GaugePanel", new Vector2(-544f, 20f), new Vector2(398f, 506f));
            AnalogGaugeBuilder.Label(panel.transform, "PlateName", "TORQUE WRENCH", 21f, new Vector2(0f, 216f),
                new Color(0.76f, 0.72f, 0.60f));
            AnalogGaugeBuilder.Label(panel.transform, "PlateSub", "MODEL TW-4", 15f, new Vector2(0f, 190f),
                new Color(0.56f, 0.53f, 0.44f));

            AnalogGauge torque = AnalogGaugeBuilder.MakeGauge(panel.transform, "TorqueGauge",
                new Vector2(0f, 16f), 300f, "TORQUE", 160, "Nm");

            // 계기판 아래 놋쇠 명판
            AnalogGaugeBuilder.Img(panel.transform, "PlateRim", new Color(0.45f, 0.41f, 0.33f),
                new Vector2(0f, -212f), new Vector2(256f, 48f));
            AnalogGaugeBuilder.Img(panel.transform, "PlateInset", new Color(0.16f, 0.15f, 0.13f),
                new Vector2(0f, -212f), new Vector2(246f, 38f));
            AnalogGaugeBuilder.Label(panel.transform, "PlateNote", "COUNTER-CLOCKWISE", 14f, new Vector2(0f, -212f),
                new Color(0.80f, 0.76f, 0.62f));

            puzzle.torqueGauge = torque;
            puzzle.progressGauge = null;   // 풀린 정도는 막대 + 바퀴 표시가 계속 담당한다
            log.AppendLine("  BoltTurnPuzzle.torqueGauge 배선 (풀린 정도는 기존 막대 유지)");

            PrefabUtility.SaveAsPrefabAsset(contents, BoltPath);
            log.AppendLine("  저장 완료");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        return log.ToString();
    }

    // ---------------------------------------------------------------- 유틸

    /// 옛 막대 게이지는 지우지 않고 끈다. 되돌릴 수 있게.
    static void HideNamed(Transform root, System.Text.StringBuilder log, params string[] names)
    {
        int n = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root || !t.gameObject.activeSelf) continue;

            bool match = false;
            for (int i = 0; i < names.Length && !match; i++) match = t.name == names[i];
            if (!match) continue;

            t.gameObject.SetActive(false);
            n++;
        }
        if (n > 0) log.AppendLine("  옛 막대/라벨 " + n + "개 비활성 (지우지 않음)");
    }

    /// 남겨둬야 하는 요소를 다시 켠다.
    static void ShowNamed(Transform root, System.Text.StringBuilder log, params string[] names)
    {
        int n = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root || t.gameObject.activeSelf) continue;

            bool match = false;
            for (int i = 0; i < names.Length && !match; i++) match = t.name == names[i];
            if (!match) continue;

            t.gameObject.SetActive(true);
            n++;
        }
        if (n > 0) log.AppendLine("  남겨둘 요소 " + n + "개 다시 활성");
    }

    static void Wipe(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

    static Transform Find(GameObject root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
