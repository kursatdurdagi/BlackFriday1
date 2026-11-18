using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace BlackFriday.EditorTools
{
    /// <summary>
    /// Black Friday içerik üreticisi: materyaller, ProBuilder modelleri, karakter prefabları,
    /// animatör, sahneler. Menü: BlackFriday/...
    /// </summary>
    public static partial class BFBuilder
    {
        public const string R = "Assets/BlackFriday";

        [MenuItem("BlackFriday/HEPSİNİ KUR (Build All)", priority = 0)]
        public static void BuildAll()
        {
            EnsureFolders();
            EnsureLayersAndRendering();
            ConvertToonMaterials();
            StylizeToonMaterials();
            BuildMaterials();
            BuildAnimator();
            BuildCharacters();
            BuildProducts();
            BuildNetworkPrefabs();
            BuildPropPrefabs();
            BuildMallProps();
            BuildLevel01();
            BuildMainMenu();
            SetupBuildSettings();
            Debug.Log("[BF] Hepsi kuruldu. MainMenu sahnesini açıp Play'e bas.");
        }

        public static void EnsureFolders()
        {
            foreach (var f in new[] { "Materials", "Textures", "Meshes", "Prefabs", "Prefabs/Characters", "Prefabs/Products", "Prefabs/Props", "Animation", "Scenes", "Resources", "UI", "Settings" })
            {
                string full = R + "/" + f;
                if (!AssetDatabase.IsValidFolder(full))
                {
                    string parent = Path.GetDirectoryName(full).Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(full));
                }
            }
        }

        // =====================================================================
        // 1) ToonPeople materyallerini URP Lit'e çevir
        // =====================================================================
        [MenuItem("BlackFriday/1 - ToonPeople materyalleri -> URP")]
        public static void ConvertToonMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ToonPeople" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.EndsWith(".mat")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) continue;
                string sn = m.shader.name;
                if (!(sn.StartsWith("Standard") || sn.StartsWith("Legacy Shaders") || sn.Contains("InternalErrorShader"))) continue;

                Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                Vector2 scale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
                Vector2 offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
                Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0;
                float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;

                m.shader = lit;
                m.SetTexture("_BaseMap", main);
                m.SetTextureScale("_BaseMap", scale);
                m.SetTextureOffset("_BaseMap", offset);
                m.SetColor("_BaseColor", col);
                if (bump != null) m.SetTexture("_BumpMap", bump);
                m.SetFloat("_Smoothness", 0.18f);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_EnvironmentReflections", 0f);
                if (mode == 1) { m.SetFloat("_Surface", 0); m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", cutoff); }
                else if (mode >= 2) { m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_AlphaClip", 0); }
                else { m.SetFloat("_Surface", 0); m.SetFloat("_AlphaClip", 0); }
                BaseShaderGUI.SetMaterialKeywords(m);
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BF] {n} ToonPeople materyali URP Lit'e çevrildi.");
        }

        /// <summary>ToonPeople opak materyallerini stilize shader'a geçirir (dış hat + toon ışık).</summary>
        [MenuItem("BlackFriday/1b - ToonPeople -> Stilize shader")]
        public static void StylizeToonMaterials()
        {
            var sty = Shader.Find("BlackFriday/StylizedLit");
            if (sty == null) { Debug.LogWarning("[BF] Stilize shader yok"); return; }
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ToonPeople" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.EndsWith(".mat")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) continue;
                if (m.shader.name == "Universal Render Pipeline/Lit")
                {
                    if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) continue; // şeffaf (gözlük camı vb.)
                    if (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) continue;
                }
                else if (m.shader != sty) continue;
                var tex = m.GetTexture("_BaseMap");
                var col = m.GetColor("_BaseColor");
                m.shader = sty;
                m.shaderKeywords = new string[0];
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", new Color(col.r, col.g, col.b, 1));
                m.SetTexture("_NoiseTex", PaintNoise());
                m.SetFloat("_NoiseStrength", 0.02f);
                m.SetFloat("_Smoothness", 0.18f);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_SpecStrength", 0.1f);
                m.SetFloat("_RimStrength", 0.12f);
                m.SetFloat("_RampSmooth", 0.12f);
                m.SetFloat("_GradientStrength", 0.06f);
                m.SetFloat("_EnvStrength", 0.15f);
                m.SetTexture("_EmissionMap", null);
                m.SetColor("_EmissionColor", Color.black);
                string ln = m.name.ToLowerInvariant();
                bool noOutline = ln.Contains("eye") || ln.Contains("teeth") || ln.Contains("brow") || ln.Contains("lash") || ln.Contains("glasses");
                m.SetFloat("_OutlineWidth", noOutline ? 0f : 1.0f);
                m.SetColor("_OutlineColor", new Color(0.09f, 0.06f, 0.07f, 1));
                m.SetShaderPassEnabled("BFOutline", !noOutline);
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BF] {n} ToonPeople materyali stilize shader'a geçirildi.");
        }

        // =====================================================================
        // Materyaller & dokular (ToonPeople'ın parlak, düz toon paletine uygun)
        // =====================================================================
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static Material M(string name)
        {
            if (mats.TryGetValue(name, out var m) && m != null) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>($"{R}/Materials/{name}.mat");
            if (m == null) { BuildMaterials(); m = AssetDatabase.LoadAssetAtPath<Material>($"{R}/Materials/{name}.mat"); }
            mats[name] = m;
            return m;
        }

        [MenuItem("BlackFriday/2 - Materyaller ve dokular")]
        public static void BuildMaterials()
        {
            EnsureFolders();
            mats.Clear();
            var floor = Tex("T_FloorTile", 256, (u, v) =>
            {
                float gu = Mathf.Abs(Frac(u * 2) - 0.5f), gv = Mathf.Abs(Frac(v * 2) - 0.5f);
                bool grout = gu > 0.485f || gv > 0.485f;
                bool alt = ((int)(u * 2) + (int)(v * 2)) % 2 == 0;
                float n = Mathf.PerlinNoise(u * 12, v * 12) * 0.05f;
                Color c = alt ? new Color(0.95f, 0.93f, 0.88f) : new Color(0.88f, 0.86f, 0.82f);
                return grout ? new Color(0.7f, 0.68f, 0.64f) : c - new Color(n, n, n, 0);
            });
            var wall = Tex("T_Wall", 128, (u, v) =>
            {
                float s = Frac(u * 4) < 0.5f ? 0f : 0.04f;
                return new Color(1f - s, 0.86f - s, 0.58f - s);
            });
            var card = Tex("T_Cardboard", 128, (u, v) =>
            {
                float n = Mathf.PerlinNoise(u * 20, v * 3) * 0.08f;
                bool tape = Mathf.Abs(u - 0.5f) < 0.08f;
                return tape ? new Color(0.86f, 0.74f, 0.55f) : new Color(0.76f - n, 0.58f - n, 0.38f - n);
            });
            var wood = Tex("T_Wood", 128, (u, v) =>
            {
                float g = Mathf.PerlinNoise(u * 3, v * 30) * 0.5f + Mathf.Sin(v * 60 + Mathf.PerlinNoise(u * 4, v * 4) * 6) * 0.05f;
                return new Color(0.86f - g * 0.15f, 0.64f - g * 0.15f, 0.40f - g * 0.12f);
            });
            var asphalt = Tex("T_Asphalt", 128, (u, v) =>
            {
                float n = Mathf.PerlinNoise(u * 40, v * 40) * 0.12f + Random.value * 0.04f;
                return new Color(0.26f + n, 0.27f + n, 0.3f + n);
            });
            var rubber = Tex("T_Conveyor", 64, (u, v) =>
            {
                bool line = Frac(v * 8) < 0.12f;
                return line ? new Color(0.25f, 0.25f, 0.28f) : new Color(0.12f, 0.12f, 0.14f);
            });
            var stripes = Tex("T_SaleStripes", 128, (u, v) =>
            {
                return Frac((u + v) * 4) < 0.5f ? new Color(1f, 0.82f, 0.1f) : new Color(0.08f, 0.08f, 0.1f);
            });

            Lit("M_Floor", Color.white, floor, 0.25f);
            Lit("M_Wall", Color.white, wall, 0.1f);
            Lit("M_WallTrim", new Color(0.93f, 0.22f, 0.27f));
            Lit("M_ShelfMetal", new Color(0.86f, 0.88f, 0.92f), null, 0.4f, 0.2f);
            Lit("M_ShelfBoard", new Color(0.97f, 0.97f, 0.97f), null, 0.3f);
            Lit("M_Wood", Color.white, wood, 0.15f);
            Lit("M_Cardboard", Color.white, card, 0.05f);
            Lit("M_Asphalt", Color.white, asphalt, 0.05f);
            Lit("M_Conveyor", Color.white, rubber, 0.2f);
            Lit("M_SaleStripes", Color.white, stripes, 0.2f);
            Lit("M_CounterBlue", new Color(0.22f, 0.45f, 0.9f));
            Lit("M_White", new Color(0.97f, 0.97f, 0.97f));
            Lit("M_Black", new Color(0.09f, 0.09f, 0.11f), null, 0.5f);
            Lit("M_DarkGrey", new Color(0.25f, 0.26f, 0.3f), null, 0.4f);
            Lit("M_Yellow", new Color(1f, 0.82f, 0.12f));
            Lit("M_Red", new Color(0.95f, 0.22f, 0.25f));
            Lit("M_Green", new Color(0.3f, 0.8f, 0.35f));
            Lit("M_Cyan", new Color(0.2f, 0.8f, 0.95f));
            Lit("M_Pink", new Color(1f, 0.45f, 0.7f));
            Lit("M_Orange", new Color(1f, 0.55f, 0.15f));
            Lit("M_Purple", new Color(0.55f, 0.35f, 0.9f));
            Lit("M_Brown", new Color(0.62f, 0.4f, 0.25f), null, 0.05f);
            Lit("M_LightBrown", new Color(0.88f, 0.72f, 0.52f), null, 0.05f);
            Lit("M_Plant", new Color(0.25f, 0.68f, 0.3f));
            Lit("M_Concrete", new Color(0.6f, 0.6f, 0.64f), null, 0.1f);
            Lit("M_Magenta", new Color(0.88f, 0.22f, 0.58f), null, 0.45f);
            Lit("M_MagentaDark", new Color(0.55f, 0.1f, 0.35f), null, 0.5f);
            Lit("M_Gold", new Color(1f, 0.78f, 0.3f), null, 0.75f, 0.85f);
            PbrLit("M_GlassPink", new Color(1f, 0.75f, 0.9f, 0.45f), 0.95f, true);
            Lit("M_PerfumeLiquid", new Color(0.95f, 0.45f, 0.75f), null, 0.9f);
            BuildMallMaterials();
            Lit("M_Pot", new Color(0.82f, 0.45f, 0.3f));
            Lit("M_Chrome", new Color(0.8f, 0.82f, 0.86f), null, 0.85f, 0.9f);
            var glow = Lit("M_ScreenGlow", new Color(0.25f, 0.75f, 1f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.3f, 0.9f, 1.4f));
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            var panel = Lit("M_LightPanel", new Color(1f, 1f, 0.95f));
            panel.EnableKeyword("_EMISSION");
            panel.SetColor("_EmissionColor", new Color(0.8f, 0.78f, 0.72f));
            var neon = Lit("M_NeonYellow", new Color(1f, 0.85f, 0.2f));
            neon.EnableKeyword("_EMISSION");
            neon.SetColor("_EmissionColor", new Color(1.1f, 0.85f, 0.15f));
            PbrLit("M_Glass", new Color(0.72f, 0.88f, 1f, 0.22f), 0.95f, true);
            // ürün ekranları (stilize görüntü dokuları, parlayan)
            var tvTex = Tex("T_TVScreen", 256, TVScreenPixel);
            var tv = Lit("M_TVScreen", Color.white, tvTex, 0.9f);
            tv.SetTexture("_EmissionMap", tvTex); tv.SetColor("_EmissionColor", new Color(1.3f, 1.3f, 1.3f));
            var phTex = Tex("T_PhoneScreen", 128, PhoneScreenPixel);
            var ph = Lit("M_PhoneScreen", Color.white, phTex, 0.9f);
            ph.SetTexture("_EmissionMap", phTex); ph.SetColor("_EmissionColor", new Color(1.2f, 1.2f, 1.2f));
            var shoeTex = Tex("T_ShoeLid", 128, (u, v) =>
            {
                float band = Mathf.Abs(v - (0.35f + 0.25f * Mathf.Sin(u * 3.2f)));
                return band < 0.07f ? new Color(1f, 1f, 1f) : (band < 0.09f ? new Color(0.15f, 0.15f, 0.18f) : new Color(1f, 0.55f, 0.15f));
            });
            Lit("M_ShoeBoxArt", Color.white, shoeTex, 0.25f);
            var teddy = Lit("M_Teddy", new Color(0.62f, 0.4f, 0.25f), null, 0.05f);
            teddy.SetFloat("_NoiseStrength", 0.22f); teddy.SetFloat("_NoiseScale", 6f); teddy.SetFloat("_RampSmooth", 0.3f);
            Unlit("M_ZoneGlow", new Color(0.25f, 1f, 0.5f, 0.85f), true);
            Unlit("M_PlayerRing", Color.white, false);
            Unlit("M_LineWhite", new Color(0.95f, 0.95f, 0.95f), false);
            Unlit("M_ArcLine", new Color(1f, 0.85f, 0.15f, 0.85f), true);
            AssetDatabase.SaveAssets();
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        static Texture2D Tex(string name, int size, System.Func<float, float, Color> f)
        {
            string path = $"{R}/Textures/{name}.png";
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f(x / (float)size, y / (float)size);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.anisoLevel = 4;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // dış hat çizilmeyecek büyük yüzeyler (mimari) ve ışık/neon yüzeyleri
        static readonly HashSet<string> Architecture = new HashSet<string>
        {
            "M_Floor", "M_Wall", "M_Marble", "M_WoodFloor", "M_Carpet", "M_DarkTile", "M_SportFloor", "M_ToyFloor", "M_MallWall", "M_MallTrim",
            "M_Asphalt", "M_Concrete", "M_LightPanel", "M_ScreenWall", "M_Shutter", "M_Fascia", "M_Water", "M_Bulb", "M_Mirror", "M_WallTrim",
            "M_ScreenGlow", "M_TVScreen", "M_PhoneScreen", "M_NeonYellow", "M_CeilingTile", "M_Downlight", "M_ZoneMat"
        };

        static Texture2D paintNoise;
        static Texture2D PaintNoise()
        {
            if (paintNoise != null) return paintNoise;
            string path = $"{R}/Textures/T_PaintNoise.png";
            paintNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (paintNoise != null) return paintNoise;
            paintNoise = Tex("T_PaintNoise", 256, (u, v) =>
            {
                // fırça darbesi hissi: uzatılmış perlin + yumuşak lekeler
                float a = Mathf.PerlinNoise(u * 3f + 11f, v * 14f + 3f);
                float b = Mathf.PerlinNoise(u * 9f + 5f, v * 4f + 17f);
                float c = Mathf.PerlinNoise(u * 26f, v * 26f);
                float n = a * 0.5f + b * 0.3f + c * 0.2f;
                return new Color(n, n, n, 1);
            });
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Mirror;
            imp.sRGBTexture = false;
            imp.SaveAndReimport();
            paintNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return paintNoise;
        }

        /// <summary>Stilize (toon) materyal.</summary>
        static Material Lit(string name, Color c, Texture tex = null, float smooth = 0.2f, float metal = 0f)
        {
            string path = $"{R}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("BlackFriday/StylizedLit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.shaderKeywords = new string[0];
            m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1));
            m.SetTexture("_BaseMap", tex);
            // mat görünüm: parlaklık ve yansıma kısık
            m.SetFloat("_Smoothness", smooth * 0.55f);
            m.SetFloat("_Metallic", metal * 0.6f);
            m.SetFloat("_EnvStrength", 0.3f);
            m.SetTexture("_NoiseTex", PaintNoise());
            m.SetTexture("_EmissionMap", null);
            m.SetColor("_EmissionColor", Color.black);
            bool arch = Architecture.Contains(name) || name.StartsWith("M_Wall_") || name.StartsWith("M_Neon_");
            m.SetFloat("_OutlineWidth", arch ? 0f : 1.1f);
            m.SetColor("_OutlineColor", new Color(c.r * 0.25f + 0.03f, c.g * 0.22f + 0.02f, c.b * 0.28f + 0.04f, 1));
            m.SetShaderPassEnabled("BFOutline", !arch);
            m.SetFloat("_NoiseStrength", arch ? 0.045f : 0.08f);
            m.SetFloat("_NoiseScale", arch ? 0.35f : 1.2f);
            m.SetFloat("_GradientStrength", arch ? 0.12f : 0.1f);
            m.SetFloat("_SpecStrength", 0.08f + smooth * 0.22f);
            m.SetFloat("_RampSmooth", 0.16f);
            m.SetFloat("_RimStrength", arch ? 0.03f : 0.09f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        /// <summary>Fiziksel (PBR) URP Lit — cam gibi şeffaf yüzeyler için.</summary>
        static Material PbrLit(string name, Color c, float smooth, bool transparent)
        {
            string path = $"{R}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", null);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Surface", transparent ? 1 : 0);
            m.SetFloat("_Blend", 0);
            BaseShaderGUI.SetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static Color TVScreenPixel(float u, float v)
        {
            // stilize gün batımı: gökyüzü degrade, güneş, dağlar, su yansıması
            Color top = new Color(0.22f, 0.15f, 0.5f), hor = new Color(1f, 0.55f, 0.35f);
            Color c = Color.Lerp(hor, top, Mathf.Clamp01((v - 0.45f) / 0.55f));
            float sun = new Vector2(u - 0.5f, (v - 0.5f) * 0.6f).magnitude;
            if (v > 0.45f && sun < 0.12f) c = Color.Lerp(new Color(1f, 0.9f, 0.5f), c, Mathf.Clamp01((sun - 0.1f) / 0.02f));
            float m1 = 0.45f + 0.08f * Mathf.Sin(u * 9f) + 0.05f * Mathf.Sin(u * 23f + 1f);
            float m2 = 0.42f + 0.06f * Mathf.Sin(u * 6f + 2f);
            if (v < m1 && v > 0.4f) c = new Color(0.35f, 0.18f, 0.4f);
            if (v < m2 && v > 0.4f) c = new Color(0.2f, 0.1f, 0.28f);
            if (v <= 0.4f) { c = Color.Lerp(new Color(0.15f, 0.2f, 0.45f), hor, Mathf.Clamp01(v / 0.4f) * 0.6f); if (Mathf.Abs(u - 0.5f) < 0.08f * v / 0.4f && Mathf.Sin(v * 90f) > 0.3f) c = new Color(1f, 0.75f, 0.45f); }
            if (u < 0.015f || u > 0.985f || v < 0.02f || v > 0.98f) c = new Color(0.05f, 0.05f, 0.07f);
            return c;
        }

        static Color PhoneScreenPixel(float u, float v)
        {
            Color c = Color.Lerp(new Color(0.95f, 0.4f, 0.65f), new Color(0.3f, 0.45f, 1f), v);
            float gu = Frac(u * 4f), gv = Frac(v * 6f);
            if (v < 0.85f && v > 0.1f && gu > 0.18f && gu < 0.82f && gv > 0.2f && gv < 0.8f) c = Color.Lerp(c, Color.white, 0.75f);
            return c;
        }

        static Material Unlit(string name, Color c, bool transparent)
        {
            string path = $"{R}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", transparent ? 1 : 0);
            m.SetFloat("_Blend", 0);
            BaseShaderGUI.SetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        // =====================================================================
        // ProBuilder yardımcıları
        // =====================================================================
        static GameObject Finish(ProBuilderMesh pb, string name, Transform parent, Vector3 pos, Vector3 euler, Material mat)
        {
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localPosition = pos;
            pb.transform.localEulerAngles = euler;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
            pb.ToMesh();
            pb.Refresh();
            return pb.gameObject;
        }

        public static GameObject Box(Transform parent, string name, Vector3 size, Vector3 center, Material mat, Vector3 euler = default, bool collider = false, bool fitUV = false)
        {
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            BevelAndSmooth(pb, size);
            if (fitUV)
            {
                foreach (var f in pb.faces) { var uv = f.uv; uv.fill = AutoUnwrapSettings.Fill.Stretch; f.uv = uv; }
            }
            var go = Finish(pb, name, parent, center, euler, mat);
            if (collider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = Vector3.zero; bc.size = size;
            }
            return go;
        }

        /// <summary>Stilize görünüm: kenarları pahla, pah yüzeylerini yumuşat (ışığı yakalayan yuvarlak kenarlar).</summary>
        static void BevelAndSmooth(ProBuilderMesh pb, Vector3 size)
        {
            float minDim = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            if (minDim < 0.03f) return;
            float amt = Mathf.Min(0.04f, minDim * 0.16f);
            try
            {
                var edges = new List<Edge>();
                foreach (var f in pb.faces) edges.AddRange(f.edges);
                Bevel.BevelEdges(pb, edges, amt);
                Smoothing.ApplySmoothingGroups(pb, pb.faces, 50f);
                pb.ToMesh();
                pb.Refresh();
            }
            catch (System.Exception e) { Debug.LogWarning("[BF] pah uygulanamadı: " + e.Message); }
        }

        public static GameObject Cyl(Transform parent, string name, float radius, float height, Vector3 center, Material mat, Vector3 euler = default, int sides = 16)
        {
            return Finish(ShapeGenerator.GenerateCylinder(PivotLocation.Center, sides, radius, height, 0, 1), name, parent, center, euler, mat);
        }

        public static GameObject Pipe(Transform parent, string name, float radius, float height, float thickness, Vector3 center, Material mat, Vector3 euler = default)
        {
            return Finish(ShapeGenerator.GeneratePipe(PivotLocation.Center, radius, height, thickness, 24, 1), name, parent, center, euler, mat);
        }

        public static GameObject Ball(Transform parent, string name, float radius, Vector3 center, Material mat, Vector3 scale = default)
        {
            // çok küçük kürelerde normaller sıfıra yuvarlanıp NaN üretiyor: en az 5 cm yarıçapla üret, ölçekle küçült
            float genR = Mathf.Max(radius, 0.05f);
            var pb = ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, genR, radius < 0.045f ? 1 : 2, true, false);
            try { Smoothing.ApplySmoothingGroups(pb, pb.faces, 80f); } catch { }
            var go = Finish(pb, name, parent, center, Vector3.zero, mat);
            go.transform.localScale = (scale != default ? scale : Vector3.one) * (radius / genR);
            return go;
        }

        public static GameObject Cone(Transform parent, string name, float radius, float height, Vector3 center, Material mat)
        {
            return Finish(ShapeGenerator.GenerateCone(PivotLocation.Center, radius, height, 12), name, parent, center, Vector3.zero, mat);
        }

        /// <summary>Prefab için ProBuilder meshlerini kalıcı mesh asset'ine dönüştürür.</summary>
        static void BakeToAsset(GameObject root, string assetName)
        {
            string path = $"{R}/Meshes/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            Mesh container = null;
            int i = 0;
            foreach (var pb in root.GetComponentsInChildren<ProBuilderMesh>(true))
            {
                pb.ToMesh(); pb.Refresh();
                var go = pb.gameObject;
                var mf = go.GetComponent<MeshFilter>();
                var mesh = Object.Instantiate(mf.sharedMesh);
                mesh.name = go.name + "_" + (i++);
                if (container == null) { AssetDatabase.CreateAsset(mesh, path); container = mesh; }
                else AssetDatabase.AddObjectToAsset(mesh, container);
                foreach (var mb in go.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Namespace != null && mb.GetType().Namespace.StartsWith("UnityEngine.ProBuilder") && !(mb is ProBuilderMesh))
                        Object.DestroyImmediate(mb);
                Object.DestroyImmediate(pb);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var bc = go.GetComponent<BoxCollider>();
                if (bc != null) { bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
            }
            AssetDatabase.SaveAssets();
        }

        static Font font;
        public static TextMesh Text3D(Transform parent, string text, Vector3 pos, Vector3 euler, float size, Color c, bool neon = false)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Text_" + text.Replace(" ", "_"));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            tm.fontStyle = FontStyle.Bold;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(neon);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }

        static Material TextMaterial(bool neon)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string path = neon ? $"{R}/Materials/M_Text3DNeon.mat" : $"{R}/Materials/M_Text3D.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("BlackFriday/Text3D");
            if (sh == null) return font.material;
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.mainTexture = font.material.mainTexture;
            m.SetFloat("_Glow", neon ? 3.5f : 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void NewTempScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // =====================================================================
        // 4) Karakterler (ToonPeople prefablarından temiz model prefabları)
        // =====================================================================
        [MenuItem("BlackFriday/4 - Karakter prefabları")]
        public static void BuildCharacters()
        {
            EnsureFolders();
            NewTempScene();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(R + "/Animation/BF_Humanoid.controller");
            var sources = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ToonPeople/prefabs/142 prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Contains("PLAYER") && !p.Contains("SPAWNER"))
                .OrderBy(p => p).ToList();
            var elders = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ToonPeople/prefabs/71 elders" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();

            // isimler
            string[] maleNames = { "Kemal", "Murat", "Burak", "Emre" };
            string[] femaleNames = { "Zeynep", "Elif", "Ayşe", "Selin" };

            var lib = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(R + "/Resources/BF_CharacterLibrary.asset");
            if (lib == null) { lib = ScriptableObject.CreateInstance<CharacterLibrary>(); AssetDatabase.CreateAsset(lib, R + "/Resources/BF_CharacterLibrary.asset"); }
            lib.playerModels.Clear(); lib.playerModelNames.Clear(); lib.npcModels.Clear();
            lib.controller = ctrl;

            // oyuncular: her tipten (MaleA..D, FemaleA..D) birer tane
            int mi = 0, fi = 0;
            foreach (var type in new[] { "TPMaleA", "TPFemaleA", "TPMaleB", "TPFemaleB", "TPMaleC", "TPFemaleC", "TPMaleD", "TPFemaleD" })
            {
                var src = sources.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).StartsWith(type + " (3)")) ?? sources.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).StartsWith(type));
                if (src == null) continue;
                var pf = MakeCleanModel(src, $"{R}/Prefabs/Characters/BF_Player_{type}.prefab", ctrl);
                if (pf == null) continue;
                lib.playerModels.Add(pf);
                bool female = type.Contains("Female");
                lib.playerModelNames.Add(female ? femaleNames[fi++ % 4] : maleNames[mi++ % 4]);
            }
            // NPC: çeşitli müşteriler + yaşlılar
            int step = Mathf.Max(1, sources.Count / 14);
            for (int i = 0, k = 0; i < sources.Count && k < 14; i += step, k++)
            {
                var pf = MakeCleanModel(sources[i], $"{R}/Prefabs/Characters/BF_NPC_{k:00}.prefab", ctrl);
                if (pf != null) lib.npcModels.Add(pf);
            }
            int estep = Mathf.Max(1, elders.Count / 4);
            for (int i = 0, k = 0; i < elders.Count && k < 4; i += estep, k++)
            {
                var pf = MakeCleanModel(elders[i], $"{R}/Prefabs/Characters/BF_NPC_Elder_{k:00}.prefab", ctrl);
                if (pf != null) lib.npcModels.Add(pf);
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BF] {lib.playerModels.Count} oyuncu, {lib.npcModels.Count} NPC modeli hazır.");
        }

        static GameObject MakeCleanModel(string srcPath, string dstPath, RuntimeAnimatorController ctrl)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (src == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = Path.GetFileNameWithoutExtension(dstPath);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (var j in inst.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var anim = inst.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = false;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var pf = PrefabUtility.SaveAsPrefabAsset(inst, dstPath);
            Object.DestroyImmediate(inst);
            return pf;
        }
    }
}
