using System.Collections.Generic;
using UnityEngine;

// 착지 지점 기준 실제 월드공간 거리로 링을 그린다. 바닥 평면 하나 + 주변에 감지된 벽에
// 전부 같은 히트포인트/반경을 넣어서, 바닥에서 벽으로 자연스럽게 이어지는 것처럼 보이게 한다
// (RingPulse 셰이더가 UV가 아니라 월드 좌표로 거리를 계산하기 때문에 가능함).
public class WorldRingEffect : MonoBehaviour
{
    [SerializeField] Material ringMaterial;
    [SerializeField] float duration = 1f;
    [SerializeField] float maxRadius = 2f;
    [SerializeField] float wallProbeDistance = 2f;
    [SerializeField] LayerMask wallMask = ~0;

    static readonly Vector3[] ProbeDirections = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
    static readonly int HitPointID = Shader.PropertyToID("_HitPoint");
    static readonly int ProgressID = Shader.PropertyToID("_Progress");
    static readonly int MaxRadiusID = Shader.PropertyToID("_MaxRadius");

    Vector3 hitPoint;
    float timer;
    readonly List<Renderer> quads = new List<Renderer>();
    MaterialPropertyBlock block;

    public void Init(Vector3 hitPoint, Vector3 surfaceNormal)
    {
        this.hitPoint = hitPoint;
        block = new MaterialPropertyBlock();

        AddQuad(hitPoint, surfaceNormal);
        foreach (Vector3 dir in ProbeDirections)
        {
            if (Physics.Raycast(hitPoint + surfaceNormal * 0.05f, dir, out RaycastHit hit, wallProbeDistance, wallMask))
                AddQuad(hit.point, hit.normal);
        }
    }

    void AddQuad(Vector3 position, Vector3 normal)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(go.GetComponent<Collider>());
        go.name = "RingSurface";
        go.transform.SetParent(transform, true);
        go.transform.position = position + normal * 0.02f; // z-fighting 방지
        go.transform.rotation = Quaternion.LookRotation(normal);
        go.transform.localScale = Vector3.one * (maxRadius * 2.2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = ringMaterial;
        quads.Add(go.GetComponent<Renderer>());
    }

    void Update()
    {
        timer += Time.deltaTime;
        float radius = Mathf.Clamp01(timer / duration) * maxRadius;

        foreach (Renderer r in quads)
        {
            r.GetPropertyBlock(block);
            block.SetVector(HitPointID, hitPoint);
            block.SetFloat(ProgressID, radius);
            block.SetFloat(MaxRadiusID, maxRadius);
            r.SetPropertyBlock(block);
        }

        if (timer >= duration) Destroy(gameObject);
    }
}
