using UnityEngine;

// 탐지/소음 범위를 바닥에 거의 안 보일 정도로 얇게 표시하는 디버그용 원판. Monster/Player 둘 다 재사용.
public static class RangeGizmo
{
    public static Transform CreateDisc(Transform parent, string discName, Color color)
    {
        // 플레이 중에 만든 원판이 씬에 저장돼 남는 일이 있다. 그대로 두면 원판이 둘이 되어
        // 표시 on/off 가 새로 만든 쪽에만 걸리고 옛날 것은 계속 보인다.
        Transform stale = parent.Find(discName);
        if (stale != null) Object.Destroy(stale.gameObject);

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = discName;
        disc.layer = 2; // Ignore Raycast — 카메라/AI 계산에 안 끼도록
        Object.Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(parent, false);

        var mr = disc.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color); // URP는 _Color가 아니라 _BaseColor
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterial = mat;

        return disc.transform;
    }

    public static void UpdateDisc(Transform disc, Vector3 center, float range, float groundOffset)
    {
        if (disc == null) return;
        disc.position = center + Vector3.up * groundOffset;
        disc.localScale = new Vector3(range * 2f, 0.01f, range * 2f);
    }

    public static void SetColor(Transform disc, Color color)
    {
        if (disc == null) return;
        var mr = disc.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial.SetColor("_BaseColor", color); // CreateDisc에서 디스크마다 고유 Material을 만들어주므로 안전
    }
}
