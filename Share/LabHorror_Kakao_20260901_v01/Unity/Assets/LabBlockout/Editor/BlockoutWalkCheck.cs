using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BlockoutWalkCheck
{
    [MenuItem("Lab Blockout/Check player round trip (Play Mode)")]
    public static void RunMenu() => Debug.Log(Run());

    [MenuItem("Lab Blockout/Check 3F envelope (Play Mode)")]
    public static void RunEnvelopeMenu() => Debug.Log(RunEnvelope());

    [MenuItem("Lab Blockout/Check 3F Act 4 route (Play Mode)")]
    public static void RunThirdFloorRouteMenu() => Debug.Log(RunThirdFloorRoute());

    // Walks Act 4 on foot: stair arrival -> core -> corridor -> security door
    // -> broadcast control -> escape window. Blender x maps to -x here and
    // Blender y to -z, so these are the plan coordinates mirrored.
    public static string RunThirdFloorRoute()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var player = UnityEngine.Object.FindFirstObjectByType<BlockoutWalkPlayer>();
        if (player == null) throw new InvalidOperationException("WalkTestPlayer is missing.");
        var body = player.GetComponent<CharacterController>();
        var legs = new[]
        {
            new KeyValuePair<string, Vector3>("stair arrival", new Vector3(-10.0f, ThirdFloor, -2.60f)),
            new KeyValuePair<string, Vector3>("core lobby", new Vector3(-9.0f, ThirdFloor, -0.50f)),
            new KeyValuePair<string, Vector3>("corridor east", new Vector3(-7.0f, ThirdFloor, -0.60f)),
            new KeyValuePair<string, Vector3>("corridor mid", new Vector3(0.0f, ThirdFloor, -0.60f)),
            new KeyValuePair<string, Vector3>("security door", new Vector3(4.0f, ThirdFloor, -0.60f)),
            new KeyValuePair<string, Vector3>("through the door", new Vector3(4.0f, ThirdFloor, -3.00f)),
            new KeyValuePair<string, Vector3>("broadcast control", new Vector3(6.0f, ThirdFloor, -4.60f)),
            new KeyValuePair<string, Vector3>("escape window", new Vector3(9.2f, ThirdFloor, -4.50f)),
        };
        bool wasEnabled = player.enabled;
        player.enabled = false;
        try
        {
            Place(player, body, legs[0].Value);
            for (int i = 1; i < legs.Length; i++)
            {
                Vector3 target = legs[i].Value;
                int ticks = 0;
                while (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                           new Vector2(target.x, target.z)) > 0.10f && ticks++ < 900)
                {
                    Vector3 delta = target - player.transform.position;
                    delta.y = 0;
                    Vector3 local = player.transform.InverseTransformDirection(delta.normalized);
                    player.Move(new Vector2(local.x, local.z), 1f / 60f);
                    if (player.transform.position.y < ThirdFloor - 0.5f)
                        throw new Exception("Fell off the 3F on the way to " + legs[i].Key);
                }
                for (int s = 0; s < 15; s++) player.Move(Vector2.zero, 1f / 60f);
                if (ticks >= 900)
                    throw new Exception("Blocked before " + legs[i].Key + "; stopped at "
                                        + player.transform.position.ToString("F2"));
                if (!body.isGrounded) throw new Exception("Not grounded at " + legs[i].Key);
            }

            // The escape window must be a real hole: chest height solid, sill
            // height open, because you climb out over a 0.90 m sill.
            RaycastHit hit;
            bool sillSolid = Physics.Raycast(new Vector3(9.2f, ThirdFloor + 0.45f, -4.50f), Vector3.right, out hit, 1.2f);
            bool openAbove = !Physics.Raycast(new Vector3(9.2f, ThirdFloor + 1.60f, -4.50f), Vector3.right, out hit, 1.2f);
            if (!sillSolid) throw new Exception("The escape window has no sill to climb over.");
            if (!openAbove) throw new Exception("The escape window is not open above its sill.");

            return "PASS: Act 4 walked in full - " + legs.Length
                 + " legs from the stair arrival to the escape window, sill and opening both correct.";
        }
        finally
        {
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }

    const float ThirdFloor = 6.40f;

    // Pushes the live CharacterController at every 3F edge and asserts it stays
    // inside. Run this after any re-export: the roof and the stair-opening guards
    // are the only thing between the player and a fall.
    public static string RunEnvelope()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var player = UnityEngine.Object.FindFirstObjectByType<BlockoutWalkPlayer>();
        if (player == null) throw new InvalidOperationException("WalkTestPlayer is missing.");
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = player.enabled;
        player.enabled = false;
        int edgeTests = 0, lipTests = 0, rayOnly = 0;
        try
        {
            // Exterior walls: stand 0.75 m inside each one and walk straight at it.
            // Sample points over the stair/elevator openings have no floor to stand
            // on, so those fall back to a chest-height ray at the same spot.
            for (float z = -6.0f; z <= 6.0f; z += 1.0f)
            {
                if (CheckContained(player, body, new Vector3(9.15f, ThirdFloor, z), Vector3.right, "east wall")) edgeTests++; else rayOnly++;
                if (CheckContained(player, body, new Vector3(-11.15f, ThirdFloor, z), Vector3.left, "west wall")) edgeTests++; else rayOnly++;
            }
            for (float x = -7.0f; x <= 9.0f; x += 1.0f)
            {
                if (CheckContained(player, body, new Vector3(x, ThirdFloor, 6.15f), Vector3.forward, "north wall")) edgeTests++; else rayOnly++;
                if (CheckContained(player, body, new Vector3(x, ThirdFloor, -6.15f), Vector3.back, "south wall")) edgeTests++; else rayOnly++;
            }

            // Stair opening: west of the upper flight the lip must be guarded.
            for (float x = -11.80f; x <= -10.00f; x += 0.20f)
            {
                Place(player, body, new Vector3(x, ThirdFloor, -2.80f));
                Push(player, Vector3.back, 40);
                Vector3 p = player.transform.position;
                if (p.y < ThirdFloor - 0.30f || p.z < -3.25f)
                    throw new Exception("Fell into the stair opening from x=" + x.ToString("F2") + "; ended at " + p);
                lipTests++;
            }

            // Roof: the deck must be capped everywhere a player can stand.
            int capped = 0, uncapped = 0;
            RaycastHit hit;
            for (float x = -7.5f; x <= 9.5f; x += 1.0f)
                for (float z = -6.5f; z <= 6.5f; z += 1.0f)
                {
                    if (Physics.Raycast(new Vector3(x, ThirdFloor + 0.5f, z), Vector3.up, out hit, 6f)) capped++;
                    else uncapped++;
                }
            if (uncapped > 0) throw new Exception(uncapped + " deck cells have no roof above them.");

            return "PASS: " + edgeTests + " exterior pushes contained (" + rayOnly
                 + " sample spots had no floor, ray-checked instead), " + lipTests
                 + " stair-lip pushes guarded, " + capped + " deck cells roofed.";
        }
        finally
        {
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }

    // Returns true when the spot was actually walked; false when there was no
    // floor there and the wall was verified with a ray instead.
    static bool CheckContained(BlockoutWalkPlayer player, CharacterController body, Vector3 start, Vector3 dir, string label)
    {
        RaycastHit hit;
        if (!Physics.Raycast(start + Vector3.up * 0.5f, Vector3.down, out hit, 0.7f))
        {
            if (!Physics.Raycast(start + Vector3.up * 1.0f, dir, out hit, 1.5f))
                throw new Exception("No floor and no " + label + " at " + start);
            return false;
        }
        Place(player, body, start);
        Push(player, dir, 60);
        Vector3 p = player.transform.position;
        if (p.x < -12f || p.x > 10f || p.z < -7f || p.z > 7f)
            throw new Exception("Walked through the " + label + " from " + start + " to " + p);
        if (p.y < ThirdFloor - 0.30f)
            throw new Exception("Fell at the " + label + " from " + start + "; ended at " + p);
        return true;
    }

    static void Place(BlockoutWalkPlayer player, CharacterController body, Vector3 position)
    {
        body.enabled = false;
        player.transform.position = position;
        body.enabled = true;
        Physics.SyncTransforms();
        for (int i = 0; i < 10; i++) player.Move(Vector2.zero, 1f / 60f);
    }

    static void Push(BlockoutWalkPlayer player, Vector3 worldDirection, int ticks)
    {
        Vector3 local = player.transform.InverseTransformDirection(worldDirection.normalized);
        var input = new Vector2(local.x, local.z);
        for (int i = 0; i < ticks; i++) player.Move(input, 1f / 60f);
    }

    // Exercises the live CharacterController movement, not raycasts or teleports.
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var player = UnityEngine.Object.FindFirstObjectByType<BlockoutWalkPlayer>();
        if (player == null) throw new InvalidOperationException("WalkTestPlayer is missing.");
        var body = player.GetComponent<CharacterController>();
        var camera = player.GetComponentInChildren<Camera>();
        var points = new[]
        {
            new Vector3(0, 0, -4.5f),
            new Vector3(4.2f, 0, -4.5f), new Vector3(5.8f, 0, -4.5f),
            new Vector3(4.2f, 0, -4.5f), new Vector3(0, 0, -2.85f),
            new Vector3(-7.2f, 0, -2.85f), new Vector3(-8.8f, 0, -2.85f),
            new Vector3(-10.8f, 0, -2.65f),
            new Vector3(-10.8f, 1.6f, -6.12f), new Vector3(-9.2f, 1.6f, -6.12f),
            new Vector3(-9.2f, 3.2f, -2.6f),
            new Vector3(-9.2f, 3.2f, -1.2f), new Vector3(-7, 3.2f, -1.2f),
            new Vector3(-9.2f, 3.2f, -1.2f), new Vector3(-9.2f, 3.2f, -2.6f),
            new Vector3(-10.8f, 3.2f, -2.65f),
            new Vector3(-10.8f, 4.8f, -6.12f), new Vector3(-9.2f, 4.8f, -6.12f),
            new Vector3(-9.2f, 6.4f, -2.6f), new Vector3(-6.5f, 6.4f, -2.6f)
        };
        var route = new List<Vector3>(points);
        for (int i = points.Length - 2; i >= 0; i--) route.Add(points[i]);
        route.Add(new Vector3(0, 0, -8.25f));
        bool wasEnabled = player.enabled;
        player.enabled = false;
        try
        {
            player.ResetToStart();
            Physics.SyncTransforms();
            foreach (Vector3 target in route)
            {
                int ticks = 0;
                while (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                           new Vector2(target.x, target.z)) > 0.08f && ticks++ < 600)
                {
                    Vector3 delta = target - player.transform.position;
                    delta.y = 0;
                    Vector3 local = player.transform.InverseTransformDirection(delta.normalized);
                    player.Move(new Vector2(local.x, local.z), 1f / 60f);
                    if (player.transform.position.y < -0.3f)
                        throw new Exception("Fell below the floor toward " + target);
                }
                for (int i = 0; i < 15; i++) player.Move(Vector2.zero, 1f / 60f);
                if (ticks >= 600 || Mathf.Abs(player.transform.position.y - target.y) > 0.22f)
                    throw new Exception("Blocked/misaligned toward " + target + "; stopped at " + player.transform.position);
                if (!body.isGrounded) throw new Exception("Not grounded at " + target);
            }
            if (Mathf.Abs(camera.transform.localPosition.y - 1.65f) > 0.001f)
                throw new Exception("Eye height changed.");
            player.ApplyLook(new Vector2(100, 100000));
            float tilt = Mathf.DeltaAngle(0, camera.transform.localEulerAngles.x);
            if (Mathf.Abs(tilt + 80) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(0, player.transform.eulerAngles.y) - 9) > 0.01f)
                throw new Exception("Mouse look or vertical clamp failed.");
            return "PASS: " + route.Count + " waypoints, entrance/guard room/2F corridor/3F round trip; eye height 1.65m; mouse look clamped.";
        }
        finally
        {
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }
}
