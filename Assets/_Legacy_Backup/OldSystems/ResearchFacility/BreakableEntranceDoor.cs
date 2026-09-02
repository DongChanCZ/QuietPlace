using UnityEngine;
using System.Collections;

public class BreakableEntranceDoor : MonoBehaviour
{
    public Transform player;
    public float activationDistance = 3.2f;
    bool broken;

    float DistanceToDoor()
    {
        var r = GetComponent<Renderer>();
        Vector3 target = r ? r.bounds.ClosestPoint(player.position) : transform.position;
        target.y = player.position.y;
        return Vector3.Distance(player.position, target);
    }

    void Update()
    {
        if (!broken && player && DistanceToDoor() <= activationDistance) BreakDoor();
    }

    public void BreakDoor()
    {
        if (broken) return;
        broken = true;

        var sourceRenderer = GetComponent<MeshRenderer>();
        var sourceFilter = GetComponent<MeshFilter>();
        Bounds bounds = sourceRenderer ? sourceRenderer.bounds : new Bounds(transform.position, new Vector3(2.7f, 3.15f, .3f));
        Material[] materials = sourceRenderer ? sourceRenderer.sharedMaterials : new Material[0];

        foreach (var c in GetComponents<Collider>()) c.enabled = false;
        if (sourceRenderer) sourceRenderer.enabled = false;

        GameObject fallingDoor = null;
        if (sourceFilter && sourceFilter.sharedMesh)
        {
            fallingDoor = new GameObject("Falling Main Door");
            fallingDoor.transform.SetParent(transform.parent, true);
            fallingDoor.transform.position = transform.position;
            fallingDoor.transform.rotation = transform.rotation;
            fallingDoor.transform.localScale = transform.localScale;
            var filter = fallingDoor.AddComponent<MeshFilter>();
            filter.sharedMesh = sourceFilter.sharedMesh;
            var renderer = fallingDoor.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            var collider = fallingDoor.AddComponent<MeshCollider>();
            collider.sharedMesh = sourceFilter.sharedMesh;
            collider.convex = true;
            var body = fallingDoor.AddComponent<Rigidbody>();
            body.mass = 42f;
            body.linearDamping = .08f;
            body.angularDamping = .12f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.centerOfMass = new Vector3(0f, -.9f, 0f);
            body.AddForce(new Vector3(Random.Range(-.3f, .3f), .25f, 4.2f), ForceMode.Impulse);
            body.AddTorque(new Vector3(Random.Range(7f, 10f), Random.Range(-.5f, .5f), Random.Range(-.4f, .4f)), ForceMode.Impulse);
        }

        StartCoroutine(ShatterOnFloor(fallingDoor, bounds, materials.Length > 0 ? materials[0] : null));
    }

    IEnumerator ShatterOnFloor(GameObject fallingDoor, Bounds originalBounds, Material material)
    {
        yield return new WaitForSeconds(1.05f);
        Vector3 impact = fallingDoor ? fallingDoor.transform.position : originalBounds.center;
        if (fallingDoor) Destroy(fallingDoor);

        var rubbleRoot = new GameObject("Collapsed Entrance Rubble");
        rubbleRoot.transform.SetParent(transform.parent, true);
        float floorY = originalBounds.min.y;

        for (int i = 0; i < 34; i++)
        {
            var piece = GameObject.CreatePrimitive(i % 4 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            piece.name = "Door Fragment " + (i + 1);
            piece.transform.SetParent(rubbleRoot.transform, true);
            float side = Random.Range(-originalBounds.extents.x * 1.1f, originalBounds.extents.x * 1.1f);
            piece.transform.position = new Vector3(originalBounds.center.x + side, floorY + Random.Range(.12f, .65f), originalBounds.center.z + Random.Range(-.15f, 1.5f));
            piece.transform.localScale = i % 4 == 0
                ? new Vector3(Random.Range(.12f, .24f), Random.Range(.18f, .45f), Random.Range(.12f, .24f))
                : new Vector3(Random.Range(.18f, .65f), Random.Range(.10f, .32f), Random.Range(.25f, .8f));
            piece.transform.rotation = Random.rotation;
            if (material) piece.GetComponent<Renderer>().sharedMaterial = material;
            var body = piece.AddComponent<Rigidbody>();
            body.mass = Random.Range(.6f, 2.2f);
            body.linearDamping = .35f;
            body.angularDamping = .3f;
            body.AddForce(new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(.3f, 1.5f), Random.Range(.2f, 2f)), ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * Random.Range(2f, 7f), ForceMode.Impulse);
        }

        var blocker = new GameObject("Collapsed Entrance Blocker");
        blocker.transform.SetParent(rubbleRoot.transform, true);
        blocker.transform.position = new Vector3(originalBounds.center.x, floorY + .75f, originalBounds.center.z - .8f);
        blocker.AddComponent<BoxCollider>().size = new Vector3(Mathf.Max(2.5f, originalBounds.size.x + .4f), 1.5f, 1.1f);
    }

    void OnGUI()
    {
        if (!broken && player && DistanceToDoor() <= activationDistance + 1.5f)
            GUI.Label(new Rect(Screen.width / 2 - 125, Screen.height / 2 + 30, 280, 28), "Approach: entrance will collapse");
    }
}
