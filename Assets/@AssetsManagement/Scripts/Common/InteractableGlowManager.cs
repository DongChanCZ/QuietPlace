using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 있는 모든 InteractableGlow 를 한 곳에서 돌려준다.
///
/// 반짝이는 물건이 수십 개가 될 수 있어서 각자 Update 를 돌면 낭비다. 더 중요한 건
/// 각자 플레이어를 찾으려면 Find 계열을 써야 한다는 점이다(CLAUDE.md 금지). 그래서
/// 플레이어/카메라 참조는 이 매니저가 인스펙터로 하나만 들고, 등록된 것들에 넘겨준다.
/// </summary>
[DefaultExecutionOrder(-900)]
public class InteractableGlowManager : MonoBehaviour
{
    public static InteractableGlowManager Instance { get; private set; }

    [Header("플레이어 (거리 판정 기준)")]
    [SerializeField] private Transform player;

    [Header("플레이어 카메라 (반짝이가 마주볼 방향) - 비우면 player 를 쓴다")]
    [SerializeField] private Transform eyes;

    [Header("반짝이 머티리얼 (에디터 도구가 만들어 넣는다)")]
    [SerializeField] private Material sparkleMaterial;

    // 등록은 매니저가 없을 때도 일어날 수 있다(스크립트 실행 순서). 그래서 목록은 static 이다.
    private static readonly List<InteractableGlow> _glows = new List<InteractableGlow>();

    private Mesh _quad;
    private readonly HashSet<InteractableGlow> _sparkled = new HashSet<InteractableGlow>();

    public static void Register(InteractableGlow glow)
    {
        if (glow == null || _glows.Contains(glow)) return;
        _glows.Add(glow);
    }

    public static void Unregister(InteractableGlow glow)
    {
        _glows.Remove(glow);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _quad = BuildQuad();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_quad != null) Destroy(_quad);
    }

    void Update()
    {
        if (player == null) return;

        Transform look = eyes != null ? eyes : player;
        Vector3 pos = player.position;
        float t = Time.time;

        // 뒤에서부터 돌면서 사라진 항목을 안전하게 걷어낸다
        for (int i = _glows.Count - 1; i >= 0; i--)
        {
            InteractableGlow g = _glows[i];
            if (g == null) { _glows.RemoveAt(i); continue; }

            if (sparkleMaterial != null && _sparkled.Add(g)) g.AttachSparkle(sparkleMaterial, _quad);

            g.Tick(pos, look, t);
        }
    }

    /// 반짝이용 사각형 하나. Resources.GetBuiltinResource 는 빌드에서 빠질 수 있어서 직접 만든다.
    private static Mesh BuildQuad()
    {
        Mesh m = new Mesh { name = "SparkleQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
        };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }
}
