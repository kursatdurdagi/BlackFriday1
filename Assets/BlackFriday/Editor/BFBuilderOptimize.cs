using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlackFriday.EditorTools
{
    /// <summary>
    /// Performans araçları:
    /// - Ürün atlası: her ürün tek mesh + tek (atlas) materyal → 1 draw call.
    /// - Statik birleştirme: mağaza/koridor geometrisi bölge ve materyal bazında tek meshe birleşir.
    /// - Mağaza teması: raf/kasa materyallerini mağazaya özel renklere çevirir.
    /// </summary>
    public static partial class BFBuilder
    {
        // =====================================================================
        // Ürün atlası
        // =====================================================================
        const float AtlasEmission = 1.6f;

        static bool IsTransparent(Material m) =>
            m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit" && m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f;

        static Texture2D ReadableCopy(Texture src, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        class AtlasInfo
        {
            public Material material;
            public Dictionary<Material, Rect> rects = new Dictionary<Material, Rect>();
            public Dictionary<Material, bool> textured = new Dictionary<Material, bool>();
            public int width, height;
        }

        static AtlasInfo BuildProductAtlas(IEnumerable<Material> source)
        {
            var mats = source.Where(m => m != null && !IsTransparent(m)).Distinct().ToList();
            var tiles = new List<Texture2D>();
            var emissive = new List<Color>();
            var info = new AtlasInfo();
            foreach (var m in mats)
            {
                Color bc = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                Texture2D tile;
                if (tex != null)
                {
                    tile = ReadableCopy(tex, 128);
                    var px = tile.GetPixels();
                    for (int i = 0; i < px.Length; i++) { var c = px[i] * bc; c.a = 1; px[i] = c; }
                    tile.SetPixels(px); tile.Apply();
                    info.textured[m] = true;
                }
                else
                {
                    tile = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                    var px = new Color[256];
                    for (int i = 0; i < px.Length; i++) px[i] = new Color(bc.r, bc.g, bc.b, 1);
                    tile.SetPixels(px); tile.Apply();
                    info.textured[m] = false;
                }
                tiles.Add(tile);
                Color em = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                emissive.Add(em);
            }

            var atlas = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
            var rects = atlas.PackTextures(tiles.ToArray(), 4, 1024, false);
            info.width = atlas.width; info.height = atlas.height;

            // emisyon atlası (ekranlar, ışıklar)
            var emit = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
            var black = new Color[atlas.width * atlas.height];
            for (int i = 0; i < black.Length; i++) black[i] = Color.black;
            emit.SetPixels(black);
            for (int i = 0; i < mats.Count; i++)
            {
                info.rects[mats[i]] = rects[i];
                var em = emissive[i];
                if (em.maxColorComponent < 0.01f) continue;
                var px = tiles[i].GetPixels();
                Color k = em / AtlasEmission;
                for (int j = 0; j < px.Length; j++) { var c = px[j] * k; c.a = 1; px[j] = c; }
                int x = Mathf.RoundToInt(rects[i].x * atlas.width), y = Mathf.RoundToInt(rects[i].y * atlas.height);
                emit.SetPixels(x, y, tiles[i].width, tiles[i].height, px);
            }
            emit.Apply();

            string aPath = $"{R}/Textures/T_ProductAtlas.png", ePath = $"{R}/Textures/T_ProductAtlasEmit.png";
            File.WriteAllBytes(aPath, atlas.EncodeToPNG());
            File.WriteAllBytes(ePath, emit.EncodeToPNG());
            foreach (var t in tiles) Object.DestroyImmediate(t);
            Object.DestroyImmediate(atlas); Object.DestroyImmediate(emit);
            foreach (var p in new[] { aPath, ePath })
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(p);
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.mipmapEnabled = true;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(aPath);
            var emitTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ePath);
            var mat = Lit("M_ProductAtlas", Color.white, atlasTex, 0.35f);
            mat.SetTexture("_EmissionMap", emitTex);
            mat.SetColor("_EmissionColor", Color.white * AtlasEmission);
            mat.SetColor("_OutlineColor", new Color(0.08f, 0.07f, 0.09f));
            mat.SetFloat("_SpecStrength", 0.18f);
            EditorUtility.SetDirty(mat);
            info.material = mat;
            return info;
        }

        /// <summary>Ürün görselini (ProBuilder parçaları) tek mesh + atlas materyaline çevirir.</summary>
        static void CombineProductVisual(Transform visual, AtlasInfo atlas, string assetName)
        {
            var opaque = new List<CombineInstance>();
            var transparent = new Dictionary<Material, List<CombineInstance>>();
            var temp = new List<Mesh>();
            Matrix4x4 toLocal = visual.worldToLocalMatrix;
            float inset = 1.5f / Mathf.Max(atlas.width, 1);

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var pb = mr.GetComponent<UnityEngine.ProBuilder.ProBuilderMesh>();
                if (pb != null) { pb.ToMesh(); pb.Refresh(); }
                var m = mr.sharedMaterial;
                var mesh = Object.Instantiate(mf.sharedMesh);
                temp.Add(mesh);
                var ci = new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = toLocal * mr.localToWorldMatrix };
                if (IsTransparent(m))
                {
                    if (!transparent.TryGetValue(m, out var l)) transparent[m] = l = new List<CombineInstance>();
                    l.Add(ci);
                    continue;
                }
                if (m != null && atlas.rects.TryGetValue(m, out var rect))
                {
                    var uv = mesh.uv;
                    if (uv == null || uv.Length != mesh.vertexCount) uv = new Vector2[mesh.vertexCount];
                    bool tex = atlas.textured.TryGetValue(m, out var tx) && tx;
                    for (int i = 0; i < uv.Length; i++)
                    {
                        if (tex)
                        {
                            float u = Mathf.Clamp01(uv[i].x), v = Mathf.Clamp01(uv[i].y);
                            uv[i] = new Vector2(Mathf.Lerp(rect.xMin + inset, rect.xMax - inset, u), Mathf.Lerp(rect.yMin + inset, rect.yMax - inset, v));
                        }
                        else uv[i] = rect.center;
                    }
                    mesh.uv = uv;
                }
                opaque.Add(ci);
            }

            var parts = new List<Mesh>();
            var partMats = new List<Material>();
            if (opaque.Count > 0)
            {
                var om = new Mesh(); om.CombineMeshes(opaque.ToArray(), true, true);
                parts.Add(om); partMats.Add(atlas.material);
            }
            foreach (var kv in transparent)
            {
                var tm = new Mesh(); tm.CombineMeshes(kv.Value.ToArray(), true, true);
                parts.Add(tm); partMats.Add(kv.Key);
            }
            var final = new Mesh { name = assetName };
            var cis = parts.Select(p => new CombineInstance { mesh = p, transform = Matrix4x4.identity }).ToArray();
            final.CombineMeshes(cis, false, false);
            final.RecalculateBounds();
            final.Optimize();

            string path = $"{R}/Meshes/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(final, path);

            for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
            foreach (var t in temp) Object.DestroyImmediate(t);
            foreach (var p in parts) Object.DestroyImmediate(p);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = final;
            var r = visual.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = partMats.ToArray();
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        // =====================================================================
        // Statik geometri birleştirme
        // =====================================================================
        /// <summary>
        /// Bölgedeki statik MeshRenderer'ları materyal bazında birleştirir; orijinallerin sadece renderer'ı kapatılır
        /// (collider'lar, ProBuilder verisi ve NavMesh yerinde kalır).
        /// </summary>
        public static int MergeStatic(Transform chunk, string assetName)
        {
            var groups = new Dictionary<(Material, ShadowCastingMode, bool), List<CombineInstance>>();
            var vcount = new Dictionary<(Material, ShadowCastingMode, bool), int>();
            var disabled = new List<Renderer>();
            Matrix4x4 toLocal = chunk.worldToLocalMatrix;
            foreach (var mr in chunk.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!mr.enabled) continue;
                if (mr.GetComponent<TextMesh>() != null) continue;
                if (mr.GetComponentInParent<CheckoutZone>() != null) continue;
                if (mr.transform.parent != null && mr.transform.parent.name == "_Merged") continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var mats = mr.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    if (mats[s] == null) continue;
                    var key = (mats[s], mr.shadowCastingMode, mr.receiveShadows);
                    if (!groups.TryGetValue(key, out var list)) { groups[key] = list = new List<CombineInstance>(); vcount[key] = 0; }
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = toLocal * mr.localToWorldMatrix });
                    vcount[key] += mesh.vertexCount;
                }
                disabled.Add(mr);
            }
            if (groups.Count == 0) return 0;

            var old = chunk.Find("_Merged");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var parent = new GameObject("_Merged").transform;
            parent.SetParent(chunk, false);

            if (!AssetDatabase.IsValidFolder($"{R}/Meshes/Merged")) AssetDatabase.CreateFolder($"{R}/Meshes", "Merged");
            string path = $"{R}/Meshes/Merged/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            Mesh container = null;
            int made = 0;
            foreach (var kv in groups)
            {
                var mat = kv.Key.Item1;
                var m = new Mesh { name = $"{assetName}_{mat.name}_{made}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                m.RecalculateBounds();
                if (container == null) { AssetDatabase.CreateAsset(m, path); container = m; }
                else AssetDatabase.AddObjectToAsset(m, container);
                var go = new GameObject(m.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = kv.Key.Item2;
                r.receiveShadows = kv.Key.Item3;
                var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
                if (!IsTransparent(mat)) flags |= StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
                else flags |= StaticEditorFlags.OccludeeStatic;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                made++;
            }
            foreach (var r in disabled) r.enabled = false;
            // birleştirilen koli stoku artık gereksiz (sahnede kopya mesh tutmasın)
            foreach (var r in disabled)
                if (r != null && r.gameObject.name == "Stock" && r.GetComponent<Collider>() == null) Object.DestroyImmediate(r.gameObject);
            AssetDatabase.SaveAssets();
            return made;
        }

        // =====================================================================
        // Mağaza teması (raf, kasa, masa renkleri)
        // =====================================================================
        class Theme { public Color accent, back, trim; public bool woodBoard; public float accentSmooth; }

        static Theme ThemeFor(string store)
        {
            switch (store)
            {
                case "TEKNOMAX": return new Theme { accent = new Color(0.13f, 0.17f, 0.3f), back = new Color(0.78f, 0.81f, 0.86f), trim = new Color(0.3f, 0.85f, 1f), woodBoard = false, accentSmooth = 0.6f };
                case "OYUN CENNETİ": return new Theme { accent = new Color(0.25f, 0.55f, 1f), back = new Color(1f, 0.88f, 0.5f), trim = new Color(1f, 0.35f, 0.4f), woodBoard = false, accentSmooth = 0.3f };
                case "SPORZONE": return new Theme { accent = new Color(0.18f, 0.5f, 0.32f), back = new Color(0.26f, 0.28f, 0.31f), trim = new Color(1f, 0.6f, 0.15f), woodBoard = true, accentSmooth = 0.3f };
                case "EVİM MUTFAK": return new Theme { accent = new Color(0.96f, 0.92f, 0.83f), back = new Color(0.83f, 0.58f, 0.45f), trim = new Color(0.75f, 0.45f, 0.25f), woodBoard = true, accentSmooth = 0.25f };
                case "MODAPARK": return new Theme { accent = new Color(0.97f, 0.96f, 0.95f), back = new Color(0.98f, 0.86f, 0.89f), trim = new Color(0.95f, 0.75f, 0.35f), woodBoard = false, accentSmooth = 0.4f };
                default: return new Theme { accent = new Color(0.32f, 0.16f, 0.3f), back = new Color(0.93f, 0.87f, 0.91f), trim = new Color(0.95f, 0.75f, 0.35f), woodBoard = false, accentSmooth = 0.6f };
            }
        }

        static Dictionary<string, Material> ThemeMaterials(string store, int index)
        {
            var t = ThemeFor(store);
            var accent = Lit($"M_Store{index}_Accent", t.accent, null, t.accentSmooth);
            var back = Lit($"M_Store{index}_Back", t.back, null, 0.2f);
            var trim = Lit($"M_Store{index}_Trim", t.trim, null, 0.7f, 0.6f);
            var board = t.woodBoard ? M("M_Wood") : M("M_ShelfBoard");
            return new Dictionary<string, Material>
            {
                { "M_WallTrim", accent }, { "M_SaleStripes", accent }, { "M_CounterBlue", accent },
                { "M_ShelfMetal", back }, { "M_Yellow", trim }, { "M_ShelfBoard", board },
            };
        }

        static void ApplyTheme(GameObject inst, Dictionary<string, Material> map)
        {
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && map.TryGetValue(mats[i].name, out var nm)) { mats[i] = nm; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
