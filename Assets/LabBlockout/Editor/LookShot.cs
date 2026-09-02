using System.IO;
using UnityEditor;
using UnityEngine;

// Renders a still from an arbitrary viewpoint without entering play mode.
// The look pass is judged by eye, so it needs the same shot taken again after
// every change; play-mode capture is no use here because frames barely advance
// while the editor is unfocused.
public static class LookShot
{
    public const string DefaultDir =
        @"C:\Users\MBC-50~1\AppData\Local\Temp\claude\C--Users-MBC-501-14-Desktop-20260720-unitystudy-Assets\c35ca5c6-315d-4a77-88dd-801a82c0997d\scratchpad";

    public static string Capture(Vector3 from, Vector3 lookAt, string name,
        float fieldOfView = 60f, int width = 1280, int height = 720, string directory = null)
    {
        directory = directory ?? DefaultDir;
        Directory.CreateDirectory(directory);

        var holder = new GameObject("__LookShotCam");
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        RenderTexture previous = RenderTexture.active;
        try
        {
            var camera = holder.AddComponent<Camera>();
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            // Without this the shot skips the volume stack and shows a scene the
            // player never sees - grading, vignette and grain all missing.
            var urp = holder.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            holder.transform.position = from;
            holder.transform.LookAt(lookAt);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            string path = Path.Combine(directory, name + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return path;
        }
        finally
        {
            RenderTexture.active = previous;
            // Clear the camera's reference before releasing the texture, or Unity
            // logs "Releasing render texture that is set as Camera.targetTexture".
            var cam = holder.GetComponent<Camera>();
            if (cam != null) cam.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(holder);
        }
    }

    // A viewpoint is useless if it is inside a wall, which is easy to do by hand
    // and reads as a solid colour filling the frame. Reports what the camera is
    // actually touching so a bad shot is diagnosed instead of re-judged.
    public static string Describe(Vector3 from, Vector3 lookAt)
    {
        Collider[] overlap = Physics.OverlapSphere(from, 0.35f);
        string inside = overlap.Length == 0 ? "clear" : overlap.Length + " collider(s): " + overlap[0].name;
        RaycastHit hit;
        string ahead = Physics.Raycast(from, (lookAt - from).normalized, out hit, 60f)
            ? hit.collider.name + " at " + hit.distance.ToString("F2") + "m"
            : "nothing within 60m";
        RaycastHit down;
        string floor = Physics.Raycast(from, Vector3.down, out down, 12f)
            ? down.collider.name + " " + (from.y - down.point.y).ToString("F2") + "m below"
            : "NO FLOOR BELOW";
        return "at " + from.ToString("F1") + " -> " + inside + " | ahead: " + ahead + " | floor: " + floor;
    }
}
