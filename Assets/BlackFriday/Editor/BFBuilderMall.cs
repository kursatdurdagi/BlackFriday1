using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BlackFriday.EditorTools
{
    /// <summary>
    /// BÖLÜM 1 — MEGA AVM: iki katlı alışveriş merkezi, cam tavanlı atrium, 6 temalı mağaza.
    /// Tüm yapı ProBuilder; ışıklar gerçek zamanlı (Forward+), yansıma probları bake edilir.
    /// </summary>
    public static partial class BFBuilder
    {
        // ---------------------------------------------------------------- mağaza tanımları
        class StoreDef
        {
            public string name, subtitle;
            public float cx; public int side;              // side: +1 kuzey, -1 güney
            public string floorMat, wallMat, neonMat;
            public Color neonColor, lightColor;
            public int[] products;
            public System.Action<Transform, StoreDef> decor;
        }

        static List<StoreDef> Stores() => new List<StoreDef>
        {
            new StoreDef { name = "TEKNOMAX", subtitle = "ELEKTRONİK", cx = -26, side = 1, floorMat = "M_DarkTile", wallMat = "M_Wall_Navy", neonMat = "M_Neon_Cyan", neonColor = new Color(0.3f, 0.9f, 1f), lightColor = new Color(0.82f, 0.9f, 1f), products = new[] { 0, 1, 4, 7 }, decor = DecorTech },
            new StoreDef { name = "OYUN CENNETİ", subtitle = "OYUNCAK", cx = 0, side = 1, floorMat = "M_ToyFloor", wallMat = "M_Wall_Yellow", neonMat = "M_Neon_Pink", neonColor = new Color(1f, 0.4f, 0.75f), lightColor = new Color(1f, 0.93f, 0.82f), products = new[] { 5, 1 }, decor = DecorToy },
            new StoreDef { name = "SPORZONE", subtitle = "SPOR & AYAKKABI", cx = 26, side = 1, floorMat = "M_SportFloor", wallMat = "M_Wall_Green", neonMat = "M_Neon_Orange", neonColor = new Color(1f, 0.6f, 0.15f), lightColor = new Color(1f, 0.97f, 0.92f), products = new[] { 3 }, decor = DecorSport },
            new StoreDef { name = "EVİM MUTFAK", subtitle = "EV & MUTFAK", cx = -26, side = -1, floorMat = "M_WoodFloor", wallMat = "M_Wall_Terracotta", neonMat = "M_Neon_Red", neonColor = new Color(1f, 0.3f, 0.25f), lightColor = new Color(1f, 0.84f, 0.62f), products = new[] { 2, 6 }, decor = DecorKitchen },
            new StoreDef { name = "MODAPARK", subtitle = "MODA & ÇANTA", cx = 0, side = -1, floorMat = "M_Carpet", wallMat = "M_Wall_Blush", neonMat = "M_Neon_Magenta", neonColor = new Color(1f, 0.35f, 0.85f), lightColor = new Color(1f, 0.92f, 0.95f), products = new[] { 8 }, decor = DecorFashion },
            new StoreDef { name = "GÜZEL KOKU", subtitle = "KOZMETİK & PARFÜM", cx = 26, side = -1, floorMat = "M_Marble", wallMat = "M_Wall_Plum", neonMat = "M_Neon_Gold", neonColor = new Color(1f, 0.8f, 0.35f), lightColor = new Color(1f, 0.88f, 0.75f), products = new[] { 9 }, decor = DecorBeauty },
        };

        const float StoreW = 22f, StoreD = 18f, StoreH = 5f, UpperY = 5.3f, RoofY = 10.5f;

        // =====================================================================
        // Materyaller
        // =====================================================================
        static void BuildMallMaterials()
        {
            var marble = Tex("T_Marble", 512, (u, v) =>
            {
                float gu = Mathf.Abs(Frac(u) - 0.5f), gv = Mathf.Abs(Frac(v) - 0.5f);
                bool grout = gu > 0.494f || gv > 0.494f;
                float vein = Mathf.PerlinNoise(u * 6 + Mathf.PerlinNoise(u * 3, v * 3) * 3, v * 6);
                float line = Mathf.Clamp01(1f - Mathf.Abs(vein - 0.5f) * 40f) * 0.06f;
                float n = Mathf.PerlinNoise(u * 25, v * 25) * 0.03f;
                Color c = new Color(0.95f, 0.93f, 0.9f) - new Color(line + n, line + n, line * 0.8f + n, 0);
                return grout ? new Color(0.78f, 0.76f, 0.72f) : c;
            });
            var wood = Tex("T_WoodFloor", 256, (u, v) =>
            {
                int plank = Mathf.FloorToInt(u * 5);
                float off = (plank * 0.37f) % 1f;
                bool seam = Frac(u * 5) < 0.03f || Frac(v + off) < 0.01f;
                float g = Mathf.PerlinNoise(u * 5 + plank, v * 40) * 0.12f + plank % 3 * 0.03f;
                return seam ? new Color(0.35f, 0.22f, 0.12f) : new Color(0.72f - g, 0.5f - g, 0.3f - g);
            });
            var carpet = Tex("T_Carpet", 128, (u, v) =>
            {
                float n = Mathf.PerlinNoise(u * 60, v * 60) * 0.06f + Random.value * 0.02f;
                return new Color(0.92f - n, 0.8f - n, 0.82f - n);
            });
            var dark = Tex("T_DarkTile", 256, (u, v) =>
            {
                float gu = Mathf.Abs(Frac(u * 2) - 0.5f), gv = Mathf.Abs(Frac(v * 2) - 0.5f);
                bool grout = gu > 0.49f || gv > 0.49f;
                float n = Mathf.PerlinNoise(u * 20, v * 20) * 0.03f;
                return grout ? new Color(0.32f, 0.34f, 0.4f) : new Color(0.13f + n, 0.14f + n, 0.18f + n);
            });
            var sport = Tex("T_SportFloor", 256, (u, v) =>
            {
                bool line = Mathf.Abs(Frac(u * 0.5f + 0.25f) - 0.5f) < 0.006f || Mathf.Abs(Frac(v * 0.5f) - 0.5f) < 0.006f;
                float n = Mathf.PerlinNoise(u * 40, v * 40) * 0.04f;
                return line ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.2f + n, 0.55f + n, 0.35f + n);
            });
            var toy = Tex("T_ToyFloor", 128, (u, v) =>
            {
                int i = (Mathf.FloorToInt(u * 2) + Mathf.FloorToInt(v * 2) * 2) % 4;
                Color[] cs = { new Color(1f, 0.85f, 0.3f), new Color(0.45f, 0.75f, 1f), new Color(1f, 0.55f, 0.6f), new Color(0.55f, 0.9f, 0.55f) };
                bool grout = Mathf.Abs(Frac(u * 2) - 0.5f) > 0.485f || Mathf.Abs(Frac(v * 2) - 0.5f) > 0.485f;
                return grout ? Color.white : cs[i];
            });
            var shutter = Tex("T_Shutter", 128, (u, v) =>
            {
                float s = Frac(v * 16);
                float shade = s < 0.15f ? 0.45f : 0.62f + s * 0.12f;
                return new Color(shade, shade, shade + 0.02f);
            });
            var screen = Tex("T_ScreenWall", 256, (u, v) =>
            {
                bool bezel = Frac(u * 4) < 0.012f || Frac(v * 2) < 0.02f;
                Color a = new Color(0.1f, 0.5f, 1f), b = new Color(0.9f, 0.2f, 0.8f);
                Color c = Color.Lerp(a, b, Mathf.PerlinNoise(u * 3, v * 2));
                return bezel ? Color.black : c;
            });

            Lit("M_Marble", new Color(0.86f, 0.85f, 0.83f), marble, 0.82f);
            Lit("M_WoodFloor", Color.white, wood, 0.45f);
            Lit("M_Carpet", Color.white, carpet, 0.05f);
            Lit("M_DarkTile", Color.white, dark, 0.7f);
            Lit("M_SportFloor", Color.white, sport, 0.35f);
            Lit("M_ToyFloor", Color.white, toy, 0.45f);
            Lit("M_Shutter", Color.white, shutter, 0.5f, 0.6f);
            Lit("M_MallWall", new Color(0.86f, 0.83f, 0.78f), null, 0.15f);
            var ceilTex = Tex("T_CeilingTile", 128, (u, v) =>
            {
                bool groove = Frac(u * 2) < 0.025f || Frac(v * 2) < 0.025f;
                float n = Mathf.PerlinNoise(u * 40, v * 40) * 0.025f;
                return groove ? new Color(0.62f, 0.62f, 0.64f) : new Color(0.93f - n, 0.93f - n, 0.92f - n);
            });
            Lit("M_CeilingTile", Color.white, ceilTex, 0.05f);
            var dl = Lit("M_Downlight", new Color(1f, 0.96f, 0.88f));
            dl.SetColor("_EmissionColor", new Color(1.6f, 1.5f, 1.25f));
            Lit("M_ZoneMat", new Color(0.12f, 0.17f, 0.14f), null, 0.1f);
            Lit("M_MallTrim", new Color(0.2f, 0.2f, 0.23f), null, 0.6f, 0.3f);
            Lit("M_Fascia", new Color(0.08f, 0.08f, 0.1f), null, 0.6f);
            Lit("M_Wall_Navy", new Color(0.12f, 0.16f, 0.3f), null, 0.3f);
            Lit("M_Wall_Yellow", new Color(1f, 0.86f, 0.45f), null, 0.15f);
            Lit("M_Wall_Green", new Color(0.35f, 0.62f, 0.45f), null, 0.15f);
            Lit("M_Wall_Terracotta", new Color(0.85f, 0.55f, 0.4f), null, 0.15f);
            Lit("M_Wall_Blush", new Color(0.98f, 0.86f, 0.88f), null, 0.15f);
            Lit("M_Wall_Plum", new Color(0.4f, 0.2f, 0.35f), null, 0.3f);
            Lit("M_Leaf", new Color(0.25f, 0.62f, 0.28f), null, 0.1f);
            Lit("M_Trunk", new Color(0.45f, 0.3f, 0.2f), null, 0.1f);
            Lit("M_Mirror", new Color(0.85f, 0.88f, 0.92f), null, 0.98f, 1f);
            var water = Lit("M_Water", new Color(0.25f, 0.6f, 0.85f, 0.85f), null, 0.97f);
            water.EnableKeyword("_EMISSION"); water.SetColor("_EmissionColor", new Color(0.05f, 0.25f, 0.45f));
            var scr = Lit("M_ScreenWall", Color.white, screen, 0.9f);
            scr.EnableKeyword("_EMISSION"); scr.SetTexture("_EmissionMap", screen); scr.SetColor("_EmissionColor", new Color(1.6f, 1.6f, 1.6f));
            var bulb = Lit("M_Bulb", new Color(1f, 0.95f, 0.85f));
            bulb.EnableKeyword("_EMISSION"); bulb.SetColor("_EmissionColor", new Color(2.2f, 1.9f, 1.4f));
            foreach (var (n, c) in new[] {
                ("M_Neon_Cyan", new Color(0.3f, 0.9f, 1f)), ("M_Neon_Pink", new Color(1f, 0.4f, 0.75f)), ("M_Neon_Orange", new Color(1f, 0.6f, 0.15f)),
                ("M_Neon_Red", new Color(1f, 0.3f, 0.25f)), ("M_Neon_Magenta", new Color(1f, 0.35f, 0.85f)), ("M_Neon_Gold", new Color(1f, 0.8f, 0.35f)),
                ("M_Neon_White", new Color(1f, 1f, 1f)) })
            {
                var m = Lit(n, c * 0.6f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 2.2f);
            }
            var sky = AssetDatabase.LoadAssetAtPath<Material>($"{R}/Materials/M_Sky.mat");
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, $"{R}/Materials/M_Sky.mat"); }
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.6f, 0.75f));
            sky.SetColor("_GroundColor", new Color(0.4f, 0.38f, 0.36f));
            sky.SetFloat("_Exposure", 1.0f);
            EditorUtility.SetDirty(sky);
        }

        // =====================================================================
        // Katmanlar + URP kalite ayarları + post-process profili
        // =====================================================================
        [MenuItem("BlackFriday/0 - Katman ve render ayarları")]
        public static void EnsureLayersAndRendering()
        {
            // FPArms katmanı
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            bool has = false;
            for (int i = 8; i < 32; i++) if (layers.GetArrayElementAtIndex(i).stringValue == FirstPersonArms.LayerName) has = true;
            if (!has)
                for (int i = 8; i < 32; i++)
                {
                    var l = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(l.stringValue)) { l.stringValue = FirstPersonArms.LayerName; break; }
                }
            tm.ApplyModifiedPropertiesWithoutUndo();

            // URP: gölgeler, ek ışık gölgeleri, yansıma probu karışımı
            foreach (var rp in new[] { "Assets/Settings/PC_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rp);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                void Set(string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; else Debug.Log("[BF] URP alanı yok: " + prop); }
                void SetF(string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; else Debug.Log("[BF] URP alanı yok: " + prop); }
                void SetB(string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; else Debug.Log("[BF] URP alanı yok: " + prop); }
                // performans/kalite dengesi: 2 cascade, 2048, 45 m; ek ışıklarda sadece fıskiye spotu gölgeli
                Set("m_MainLightShadowmapResolution", 2048);
                Set("m_ShadowCascadeCount", 2);
                SetF("m_Cascade2Split", 0.28f);
                SetB("m_AdditionalLightShadowsSupported", true);
                Set("m_AdditionalLightsShadowmapResolution", 1024);
                SetB("m_SoftShadowsSupported", true);
                Set("m_SoftShadowQuality", 2);
                SetF("m_ShadowDistance", 45f);
                SetF("m_ShadowDepthBias", 1f);
                SetF("m_ShadowNormalBias", 0.6f);
                Set("m_AdditionalLightsPerObjectLimit", 8);
                Set("m_MSAA", 1);
                SetF("m_RenderScale", 1f);
                SetB("m_RequireDepthTexture", false);
                SetB("m_RequireOpaqueTexture", false);
                SetB("m_ReflectionProbeBlending", true);
                SetB("m_ReflectionProbeBoxProjection", true);
                SetB("m_SupportsHDR", true);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // SSAO: yumuşak temas gölgeleri
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (rd != null)
                foreach (var feat in rd.rendererFeatures)
                {
                    if (feat == null || feat.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    // SSAO kapalı: tüm sahneyi ikinci kez çizen derinlik-normal ön geçişini kaldırır
                    // (ve FPS'te elde tutulan ürüne arkadaki dünyanın gölgesi düşmesini engeller)
                    feat.SetActive(false);
                    EditorUtility.SetDirty(feat);
                    var fso = new SerializedObject(feat);
                    var st = fso.FindProperty("m_Settings");
                    if (st != null)
                    {
                        var p = st.FindPropertyRelative("Intensity"); if (p != null) p.floatValue = 0.85f;
                        p = st.FindPropertyRelative("Radius"); if (p != null) p.floatValue = 0.3f;
                        p = st.FindPropertyRelative("DirectLightingStrength"); if (p != null) p.floatValue = 0.3f;
                        p = st.FindPropertyRelative("Falloff"); if (p != null) p.floatValue = 60f;
                        fso.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            if (rd != null) EnsureOutlineFeature(rd);
            GetPostProfile();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Kontur (ters gövde) geçişi ayrı bir "BFOutline" ışık modunda; tüm opak nesnelerden sonra tek seferde çizilir.
        /// Böylece her nesnede ForwardLit ↔ Outline gölgelendirici değişimi (SetPass) olmaz, SRP Batcher toplu çizer.
        /// </summary>
        static void EnsureOutlineFeature(UniversalRendererData rd)
        {
            RenderObjects ro = null;
            foreach (var f in rd.rendererFeatures) if (f is RenderObjects r && r.name == "BF Outline") ro = r;
            if (ro == null)
            {
                ro = ScriptableObject.CreateInstance<RenderObjects>();
                ro.name = "BF Outline";
                AssetDatabase.AddObjectToAsset(ro, rd);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ro, out string _, out long localId);
                var so = new SerializedObject(rd);
                var feats = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                feats.arraySize++;
                feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = ro;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            ro.settings.passTag = "BF Outline";
            ro.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            ro.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            ro.settings.filterSettings.LayerMask = ~0;
            ro.settings.filterSettings.PassNames = new[] { "BFOutline" };
            ro.settings.overrideMaterial = null;
            ro.settings.overrideShader = null;
            ro.SetActive(true);
            ro.Create();
            EditorUtility.SetDirty(ro);
            EditorUtility.SetDirty(rd);
            rd.SetDirty();
        }

        static VolumeProfile GetPostProfile()
        {
            string path = R + "/Settings/BF_PostProfile.asset";
            if (!AssetDatabase.IsValidFolder(R + "/Settings")) AssetDatabase.CreateFolder(R, "Settings");
            AssetDatabase.DeleteAsset(path);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, path);

            var tone = prof.Add<Tonemapping>(true); tone.mode.value = TonemappingMode.Neutral;
            var bloom = prof.Add<Bloom>(true); bloom.threshold.value = 1.15f; bloom.intensity.value = 0.38f; bloom.scatter.value = 0.65f; bloom.highQualityFiltering.value = true;
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.value = 0.12f; ca.contrast.value = 10f; ca.saturation.value = 12f; ca.colorFilter.overrideState = false; ca.hueShift.overrideState = false;
            var wb = prof.Add<WhiteBalance>(true); wb.temperature.value = 4f; wb.tint.value = 0f;
            // stilize renk: gölgeler hafif mavi-mor, parlak yerler sıcak
            var st = prof.Add<SplitToning>(true); st.shadows.value = new Color(0.46f, 0.47f, 0.58f); st.highlights.value = new Color(0.6f, 0.55f, 0.45f); st.balance.value = 10f;
            var vig = prof.Add<Vignette>(true); vig.intensity.value = 0.18f; vig.smoothness.value = 0.45f; vig.color.overrideState = false; vig.center.overrideState = false; vig.rounded.overrideState = false;
            foreach (var c in prof.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, prof); }
            EditorUtility.SetDirty(prof);
            AssetDatabase.SaveAssets();
            return prof;
        }

        static void AddPostVolume()
        {
            var go = new GameObject("PostProcess");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(R + "/Settings/BF_PostProfile.asset") ?? GetPostProfile();
        }

        static void SetupCameraQuality(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
        }

        static void MallAmbient()
        {
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{R}/Materials/M_Sky.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.64f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.52f, 0.49f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.31f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.45f;
            RenderSettings.fog = false;
        }

        // ---------------------------------------------------------------- ışık yardımcıları
        static Light PointL(Transform parent, Vector3 pos, Color c, float intensity, float range, bool shadows = false)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        static Light SpotL(Transform parent, Vector3 pos, Vector3 lookAtLocal, Color c, float intensity, float range, float angle, bool shadows)
        {
            var go = new GameObject("SpotLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.LookRotation(lookAtLocal - pos, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.color = c; l.intensity = intensity; l.range = range; l.spotAngle = angle; l.innerSpotAngle = angle * 0.6f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        static GameObject NoShadow(GameObject go)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static void Probe(Transform parent, Vector3 center, Vector3 size, List<ReflectionProbe> list)
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Custom;
            p.size = size;
            p.boxProjection = true;
            p.resolution = 256;
            p.hdr = true;
            p.intensity = 1f;
            p.blendDistance = 1.5f;
            list.Add(p);
        }

        /// <summary>Prefab örneğindeki raf noktalarını mağaza ürünleriyle sınırla (boyut kısıtını koruyarak).</summary>
        static void RestrictSpawns(GameObject inst, int[] storeProducts)
        {
            foreach (var sp in inst.GetComponentsInChildren<ShelfSpawnPoint>(true))
            {
                var allowed = storeProducts.Where(id => sp.Allows(id)).ToArray();
                sp.allowedProducts = allowed.Length > 0 ? allowed : new[] { -1 };
                sp.hotDeal = false;
            }
        }

        static GameObject DisplayCopy(int productId, Transform parent, Vector3 localPos, float yaw, float scale = 1f)
        {
            var db = AssetDatabase.LoadAssetAtPath<ProductDatabase>(R + "/Resources/BF_ProductDatabase.asset");
            var def = db != null ? db.Get(productId) : null;
            if (def == null || def.prefab == null) return null;
            var vis = Object.Instantiate(def.prefab.transform.Find("Visual").gameObject, parent);
            vis.name = "Display_" + def.displayName;
            vis.transform.localPosition = localPos;
            vis.transform.localEulerAngles = new Vector3(0, yaw, 0);
            vis.transform.localScale *= scale;
            return vis;
        }

        // =====================================================================
        // Ek mağaza propları
        // =====================================================================
        [MenuItem("BlackFriday/7b - AVM propları")]
        public static void BuildMallProps()
        {
            EnsureFolders();
            NewTempScene();

            // teşhir masası (mağaza içi)
            {
                var root = new GameObject("DisplayTable").transform;
                Box(root, "Top", new Vector3(2.6f, 0.08f, 1.3f), new Vector3(0, 0.78f, 0), M("M_Wood"));
                Box(root, "Base", new Vector3(2.3f, 0.74f, 1.0f), new Vector3(0, 0.37f, 0), M("M_White"));
                BakeToAsset(root.gameObject, "DisplayTable");
                var col = root.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.41f, 0); col.size = new Vector3(2.6f, 0.82f, 1.3f);
                var spr = new GameObject("SpawnPoints").transform; spr.SetParent(root, false);
                foreach (float x in new[] { -0.8f, 0f, 0.8f })
                    foreach (float z in new[] { -0.3f, 0.3f })
                    {
                        var sp = new GameObject("SP").transform; sp.SetParent(spr, false);
                        sp.localPosition = new Vector3(x, 0.825f, z);
                        sp.localEulerAngles = new Vector3(0, z > 0 ? 180 : 0, 0);
                        sp.gameObject.AddComponent<ShelfSpawnPoint>();
                    }
                SavePrefab(root.gameObject, R + "/Prefabs/Props/DisplayTable.prefab");
            }
            // cam tezgah (kozmetik)
            {
                var root = new GameObject("GlassCounter").transform;
                Box(root, "Base", new Vector3(2.4f, 0.85f, 0.8f), new Vector3(0, 0.425f, 0), M("M_Wall_Plum"));
                Box(root, "Trim", new Vector3(2.42f, 0.05f, 0.82f), new Vector3(0, 0.875f, 0), M("M_Gold"));
                NoShadow(Box(root, "Glass", new Vector3(2.3f, 0.3f, 0.7f), new Vector3(0, 1.05f, 0), M("M_Glass")));
                Box(root, "Top", new Vector3(2.42f, 0.03f, 0.82f), new Vector3(0, 1.215f, 0), M("M_Glass"));
                BakeToAsset(root.gameObject, "GlassCounter");
                var col = root.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.615f, 0); col.size = new Vector3(2.42f, 1.23f, 0.82f);
                var spr = new GameObject("SpawnPoints").transform; spr.SetParent(root, false);
                foreach (float x in new[] { -0.8f, 0f, 0.8f })
                {
                    var sp = new GameObject("SP").transform; sp.SetParent(spr, false);
                    sp.localPosition = new Vector3(x, 1.235f, 0);
                    sp.gameObject.AddComponent<ShelfSpawnPoint>().allowedProducts = new[] { 9, 4, 7, 8 };
                }
                SavePrefab(root.gameObject, R + "/Prefabs/Props/GlassCounter.prefab");
            }
            // ağaç (saksıda)
            {
                var root = new GameObject("Tree").transform;
                Box(root, "Planter", new Vector3(1.6f, 0.7f, 1.6f), new Vector3(0, 0.35f, 0), M("M_MallTrim"));
                Box(root, "Soil", new Vector3(1.45f, 0.05f, 1.45f), new Vector3(0, 0.7f, 0), M("M_Trunk"));
                Cyl(root, "Trunk", 0.12f, 2.6f, new Vector3(0, 2f, 0), M("M_Trunk"), default, 8);
                Ball(root, "Crown1", 1.0f, new Vector3(0, 3.6f, 0), M("M_Leaf"));
                Ball(root, "Crown2", 0.75f, new Vector3(0.5f, 3.1f, 0.3f), M("M_Leaf"));
                Ball(root, "Crown3", 0.7f, new Vector3(-0.45f, 3.2f, -0.35f), M("M_Leaf"));
                BakeToAsset(root.gameObject, "Tree");
                var col = root.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(1.6f, 1f, 1.6f);
                SavePrefab(root.gameObject, R + "/Prefabs/Props/Tree.prefab");
            }
            // bank
            {
                var root = new GameObject("Bench").transform;
                Box(root, "Seat", new Vector3(2.2f, 0.08f, 0.6f), new Vector3(0, 0.46f, 0), M("M_Wood"));
                Box(root, "Back", new Vector3(2.2f, 0.45f, 0.06f), new Vector3(0, 0.75f, 0.27f), M("M_Wood"), new Vector3(-10, 0, 0));
                Box(root, "LegL", new Vector3(0.08f, 0.46f, 0.55f), new Vector3(-0.95f, 0.23f, 0), M("M_MallTrim"));
                Box(root, "LegR", new Vector3(0.08f, 0.46f, 0.55f), new Vector3(0.95f, 0.23f, 0), M("M_MallTrim"));
                BakeToAsset(root.gameObject, "Bench");
                var col = root.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.45f, 0.05f); col.size = new Vector3(2.2f, 0.9f, 0.65f);
                SavePrefab(root.gameObject, R + "/Prefabs/Props/Bench.prefab");
            }
            // askılık (moda)
            {
                var root = new GameObject("ClothesRack").transform;
                Cyl(root, "PoleL", 0.025f, 1.6f, new Vector3(-0.9f, 0.8f, 0), M("M_Chrome"), default, 8);
                Cyl(root, "PoleR", 0.025f, 1.6f, new Vector3(0.9f, 0.8f, 0), M("M_Chrome"), default, 8);
                Cyl(root, "Bar", 0.02f, 1.85f, new Vector3(0, 1.58f, 0), M("M_Chrome"), new Vector3(0, 0, 90), 8);
                Box(root, "FootL", new Vector3(0.06f, 0.04f, 0.6f), new Vector3(-0.9f, 0.02f, 0), M("M_Chrome"));
                Box(root, "FootR", new Vector3(0.06f, 0.04f, 0.6f), new Vector3(0.9f, 0.02f, 0), M("M_Chrome"));
                string[] cm = { "M_Pink", "M_Cyan", "M_White", "M_Magenta", "M_Yellow", "M_Black", "M_Purple" };
                for (int i = 0; i < 9; i++)
                {
                    float x = -0.75f + i * 0.19f;
                    Box(root, "Cloth" + i, new Vector3(0.05f, 0.75f, 0.45f), new Vector3(x, 1.18f, 0), M(cm[i % cm.Length]), new Vector3(0, 0, Random.Range(-3f, 3f)));
                }
                BakeToAsset(root.gameObject, "ClothesRack");
                var col = root.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.8f, 0); col.size = new Vector3(1.9f, 1.6f, 0.6f);
                SavePrefab(root.gameObject, R + "/Prefabs/Props/ClothesRack.prefab");
            }
            // sarkıt avize
            {
                var root = new GameObject("Pendant").transform;
                Cyl(root, "Cable", 0.015f, 3f, new Vector3(0, 1.5f, 0), M("M_Black"), default, 6);
                Cone(root, "Shade", 0.9f, 0.6f, new Vector3(0, -0.25f, 0), M("M_Gold"));
                Ball(root, "Bulb", 0.28f, new Vector3(0, -0.45f, 0), M("M_Bulb"));
                BakeToAsset(root.gameObject, "Pendant");
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                SavePrefab(root.gameObject, R + "/Prefabs/Props/Pendant.prefab");
            }
            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // 8) BÖLÜM 1 — MEGA AVM
        // =====================================================================
        [MenuItem("BlackFriday/8 - Bölüm 1 sahnesi (Mega AVM)")]
        public static void BuildLevel01()
        {
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MallAmbient();
            var sun = AddSun(1.15f, new Vector3(52, 34, 0));
            sun.color = new Color(1f, 0.9f, 0.74f);
            sun.shadowStrength = 0.85f;
            AddPostVolume();
            var probes = new List<ReflectionProbe>();

            // ---------- kamera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 300f;
            SetupCameraQuality(cam);
            camGo.AddComponent<AudioListener>();
            var pc = camGo.AddComponent<PlayerCamera>();
            pc.fpArmsController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(R + "/Animation/BF_FPArms.controller");
            camGo.transform.position = new Vector3(-41f, 2f, 0f);
            camGo.transform.eulerAngles = new Vector3(8, 90, 0);

            var mall = new GameObject("Mall").transform;           // NavMesh buradan
            var shell = new GameObject("Shell").transform; shell.SetParent(mall, false);
            var lights = new GameObject("Lights").transform;
            var probeRoot = new GameObject("ReflectionProbes").transform;

            // ---------- zemin, dış duvarlar, çatı
            Box(shell, "Floor", new Vector3(80.8f, 0.2f, 54.8f), new Vector3(0, -0.1f, 0), M("M_Marble"), default, true);
            // koridor süsü: koyu mermer şeritler
            foreach (float z in new[] { -6.2f, 6.2f })
                NoShadow(Box(shell, "Inlay", new Vector3(80f, 0.012f, 0.35f), new Vector3(0, 0.006f, z), M("M_MallTrim")));
            NoShadow(Box(shell, "InlayCenter", new Vector3(80f, 0.012f, 0.2f), new Vector3(0, 0.006f, 0), M("M_Gold")));

            foreach (int s in new[] { 1, -1 })
            {
                Box(shell, "OuterWall_" + s, new Vector3(80.8f, 10.8f, 0.4f), new Vector3(0, 5.4f, s * 25.3f), M("M_MallWall"), default, true);
                // üst kat zemin/ alt kat tavan
                var slab = Box(shell, "Slab_" + s, new Vector3(80.8f, 0.3f, 18.4f), new Vector3(0, 5.15f, s * 16f), M("M_MallWall"), default, true);
                // balkon zemini
                NoShadow(Box(shell, "BalconyFloor_" + s, new Vector3(80.8f, 0.02f, 4f), new Vector3(0, UpperY + 0.01f, s * 9f), M("M_Marble")));
                // üst kat cephesi (kapalı mağazalar)
                Box(shell, "UpperFacade_" + s, new Vector3(80.8f, RoofY - UpperY, 0.3f), new Vector3(0, (UpperY + RoofY) / 2, s * 11.15f), M("M_MallWall"), default, true);
                // çatı (mağaza blokları üstü)
                Box(shell, "RoofBlock_" + s, new Vector3(80.8f, 0.3f, 14.4f), new Vector3(0, RoofY + 0.15f, s * 18.2f), M("M_MallWall"));
            }
            // doğu duvarı: ortada doğu kanadına (yemek alanı + market) açılan geçit
            Box(shell, "EastWall_S", new Vector3(0.4f, 10.8f, 18.5f), new Vector3(40.2f, 5.4f, -16.25f), M("M_MallWall"), default, true);
            Box(shell, "EastWall_N", new Vector3(0.4f, 10.8f, 18.5f), new Vector3(40.2f, 5.4f, 16.25f), M("M_MallWall"), default, true);
            Box(shell, "EastWall_Top", new Vector3(0.4f, 6.4f, 14f), new Vector3(40.2f, 7.6f, 0), M("M_MallWall"), default, true);
            foreach (float z in new[] { -6.85f, 6.85f })
                Box(shell, "PortalPost", new Vector3(0.7f, 4.4f, 0.3f), new Vector3(40.2f, 2.2f, z), M("M_MallTrim"), default, true);
            NoShadow(Box(shell, "PortalNeon", new Vector3(0.44f, 0.08f, 13.4f), new Vector3(40.2f, 4.36f, 0), M("M_Neon_Gold")));
            Box(shell, "WestWall_S", new Vector3(0.4f, 10.8f, 21.3f), new Vector3(-40.2f, 5.4f, -14.65f), M("M_MallWall"), default, true);
            Box(shell, "WestWall_N", new Vector3(0.4f, 10.8f, 21.3f), new Vector3(-40.2f, 5.4f, 14.65f), M("M_MallWall"), default, true);
            Box(shell, "WestWall_Top", new Vector3(0.4f, 6.3f, 8f), new Vector3(-40.2f, 7.65f, 0), M("M_MallWall"), default, true);
            // koridor çatısı: atrium dışında kapalı, atriumda cam
            Box(shell, "Roof_W", new Vector3(26.4f, 0.3f, 22.4f), new Vector3(-27.2f, RoofY + 0.15f, 0), M("M_MallWall"));
            Box(shell, "Roof_E", new Vector3(26.4f, 0.3f, 22.4f), new Vector3(27.2f, RoofY + 0.15f, 0), M("M_MallWall"));
            NoShadow(Box(shell, "Skylight", new Vector3(28f, 0.06f, 22.4f), new Vector3(0, RoofY + 0.1f, 0), M("M_Glass")));
            for (float x = -14f; x <= 14.01f; x += 3.5f)
                Box(shell, "SkyBeam", new Vector3(0.22f, 0.45f, 22.4f), new Vector3(x, RoofY - 0.15f, 0), M("M_MallTrim"));
            foreach (float z in new[] { -5.6f, 0f, 5.6f })
                Box(shell, "SkyBeamL", new Vector3(28f, 0.3f, 0.18f), new Vector3(0, RoofY - 0.2f, z), M("M_MallTrim"));
            // koridor tavan ışık panelleri (atrium dışı)
            foreach (float x in new[] { -34f, -28f, -22f, -16f, 16f, 22f, 28f, 34f })
                NoShadow(Box(shell, "CeilingPanel", new Vector3(3f, 0.05f, 8f), new Vector3(x, RoofY - 0.03f, 0), M("M_LightPanel")));

            // ---------- balkon korkulukları (yürüyen merdiven inişlerinde boşluk)
            void Railing(int s, float x0, float x1)
            {
                float len = x1 - x0, cx = (x0 + x1) / 2;
                if (len < 0.2f) return;
                NoShadow(Box(shell, "RailGlass", new Vector3(len, 1.0f, 0.05f), new Vector3(cx, UpperY + 0.55f, s * 7f), M("M_Glass"), default, true));
                Box(shell, "RailTop", new Vector3(len, 0.06f, 0.12f), new Vector3(cx, UpperY + 1.08f, s * 7f), M("M_Chrome"));
                Box(shell, "SlabEdge", new Vector3(len, 0.3f, 0.12f), new Vector3(cx, 5.15f, s * 7.0f), M("M_MallTrim"));
            }
            // (x = ±28'de üst kat köprüleri için boşluk)
            Railing(-1, -40.4f, -29.3f); Railing(-1, -26.7f, -5f); Railing(-1, -2.5f, 26.7f); Railing(-1, 29.3f, 40.4f);
            Railing(1, -40.4f, -29.3f); Railing(1, -26.7f, 2.5f); Railing(1, 5f, 26.7f); Railing(1, 29.3f, 40.4f);

            // ---------- yürüyen merdivenler (ProBuilder merdiven) + iniş platformları
            Escalator(shell, -1);
            Escalator(shell, 1);

            // ---------- mağazalar arası dolgu blokları
            foreach (int s in new[] { 1, -1 })
                foreach (var (x0, x1) in new[] { (-40.4f, -37f), (-15f, -11f), (11f, 15f), (37f, 40.4f) })
                {
                    float w = x1 - x0, cx = (x0 + x1) / 2;
                    Box(shell, "Filler", new Vector3(w, StoreH, StoreD), new Vector3(cx, StoreH / 2, s * (7f + StoreD / 2)), M("M_MallWall"), default, true);
                    Box(shell, "FillerClad", new Vector3(w + 0.1f, StoreH, 0.3f), new Vector3(cx, StoreH / 2, s * 7.05f), M("M_MallTrim"), default, true);
                }

            // ---------- mağazalar
            var storesRoot = new GameObject("Stores").transform; storesRoot.SetParent(mall, false);
            var lib = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(R + "/Resources/BF_CharacterLibrary.asset");
            int storeIndex = 0;
            foreach (var st in Stores())
            {
                BuildStore(storesRoot, lights, probeRoot, probes, st, storeIndex++, lib);
            }

            // ---------- üst kat (kapalı mağazalar: "YAKINDA")
            foreach (var st in Stores())
            {
                float s = st.side;
                var shutter = Box(shell, "UpperShutter", new Vector3(16f, 3.3f, 0.06f), new Vector3(st.cx, UpperY + 1.75f, s * 10.97f), M("M_Shutter"));
                Box(shell, "UpperSign", new Vector3(10f, 0.8f, 0.12f), new Vector3(st.cx, UpperY + 4.1f, s * 10.95f), M("M_Fascia"));
                Text3D(shell, "YAKINDA  •  BÖLÜM 2", new Vector3(st.cx, UpperY + 4.1f, s * 10.87f), new Vector3(0, s > 0 ? 0 : 180, 0), 0.03f, new Color(1f, 0.85f, 0.3f), true);
                SpotL(lights, new Vector3(st.cx, RoofY - 0.6f, s * 8.6f), new Vector3(st.cx, UpperY, s * 10.5f), new Color(1f, 0.9f, 0.78f), 5f, 9f, 110f, false);
            }

            // ---------- atrium: fıskiye + sarkıt afişler
            var atrium = new GameObject("Atrium").transform; atrium.SetParent(mall, false);
            Fountain(atrium);
            foreach (float x in new[] { -20f, 20f })
            {
                Box(atrium, "Banner", new Vector3(0.12f, 2.4f, 9f), new Vector3(x, 8.4f, 0), M("M_Fascia"));
                NoShadow(Box(atrium, "BannerStripe", new Vector3(0.13f, 0.12f, 9f), new Vector3(x, 7.3f, 0), M("M_Neon_Gold")));
                Cyl(atrium, "BannerCable", 0.02f, 0.9f, new Vector3(x, 10f, -4f), M("M_Black"), default, 6);
                Cyl(atrium, "BannerCable", 0.02f, 0.9f, new Vector3(x, 10f, 4f), M("M_Black"), default, 6);
                Text3D(atrium, "BLACK FRIDAY", new Vector3(x - 0.07f, 8.7f, 0), new Vector3(0, 90, 0), 0.06f, new Color(1f, 0.82f, 0.15f), true);
                Text3D(atrium, "%90'A VARAN İNDİRİM", new Vector3(x - 0.07f, 7.85f, 0), new Vector3(0, 90, 0), 0.03f, Color.white);
                Text3D(atrium, "BLACK FRIDAY", new Vector3(x + 0.07f, 8.7f, 0), new Vector3(0, -90, 0), 0.06f, new Color(1f, 0.82f, 0.15f), true);
                Text3D(atrium, "%90'A VARAN İNDİRİM", new Vector3(x + 0.07f, 7.85f, 0), new Vector3(0, -90, 0), 0.03f, Color.white);
            }
            // doğu duvarı dev pano
            Box(shell, "EastBoard", new Vector3(0.2f, 4f, 12f), new Vector3(40f, 6.5f, 0), M("M_Fascia"));
            NoShadow(Box(shell, "EastBoardNeon", new Vector3(0.22f, 0.1f, 12f), new Vector3(40f, 4.55f, 0), M("M_Neon_Gold")));
            Text3D(shell, "MEGA AVM", new Vector3(39.88f, 7.2f, 0), new Vector3(0, 90, 0), 0.11f, new Color(1f, 0.82f, 0.15f), true);
            Text3D(shell, "YEMEK ALANI  •  MEGA MARKET  →", new Vector3(39.88f, 5.6f, 0), new Vector3(0, 90, 0), 0.04f, Color.white, true);

            // ---------- koridor: indirim standları, ağaçlar, banklar
            var conc = new GameObject("Concourse").transform; conc.SetParent(mall, false);
            foreach (var p in new[] { new Vector3(-32f, 0, 3.6f), new Vector3(-21f, 0, -3.6f), new Vector3(21f, 0, 3.6f), new Vector3(32f, 0, -3.6f), new Vector3(-8.5f, 0, 0.5f), new Vector3(8.5f, 0, -0.5f) })
                Place(P("Props/DealTable"), conc, p, Random.Range(0, 360));
            foreach (int s in new[] { 1, -1 })
                foreach (float x in new[] { -38.5f, -13f, 13f, 38.5f })
                    Place(P("Props/Tree"), conc, new Vector3(x, 0, s * 5.6f), 0);
            foreach (var p in new[] { new Vector3(-26f, 0, 0f), new Vector3(26f, 0, 0f), new Vector3(-36f, 0, -4.5f), new Vector3(36f, 0, 4.5f) })
                Place(P("Props/Bench"), conc, p, p.x < 0 ? 0 : 180);

            // ---------- üst kat köprüleri, balkon fırsatları, bayraklar
            BuildBridges(shell, conc);

            // ---------- doğu kanadı: yemek alanı + MEGA MARKET
            var wingChunks = BuildEastWing(mall, lights, probeRoot, probes, lib);

            // ---------- koridor ışıkları
            foreach (float x in new[] { -32f, -24f, -16f, 16f, 24f, 32f })
            {
                var pend = Place(P("Props/Pendant"), conc, new Vector3(x, 7.4f, 0), 0);
                PointL(lights, new Vector3(x, 6.6f, 0), new Color(1f, 0.88f, 0.72f), 3.2f, 16f);
            }
            foreach (var p in new[] { new Vector3(-10f, 4.5f, 4f), new Vector3(10f, 4.5f, -4f), new Vector3(-10f, 4.5f, -4f), new Vector3(10f, 4.5f, 4f) })
                PointL(lights, p, new Color(0.95f, 0.95f, 1f), 1.6f, 12f);
            // fıskiye spotları (gölgeli)
            SpotL(lights, new Vector3(0, 9.8f, -5f), new Vector3(0, 0, 0), new Color(1f, 0.95f, 0.85f), 18f, 16f, 50f, true);
            SpotL(lights, new Vector3(0, 9.8f, 5f), new Vector3(0, 0, 0), new Color(0.85f, 0.92f, 1f), 12f, 16f, 50f, false);
            // giriş
            PointL(lights, new Vector3(-37f, 4f, 0), new Color(1f, 0.92f, 0.8f), 2.2f, 12f);

            // ---------- yansıma probları (koridor)
            Probe(probeRoot, new Vector3(0, 3f, 0), new Vector3(30f, 10f, 14.2f), probes);
            Probe(probeRoot, new Vector3(-27.5f, 3f, 0), new Vector3(26f, 10f, 14.2f), probes);
            Probe(probeRoot, new Vector3(27.5f, 3f, 0), new Vector3(26f, 10f, 14.2f), probes);

            // ---------- giriş ve dış alan
            Entrance(shell);
            var outside = new GameObject("Outside").transform;
            Box(outside, "Ground", new Vector3(80f, 0.2f, 120f), new Vector3(-80.5f, -0.13f, 0), M("M_Asphalt"), default, true);
            Box(outside, "Sidewalk", new Vector3(6f, 0.08f, 40f), new Vector3(-43.5f, -0.01f, 0), M("M_Concrete"), default, true);
            for (int i = -8; i <= 8; i++)
                NoShadow(Box(outside, "ParkLine", new Vector3(5f, 0.02f, 0.12f), new Vector3(-55f, 0f, i * 3f), M("M_LineWhite")));
            Place(P("Props/Cart"), outside, new Vector3(-48f, 0, 6f), 70);
            Place(P("Props/Cart"), outside, new Vector3(-49f, 0, -8f), -40);
            foreach (var (n, size, pos) in new[] { ("Bound_W", new Vector3(0.5f, 3f, 40f), new Vector3(-47f, 1.5f, 0)), ("Bound_N", new Vector3(7f, 3f, 0.5f), new Vector3(-43.5f, 1.5f, 20f)), ("Bound_S", new Vector3(7f, 3f, 0.5f), new Vector3(-43.5f, 1.5f, -20f)) })
                Box(outside, n, size, pos, M("M_WallTrim"), default, true).GetComponent<MeshRenderer>().enabled = false;

            // ---------- doğma noktaları
            var spawns = new GameObject("PlayerSpawns").transform;
            for (int i = 0; i < 4; i++)
            {
                var sgo = new GameObject("Spawn_" + (i + 1));
                sgo.transform.SetParent(spawns, false);
                sgo.transform.position = new Vector3(-36.5f, 0.05f, -2.25f + i * 1.5f);
                sgo.transform.rotation = Quaternion.Euler(0, 90, 0);
                sgo.AddComponent<NetworkStartPosition>();
            }

            // ---------- etkileşimli nesneler (arabalar, koli kuleleri, kaygan zemin, bantlar, kapı)
            BuildInteractives(conc);

            // ---------- NavMesh
            var surf = mall.gameObject.AddComponent<NavMeshSurface>();
            surf.collectObjects = CollectObjects.Children;
            surf.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surf.BuildNavMesh();
            string navPath = R + "/Scenes/Level01_NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surf.navMeshData, navPath);

            // ---------- oyun modu + UI
            var modeGo = new GameObject("GameMode_ShoppingList");
            modeGo.AddComponent<NetworkIdentity>();
            var mode = modeGo.AddComponent<ShoppingListMode>();
            mode.npcPrefab = P("BF_NPC");
            mode.roundSeconds = 240f;
            mode.decoyItems = 240;
            mode.npcCount = 26;
            AddUI("HUD", "HUD.uxml", out var hud);
            hud.AddComponent<HUDController>();
            AddEventSystem();

            foreach (var r in mall.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);

            // ---------- performans: statik geometriyi bölge + materyal bazında birleştir
            int merged = 0;
            foreach (Transform store in storesRoot) merged += MergeStatic(store, "L01_" + store.name.Replace(" ", "_").Replace("İ", "I").Replace("Ü", "U"));
            merged += MergeStatic(shell, "L01_Shell");
            merged += MergeStatic(atrium, "L01_Atrium");
            merged += MergeStatic(conc, "L01_Concourse");
            foreach (var wc in wingChunks) merged += MergeStatic(wc, "L01_" + wc.name);
            merged += MergeStatic(outside, "L01_Outside");
            Debug.Log($"[BF] Statik birleştirme: {merged} birleşik mesh");

            EditorSceneManager.SaveScene(scene, Level01Scene);

            // ---------- yansıma problarını bake et
            BakeProbes(probes);

            var re = EditorSceneManager.OpenScene(Level01Scene, OpenSceneMode.Single);
            foreach (var ni in Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None)) EditorUtility.SetDirty(ni);

            // ---------- oklüzyon: duvarların / mağazaların arkasında kalanlar çizilmez
            StaticOcclusionCulling.smallestOccluder = 2f;
            StaticOcclusionCulling.smallestHole = 0.25f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            bool occ = StaticOcclusionCulling.Compute();
            Debug.Log("[BF] Oklüzyon verisi: " + (occ ? "hazır" : "HATA"));

            EditorSceneManager.MarkSceneDirty(re);
            EditorSceneManager.SaveScene(re);
            Debug.Log("[BF] Mega AVM kuruldu: " + Level01Scene);
        }

        static void BakeProbes(List<ReflectionProbe> probes)
        {
            string dir = R + "/Scenes/Level01_Probes";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(R + "/Scenes", "Level01_Probes");
            int i = 0;
            foreach (var p in probes)
            {
                if (p == null) continue;
                string path = $"{dir}/Probe_{i++:00}.exr";
                if (Lightmapping.BakeReflectionProbe(p, path))
                {
                    AssetDatabase.ImportAsset(path);
                    p.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ---------------------------------------------------------------- yürüyen merdiven
        static void Escalator(Transform parent, int s)
        {
            // güney (s=-1): x -14 → -5 doğuya yükselir; kuzey (s=1): x 14 → 5 batıya yükselir
            float startX = s < 0 ? -14f : 14f;
            float yaw = s < 0 ? 90f : -90f;
            var esc = new GameObject("Escalator_" + (s < 0 ? "S" : "N")).transform;
            esc.SetParent(parent, false);
            esc.localPosition = new Vector3(startX, 0, s < 0 ? -5.1f : 5.1f);
            esc.localEulerAngles = new Vector3(0, yaw, 0);
            var stair = ShapeGenerator.GenerateStair(PivotLocation.FirstVertex, new Vector3(1.8f, UpperY, 9f), 26, true);
            var go = Finish(stair, "Steps", esc, Vector3.zero, Vector3.zero, M("M_MallTrim"));
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            float ang = Mathf.Atan2(UpperY, 9f) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(81f + UpperY * UpperY);
            foreach (float lx in new[] { -0.06f, 1.86f })
            {
                NoShadow(Box(esc, "SideGlass", new Vector3(0.05f, 0.9f, len), new Vector3(lx, UpperY / 2 + 0.6f, 4.5f), M("M_Glass"), new Vector3(-ang, 0, 0)));
                Box(esc, "Handrail", new Vector3(0.1f, 0.08f, len), new Vector3(lx, UpperY / 2 + 1.08f, 4.5f), M("M_Black"), new Vector3(-ang, 0, 0));
                Box(esc, "Skirt", new Vector3(0.08f, 0.35f, len), new Vector3(lx, UpperY / 2 + 0.05f, 4.5f), M("M_Chrome"), new Vector3(-ang, 0, 0), true);
            }
            // iniş platformu
            float lx0 = s < 0 ? -5f : 2.5f, lx1 = s < 0 ? -2.5f : 5f;
            float lz = s < 0 ? -6.05f : 6.05f;
            Box(parent, "Landing", new Vector3(lx1 - lx0, 0.3f, 1.9f), new Vector3((lx0 + lx1) / 2, 5.15f, lz), M("M_MallWall"), default, true);
            NoShadow(Box(parent, "LandingFloor", new Vector3(lx1 - lx0, 0.02f, 1.9f), new Vector3((lx0 + lx1) / 2, UpperY + 0.01f, lz), M("M_Marble")));
            // platform korkulukları
            float edgeZ = s < 0 ? -5.1f : 5.1f;
            float endX = s < 0 ? -2.5f : 2.5f;
            NoShadow(Box(parent, "LandingRail", new Vector3(lx1 - lx0, 1f, 0.05f), new Vector3((lx0 + lx1) / 2, UpperY + 0.55f, edgeZ), M("M_Glass"), default, true));
            NoShadow(Box(parent, "LandingRailEnd", new Vector3(0.05f, 1f, 1.9f), new Vector3(endX, UpperY + 0.55f, lz), M("M_Glass"), default, true));
            Box(parent, "LandingRailTop", new Vector3(lx1 - lx0, 0.06f, 0.12f), new Vector3((lx0 + lx1) / 2, UpperY + 1.08f, edgeZ), M("M_Chrome"));
        }

        // ---------------------------------------------------------------- fıskiye
        static void Fountain(Transform parent)
        {
            var f = new GameObject("Fountain").transform; f.SetParent(parent, false);
            var ring = Pipe(f, "PoolRing", 4f, 0.6f, 0.45f, new Vector3(0, 0.3f, 0), M("M_Marble"));
            ring.AddComponent<MeshCollider>().sharedMesh = ring.GetComponent<MeshFilter>().sharedMesh;
            Pipe(f, "PoolRim", 4.05f, 0.08f, 0.55f, new Vector3(0, 0.62f, 0), M("M_Gold"));
            NoShadow(Cyl(f, "Water", 3.6f, 0.04f, new Vector3(0, 0.45f, 0), M("M_Water"), default, 40));
            var core = Cyl(f, "Pedestal", 1.1f, 0.8f, new Vector3(0, 0.4f, 0), M("M_Marble"), default, 24);
            core.AddComponent<CapsuleCollider>().radius = 1.1f;
            Cyl(f, "Basin", 1.7f, 0.18f, new Vector3(0, 1.0f, 0), M("M_Marble"), default, 32);
            NoShadow(Cyl(f, "BasinWater", 1.55f, 0.03f, new Vector3(0, 1.1f, 0), M("M_Water"), default, 32));
            Cyl(f, "Column", 0.22f, 1.4f, new Vector3(0, 1.8f, 0), M("M_Gold"), default, 12);
            Cyl(f, "TopBasin", 0.8f, 0.12f, new Vector3(0, 2.5f, 0), M("M_Marble"), default, 24);
            NoShadow(Cone(f, "Spray", 0.35f, 0.9f, new Vector3(0, 3.0f, 0), M("M_Water")));
            // su içi ışıklar
            foreach (var a in new[] { 0f, 120f, 240f })
            {
                var p = Quaternion.Euler(0, a, 0) * new Vector3(2.6f, 0.6f, 0);
                PointL(f, p, new Color(0.4f, 0.75f, 1f), 1.5f, 5f);
            }
        }

        // ---------------------------------------------------------------- giriş
        static void Entrance(Transform shell)
        {
            Box(shell, "Canopy", new Vector3(5f, 0.35f, 12f), new Vector3(-42.6f, 4.8f, 0), M("M_Fascia"));
            NoShadow(Box(shell, "CanopyNeon", new Vector3(5f, 0.08f, 12.02f), new Vector3(-42.6f, 4.6f, 0), M("M_Neon_Gold")));
            Box(shell, "DoorFrameTop", new Vector3(0.5f, 0.4f, 8f), new Vector3(-40.2f, 4.3f, 0), M("M_MallTrim"));
            foreach (float z in new[] { -4f, 4f })
                Box(shell, "DoorFrameSide", new Vector3(0.6f, 4.5f, 0.3f), new Vector3(-40.2f, 2.25f, z), M("M_MallTrim"), default, true);
            // sabit cam yan paneller (ortadaki kanatlar otomatik: bkz. BuildInteractives)
            foreach (float z in new[] { -3f, 3f })
                NoShadow(Box(shell, "DoorSideGlass", new Vector3(0.06f, 4f, 2f), new Vector3(-40.45f, 2.05f, z), M("M_Glass"), default, true));
            Box(shell, "FacadeSign", new Vector3(0.3f, 2.2f, 14f), new Vector3(-40.55f, 7.4f, 0), M("M_Fascia"));
            Text3D(shell, "MEGA AVM", new Vector3(-40.72f, 7.7f, 0), new Vector3(0, 90, 0), 0.1f, new Color(1f, 0.82f, 0.15f), true);
            Text3D(shell, "BLACK FRIDAY", new Vector3(-40.72f, 6.75f, 0), new Vector3(0, 90, 0), 0.035f, Color.white, true);
            NoShadow(Box(shell, "EntranceMat", new Vector3(4f, 0.02f, 7f), new Vector3(-38.2f, 0.01f, 0), M("M_Red")));
        }

        // ---------------------------------------------------------------- mağaza
        static void BuildStore(Transform storesRoot, Transform lightsRoot, Transform probeRoot, List<ReflectionProbe> probes, StoreDef st, int index, CharacterLibrary lib)
        {
            int s = st.side;
            var root = new GameObject("Store_" + st.name).transform;
            root.SetParent(storesRoot, false);
            root.localPosition = new Vector3(st.cx, 0, s * 7f);
            root.localEulerAngles = new Vector3(0, s > 0 ? 0 : 180, 0);
            // yerel çerçeve: x ∈ [-11,11], z ∈ [0,18] (z=0 vitrin, +z içeri)
            float hw = StoreW / 2;
            var theme = ThemeMaterials(st.name, index);

            NoShadow(Box(root, "Floor", new Vector3(StoreW, 0.02f, StoreD), new Vector3(0, 0.011f, StoreD / 2), M(st.floorMat)));
            Box(root, "WallL", new Vector3(0.2f, StoreH, StoreD), new Vector3(-hw + 0.1f, StoreH / 2, StoreD / 2), M(st.wallMat), default, true);
            Box(root, "WallR", new Vector3(0.2f, StoreH, StoreD), new Vector3(hw - 0.1f, StoreH / 2, StoreD / 2), M(st.wallMat), default, true);
            Box(root, "WallBack", new Vector3(StoreW + 0.4f, StoreH, 0.2f), new Vector3(0, StoreH / 2, StoreD + 0.1f), M(st.wallMat), default, true);
            // süpürgelik
            Box(root, "Skirting", new Vector3(StoreW, 0.15f, 0.05f), new Vector3(0, 0.075f, StoreD - 0.03f), M("M_MallTrim"));

            // vitrin
            foreach (int side in new[] { -1, 1 })
            {
                float cx = side * 7.45f;
                Box(root, "WindowBase", new Vector3(6.9f, 0.45f, 0.3f), new Vector3(cx, 0.225f, 0), M("M_Fascia"), default, true);
                NoShadow(Box(root, "WindowGlass", new Vector3(6.9f, 3.35f, 0.05f), new Vector3(cx, 0.45f + 1.675f, 0), M("M_Glass"), default, true));
                // vitrin teşhiri
                Box(root, "WindowPodium", new Vector3(5f, 0.35f, 1.4f), new Vector3(cx, 0.175f, 1.2f), M("M_White"), default, true);
                for (int k = 0; k < 3; k++)
                    DisplayCopy(st.products[(k + (side > 0 ? 1 : 0)) % st.products.Length], root, new Vector3(cx - 1.6f + k * 1.6f, 0.36f, 1.2f), Random.Range(-20f, 20f), 1.1f);
            }
            foreach (int side in new[] { -1, 1 })
            {
                var spot = SpotL(root, new Vector3(side * 7.45f, 4.6f, 2.6f), new Vector3(side * 7.45f, 0.4f, 1.2f), st.lightColor, 9f, 7f, 55f, false);
                spot.transform.SetParent(lightsRoot, true);
            }
            foreach (float x in new[] { -hw - 0.1f, -7.45f, -4f, 4f, 7.45f, hw + 0.1f })
                Box(root, "Mullion", new Vector3(0.14f, 3.8f, 0.16f), new Vector3(x, 1.9f, 0), M("M_Chrome"));
            Box(root, "Fascia", new Vector3(StoreW + 0.4f, 1.2f, 0.4f), new Vector3(0, 4.4f, 0), M("M_Fascia"), default, true);
            NoShadow(Box(root, "FasciaNeon", new Vector3(StoreW + 0.4f, 0.07f, 0.42f), new Vector3(0, 3.83f, 0), M(st.neonMat)));
            NoShadow(Box(root, "FasciaNeonTop", new Vector3(StoreW + 0.4f, 0.05f, 0.42f), new Vector3(0, 4.98f, 0), M(st.neonMat)));
            Text3D(root, st.name, new Vector3(0, 4.5f, -0.22f), Vector3.zero, 0.05f, st.neonColor, true);
            Text3D(root, st.subtitle, new Vector3(0, 4.05f, -0.22f), Vector3.zero, 0.016f, Color.white);
            Text3D(root, st.name, new Vector3(0, 4.4f, 0.22f), new Vector3(0, 180, 0), 0.03f, st.neonColor, true); // içeriden

            // raflar (mağaza temasına göre renklendirilir)
            foreach (float x in new[] { -7f, 0f, 7f })
            {
                var ws = Place(P("Props/WallShelf"), root, new Vector3(x, 0, StoreD - 0.35f), 0);
                RestrictSpawns(ws, st.products); ApplyTheme(ws, theme);
            }
            foreach (float z in new[] { 7.6f, 13.8f })
            {
                var a = Place(P("Props/WallShelf"), root, new Vector3(-hw + 0.35f, 0, z), -90);
                var b = Place(P("Props/WallShelf"), root, new Vector3(hw - 0.35f, 0, z), 90);
                RestrictSpawns(a, st.products); RestrictSpawns(b, st.products);
                ApplyTheme(a, theme); ApplyTheme(b, theme);
            }
            foreach (float x in new[] { -4f, 4f })
            {
                var g = Place(P("Props/Gondola"), root, new Vector3(x, 0, 11.5f), 90);
                RestrictSpawns(g, st.products); ApplyTheme(g, theme);
            }
            var dt = Place(P("Props/DisplayTable"), root, new Vector3(0, 0, 5.2f), 0);
            RestrictSpawns(dt, st.products); ApplyTheme(dt, theme);

            // kasa (girişin yanında, müşteri tarafı mağaza ortasına bakar)
            var co = Place(P("Props/Checkout"), root, new Vector3(8.6f, 0, 3.6f), 180);
            ApplyTheme(co, theme);
            foreach (var tm in co.GetComponentsInChildren<TextMesh>()) tm.text = "KASA";
            var zoneGo = new GameObject("CheckoutZone");
            zoneGo.transform.SetParent(root, false);
            zoneGo.transform.localPosition = new Vector3(6.85f, 0, 3.6f);
            var zone = zoneGo.AddComponent<CheckoutZone>();
            zone.size = new Vector3(2.3f, 3f, 3.4f);
            // koyu kauçuk paspas + parlayan çerçeve (çerçeve teslimde yanıp söner)
            NoShadow(Box(zoneGo.transform, "ZoneMat", new Vector3(2.2f, 0.012f, 3.3f), new Vector3(0, 0.017f, 0), M("M_ZoneMat")));
            var border = new GameObject("ZoneBorder").transform;
            border.SetParent(zoneGo.transform, false);
            NoShadow(Box(border, "B1", new Vector3(2.3f, 0.02f, 0.09f), new Vector3(0, 0.03f, 1.66f), M("M_ZoneGlow")));
            NoShadow(Box(border, "B2", new Vector3(2.3f, 0.02f, 0.09f), new Vector3(0, 0.03f, -1.66f), M("M_ZoneGlow")));
            NoShadow(Box(border, "B3", new Vector3(0.09f, 0.02f, 3.4f), new Vector3(1.11f, 0.03f, 0), M("M_ZoneGlow")));
            NoShadow(Box(border, "B4", new Vector3(0.09f, 0.02f, 3.4f), new Vector3(-1.11f, 0.03f, 0), M("M_ZoneGlow")));
            zone.highlightRoot = border;
            Text3D(zoneGo.transform, "KASA", new Vector3(0, 0.04f, 0), new Vector3(90, -90, 0), 0.04f, new Color(0.75f, 1f, 0.8f, 0.9f));
            if (lib != null && lib.npcModels.Count > 0)
            {
                var cashier = (GameObject)PrefabUtility.InstantiatePrefab(lib.npcModels[(index * 5 + 3) % lib.npcModels.Count]);
                cashier.name = "Cashier";
                cashier.transform.SetParent(root, false);
                cashier.transform.localPosition = new Vector3(9.7f, 0, 2.6f);
                cashier.transform.localEulerAngles = new Vector3(0, -90, 0);
                foreach (var an in cashier.GetComponentsInChildren<Animator>()) an.cullingMode = AnimatorCullingMode.CullCompletely;
            }

            // tavan: karo + gömme spotlar; ışık aşağı bakan spotlardan gelir (tavanda leke yapmaz)
            NoShadow(Box(root, "Ceiling", new Vector3(StoreW - 0.4f, 0.02f, StoreD - 0.2f), new Vector3(0, StoreH - 0.02f, StoreD / 2 + 0.05f), M("M_CeilingTile")));
            foreach (float x in new[] { -7.5f, -2.5f, 2.5f, 7.5f })
                foreach (float z in new[] { 4.5f, 9.5f, 14.5f })
                {
                    NoShadow(Cyl(root, "Downlight", 0.17f, 0.03f, new Vector3(x, StoreH - 0.04f, z), M("M_Downlight"), default, 12));
                    NoShadow(Pipe(root, "DownlightRim", 0.2f, 0.03f, 0.035f, new Vector3(x, StoreH - 0.045f, z), M("M_Chrome")));
                }
            foreach (float x in new[] { -5f, 5f })
                foreach (float z in new[] { 6.5f, 13f })
                {
                    var l = SpotL(root, new Vector3(x, StoreH - 0.15f, z), new Vector3(x, 0f, z + 0.01f), st.lightColor, 7.5f, 7.5f, 125f, false);
                    l.innerSpotAngle = 70f;
                    l.transform.SetParent(lightsRoot, true);
                }
            var feat = SpotL(root, new Vector3(0, StoreH - 0.15f, 7.2f), new Vector3(0, 0.8f, 5.2f), st.lightColor, 9f, 8f, 55f, false);
            feat.transform.SetParent(lightsRoot, true);

            // yansıma probu
            Probe(probeRoot, root.TransformPoint(new Vector3(0, 2.5f, StoreD / 2)), new Vector3(StoreW, StoreH, StoreD), probes);

            st.decor?.Invoke(root, st);

            // raf stoku (koliler): mağaza dolu görünsün
            BuildStoreStock(root, st.name, index);
        }

        // ---------------------------------------------------------------- mağaza dekorları
        static void DecorTech(Transform root, StoreDef st)
        {
            NoShadow(Box(root, "VideoWall", new Vector3(19f, 1.9f, 0.06f), new Vector3(0, 3.7f, StoreD - 0.08f), M("M_ScreenWall")));
            Text3D(root, "4K ULTRA HD  %60", new Vector3(0, 3.7f, StoreD - 0.14f), new Vector3(0, 0, 0), 0.04f, Color.white, true);
            // tavan neon şeritleri
            foreach (float x in new[] { -8f, 8f })
                NoShadow(Box(root, "NeonStrip", new Vector3(0.08f, 0.05f, 14f), new Vector3(x, StoreH - 0.04f, 9f), M("M_Neon_Cyan")));
        }

        static void DecorToy(Transform root, StoreDef st)
        {
            string[] cols = { "M_Red", "M_Yellow", "M_Cyan", "M_Pink", "M_Green", "M_Purple" };
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(-8.5f + Random.Range(-1f, 1f), 3.6f + Random.Range(-0.3f, 0.6f), 2.5f + Random.Range(-1f, 1.5f));
                if (i >= 5) p.x = -p.x;
                NoShadow(Ball(root, "Balloon", 0.32f, p, M(cols[i % cols.Length]), new Vector3(1f, 1.2f, 1f)));
                NoShadow(Box(root, "String", new Vector3(0.01f, 1.4f, 0.01f), p + Vector3.down * 1.05f, M("M_White")));
            }
            // dev ayıcık
            var bear = DisplayCopy(5, root, new Vector3(-9f, 0, 5.5f), 160f, 4f);
            if (bear != null) { var c = bear.AddComponent<CapsuleCollider>(); c.center = new Vector3(0, 0.22f, 0); c.radius = 0.16f; c.height = 0.5f; }
            Text3D(root, "OYUNCAK ŞÖLENİ", new Vector3(0, 3.9f, StoreD - 0.25f), Vector3.zero, 0.035f, new Color(1f, 0.4f, 0.75f), true);
        }

        static void DecorSport(Transform root, StoreDef st)
        {
            NoShadow(Box(root, "TrackStripe", new Vector3(1.2f, 0.015f, 16f), new Vector3(0, 0.025f, 9f), M("M_Orange")));
            Text3D(root, "KOŞ! ZIPLA! KAZAN!", new Vector3(0, 3.9f, StoreD - 0.25f), Vector3.zero, 0.035f, new Color(1f, 0.6f, 0.15f), true);
            Place(P("Props/Bench"), root, new Vector3(-6f, 0, 3.2f), 0);
            Place(P("Props/Bench"), root, new Vector3(0, 0, 8.2f), 90);
        }

        static void DecorKitchen(Transform root, StoreDef st)
        {
            var island = Box(root, "KitchenIsland", new Vector3(3.6f, 0.9f, 1.4f), new Vector3(0, 0.45f, 8.2f), M("M_White"), default, true);
            Box(root, "IslandTop", new Vector3(3.8f, 0.06f, 1.6f), new Vector3(0, 0.93f, 8.2f), M("M_Wood"), default, true);
            foreach (float x in new[] { -1.2f, 0f, 1.2f })
            {
                var sp = new GameObject("SP").transform; sp.SetParent(root, false);
                sp.localPosition = new Vector3(x, 0.965f, 8.2f);
                sp.gameObject.AddComponent<ShelfSpawnPoint>().allowedProducts = st.products;
                var pend = Place(P("Props/Pendant"), root, new Vector3(x, 3.3f, 8.2f), 0);
                pend.transform.localScale = Vector3.one * 0.5f;
            }
            Text3D(root, "MUTFAKTA BLACK FRIDAY", new Vector3(0, 3.9f, StoreD - 0.25f), Vector3.zero, 0.03f, new Color(1f, 0.35f, 0.25f), true);
        }

        static void DecorFashion(Transform root, StoreDef st)
        {
            foreach (var p in new[] { new Vector3(-8f, 0, 3.5f), new Vector3(-8f, 0, 6.2f), new Vector3(6.5f, 0, 15.6f), new Vector3(-6.5f, 0, 15.6f) })
                Place(P("Props/ClothesRack"), root, p, p.z > 10 ? 0 : 90);
            foreach (float x in new[] { -3.5f, 3.5f })
                Box(root, "Mirror", new Vector3(1.2f, 2.2f, 0.05f), new Vector3(x, 1.4f, StoreD - 0.06f), M("M_Mirror"));
            Text3D(root, "YENİ SEZON %70", new Vector3(0, 3.9f, StoreD - 0.25f), Vector3.zero, 0.035f, new Color(1f, 0.35f, 0.85f), true);
        }

        static void DecorBeauty(Transform root, StoreDef st)
        {
            foreach (var p in new[] { new Vector3(-7f, 0, 6f), new Vector3(-7f, 0, 9.5f) })
                RestrictSpawns(Place(P("Props/GlassCounter"), root, p, 90), st.products);
            NoShadow(Box(root, "GoldStripe", new Vector3(StoreW - 0.4f, 0.06f, 0.05f), new Vector3(0, 2.7f, StoreD - 0.05f), M("M_Neon_Gold")));
            Text3D(root, "IŞILTI SENİN HAKKIN", new Vector3(0, 3.9f, StoreD - 0.25f), Vector3.zero, 0.03f, new Color(1f, 0.8f, 0.35f), true);
        }
    }
}
