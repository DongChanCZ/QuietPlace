using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Every coordinate below is in DESIGN metres - the same 1 m module the Blender
// scripts use - and is converted to world space through the blockout root's
// actual scale. The scene instance currently carries a (1.5, 1.3, 1.5)
// override, so hardcoded world metres would put the player two storeys off and
// report a false failure. Read the scale, never assume it.
public static class BlockoutWalkCheck
{
    [MenuItem("Lab Blockout/Check player round trip (Play Mode)")]
    public static void RunMenu() => Debug.Log(Run());

    [MenuItem("Lab Blockout/Check 3F envelope (Play Mode)")]
    public static void RunEnvelopeMenu() => Debug.Log(RunEnvelope());

    [MenuItem("Lab Blockout/Check 3F Act 4 route (Play Mode)")]
    public static void RunThirdFloorRouteMenu() => Debug.Log(RunThirdFloorRoute());

    [MenuItem("Lab Blockout/Check stair landing guards (Play Mode)")]
    public static void RunLandingGuardsMenu() => Debug.Log(RunLandingGuards());

    [MenuItem("Lab Blockout/Check B1 rooms (Play Mode)")]
    public static void RunBasementMenu() => Debug.Log(RunBasement());

    [MenuItem("Lab Blockout/Check guard window entry (Play Mode)")]
    public static void RunGuardWindowMenu() => Debug.Log(RunGuardWindowEntry());

    [MenuItem("Lab Blockout/Check exterior containment (Play Mode)")]
    public static void RunExteriorMenu() => Debug.Log(RunExteriorContainment());

    const float ThirdFloor = 6.40f;          // design metres
    static Vector3 scale = Vector3.one;

    static void ReadScale()
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name.StartsWith("LabHorror_Blockout"))
            {
                scale = root.transform.localScale;
                return;
            }
        throw new InvalidOperationException("Blockout root is not in the scene, so the scale is unknown.");
    }

    static string ScaleTag()
    {
        return " [blockout scale " + scale.ToString("F2") + "]";
    }

    static Vector3 W(float x, float y, float z)
    {
        return new Vector3(x * scale.x, y * scale.y, z * scale.z);
    }

    static Vector3 W(Vector3 design) { return Vector3.Scale(design, scale); }
    static float Hz(float design) { return design * scale.x; }      // horizontal span
    static float Vt(float design) { return design * scale.y; }      // vertical span
    static int Ticks(int design) { return Mathf.CeilToInt(design * Mathf.Max(1f, scale.x)); }

    static BlockoutWalkPlayer FindPlayer()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var player = UnityEngine.Object.FindFirstObjectByType<BlockoutWalkPlayer>();
        if (player == null) throw new InvalidOperationException("WalkTestPlayer is missing.");
        ReadScale();
        return player;
    }

    // Walks Act 4 on foot: stair arrival -> core -> corridor -> security door
    // -> broadcast control -> escape window. Blender x maps to -x here and
    // Blender y to -z, so these are the plan coordinates mirrored.
    public static string RunThirdFloorRoute()
    {
        var player = FindPlayer();
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
        var doorsInTheWay = new List<string>();
        try
        {
            Place(player, body, W(legs[0].Value));
            for (int i = 1; i < legs.Length; i++)
            {
                Vector3 target = W(legs[i].Value);
                int budget = Ticks(900);
                int ticks = 0;
                while (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                           new Vector2(target.x, target.z)) > Hz(0.10f) && ticks++ < budget)
                {
                    Vector3 delta = target - player.transform.position;
                    delta.y = 0;
                    Vector3 local = player.transform.InverseTransformDirection(delta.normalized);
                    player.Move(new Vector2(local.x, local.z), 1f / 60f);
                    if (player.transform.position.y < W(0, ThirdFloor, 0).y - Vt(0.5f))
                        throw new Exception("Fell off the 3F on the way to " + legs[i].Key);
                }
                for (int s = 0; s < 15; s++) player.Move(Vector2.zero, 1f / 60f);
                if (ticks >= budget)
                {
                    // A shut door leaf or door prop is gameplay state, not a hole
                    // in the blockout. Name it, prove the wall opening is real,
                    // and carry on past it rather than failing the structure.
                    string blocker = ForgivableBlockerBetween(player.transform.position, target);
                    if (blocker == null)
                        throw new Exception("Blocked before " + legs[i].Key + " by blockout structure; stopped at "
                                            + player.transform.position.ToString("F2"));
                    doorsInTheWay.Add(legs[i].Key + " <- " + blocker);
                    Place(player, body, target);
                }
                if (!body.isGrounded) throw new Exception("Not grounded at " + legs[i].Key);
            }

            // The escape window must be a real hole: sill height solid, above it
            // open, because you climb out over a 0.90 m sill.
            RaycastHit hit;
            bool sillSolid = Physics.Raycast(W(9.2f, ThirdFloor + 0.45f, -4.50f), Vector3.right, out hit, Hz(1.2f));
            bool openAbove = !Physics.Raycast(W(9.2f, ThirdFloor + 1.60f, -4.50f), Vector3.right, out hit, Hz(1.2f));
            if (!sillSolid) throw new Exception("The escape window has no sill to climb over.");
            if (!openAbove) throw new Exception("The escape window is not open above its sill.");

            string doors = doorsInTheWay.Count == 0
                ? "no doors shut"
                : doorsInTheWay.Count + " leg(s) crossed a shut door: " + string.Join("; ", doorsInTheWay.ToArray());
            return "PASS: Act 4 walked in full - " + legs.Length
                 + " legs from the stair arrival to the escape window, sill and opening correct, "
                 + doors + "." + ScaleTag();
        }
        finally
        {
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }

    // Names whatever solid thing sits between here and there, but ONLY when it is
    // not part of the blockout. A shut door leaf or a placed prop is gameplay
    // state and may be stepped over; a wall that stands in the route is a real
    // failure and must not be excused, or the check hides exactly what it exists
    // to find.
    static string ForgivableBlockerBetween(Vector3 from, Vector3 to)
    {
        Vector3 eye = Vector3.up * Vt(1.0f);
        RaycastHit hit;
        if (!Physics.Linecast(from + eye, to + eye, out hit)) return null;
        Transform root = hit.collider.transform;
        while (root.parent != null) root = root.parent;
        if (root.name.StartsWith("LabHorror_Blockout")) return null;   // structure: not forgivable
        return hit.collider.name + " (under " + root.name + ")";
    }

    // Pushes the live CharacterController at every 3F edge and asserts it stays
    // inside. Run this after any re-export: the roof and the stair-opening guards
    // are the only thing between the player and a fall.
    public static string RunEnvelope()
    {
        var player = FindPlayer();
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = player.enabled;
        player.enabled = false;
        int edgeTests = 0, lipTests = 0, rayOnly = 0;
        try
        {
            // Exterior walls: stand 0.85 m inside each one and walk straight at it.
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
                Place(player, body, W(x, ThirdFloor, -2.80f));
                Push(player, Vector3.back, Ticks(40));
                Vector3 p = player.transform.position;
                if (p.y < W(0, ThirdFloor, 0).y - Vt(0.30f) || p.z < W(0, 0, -3.25f).z)
                    throw new Exception("Fell into the stair opening from design x=" + x.ToString("F2")
                                        + "; ended at " + p.ToString("F2"));
                lipTests++;
            }

            // Roof: the deck must be capped everywhere a player can stand.
            int capped = 0, uncapped = 0;
            RaycastHit hit;
            for (float x = -7.5f; x <= 9.5f; x += 1.0f)
                for (float z = -6.5f; z <= 6.5f; z += 1.0f)
                {
                    if (Physics.Raycast(W(x, ThirdFloor + 0.5f, z), Vector3.up, out hit, Vt(6f))) capped++;
                    else uncapped++;
                }
            if (uncapped > 0) throw new Exception(uncapped + " deck cells have no roof above them.");

            return "PASS: " + edgeTests + " exterior pushes contained (" + rayOnly
                 + " sample spots had no floor, ray-checked instead), " + lipTests
                 + " stair-lip pushes guarded, " + capped + " deck cells roofed." + ScaleTag();
        }
        finally
        {
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }

    // Each stair middle landing has an open gap to the stairwell wall on three
    // sides with a drop below, guarded only by a railing.
    //
    // Note on why this asserts the colliders explicitly rather than only pushing:
    // the gap measures 0.585 m and the capsule is 0.56 m across plus 0.03 m of
    // skin either side, so the player physically cannot enter the gap even with
    // the railing switched off. A push-only test therefore passes either way and
    // proves nothing. The collider assertion is the part that can actually fail,
    // and the push still catches anyone widening the stairwell later.
    public static string RunLandingGuards()
    {
        var player = FindPlayer();
        var body = player.GetComponent<CharacterController>();

        int guards = 0, guardsWithCollider = 0;
        var naked = new List<string>();
        Bounds landingBounds = new Bounds();
        bool haveLanding = false;
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("MID_") && t.name.Contains("GUARD"))
                {
                    guards++;
                    Collider c = t.GetComponent<Collider>();
                    if (c != null && c.enabled) guardsWithCollider++; else naked.Add(t.name);
                }
                if (!haveLanding && t.name.StartsWith("MIDDLE_LANDING"))
                {
                    Renderer rend = t.GetComponent<Renderer>();
                    if (rend != null) { landingBounds = rend.bounds; haveLanding = true; }
                }
            }
        if (guards == 0) throw new Exception("No MID_*_GUARD railings found at all.");
        if (naked.Count > 0)
            throw new Exception(naked.Count + " of " + guards + " landing railings have no collider, so the "
                                + "player walks through them: " + string.Join(", ", naked.ToArray()));
        if (!haveLanding) throw new Exception("MIDDLE_LANDING not found, so containment cannot be measured.");

        bool wasEnabled = player.enabled;
        player.enabled = false;
        int tests = 0;
        float worstMargin = 99f;
        try
        {
            foreach (float landing in new float[] { 1.60f, 4.80f })
                foreach (KeyValuePair<string, Vector3> dir in new[]
                {
                    new KeyValuePair<string, Vector3>("-x", Vector3.left),
                    new KeyValuePair<string, Vector3>("+x", Vector3.right),
                    new KeyValuePair<string, Vector3>("-z", Vector3.back),
                })
                {
                    Place(player, body, W(-10.0f, landing, -6.14f));
                    if (!body.isGrounded)
                        throw new Exception("Not standing on the design y=" + landing.ToString("F2")
                                            + " landing to begin with.");
                    Push(player, dir.Value, Ticks(45));
                    Vector3 p = player.transform.position;
                    if (p.y < W(0, landing, 0).y - Vt(0.30f))
                        throw new Exception("Fell off the design y=" + landing.ToString("F2")
                                            + " landing pushing " + dir.Key + "; ended at " + p.ToString("F2"));
                    // The capsule centre must not leave the landing slab footprint.
                    float mx = Mathf.Min(p.x - landingBounds.min.x, landingBounds.max.x - p.x);
                    float mz = Mathf.Min(p.z - landingBounds.min.z, landingBounds.max.z - p.z);
                    float margin = Mathf.Min(mx, mz);
                    if (margin < 0f)
                        throw new Exception("Left the landing footprint pushing " + dir.Key
                                            + " on the design y=" + landing.ToString("F2")
                                            + " landing; ended at " + p.ToString("F2"));
                    if (margin < worstMargin) worstMargin = margin;
                    tests++;
                }
            return "PASS: " + guardsWithCollider + "/" + guards + " landing railings collide, " + tests
                 + " pushes stayed on the slab (closest approach to an edge "
                 + worstMargin.ToString("F3") + " m; capsule is "
                 + (body.radius * 2f + body.skinWidth * 2f).ToString("F2") + " m across)." + ScaleTag();
        }
        finally
        {
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }

    // Returns true when the spot was actually walked; false when there was no
    // floor there and the wall was verified with a ray instead. Takes design
    // coordinates and scales them itself.
    static bool CheckContained(BlockoutWalkPlayer player, CharacterController body, Vector3 designStart, Vector3 dir, string label)
    {
        Vector3 start = W(designStart);
        RaycastHit hit;
        if (!Physics.Raycast(start + Vector3.up * Vt(0.5f), Vector3.down, out hit, Vt(0.7f)))
        {
            if (!Physics.Raycast(start + Vector3.up * Vt(1.0f), dir, out hit, Hz(1.5f)))
                throw new Exception("No floor and no " + label + " at design " + designStart.ToString("F2"));
            return false;
        }
        Place(player, body, start);
        Push(player, dir, Ticks(60));
        Vector3 p = player.transform.position;
        Vector3 low = W(-12f, 0, -7f), high = W(10f, 0, 7f);
        if (p.x < low.x || p.x > high.x || p.z < low.z || p.z > high.z)
            throw new Exception("Walked through the " + label + " from " + start.ToString("F2") + " to " + p.ToString("F2"));
        if (p.y < W(0, ThirdFloor, 0).y - Vt(0.30f))
            throw new Exception("Fell at the " + label + " from " + start.ToString("F2") + "; ended at " + p.ToString("F2"));
        return true;
    }

    static void Place(BlockoutWalkPlayer player, CharacterController body, Vector3 worldPosition)
    {
        body.enabled = false;
        player.transform.position = worldPosition;
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
    // Starts at the first interior waypoint rather than the player's spawn: the
    // spawn now sits outside on the entrance approach, behind the main entrance
    // door leaf and the collapse set, and whether those let you in is gameplay
    // state rather than a property of the blockout.
    public static string Run()
    {
        var player = FindPlayer();
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
            // z=-3.0, not -2.6: the 2F/3F corridor was widened to y -1.40..2.60
            // in Blender, which put the old waypoint inside the corridor's north
            // wall. -3.0 sits in the stair-room door band (Blender y 2.00..3.30)
            // and clear of the wall, which is what this leg always meant.
            new Vector3(-9.2f, 6.4f, -3.0f), new Vector3(-6.5f, 6.4f, -3.0f)
        };
        var route = new List<Vector3>(points);
        for (int i = points.Length - 2; i >= 0; i--) route.Add(points[i]);
        bool wasEnabled = player.enabled;
        player.enabled = false;
        var doorsInTheWay = new List<string>();
        try
        {
            Place(player, body, W(route[0]));
            for (int r = 1; r < route.Count; r++)
            {
                Vector3 target = W(route[r]);
                int budget = Ticks(600);
                int ticks = 0;
                while (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                           new Vector2(target.x, target.z)) > Hz(0.08f) && ticks++ < budget)
                {
                    Vector3 delta = target - player.transform.position;
                    delta.y = 0;
                    Vector3 local = player.transform.InverseTransformDirection(delta.normalized);
                    player.Move(new Vector2(local.x, local.z), 1f / 60f);
                    if (player.transform.position.y < Vt(-0.3f))
                        throw new Exception("Fell below the floor toward design " + route[r].ToString("F2"));
                }
                for (int i = 0; i < 15; i++) player.Move(Vector2.zero, 1f / 60f);
                if (ticks >= budget)
                {
                    string blocker = ForgivableBlockerBetween(player.transform.position, target);
                    if (blocker == null)
                        throw new Exception("Blocked toward design " + route[r].ToString("F2")
                                            + " by blockout structure; stopped at " + player.transform.position.ToString("F2"));
                    doorsInTheWay.Add(route[r].ToString("F1") + " <- " + blocker);
                    Place(player, body, target);
                }
                else if (Mathf.Abs(player.transform.position.y - target.y) > Vt(0.22f))
                    throw new Exception("Misaligned toward design " + route[r].ToString("F2")
                                        + "; stopped at " + player.transform.position.ToString("F2"));
                if (!body.isGrounded) throw new Exception("Not grounded at design " + route[r].ToString("F2"));
            }
            // The player rig is not scaled, so eye height stays in real metres.
            if (Mathf.Abs(camera.transform.localPosition.y - 1.65f) > 0.001f)
                throw new Exception("Eye height changed.");
            player.ApplyLook(new Vector2(100, 100000));
            float tilt = Mathf.DeltaAngle(0, camera.transform.localEulerAngles.x);
            if (Mathf.Abs(tilt + 80) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(0, player.transform.eulerAngles.y) - 9) > 0.01f)
                throw new Exception("Mouse look or vertical clamp failed.");
            string doors = doorsInTheWay.Count == 0
                ? "no doors shut"
                : doorsInTheWay.Count + " leg(s) crossed a shut door: " + string.Join("; ", doorsInTheWay.ToArray());
            return "PASS: " + route.Count + " waypoints, lobby/guard room/2F corridor/3F round trip; "
                 + "eye height 1.65m; mouse look clamped; " + doors + "." + ScaleTag();
        }
        finally
        {
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }

    // A shut door is gameplay state, not structure. An Unlocked one is something
    // the player can always open, so a structural walk should not be stopped by
    // it - open those first and put them back afterwards. Keycard and Sealed
    // doors stay shut, because whether you get through those IS the design.
    static SlidingDoor[] OpenUnlockedDoors()
    {
        var opened = new List<SlidingDoor>();
        foreach (var door in UnityEngine.Object.FindObjectsByType<SlidingDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (door.Locking != SlidingDoor.Access.Unlocked || door.IsOpen) continue;
            door.SetOpenImmediate(true);
            opened.Add(door);
        }
        return opened.ToArray();
    }

    static void RestoreDoors(SlidingDoor[] opened)
    {
        foreach (var door in opened) if (door != null) door.SetOpenImmediate(false);
    }

    // Outside the building the player must land on ground and stop at the site
    // wall. Before this existed, stepping out of any opening dropped forever.
    // World metres: none of the exterior lives under the scaled blockout root.
    public static string RunExteriorContainment()
    {
        var player = FindPlayer();
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = player.enabled;
        player.enabled = false;
        var walls = new[]
        {
            new object[]{ "south",     new Vector3(0f, 1.2f, -26f),   new Vector3(0, 0, -1) },
            new object[]{ "north",     new Vector3(0f, 1.2f, 23f),    new Vector3(0, 0, 1) },
            new object[]{ "west",      new Vector3(-30f, 1.2f, 0f),   new Vector3(-1, 0, 0) },
            new object[]{ "east",      new Vector3(27f, 1.2f, 0f),    new Vector3(1, 0, 0) },
            new object[]{ "SW corner", new Vector3(-30f, 1.2f, -26f), new Vector3(-1, 0, -1) },
            new object[]{ "NE corner", new Vector3(27f, 1.2f, 23f),   new Vector3(1, 0, 1) },
        };
        try
        {
            foreach (object[] probe in walls)
            {
                body.enabled = false;
                player.transform.SetPositionAndRotation((Vector3)probe[1], Quaternion.identity);
                body.enabled = true;
                Physics.SyncTransforms();
                for (int i = 0; i < 15; i++) player.Move(Vector2.zero, 1f / 60f);
                if (player.transform.position.y < -1f)
                    throw new Exception("No ground at the " + probe[0] + " probe; fell to "
                                        + player.transform.position.ToString("F2"));
                Vector3 local = player.transform.InverseTransformDirection(((Vector3)probe[2]).normalized);
                var input = new Vector2(local.x, local.z);
                for (int i = 0; i < 400; i++) player.Move(input, 1f / 60f);
                Vector3 e = player.transform.position;
                if (e.x < -34.6f || e.x > 31.6f || e.z < -30.6f || e.z > 27.6f || e.y < -1f)
                    throw new Exception("Walked past the site wall at " + probe[0] + " to " + e.ToString("F2"));
            }
            return "PASS: ground caught the player and the site wall held at all "
                 + walls.Length + " probes, sides and corners.";
        }
        finally
        {
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }

    // Entry through the guard-room window. Written in world metres on purpose:
    // the ramp, the ground and the window frame are Unity-side objects that do
    // not live under the scaled blockout root, so converting through localScale
    // would be wrong here.
    //
    // This path is narrow in three ways at once and each one broke it in turn:
    // the opening is 1.56 m against a 1.80 m standing capsule, the window has a
    // mullion dead centre at z -6.75, and the ramp has to land above the sill's
    // bottom rail without the loose rubble stopping the climb.
    public static string RunGuardWindowEntry()
    {
        var player = FindPlayer();
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = player.enabled;
        player.enabled = false;
        try
        {
            const float lane = -7.60f;          // clear of the mullion
            body.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(23f, 1.2f, lane), Quaternion.identity);
            body.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 20; i++) player.Move(Vector2.zero, 1f / 60f);
            if (player.transform.position.y < -1f)
                throw new Exception("No ground outside the guard-room window; the player fell to "
                                    + player.transform.position.ToString("F2"));

            Vector3 local = player.transform.InverseTransformDirection(Vector3.left);
            var input = new Vector2(local.x, local.z);
            for (int i = 0; i < 300; i++) player.Move(input, 1f / 60f);
            float rampTop = player.transform.position.y;
            if (rampTop < 1.45f)
                throw new Exception("Ramp did not reach the sill; stopped at " + player.transform.position.ToString("F2"));

            player.SetCrouch(true);
            if (!player.IsCrouching) throw new Exception("Crouch refused at the window.");
            for (int i = 0; i < 480; i++)
            {
                player.Move(input, 1f / 60f);
                if (player.transform.position.x < 14.4f) break;
            }
            if (player.transform.position.x > 14.8f)
                throw new Exception("Could not get through the window; stopped at "
                                    + player.transform.position.ToString("F2"));

            for (int i = 0; i < 90; i++) player.Move(Vector2.zero, 1f / 60f);
            player.SetCrouch(false);
            for (int i = 0; i < 30; i++) player.Move(Vector2.zero, 1f / 60f);
            if (player.IsCrouching)
                throw new Exception("Stuck crouched after entering at " + player.transform.position.ToString("F2"));
            Vector3 end = player.transform.position;
            if (end.x > 15f || end.x < 7.5f || end.z < -10.5f || end.z > -3f || end.y > 0.5f)
                throw new Exception("Ended outside the guard room at " + end.ToString("F2"));

            return "PASS: climbed the rubble ramp to y " + rampTop.ToString("F2")
                 + ", crouched through the window clear of the mullion, landed in the guard room at "
                 + end.ToString("F2") + " and stood back up.";
        }
        finally
        {
            player.SetCrouch(false);
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }

    const float Basement = -3.20f;           // design metres, B1 floor top

    // B1 is deliberately only the plan's bottom-right corner: a stair alcove,
    // 비밀 생체실험실 and 진화된 알, with 봉쇄문 sealing the west side. There is
    // no opening in the 1F slab, so this storey is reached by teleport here and
    // walked on foot from the stair foot.
    public static string RunBasement()
    {
        var player = FindPlayer();
        var body = player.GetComponent<CharacterController>();
        var route = new[]
        {
            new Vector3(-2.3f, Basement, -0.6f),    // stair foot, in the alcove
            new Vector3(-2.5f, Basement, 0.6f),     // through the alcove door
            new Vector3(-4.5f, Basement, 3.5f),     // 비밀 생체실험실 centre
            new Vector3(-8.0f, Basement, 3.5f),     // its east end
            new Vector3(-9.0f, Basement, 4.0f),     // the arch
            new Vector3(-10.5f, Basement, 3.5f),    // 진화된 알
        };
        var legs = new List<Vector3>(route);
        for (int i = route.Length - 2; i >= 0; i--) legs.Add(route[i]);

        bool wasEnabled = player.enabled;
        player.enabled = false;
        SlidingDoor[] opened = OpenUnlockedDoors();
        try
        {
            // Every waypoint must have B1 floor under it before anything is walked:
            // a walk that fails over a void is the test standing in mid-air, not a
            // broken building.
            foreach (Vector3 point in route)
            {
                RaycastHit probe;
                if (!Physics.Raycast(W(point) + Vector3.up * Vt(0.5f), Vector3.down, out probe, Vt(0.9f)))
                    throw new Exception("No B1 floor under design " + point.ToString("F2"));
                if (!probe.collider.name.StartsWith("B1_"))
                    throw new Exception("Design " + point.ToString("F2") + " stands on "
                                        + probe.collider.name + ", which is not B1 geometry");
            }

            Place(player, body, W(legs[0]));
            float floorY = W(0, Basement, 0).y;
            for (int r = 1; r < legs.Count; r++)
            {
                Vector3 target = W(legs[r]);
                int budget = Ticks(600);
                int ticks = 0;
                while (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                           new Vector2(target.x, target.z)) > Hz(0.08f) && ticks++ < budget)
                {
                    Vector3 delta = target - player.transform.position;
                    delta.y = 0;
                    Vector3 local = player.transform.InverseTransformDirection(delta.normalized);
                    player.Move(new Vector2(local.x, local.z), 1f / 60f);
                    if (player.transform.position.y < floorY - Vt(0.30f))
                        throw new Exception("Fell below B1 toward design " + legs[r].ToString("F2"));
                }
                for (int i = 0; i < 15; i++) player.Move(Vector2.zero, 1f / 60f);
                if (ticks >= budget)
                    throw new Exception("Blocked toward design " + legs[r].ToString("F2")
                                        + "; stopped at " + player.transform.position.ToString("F2"));
            }

            // 봉쇄문 is drawn padlocked and nothing is built past it, so it has to
            // stop the player rather than let them walk into the void.
            Place(player, body, W(-2.0f, Basement, 4.0f));
            Push(player, Vector3.right, Ticks(90));
            float lockdownX = W(-0.11f, 0, 0).x;
            if (player.transform.position.x > lockdownX)
                throw new Exception("Walked through 봉쇄문 to " + player.transform.position.ToString("F2"));

            // Climb the alcove stair one footfall at a time - each ray starts just
            // above the previous one, so it can only find treads a walker reaches.
            int treads = 0;
            float y = floorY;
            for (float z = -0.75f; z >= -4.70f; z -= 0.27f)
            {
                RaycastHit step;
                if (!Physics.Raycast(new Vector3(W(-2.3f, 0, 0).x, y + Vt(0.45f), W(0, 0, z).z),
                        Vector3.down, out step, Vt(0.75f)))
                    continue;
                if (step.point.y < y - 0.001f)
                    throw new Exception("Stair dips at design z " + z.ToString("F2"));
                y = step.point.y;
                treads++;
            }
            if (treads < 12)
                throw new Exception("Only " + treads + " stair footfalls found in the alcove");

            return "PASS: B1 walked - stair foot to 비밀 생체실험실 to 진화된 알 and back over "
                 + legs.Count + " legs; 봉쇄문 solid; " + treads + " stair footfalls climbing "
                 + (y - floorY).ToString("F2") + "m; " + opened.Length + " unlocked door(s) opened en route." + ScaleTag();
        }
        finally
        {
            RestoreDoors(opened);
            player.ResetToStart();
            player.enabled = wasEnabled;
        }
    }
}
