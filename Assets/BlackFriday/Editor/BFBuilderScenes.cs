using System.Collections.Generic;
using System.IO;
using System.Linq;
using kcp2k;
using Mirror;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace BlackFriday.EditorTools
{
    public static partial class BFBuilder
    {
        public const string MenuScene = R + "/Scenes/MainMenu.unity";
        public const string Level01Scene = R + "/Scenes/Level01_MegaMart.unity";

        static GameObject P(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{R}/Prefabs/{name}.prefab");

        static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = new Vector3(0, yaw, 0);
            return go;
        }

        // ---------------------------------------------------------------- UI assetleri
        static PanelSettings GetPanelSettings()
        {
            string path = R + "/UI/BF_PanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            if (ps.themeStyleSheet == null)
            {
                var tssGuid = AssetDatabase.FindAssets("t:ThemeStyleSheet").FirstOrDefault();
                ThemeStyleSheet tss = tssGuid != null ? AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(tssGuid)) : null;
                if (tss == null)
                {
                    string tssPath = R + "/UI/BFRuntimeTheme.tss";
                    File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\n");
                    AssetDatabase.ImportAsset(tssPath);
                    tss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);
                }
                ps.themeStyleSheet = tss;
            }
            EditorUtility.SetDirty(ps);
            AssetDatabase.SaveAssets();
            return ps;
        }

        static void AddUI(string name, string uxml, out GameObject go)
        {
            go = new GameObject(name);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = GetPanelSettings();
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{R}/UI/{uxml}");
        }

        static void AddEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static Light AddSun(float intensity, Vector3 euler)
        {
            var go = new GameObject("Sun");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = new Color(1f, 0.95f, 0.86f);
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.6f;
            go.transform.eulerAngles = euler;
            return l;
        }

        static void ToonAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.84f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.33f, 0.3f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
        }

        // =====================================================================
        // 9) ANA MENÜ
        // =====================================================================
        [MenuItem("BlackFriday/9 - Ana menü sahnesi")]
        public static void BuildMainMenu()
        {
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MallAmbient();
            AddSun(1.1f, new Vector3(40, -25, 0));
            AddPostVolume();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            SetupCameraQuality(cam);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
            cam.fieldOfView = 38f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 1.55f, -5.2f);
            camGo.transform.LookAt(new Vector3(0f, 1.15f, 0f));

            // vitrin sahnesi
            var set = new GameObject("MenuSet").transform;
            Box(set, "Floor", new Vector3(30f, 0.2f, 20f), new Vector3(0, -0.1f, 5f), M("M_Floor"));
            Box(set, "BackWall", new Vector3(30f, 8f, 0.4f), new Vector3(0, 4f, 8f), M("M_Black"));
            Box(set, "Stripe", new Vector3(30f, 0.25f, 0.42f), new Vector3(0, 3.6f, 7.98f), M("M_NeonYellow"));
            Place(P("Props/WallShelf"), set, new Vector3(-6.2f, 0, 7.4f), 0);
            Place(P("Props/WallShelf"), set, new Vector3(0f, 0, 7.4f), 0);
            Place(P("Props/WallShelf"), set, new Vector3(6.2f, 0, 7.4f), 0);
            Place(P("Props/Gondola"), set, new Vector3(-4.5f, 0, 4.5f), 0);
            Place(P("Props/Gondola"), set, new Vector3(5.5f, 0, 4.5f), 0);
            Place(P("Props/DealTable"), set, new Vector3(-2.6f, 0, 2.2f), 15);
            Place(P("Props/Cart"), set, new Vector3(3.6f, 0, 1.6f), -35);
            Text3D(set, "%90", new Vector3(3.2f, 5.2f, 7.7f), new Vector3(0, 0, 8), 0.2f, new Color(1f, 0.25f, 0.3f));

            // ürün vitrin kopyaları (ağ bileşenleri olmadan)
            var db = AssetDatabase.LoadAssetAtPath<ProductDatabase>(R + "/Resources/BF_ProductDatabase.asset");
            if (db != null)
            {
                var positions = new[] {
                    new Vector3(-2.9f, 0.765f, 2.0f), new Vector3(-2.3f, 0.765f, 2.5f), new Vector3(-2.2f, 0.765f, 1.8f),
                    new Vector3(2.4f, 0f, 0.6f), new Vector3(2.9f, 0f, 0.1f), new Vector3(0.6f, 0f, -0.4f),
                    new Vector3(-0.9f, 0f, 0.2f), new Vector3(3.5f, 0.48f, 1.6f) };
                for (int i = 0; i < positions.Length; i++)
                {
                    var def = db.products[i % db.products.Count];
                    var vis = def.prefab.transform.Find("Visual");
                    var copy = Object.Instantiate(vis.gameObject, set);
                    copy.name = "Display_" + def.displayName;
                    copy.transform.localPosition = positions[i];
                    copy.transform.localEulerAngles = new Vector3(0, 160 + i * 37, 0);
                }
            }

            // raflara vitrin ürünleri
            if (db != null)
            {
                var rnd = new System.Random(42);
                foreach (var sp in set.GetComponentsInChildren<ShelfSpawnPoint>())
                {
                    if (rnd.NextDouble() < 0.35) continue;
                    var cands = db.products.Where(d => sp.Allows(d.id)).ToList();
                    if (cands.Count == 0) continue;
                    var def = cands[rnd.Next(cands.Count)];
                    var copy = Object.Instantiate(def.prefab.transform.Find("Visual").gameObject, sp.transform.position, sp.transform.rotation * Quaternion.Euler(0, rnd.Next(-15, 15), 0), set);
                    copy.name = "Shelf_" + def.displayName;
                }
            }

            // karakter önizleme
            Cyl(set, "Pedestal", 0.75f, 0.16f, new Vector3(1.25f, 0.08f, 0.2f), M("M_Yellow"), default, 32);
            var previewGo = new GameObject("CharacterPreview");
            previewGo.transform.position = new Vector3(1.25f, 0.16f, 0.2f);
            previewGo.transform.eulerAngles = new Vector3(0, 200, 0);
            var cv = previewGo.AddComponent<CharacterVisual>();
            cv.modelRoot = previewGo.transform;
            var preview = previewGo.AddComponent<CharacterPreview>();

            var spot = new GameObject("KeyLight").AddComponent<Light>();
            spot.type = LightType.Spot; spot.range = 12; spot.spotAngle = 45; spot.intensity = 30f;
            spot.color = new Color(1f, 0.9f, 0.7f);
            spot.transform.position = new Vector3(1.6f, 4.2f, -1.8f);
            spot.transform.LookAt(new Vector3(1.25f, 1f, 0.2f));

            // ağ yöneticisi
            var nmGo = new GameObject("NetworkManager");
            var kcp = nmGo.AddComponent<KcpTransport>();
            var nm = nmGo.AddComponent<BFNetworkManager>();
            nm.transport = kcp;
            nm.playerPrefab = P("BF_Player");
            nm.autoCreatePlayer = true;
            nm.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;
            nm.offlineScene = MenuScene;
            nm.onlineScene = Level01Scene;
            nm.maxConnections = BFNetworkManager.MaxPlayers;
            nm.spawnPrefabs.Clear();
            nm.spawnPrefabs.Add(P("BF_NPC"));
            if (db != null) foreach (var d in db.products) nm.spawnPrefabs.Add(d.prefab);

            AddUI("MenuUI", "MainMenu.uxml", out var ui);
            var mmc = ui.AddComponent<MainMenuController>();
            mmc.preview = preview;
            AddEventSystem();

            MergeStatic(set, "Menu_Set");
            EditorSceneManager.SaveScene(scene, MenuScene);
            Debug.Log("[BF] Ana menü kuruldu: " + MenuScene);
        }

        [MenuItem("BlackFriday/10 - Build ayarları")]
        public static void SetupBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(Level01Scene, true),
            };
            PlayerSettings.productName = "Black Friday";
            PlayerSettings.runInBackground = true;
            // performans: DX11 (çok sayıda küçük draw'da daha hızlı), çok iş parçacıklı render, GPU skinning
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            PlayerSettings.graphicsJobs = true;
            PlayerSettings.gpuSkinning = true;
            QualitySettings.vSyncCount = 0;
            Debug.Log("[BF] Build sahneleri ayarlandı.");
        }
    }
}
