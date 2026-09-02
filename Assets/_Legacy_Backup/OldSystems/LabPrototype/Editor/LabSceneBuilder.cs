using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LabHorrorEditor
{
    public static class LabSceneBuilder
    {
        static string BuildKey => "LabHorrorEntranceBuildVersion_" + Application.dataPath;
        const string RepairSessionKey = "LabHorrorEntranceMissingScriptsRepaired";
        const string RemakeRequestPath = "Assets/LabPrototype/RemakeEntrance.request";
        const string RestoreRequestPath = "Assets/LabPrototype/RestoreEntrance.request";
        const string PreviousScenePath = "Assets/Scenes/LabEntrance.before-remake-20260805-130354.unity";

        [InitializeOnLoadMethod]
        static void ProcessRestoreRequest()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(RestoreRequestPath)) return;
                System.IO.File.Delete(RestoreRequestPath);
                AssetDatabase.Refresh();
                var restored = EditorSceneManager.OpenScene(PreviousScenePath, OpenSceneMode.Single);
                EditorSceneManager.SaveScene(restored, "Assets/Scenes/LabEntrance.unity", true);
                EditorSceneManager.OpenScene("Assets/Scenes/LabEntrance.unity", OpenSceneMode.Single);
                Debug.Log("Lab Horror: restored the manually edited LabEntrance scene from backup.");
            };
        }

        [InitializeOnLoadMethod]
        static void ProcessRemakeRequest()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(RemakeRequestPath)) return;
                System.IO.File.Delete(RemakeRequestPath);
                Build();
            };
        }

        [InitializeOnLoadMethod]
        static void RepairEntranceSceneOnce()
        {
            if (SessionState.GetBool(RepairSessionKey, false)) return;
            SessionState.SetBool(RepairSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var scene = SceneManager.GetActiveScene();
                if (scene.path != "Assets/Scenes/LabEntrance.unity") return;

                int removed = RemoveMissingScriptsFromScene(scene);
                if (removed <= 0) return;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Lab Horror: automatically removed and saved {removed} missing script component(s). ");
            };
        }

        [MenuItem("Lab Horror/Build Entrance Prototype")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Lab Horror: Exit Play Mode before rebuilding the entrance scene.");
                return;
            }
            ConvertImportedMaterialsToURP();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var controller = new GameObject("Lab Entrance Game").AddComponent<LabEntranceGame>();
            controller.buildWorldOnAwake = false;
            controller.BuildSceneNow();
            ApplyGeneratedMapImage();
            ApplyBloodyWarningImage();
            // Keep the blueprint shell authoritative. Asset-pack dressing is disabled
            // until each prefab has been checked against the room dimensions by hand.
            ApplyCollapseDustMaterial();
            ApplyRainMaterial();
            OptimizeScene(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/LabEntrance.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/LabEntrance.unity", true) };
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(BuildKey, 20);
            Debug.Log("LabEntrance prototype scene created and opened.");
        }

        [MenuItem("Lab Horror/Repair Current Scene Missing Scripts")]
        public static void RepairCurrentSceneMissingScripts()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Lab Horror: Exit Play Mode before repairing the scene.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            int removed = RemoveMissingScriptsFromScene(scene);

            if (removed > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Lab Horror: removed {removed} missing script component(s) from the current scene. Save the scene to keep the repair.");
        }

        public static void RepairEntranceSceneBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/LabEntrance.unity", OpenSceneMode.Single);
            int removed = RemoveMissingScriptsFromScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Lab Horror batch repair complete. Removed {removed} missing script component(s).");
        }

        static int RemoveMissingScriptsFromScene(Scene scene)
        {
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            return removed;
        }

        static void AddAssetPackModels()
        {
            HideVisual("Desk"); HideVisual("Desk L"); HideVisual("Desk R"); HideVisual("CRT");
            var desk = Place("Assets/ScifiOfficeLite/Prefabs/Tables/Metal/Table Metal Variant.prefab", "Security Desk", new Vector3(-10.5f, .55f, 25.7f), new Vector3(4.1f, 1.1f, 1.2f), Quaternion.identity); RemoveColliders(desk);
            var chair = Place("Assets/ScifiOfficeLite/Prefabs/Chairs and Stools/Office Chair.prefab", "Security Chair", new Vector3(-10.5f, 0, 23.7f), new Vector3(.9f, 1.15f, .9f), Quaternion.Euler(0, 180, 0)); RemoveColliders(chair);
            var pc = Place("Assets/ScifiOfficeLite/Prefabs/Tech Accessories/PC 2.prefab", "Security PC", new Vector3(-11.6f, 1.67f, 25.55f), new Vector3(.65f, .75f, .65f), Quaternion.identity); RemoveColliders(pc);
            var monitor = Place("Assets/ScifiOfficeLite/Prefabs/TVs/Tv 32 Inch.prefab", "Security Monitor", new Vector3(-9.8f, 1.67f, 25.55f), new Vector3(1.1f, .75f, .35f), Quaternion.identity); RemoveColliders(monitor);
            var shelf = Place("Assets/ScifiOfficeLite/Prefabs/Shelf with Crates.prefab", "Security Shelf", new Vector3(-13.4f, 0, 23.8f), new Vector3(1.2f, 2.3f, .65f), Quaternion.Euler(0, 90, 0)); RemoveColliders(shelf);
            Place("Assets/ScifiOfficeLite/Prefabs/Lighting/Ceiling Lights/2D/Ceiling Light Bright.prefab", "Security Ceiling Light", new Vector3(-10.5f, 4.9f, 24), new Vector3(1.5f, .3f, .5f), Quaternion.identity);
            var securityDoor = Place("Assets/ScifiOfficeLite/Prefabs/Wall/Door Wall Opaque.prefab", "Security Door Visual", new Vector3(-7, 0, 22.1f), new Vector3(1.9f, 4.2f, .35f), Quaternion.Euler(0,90,0));
            RemoveColliders(securityDoor);
            const string pillarPath = "Assets/ScifiOfficeLite/Prefabs/Wall/Wall Pillar.prefab";
            foreach (float x in new[] { -2.05f, 2.05f, -14.7f, 14.7f })
            {
                var pillar = Place(pillarPath, "SCI Structural Pillar", new Vector3(x, 0, 10.42f), new Vector3(.42f, 4.5f, .42f), Quaternion.identity);
                RemoveColliders(pillar);
            }

        }

        static void FinishInterior()
        {
            const string framePath = "Assets/Abandoned_Asylum/Prefabs/DiningRoom_EnterFrame.prefab";
            var trimMat = GetInteriorTrimMaterial();
            foreach (var doorway in new[]
            {
                new Vector3(-7,0,15), new Vector3(-7,0,25.3f),
                new Vector3(7,0,15), new Vector3(7,0,25.3f)
            })
            {
                var frame = Place(framePath, "Interior Door Frame", doorway, new Vector3(.38f,3.65f,1.55f), Quaternion.Euler(0,90,0));
                RemoveColliders(frame);
                if (frame != null && trimMat != null)
                    foreach (var renderer in frame.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=trimMat;
            }

            var officeDesk = Place("Assets/ScifiOfficeLite/Prefabs/Tables/Dark Oak/Drawer Table Long Dark Oak.prefab", "Office Finished Desk", new Vector3(-11.2f,0,15.5f), new Vector3(4.2f,1.05f,1.15f), Quaternion.identity); RemoveColliders(officeDesk);
            var officeChair = Place("Assets/Abandoned_Asylum/Prefabs/ChairOffice.prefab", "Office Finished Chair", new Vector3(-11.1f,0,17.15f), new Vector3(.8f,1.15f,.8f), Quaternion.Euler(0,180,0)); RemoveColliders(officeChair);
            var officeShelf = Place("Assets/Abandoned_Asylum/Prefabs/BigShelf.prefab", "Office Archive Shelf", new Vector3(-14.2f,0,17.6f), new Vector3(1.05f,2.8f,3.1f), Quaternion.Euler(0,90,0)); RemoveColliders(officeShelf);
            HideVisual("Office Desk"); HideVisual("Office Cabinet"); HideVisual("Office Chair");

            foreach (float z in new[] { 18.3f, 23.4f })
            {
                var bench = Place("Assets/Abandoned_Asylum/Prefabs/Bench.prefab", "Lobby Waiting Bench", new Vector3(4.7f,0,z), new Vector3(2.7f,1.0f,.75f), Quaternion.Euler(0,90,0));
                RemoveColliders(bench);
                if (bench != null && trimMat != null)
                    foreach (var renderer in bench.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=trimMat;
            }
            HideVisual("Lobby Bench North"); HideVisual("Lobby Bench South");

            var elevator = Place("Assets/Abandoned_Asylum/Prefabs/Elevator.prefab", "Finished Elevator", new Vector3(13.2f,0,14.95f), new Vector3(3.15f,4.5f,1.0f), Quaternion.identity); RemoveColliders(elevator);
            HideVisual("Elevator Door L"); HideVisual("Elevator Door R");

            foreach (var p in new[] { new Vector3(0,5.02f,16), new Vector3(0,5.02f,24), new Vector3(-10.5f,5.02f,15), new Vector3(9.2f,5.02f,24) })
                Place("Assets/ScifiOfficeLite/Prefabs/Lighting/Ceiling Lights/2D/Ceiling Light.prefab", "Interior Ceiling Fixture", p, new Vector3(1.7f,.18f,.48f), Quaternion.identity);

            var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ScifiOfficeLite/Meshes/Materials/Walls/Wall Set 1 T1.mat");
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ScifiOfficeLite/Meshes/Materials/Ground Mats/Concrete/Concrete Ground 20m.mat");
            var floorObject=GameObject.Find("1F Floor");
            if (floorObject!=null && floorMat!=null) floorObject.GetComponent<Renderer>().sharedMaterial=floorMat;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool interiorWall = t.name.StartsWith("West Lobby Wall") || t.name.StartsWith("East Lobby Wall") ||
                    t.name.Contains("Room Divider") || t.name == "Vertical Core Divider" || t.name == "Stair Elevator Divider";
                if (!interiorWall) continue;
                var renderer = t.GetComponent<Renderer>();
                if (renderer == null) continue;
                if (wallMat != null) renderer.sharedMaterial = wallMat;
                var b = renderer.bounds;
                AddTrim(t.name + " Base Trim", new Vector3(b.center.x,.12f,b.center.z), new Vector3(b.size.x+.06f,.24f,b.size.z+.06f), trimMat);
                AddTrim(t.name + " Ceiling Trim", new Vector3(b.center.x,5.08f,b.center.z), new Vector3(b.size.x+.06f,.18f,b.size.z+.06f), trimMat);
            }
        }

        static Material GetInteriorTrimMaterial()
        {
            const string path="Assets/LabPrototype/Generated/InteriorTrim.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if (shader==null) return material;
            if (material==null)
            {
                material=new Material(shader); AssetDatabase.CreateAsset(material,path);
            }
            else material.shader=shader;
            material.SetColor("_BaseColor",new Color(.075f,.085f,.09f,1));
            material.SetFloat("_Metallic",.15f); material.SetFloat("_Smoothness",.18f);
            EditorUtility.SetDirty(material); return material;
        }

        static void AddTrim(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.position=position; go.transform.localScale=scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<Renderer>().sharedMaterial=material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        static void ApplyCollapseDustMaterial()
        {
            const string materialPath = "Assets/LabPrototype/Generated/CollapseDust.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.32f,.34f,.36f,.55f));
            EditorUtility.SetDirty(material);
            var dust = GameObject.Find("Collapse Dust");
            if (dust != null) dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            AssetDatabase.SaveAssets();
        }

        static void ApplyRainMaterial()
        {
            var rain=GameObject.Find("Rain");
            if (rain==null) return;
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/LabPrototype/Generated/RainStreak.mat");
            if (material==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if(shader==null) return;
                material=new Material(shader); material.SetColor("_BaseColor",new Color(.35f,.55f,.7f,.45f));
                AssetDatabase.CreateAsset(material,"Assets/LabPrototype/Generated/RainStreak.mat");
            }
            rain.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
        }

        static void OptimizeScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.type != LightType.Directional) light.shadows = LightShadows.None;
            }
        }

        static void AddSciFiArchitecture()
        {
            const string wallPath = "Assets/ScifiOfficeLite/Prefabs/Wall/Wall 2 Variant.prefab";
            const string pillarPath = "Assets/ScifiOfficeLite/Prefabs/Wall/Wall Pillar.prefab";
            for (int i = 0; i < 4; i++)
            {
                float x = -9f + i * 2.05f;
                var leftPanel = Place(wallPath, "SCI Facade Panel", new Vector3(x, 0, 13.68f), new Vector3(1.95f, 4.2f, .22f), Quaternion.identity);
                RemoveColliders(leftPanel);
            }
            foreach (float x in new[] { -2.15f, 2.15f, 9.15f })
            {
                var pillar = Place(pillarPath, "SCI Structural Pillar", new Vector3(x, 0, 13.48f), new Vector3(.35f, 4.4f, .35f), Quaternion.identity);
                RemoveColliders(pillar);
            }
            for (int i = 0; i < 2; i++)
            {
                var corridorPanel = Place(wallPath, "SCI Entry Interior", new Vector3(i == 0 ? 4.86f : 7.14f, 0, 16.5f), new Vector3(6.6f, 3.9f, .16f), Quaternion.Euler(0, 90, 0));
                RemoveColliders(corridorPanel);
            }
            foreach (float x in new[] { 3.85f, 8.15f })
            {
                var guardFront = Place(wallPath, "SCI Guardhouse Facade", new Vector3(x, 0, 6.84f), new Vector3(1.6f, 4.75f, .16f), Quaternion.identity);
                RemoveColliders(guardFront);
            }
            foreach (float x in new[] { 3.1f, 8.9f })
            {
                var guardSide = Place(wallPath, "SCI Guardhouse Side", new Vector3(x, 0, 10f), new Vector3(5.7f, 4.75f, .16f), Quaternion.Euler(0, 90, 0));
                RemoveColliders(guardSide);
            }
        }

        static void ApplyGeneratedMapImage()
        {
            const string folder = "Assets/LabPrototype/Generated";
            const string texturePath = folder + "/RealisticLabFloorMap.png";
            const string materialPath = folder + "/LabFloorMap.mat";
            System.IO.Directory.CreateDirectory(folder);
            if (!System.IO.File.Exists(texturePath))
            {
            var tex = new Texture2D(512, 320, TextureFormat.RGBA32, false);
            var background = new Color(.015f, .035f, .05f, 1);
            var pixels = new Color[512 * 320];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;
            tex.SetPixels(pixels);
            var cyan = new Color(.05f, .8f, .95f, 1);
            DrawRect(tex, 18, 18, 476, 284, cyan, 4);
            DrawRect(tex, 48, 184, 112, 78, cyan, 3);
            DrawRect(tex, 190, 184, 126, 78, cyan, 3);
            DrawRect(tex, 350, 184, 112, 78, cyan, 3);
            DrawRect(tex, 48, 58, 132, 78, cyan, 3);
            DrawRect(tex, 224, 58, 112, 78, cyan, 3);
            DrawRect(tex, 374, 58, 88, 78, cyan, 3);
            FillRect(tex, 110, 142, 292, 16, cyan);
            FillRect(tex, 245, 134, 16, 58, cyan);
            FillRect(tex, 168, 244, 24, 8, cyan);
            FillRect(tex, 330, 244, 22, 8, cyan);
            FillRect(tex, 72, 82, 18, 18, new Color(.95f, .08f, .04f, 1));
            tex.Apply();
            System.IO.File.WriteAllBytes(texturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null) { importer.textureCompression = TextureImporterCompression.Uncompressed; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport(); }
            var mapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetTexture("_BaseMap", mapTexture); material.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(material);
            var board = GameObject.Find("1F Laboratory Map");
            if (board != null) board.GetComponent<Renderer>().sharedMaterial = material;
            AssetDatabase.SaveAssets();
        }

        static void ApplyBloodyWarningImage()
        {
            const string texturePath = "Assets/LabPrototype/Generated/BloodyShutdownNotice.png";
            const string materialPath = "Assets/LabPrototype/Generated/BloodyShutdownNotice.mat";
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(material);
            var notice = GameObject.Find("Blood Warning");
            if (notice != null) notice.GetComponent<Renderer>().sharedMaterial = material;
            var oldMark = GameObject.Find("Blood Mark");
            if (oldMark != null) oldMark.GetComponent<Renderer>().enabled = false;
            AssetDatabase.SaveAssets();
        }

        static void DrawRect(Texture2D tex, int x, int y, int w, int h, Color color, int thickness)
        {
            FillRect(tex, x, y, w, thickness, color); FillRect(tex, x, y + h - thickness, w, thickness, color);
            FillRect(tex, x, y, thickness, h, color); FillRect(tex, x + w - thickness, y, thickness, h, color);
        }

        static void FillRect(Texture2D tex, int x, int y, int w, int h, Color color)
        {
            for (int py = Mathf.Max(0, y); py < Mathf.Min(tex.height, y + h); py++)
                for (int px = Mathf.Max(0, x); px < Mathf.Min(tex.width, x + w); px++) tex.SetPixel(px, py, color);
        }

        static void RemoveColliders(GameObject root)
        {
            if (root == null) return;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        }

        static void ConvertImportedMaterialsToURP()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("URP Lit shader not found."); return; }
            string[] roots = { "Assets/ScifiOfficeLite", "Assets/Urban American Assets" };
            foreach (var guid in AssetDatabase.FindAssets("t:Material", roots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                Texture texture = null;
                Color color = Color.white;
                if (mat.HasProperty("_BaseMap")) texture = mat.GetTexture("_BaseMap");
                else if (mat.HasProperty("_MainTex")) texture = mat.GetTexture("_MainTex");
                if (mat.HasProperty("_BaseColor")) color = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color")) color = mat.GetColor("_Color");
                mat.shader = shader;
                if (texture != null) mat.SetTexture("_BaseMap", texture);
                mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .22f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", .05f);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        static void HideVisual(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        static GameObject Place(string path, string objectName, Vector3 position, Vector3 desiredSize, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("Missing asset: " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = objectName; go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
            RemoveMissingScripts(go);
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                var size = bounds.size;
                go.transform.localScale = new Vector3(desiredSize.x / Mathf.Max(size.x, .001f), desiredSize.y / Mathf.Max(size.y, .001f), desiredSize.z / Mathf.Max(size.z, .001f));
                go.transform.rotation = rotation;
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                go.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            return go;
        }

        static void RemoveMissingScripts(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }
    }
}
