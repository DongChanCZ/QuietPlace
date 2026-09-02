using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FlashlightCheck
{
    [MenuItem("Tools/Lab Blockout/Place Flashlight On Current Desk")]
    static void PlaceOnCurrentDesk()
    {
        var desk = GameObject.Find("PROP_1F_Guard_WoodenOfficeDesk");
        var item = GameObject.Find("ITEM_1F_Desk_Flashlight");
        if (desk == null || item == null)
            throw new Exception("Desk or flashlight item is missing.");

        var deskPosition = desk.transform.position;
        var deskRotation = desk.transform.rotation;
        var deskScale = desk.transform.localScale;
        NormalizeItemRoot(item);
        var deskRenderer = desk.GetComponentInChildren<Renderer>();
        var meshFilter = desk.GetComponent<MeshFilter>();
        if (deskRenderer == null || meshFilter == null || meshFilter.sharedMesh == null)
            throw new Exception("The current desk has no usable mesh.");

        var box = desk.GetComponent<BoxCollider>();
        var boxWasEnabled = box != null && box.enabled;
        var meshCollider = desk.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        if (box != null) box.enabled = false;

        try
        {
            Physics.SyncTransforms();
            var bounds = deskRenderer.bounds;
            var candidates = new List<RaycastHit>();
            for (var ix = 1; ix < 8; ix++)
            for (var iz = 1; iz < 12; iz++)
            {
                var x = Mathf.Lerp(bounds.min.x, bounds.max.x, ix / 8f);
                var z = Mathf.Lerp(bounds.min.z, bounds.max.z, iz / 12f);
                var hits = Physics.RaycastAll(
                    new Vector3(x, bounds.max.y + 0.5f, z),
                    Vector3.down,
                    bounds.size.y + 1f);
                foreach (var hit in hits)
                    if (hit.collider == meshCollider && hit.normal.y > 0.7f &&
                        hit.point.y > bounds.min.y + 0.3f &&
                        hit.point.y < bounds.max.y - 0.3f)
                        candidates.Add(hit);
            }

            if (candidates.Count == 0)
                throw new Exception("Could not find a horizontal desk surface.");

            var bestCount = 0;
            var tabletopY = 0f;
            foreach (var candidate in candidates)
            {
                var count = 0;
                foreach (var other in candidates)
                    if (Mathf.Abs(candidate.point.y - other.point.y) < 0.025f) count++;
                if (count <= bestCount) continue;
                bestCount = count;
                tabletopY = candidate.point.y;
            }

            var preferred = new Vector3(
                bounds.min.x + bounds.size.x * 0.30f,
                tabletopY,
                bounds.center.z + bounds.size.z * 0.22f);
            var placement = candidates[0].point;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (Mathf.Abs(candidate.point.y - tabletopY) >= 0.025f) continue;
                var distance = (new Vector2(candidate.point.x, candidate.point.z) -
                                new Vector2(preferred.x, preferred.z)).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                placement = candidate.point;
            }

            Undo.RecordObject(item.transform, "Place flashlight on current desk");
            var itemBounds = GetRendererBounds(item);
            item.transform.position += new Vector3(
                placement.x - itemBounds.center.x,
                tabletopY + itemBounds.extents.y + 0.008f - itemBounds.center.y,
                placement.z - itemBounds.center.z);

            if (desk.transform.position != deskPosition || desk.transform.rotation != deskRotation ||
                desk.transform.localScale != deskScale)
                throw new Exception("Desk transform changed; placement was cancelled.");

            EditorSceneManager.MarkSceneDirty(item.scene);
            EditorSceneManager.SaveScene(item.scene);
            Debug.Log($"Flashlight placed on current desk. Tabletop Y={tabletopY:F3}, item={item.transform.position:F3}, desk unchanged.");
        }
        finally
        {
            if (box != null) box.enabled = boxWasEnabled;
            UnityEngine.Object.DestroyImmediate(meshCollider);
        }
    }

    static void NormalizeItemRoot(GameObject item)
    {
        Undo.RegisterFullObjectHierarchyUndo(item, "Normalize flashlight root");
        var visualCenter = GetRendererBounds(item).center;
        var parent = item.transform.parent;
        var children = new List<Transform>();
        foreach (Transform child in item.transform) children.Add(child);
        foreach (var child in children) child.SetParent(parent, true);
        item.transform.SetPositionAndRotation(visualCenter, Quaternion.identity);
        item.transform.localScale = Vector3.one;
        foreach (var child in children) child.SetParent(item.transform, true);
    }

    static Bounds GetRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new Exception($"{root.name} has no renderers.");
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    [MenuItem("Tools/Lab Blockout/Verify Flashlight")]
    static void RunFromMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var root = new GameObject("__FlashlightCheckPlayer");
        try
        {
            root.AddComponent<CharacterController>();
            var cameraObject = new GameObject("CheckCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.AddComponent<Camera>();
            var player = root.AddComponent<BlockoutWalkPlayer>();
            typeof(BlockoutWalkPlayer).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(player, null);

            if (player.HasFlashlight || player.FlashlightEnabled)
                throw new Exception("The player must start without an owned or enabled flashlight.");
            player.GiveFlashlight();
            player.ToggleFlashlight();
            if (!player.HasFlashlight || !player.FlashlightEnabled)
                throw new Exception("Pickup or F-toggle failed.");
            player.ToggleFlashlight();
            if (player.FlashlightEnabled)
                throw new Exception("Second F-toggle failed to turn the flashlight off.");
            return "PASS: starts off, pickup grants ownership, F toggles on and off.";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
