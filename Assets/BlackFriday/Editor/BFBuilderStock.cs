using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlackFriday.EditorTools
{
    /// <summary>
    /// Raf stoku: mağaza raflarının arka sıralarını ve üst katlarını koli/kutu ambalajlarla doldurur.
    /// Etkileşimsiz dekor (çarpıştırıcı yok, gölge yok) — tüm kutular mağaza başına tek mesh'e birleşir
    /// (tek materyal, tek atlas doku) → mağazalar dolu görünür, maliyeti 1 çizim çağrısı.
    /// Alınabilen ürünler ambalajsız ve rafın önünde durur; kolilerle karışmaz.
    /// </summary>
    public static partial class BFBuilder
    {
        const int StockGrid = 4;           // 4x4 ambalaj tasarımı
        const int StockCell = 256;
        static Material stockMat;
        static readonly Dictionary<int, Mesh> cartonMeshes = new Dictionary<int, Mesh>();

        // tasarım paletleri: zemin, üst bant, ikinci vurgu
        static readonly Color[][] StockPalettes =
        {
            new[] { new Color(0.09f, 0.12f, 0.22f), new Color(0.15f, 0.75f, 0.95f), new Color(0.95f, 0.95f, 1f) },   // 0 tekno lacivert
            new[] { new Color(0.06f, 0.06f, 0.08f), new Color(0.35f, 0.55f, 1f), new Color(0.2f, 0.9f, 0.7f) },      // 1 tekno siyah
            new[] { new Color(0.85f, 0.88f, 0.92f), new Color(0.12f, 0.35f, 0.75f), new Color(0.1f, 0.1f, 0.12f) },  // 2 tekno beyaz
            new[] { new Color(1f, 0.82f, 0.15f), new Color(0.9f, 0.18f, 0.2f), new Color(0.15f, 0.45f, 0.95f) },    // 3 oyuncak sarı
            new[] { new Color(0.2f, 0.55f, 0.95f), new Color(1f, 0.85f, 0.2f), new Color(0.95f, 0.3f, 0.5f) },       // 4 oyuncak mavi
            new[] { new Color(0.95f, 0.35f, 0.55f), new Color(0.35f, 0.85f, 0.5f), new Color(1f, 0.95f, 0.4f) },     // 5 oyuncak pembe
            new[] { new Color(0.12f, 0.12f, 0.13f), new Color(1f, 0.5f, 0.1f), new Color(0.95f, 0.95f, 0.95f) },     // 6 spor siyah-turuncu
            new[] { new Color(0.15f, 0.6f, 0.35f), new Color(0.95f, 0.95f, 0.9f), new Color(0.1f, 0.1f, 0.1f) },     // 7 spor yeşil
            new[] { new Color(0.78f, 0.55f, 0.32f), new Color(0.15f, 0.15f, 0.15f), new Color(0.95f, 0.45f, 0.1f) }, // 8 ayakkabı kolisi
            new[] { new Color(0.92f, 0.9f, 0.85f), new Color(0.85f, 0.15f, 0.15f), new Color(0.3f, 0.3f, 0.3f) },    // 9 mutfak beyaz-kırmızı
            new[] { new Color(0.75f, 0.12f, 0.12f), new Color(0.98f, 0.95f, 0.88f), new Color(0.95f, 0.75f, 0.2f) }, // 10 mutfak kırmızı
            new[] { new Color(0.95f, 0.88f, 0.7f), new Color(0.2f, 0.45f, 0.35f), new Color(0.85f, 0.4f, 0.2f) },    // 11 mutfak krem
            new[] { new Color(0.98f, 0.75f, 0.82f), new Color(0.1f, 0.1f, 0.1f), new Color(0.95f, 0.95f, 0.95f) },   // 12 moda pembe
            new[] { new Color(0.1f, 0.1f, 0.1f), new Color(0.95f, 0.35f, 0.7f), new Color(0.85f, 0.7f, 0.4f) },      // 13 moda siyah
            new[] { new Color(0.38f, 0.18f, 0.45f), new Color(0.95f, 0.78f, 0.35f), new Color(0.98f, 0.92f, 0.98f) },// 14 parfüm mor
            new[] { new Color(0.96f, 0.93f, 0.97f), new Color(0.55f, 0.3f, 0.65f), new Color(0.85f, 0.7f, 0.3f) },   // 15 parfüm beyaz
        };

        static int[] StockCellsFor(string store)
        {
            if (store.StartsWith("MEGA")) return new[] { 0, 3, 6, 9, 10, 4, 8, 14, 2, 12 };
            if (store.StartsWith("TEKNO")) return new[] { 0, 1, 2 };
            if (store.StartsWith("OYUN")) return new[] { 3, 4, 5 };
            if (store.StartsWith("SPOR")) return new[] { 6, 7, 8 };
            if (store.StartsWith("EV")) return new[] { 9, 10, 11 };
            if (store.StartsWith("MODA")) return new[] { 12, 13, 8 };
            return new[] { 14, 15, 12 };
        }

        static Material StockMaterial()
        {
            if (stockMat != null) return stockMat;
            string path = $"{R}/Textures/T_StockAtlas.png";
            int size = StockGrid * StockCell;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int cy = 0; cy < StockGrid; cy++)
                for (int cx = 0; cx < StockGrid; cx++)
                {
                    var pal = StockPalettes[cy * StockGrid + cx];
                    for (int y = 0; y < StockCell; y++)
                        for (int x = 0; x < StockCell; x++)
                        {
                            float u = x / (float)StockCell, v = y / (float)StockCell;
                            px[(cy * StockCell + y) * size + cx * StockCell + x] = CartonPixel(pal, u, v, cy * StockGrid + cx);
                        }
                }
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Architecture.Add("M_Stock");
            stockMat = Lit("M_Stock", Color.white, tex, 0.12f);
            stockMat.SetFloat("_NoiseStrength", 0.03f);
            stockMat.SetFloat("_RimStrength", 0.08f);
            EditorUtility.SetDirty(stockMat);
            return stockMat;
        }

        static Color CartonPixel(Color[] pal, float u, float v, int seed)
        {
            Color baseC = pal[0], band = pal[1], acc = pal[2];
            Color c = baseC;
            // karton dokusu
            float n = Mathf.PerlinNoise(u * 18f + seed * 3.1f, v * 18f + seed * 1.7f) * 0.06f - 0.03f;
            // üst bant + alt şerit
            if (v > 0.8f) c = band;
            if (v > 0.1f && v < 0.16f) c = acc;
            // etiket
            bool label = u > 0.12f && u < 0.88f && v > 0.3f && v < 0.68f;
            if (label)
            {
                c = Color.Lerp(Color.white, baseC, 0.06f);
                // ikon dairesi
                float du = u - 0.27f, dv = v - 0.49f;
                if (du * du + dv * dv < 0.0095f) c = band;
                // yazı satırları
                foreach (var row in new[] { (0.56f, 0.8f), (0.48f, 0.74f), (0.4f, 0.62f) })
                    if (u > 0.41f && u < row.Item2 && Mathf.Abs(v - row.Item1) < 0.017f) c = new Color(0.2f, 0.2f, 0.24f);
            }
            // indirim etiketi (sağ üst köşe)
            if (u > 0.66f && u < 0.92f && v > 0.83f && v < 0.95f)
            {
                c = new Color(0.9f, 0.1f, 0.12f);
                if (Mathf.Abs(v - 0.89f) < 0.015f && u > 0.7f && u < 0.88f) c = Color.white;
            }
            // koli kenarı
            float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (e < 0.025f) c *= 0.72f;
            c.r += n; c.g += n; c.b += n; c.a = 1f;
            return c;
        }

        /// <summary>1x1x1 kutu; her yüz atlasın bir hücresini kullanır (etiket dik).</summary>
        static Mesh CartonMesh(int cell)
        {
            if (cartonMeshes.TryGetValue(cell, out var cached) && cached != null) return cached;
            float inset = 2f / (StockGrid * StockCell);
            float cu = (cell % StockGrid) / (float)StockGrid, cv = (cell / StockGrid) / (float)StockGrid, cs = 1f / StockGrid;
            Vector2 uv0 = new Vector2(cu + inset, cv + inset), uv1 = new Vector2(cu + cs - inset, cv + cs - inset);
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            // (normal, sağ, yukarı) — sağ: o yüze bakan izleyicinin sağı
            var faces = new (Vector3 n, Vector3 r, Vector3 up)[]
            {
                (Vector3.back, Vector3.right, Vector3.up),
                (Vector3.forward, Vector3.left, Vector3.up),
                (Vector3.right, Vector3.forward, Vector3.up),
                (Vector3.left, Vector3.back, Vector3.up),
                (Vector3.up, Vector3.right, Vector3.forward),
                (Vector3.down, Vector3.right, Vector3.back),
            };
            foreach (var f in faces)
            {
                int b = verts.Count;
                Vector3 c = f.n * 0.5f;
                verts.Add(c - f.r * 0.5f - f.up * 0.5f); uvs.Add(new Vector2(uv0.x, uv0.y));
                verts.Add(c + f.r * 0.5f - f.up * 0.5f); uvs.Add(new Vector2(uv1.x, uv0.y));
                verts.Add(c + f.r * 0.5f + f.up * 0.5f); uvs.Add(new Vector2(uv1.x, uv1.y));
                verts.Add(c - f.r * 0.5f + f.up * 0.5f); uvs.Add(new Vector2(uv0.x, uv1.y));
                for (int i = 0; i < 4; i++) norms.Add(f.n);
                // saat yönü (Unity önyüz): izleyiciden bakınca sol-alt, sol-üst, sağ-üst
                tris.AddRange(new[] { b, b + 3, b + 2, b, b + 2, b + 1 });
            }
            var m = new Mesh { name = "Carton_" + cell };
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            cartonMeshes[cell] = m;
            return m;
        }

        class StockBatch
        {
            public readonly List<CombineInstance> items = new List<CombineInstance>();
            public Transform root;
            public System.Random rng;
            public int[] cells;
            public float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        }

        static void AddCarton(StockBatch sb, Transform shelf, Vector3 bottomCenter, Vector3 size, float yaw, int cell)
        {
            var local = Matrix4x4.TRS(bottomCenter + Vector3.up * size.y * 0.5f, Quaternion.Euler(0, yaw, 0), size);
            sb.items.Add(new CombineInstance { mesh = CartonMesh(cell), transform = sb.root.worldToLocalMatrix * shelf.localToWorldMatrix * local });
        }

        /// <summary>Bir raf katını x0..x1 boyunca kolilerle doldurur (z: kolilerin derinlik merkezi).</summary>
        static void StockRow(StockBatch sb, Transform shelf, float y, float x0, float x1, float zc, float depth, float maxH, float gapChance = 0.07f)
        {
            float x = x0 + sb.R(0f, 0.05f);
            int cell = sb.cells[sb.rng.Next(sb.cells.Length)];
            while (true)
            {
                float w = sb.R(0.2f, 0.38f);
                if (x + w > x1) break;
                // aynı üründen 2-4 kutu yan yana (raf düzeni), sonra tasarım değişir
                if (sb.rng.NextDouble() < 0.35) cell = sb.cells[sb.rng.Next(sb.cells.Length)];
                float h = sb.R(maxH * 0.45f, maxH);
                float d = depth * sb.R(0.85f, 1f);
                AddCarton(sb, shelf, new Vector3(x + w * 0.5f, y, zc + sb.R(-0.01f, 0.01f)), new Vector3(w * 0.97f, h, d), sb.R(-2.5f, 2.5f), cell);
                // alçak kutuların üstüne ikinci kutu
                if (h < maxH * 0.6f && sb.rng.NextDouble() < 0.55)
                {
                    float h2 = Mathf.Min(sb.R(0.12f, maxH - h), maxH - h - 0.01f);
                    if (h2 > 0.08f)
                        AddCarton(sb, shelf, new Vector3(x + w * 0.5f + sb.R(-0.02f, 0.02f), y + h, zc), new Vector3(w * sb.R(0.8f, 0.95f), h2, d * 0.95f), sb.R(-5f, 5f), cell);
                }
                x += w + sb.R(0.005f, 0.03f);
                if (sb.rng.NextDouble() < gapChance) x += sb.R(0.25f, 0.5f); // kapış kapış: boşalmış yerler
            }
        }

        /// <summary>Mağazadaki duvar rafı ve reyonlara koli stoku ekler; tek mesh olarak mağaza köküne bağlanır.</summary>
        static void BuildStoreStock(Transform storeRoot, string storeName, int seed)
        {
            var sb = new StockBatch { root = storeRoot, rng = new System.Random(seed * 7919 + 17), cells = StockCellsFor(storeName) };
            foreach (Transform child in storeRoot)
            {
                string n = child.name;
                if (n.StartsWith("WallShelf"))
                {
                    // ön yüz -Z; arka panel iç yüzü z≈0.27; ürünler z≈-0.05'te
                    StockRow(sb, child, 0.15f, -2.9f, 2.9f, 0.19f, 0.15f, 0.42f);
                    StockRow(sb, child, 0.77f, -2.9f, 2.9f, 0.19f, 0.15f, 0.46f);
                    StockRow(sb, child, 1.47f, -2.9f, 2.9f, 0.19f, 0.15f, 0.44f);
                    // en üst kat: tam derinlik stok (alınamaz yükseklikte)
                    StockRow(sb, child, 2.12f, -2.9f, 2.9f, 0.0f, 0.5f, 0.42f, 0.03f);
                }
                else if (n.StartsWith("Gondola"))
                {
                    // çift taraflı: orta panel z=±0.04; ürünler z=±0.3
                    foreach (float side in new[] { -1f, 1f })
                    {
                        StockRow(sb, child, 0.15f, -3.9f, 3.9f, side * 0.115f, 0.13f, 0.36f);
                        StockRow(sb, child, 0.57f, -3.9f, 3.9f, side * 0.115f, 0.13f, 0.44f);
                        StockRow(sb, child, 1.175f, -3.9f, 3.9f, side * 0.115f, 0.13f, 0.34f);
                    }
                }
            }
            if (sb.items.Count == 0) return;
            var mesh = new Mesh { name = "Stock_" + seed, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(sb.items.ToArray(), true, true);
            mesh.RecalculateBounds();
            var go = new GameObject("Stock");
            go.transform.SetParent(storeRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = StockMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;  // mağaza spotları gölgesiz; çatı güneşi keser
            r.receiveShadows = true;
            Debug.Log($"[BF] {storeName}: {sb.items.Count} koli");
        }
    }
}
