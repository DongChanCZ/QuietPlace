using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 엔딩 컷신에 쓰는 플레이어 동작을 애니메이터에 등록한다.
///
/// PlayerController.SetAnimState(name) 은 같은 이름의 Trigger 를 켜는 것뿐이라,
/// 기존 상태들과 똑같이 "AnyState -> 상태" 전이를 트리거 조건으로 달아주면 그대로 쓸 수 있다.
///
/// 애니메이션 파일은 Assets/@Animation 에 있고 전부 Human(아바타 복사)으로 맞춰져 있어야 한다.
/// Generic 으로 들어온 파일은 플레이어 리그에서 재생되지 않는다.
/// </summary>
public static class EndingAnimationSetup
{
    const string ControllerPath = "Assets/@AssetsManagement/Animations/PlayerAnimatorController.controller";
    const string AnimDir = "Assets/@Animation/";

    /// 상태 이름 -> 애니메이션 파일 이름. 상태 이름이 곧 트리거 이름이다.
    static readonly (string state, string file, bool loop)[] Wanted =
    {
        ("EndTyping", "Typing", true),                    // 컷4 - 키보드를 두드린다
        ("EndWindow", "Opening_Door_Inwards", false),     // 창문을 열고 넘어간다
        ("EndRope", "Climbing_A_Rope", true),             // 컷5 - 완강기 하강
        ("EndLand", "Swing_To_Land", false),              // 착지
        ("EndTurn", "Right_Turn", false),                 // 뒤돈다
        ("EndWalk", "Walk_With_Briefcase", true),         // USB 를 들고 걷는다
    };

    [MenuItem("Tools/QuietPlace/엔딩 애니메이션 등록")]
    public static void Setup()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError("[EndingAnimationSetup] " + ControllerPath + " 를 못 찾았다.");
            return;
        }

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        var report = new System.Text.StringBuilder();
        int added = 0, already = 0, missing = 0;

        foreach (var want in Wanted)
        {
            if (HasState(sm, want.state))
            {
                already++;
                report.AppendLine("  이미 있음: " + want.state);
                continue;
            }

            AnimationClip clip = FindClip(AnimDir + want.file + ".fbx");
            if (clip == null)
            {
                missing++;
                report.AppendLine("  ★ 애니메이션 없음: " + want.file + ".fbx");
                continue;
            }

            // 트리거가 없으면 만든다. 이름이 상태 이름과 같아야 SetAnimState 가 찾는다.
            if (!HasParameter(ctrl, want.state)) ctrl.AddParameter(want.state, AnimatorControllerParameterType.Trigger);

            AnimatorState state = sm.AddState(want.state);
            state.motion = clip;

            // 반복 여부는 클립 자체의 설정이라 여기서 못 바꾼다. 대신 컷신이 재생 시간을 맡는다.
            AnimatorStateTransition tr = sm.AddAnyStateTransition(state);
            tr.hasExitTime = false;
            tr.duration = 0.15f;
            tr.canTransitionToSelf = true;
            tr.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = want.state,
                    threshold = 0f,
                }
            };

            added++;
            report.AppendLine($"  추가: {want.state} <- {want.file} ({clip.length:0.0}초{(want.loop ? ", 반복 전제" : "")})");
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        Debug.Log($"[EndingAnimationSetup] 상태 {added}개 추가 / 이미 있음 {already}개 / 파일 없음 {missing}개\n"
                  + report.ToString()
                  + "  PlayerController 의 animatorParamNames 캐시는 Awake 에서 다시 읽으므로 따로 손댈 것 없다.");
    }

    static bool HasState(AnimatorStateMachine sm, string name)
    {
        foreach (var s in sm.states) if (s.state.name == name) return true;
        return false;
    }

    static bool HasParameter(AnimatorController ctrl, string name)
    {
        foreach (var p in ctrl.parameters) if (p.name == name) return true;
        return false;
    }

    static AnimationClip FindClip(string fbxPath)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            AnimationClip c = o as AnimationClip;
            if (c != null && !c.name.StartsWith("__")) return c;
        }
        return null;
    }
}
