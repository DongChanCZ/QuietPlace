using UnityEngine;

// 퀵슬롯에서 던진 오브젝트에 붙는다. 착지(첫 충돌) 지점에 링 이펙트를 띄우고 소리를 낸다 - 몬스터를
// 유인하는 미끼 용도라 loudNoiseCommitThreshold(8)를 확실히 넘기는 세기로 낸다.
public sealed class ThrownItem : MonoBehaviour
{
    const float ImpactNoiseLevel = 10f;
    const float ImpactNoiseRange = 8f;

    GameObject landingRingPrefab;
    bool landed;

    public void Init(GameObject landingRingPrefab)
    {
        this.landingRingPrefab = landingRingPrefab;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (landed) return;
        landed = true;

        ContactPoint contact = collision.GetContact(0);

        if (landingRingPrefab != null)
        {
            GameObject ring = Instantiate(landingRingPrefab, contact.point, Quaternion.identity);
            ring.GetComponent<WorldRingEffect>().Init(contact.point, contact.normal);
        }

        NoiseManager.Instance?.Emit(new NoiseEvent(contact.point, ImpactNoiseLevel, ImpactNoiseRange, NoiseType.ObjectImpact));

        Destroy(gameObject);
    }
}
