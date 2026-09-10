using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 박쥐 실루엣 그림자를 만드는 메시를 코드로 찍어낸다.
///
/// 왜 이렇게 하는가. 그림자를 드리우려면 빛을 막을 물체가 필요한데,
///  - 단면 판(Quad)은 그림자를 만들지 못한다(실제로 확인했다. 아무 것도 안 비친다).
///  - 얇은 박스를 여러 개 세우면 그림자가 나오지만 네모난 조각으로 읽힌다.
/// 그래서 뾰족한 귀와 갈고리 날개를 가진 윤곽선을 그대로 두께만 준 메시로 만든다.
/// 실루엣이라 앞뒤 디테일은 필요 없고, 윤곽선 하나만 맞으면 된다.
///
/// 만든 메시는 에셋으로 저장한다. 코드로 만든 메시를 씬에서 참조만 하면
/// 에디터를 다시 켤 때 사라진다.
/// </summary>
public static class BatSilhouetteMesh
{
    const string AssetPath = "Assets/MiniGame_QuietPlace/18_Ending/BatSilhouette.asset";

    /// 머리 + 뾰족한 귀 두 개. 원점은 목 부근.
    static readonly Vector2[] Head =
    {
        new Vector2(-0.26f, -0.46f),
        new Vector2(-0.32f, -0.06f),
        new Vector2(-0.38f,  0.24f),
        new Vector2(-0.30f,  0.44f),   // 왼쪽 귀 바깥
        new Vector2(-0.44f,  0.98f),   // 왼쪽 귀 끝
        new Vector2(-0.16f,  0.40f),   // 귀 사이 골
        new Vector2( 0.00f,  0.48f),
        new Vector2( 0.16f,  0.40f),
        new Vector2( 0.44f,  0.98f),   // 오른쪽 귀 끝
        new Vector2( 0.30f,  0.44f),
        new Vector2( 0.38f,  0.24f),
        new Vector2( 0.32f, -0.06f),
        new Vector2( 0.26f, -0.46f),
        new Vector2( 0.00f, -0.58f),   // 턱
    };

    /// 오른쪽 날개. 원점이 어깨라 이 점을 축으로 회전시키면 접었다 펼 수 있다.
    /// 손가락 끝과 그 사이 오목한 막(스캘럽)이 번갈아 나와야 박쥐로 읽힌다.
    static readonly Vector2[] Wing =
    {
        new Vector2(0.00f,  0.12f),
        new Vector2(0.34f,  0.44f),   // 위팔
        new Vector2(0.60f,  0.68f),   // 손목
        new Vector2(0.54f,  0.86f),   // 엄지 갈고리
        new Vector2(0.74f,  0.74f),
        new Vector2(1.10f,  0.58f),   // 첫째 손가락 끝
        new Vector2(0.96f,  0.34f),   // 막
        new Vector2(1.22f,  0.16f),   // 둘째 손가락 끝
        new Vector2(0.94f, -0.02f),   // 막
        new Vector2(1.08f, -0.24f),   // 셋째 손가락 끝
        new Vector2(0.74f, -0.30f),   // 막
        new Vector2(0.78f, -0.56f),   // 넷째 손가락 끝
        new Vector2(0.38f, -0.38f),   // 뒤쪽 막이 몸으로
        new Vector2(0.08f, -0.24f),
    };

    /// <summary>머리 메시와 날개 메시를 만들어 에셋으로 저장하고 돌려준다.</summary>
    public static void Get(float thickness, out Mesh head, out Mesh wing)
    {
        head = Build(Head, thickness, "BatHead");
        wing = Build(Wing, thickness, "BatWing");

        // 두 메시를 한 에셋 파일에 담는다. 파일이 흩어지지 않는다.
        AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(head, AssetPath);
        AssetDatabase.AddObjectToAsset(wing, AssetPath);
        AssetDatabase.SaveAssets();

        head = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
        {
            Mesh m = o as Mesh;
            if (m != null && m.name == "BatWing") wing = m;
        }
    }

    /// 2D 윤곽선을 두께만 준 닫힌 메시로 만든다. 앞면 + 뒷면 + 옆면 띠.
    static Mesh Build(Vector2[] outline, float thickness, string name)
    {
        int[] tris = Triangulate(outline);
        int count = outline.Length;
        float half = Mathf.Max(0.005f, thickness) * 0.5f;

        var verts = new List<Vector3>(count * 4);
        var indices = new List<int>(tris.Length * 2 + count * 6);

        // 앞면 (z = -half) - 빛이 오는 쪽
        for (int i = 0; i < count; i++) verts.Add(new Vector3(outline[i].x, outline[i].y, -half));
        for (int i = 0; i < tris.Length; i += 3)
        {
            indices.Add(tris[i]); indices.Add(tris[i + 1]); indices.Add(tris[i + 2]);
        }

        // 뒷면 (z = +half) - 감는 방향을 뒤집는다
        for (int i = 0; i < count; i++) verts.Add(new Vector3(outline[i].x, outline[i].y, half));
        for (int i = 0; i < tris.Length; i += 3)
        {
            indices.Add(count + tris[i + 2]); indices.Add(count + tris[i + 1]); indices.Add(count + tris[i]);
        }

        // 옆면 띠 - 닫힌 부피가 되어야 그림자가 안정적으로 나온다
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            indices.Add(i); indices.Add(count + i); indices.Add(count + j);
            indices.Add(i); indices.Add(count + j); indices.Add(j);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 귀 자르기(ear clipping). 오목한 윤곽선도 삼각형으로 나눌 수 있어야 한다 -
    /// 부채꼴로 나누면 손가락 사이 오목한 막에서 삼각형이 윤곽선 밖으로 삐져나온다.
    /// </summary>
    static int[] Triangulate(Vector2[] poly)
    {
        int n = poly.Length;
        var indices = new List<int>(n);
        for (int i = 0; i < n; i++) indices.Add(i);

        // 시계/반시계를 통일한다. 부호 있는 면적이 음수면 뒤집는다.
        if (SignedArea(poly) < 0f) indices.Reverse();

        var tris = new List<int>((n - 2) * 3);
        int guard = 0;

        while (indices.Count > 3 && guard++ < n * n)
        {
            bool clipped = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int i0 = indices[(i - 1 + indices.Count) % indices.Count];
                int i1 = indices[i];
                int i2 = indices[(i + 1) % indices.Count];

                Vector2 a = poly[i0], b = poly[i1], c = poly[i2];

                // 오목한 꼭짓점은 자를 수 없다
                if (Cross(b - a, c - b) <= 0f) continue;

                // 다른 꼭짓점이 이 삼각형 안에 들어 있으면 자를 수 없다
                bool contains = false;
                for (int k = 0; k < indices.Count && !contains; k++)
                {
                    int ik = indices[k];
                    if (ik == i0 || ik == i1 || ik == i2) continue;
                    contains = InTriangle(poly[ik], a, b, c);
                }
                if (contains) continue;

                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                indices.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped) break;   // 자를 귀가 없다 - 여기까지로 끝낸다
        }

        if (indices.Count == 3) { tris.Add(indices[0]); tris.Add(indices[1]); tris.Add(indices[2]); }
        return tris.ToArray();
    }

    static float SignedArea(Vector2[] p)
    {
        float s = 0f;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % p.Length];
            s += a.x * b.y - b.x * a.y;
        }
        return s * 0.5f;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }
}
