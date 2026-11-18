using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;

namespace BlackFriday.EditorTools
{
    public static partial class BFBuilder
    {
        // =====================================================================
        // 5) Ürünler (ProBuilder ile modellenmiş, ağ üzerinde taşınabilir)
        // =====================================================================
        struct ProductSpec
        {
            public int id; public string name, file; public Color ui; public float carry; public int points;
            public System.Action<Transform> build;
        }

        [MenuItem("BlackFriday/5 - Ürünler")]
        public static void BuildProducts()
        {
            EnsureFolders();
            NewTempScene();
            var specs = new List<ProductSpec>
            {
                new ProductSpec { id = 0, name = "Televizyon", file = "TV", ui = new Color(0.2f, 0.8f, 0.95f), carry = 0.72f, points = 300, build = BuildTV },
                new ProductSpec { id = 1, name = "Oyun Konsolu", file = "Console", ui = new Color(0.55f, 0.35f, 0.9f), carry = 0.95f, points = 220, build = BuildConsole },
                new ProductSpec { id = 2, name = "Tost Makinesi", file = "Toaster", ui = new Color(0.95f, 0.22f, 0.25f), carry = 0.9f, points = 120, build = BuildToaster },
                new ProductSpec { id = 3, name = "Spor Ayakkabı", file = "Sneakers", ui = new Color(1f, 0.55f, 0.15f), carry = 1f, points = 100, build = BuildShoeBox },
                new ProductSpec { id = 4, name = "Kulaklık", file = "Headphones", ui = new Color(1f, 0.45f, 0.7f), carry = 1f, points = 140, build = BuildHeadphones },
                new ProductSpec { id = 5, name = "Oyuncak Ayı", file = "Teddy", ui = new Color(0.62f, 0.4f, 0.25f), carry = 1f, points = 80, build = BuildTeddy },
                new ProductSpec { id = 6, name = "Blender", file = "Blender", ui = new Color(0.3f, 0.8f, 0.35f), carry = 0.9f, points = 120, build = BuildBlender },
                new ProductSpec { id = 7, name = "Akıllı Telefon", file = "Phone", ui = new Color(1f, 0.82f, 0.12f), carry = 1f, points = 250, build = BuildPhone },
                new ProductSpec { id = 8, name = "Çanta", file = "Bag", ui = new Color(0.85f, 0.25f, 0.6f), carry = 1f, points = 160, build = BuildBag },
                new ProductSpec { id = 9, name = "Parfüm", file = "Perfume", ui = new Color(0.75f, 0.55f, 1f), carry = 1f, points = 180, build = BuildPerfume },
            };

            var db = AssetDatabase.LoadAssetAtPath<ProductDatabase>(R + "/Resources/BF_ProductDatabase.asset");
            if (db == null) { db = ScriptableObject.CreateInstance<ProductDatabase>(); AssetDatabase.CreateAsset(db, R + "/Resources/BF_ProductDatabase.asset"); }
            db.products.Clear();

            // 1) tüm görselleri ProBuilder ile kur
            var roots = new List<GameObject>();
            var allMats = new List<Material>();
            foreach (var s in specs)
            {
                var root = new GameObject("P_" + s.file);
                var vis = new GameObject("Visual").transform;
                vis.SetParent(root.transform, false);
                s.build(vis);
                vis.localScale = Vector3.one * 1.25f; // kameradan okunabilsin
                foreach (var r in vis.GetComponentsInChildren<MeshRenderer>()) allMats.Add(r.sharedMaterial);
                roots.Add(root);
            }
            // 2) ortak atlas → her ürün tek mesh, tek draw call
            var atlas = BuildProductAtlas(allMats);
            for (int si = 0; si < specs.Count; si++)
            {
                var s = specs[si];
                var root = roots[si];
                CombineProductVisual(root.transform.Find("Visual"), atlas, "P_" + s.file);

                // collider = görsel sınırlar
                var b = new Bounds(); bool first = true;
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                var bc = root.AddComponent<BoxCollider>();
                bc.center = b.center; bc.size = b.size;
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 2f; rb.linearDamping = 0.3f; rb.angularDamping = 0.6f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                root.AddComponent<NetworkIdentity>();
                var nt = root.AddComponent<NetworkTransformUnreliable>();
                nt.target = root.transform;
                nt.syncDirection = SyncDirection.ServerToClient;
                nt.syncScale = false;
                var item = root.AddComponent<ProductItem>();
                item.productId = s.id;

                string path = $"{R}/Prefabs/Products/P_{s.file}.prefab";
                var pf = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                db.products.Add(new ProductDef { id = s.id, displayName = s.name, uiColor = s.ui, prefab = pf, carrySpeed = s.carry, points = s.points });
            }
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BF] {specs.Count} ürün prefabı hazır.");
        }

        // Ön yüz = -Z. Pivot = taban.
        // ---- ürün modelleri (stilize, tombul oranlar; hepsi ortak atlasa gömülür) ----
        /// <summary>XY düzleminde yay boyunca kutular dizer (kulaklık bandı, çanta sapı).</summary>
        static void ArcBoxes(Transform p, string name, Vector3 center, float radius, float a0, float a1, int n, Vector2 section, Material mat)
        {
            float step = (a1 - a0) / n;
            float seg = 2f * radius * Mathf.Sin(Mathf.Abs(step) * Mathf.Deg2Rad * 0.5f) * 1.12f;
            for (int i = 0; i < n; i++)
            {
                float a = (a0 + step * (i + 0.5f)) * Mathf.Deg2Rad;
                Box(p, name + i, new Vector3(seg, section.x, section.y), center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius, mat,
                    new Vector3(0, 0, a * Mathf.Rad2Deg + 90f));
            }
        }

        static void SaleTag(Transform p, Vector3 pos, float rotZ)
        {
            Box(p, "Tag", new Vector3(0.11f, 0.065f, 0.008f), pos, M("M_Red"), new Vector3(0, 0, rotZ));
            Box(p, "TagLine", new Vector3(0.07f, 0.012f, 0.01f), pos, M("M_White"), new Vector3(0, 0, rotZ));
        }

        static void BuildTV(Transform p)
        {
            Box(p, "FootL", new Vector3(0.05f, 0.02f, 0.2f), new Vector3(-0.22f, 0.01f, 0), M("M_DarkGrey"), new Vector3(0, 18, 0));
            Box(p, "FootR", new Vector3(0.05f, 0.02f, 0.2f), new Vector3(0.22f, 0.01f, 0), M("M_DarkGrey"), new Vector3(0, -18, 0));
            Box(p, "LegL", new Vector3(0.035f, 0.1f, 0.035f), new Vector3(-0.22f, 0.065f, 0.015f), M("M_DarkGrey"));
            Box(p, "LegR", new Vector3(0.035f, 0.1f, 0.035f), new Vector3(0.22f, 0.065f, 0.015f), M("M_DarkGrey"));
            Box(p, "Panel", new Vector3(0.76f, 0.45f, 0.045f), new Vector3(0, 0.335f, 0), M("M_Black"));
            Box(p, "BackBump", new Vector3(0.5f, 0.26f, 0.05f), new Vector3(0, 0.28f, 0.04f), M("M_DarkGrey"));
            Box(p, "Screen", new Vector3(0.715f, 0.395f, 0.008f), new Vector3(0, 0.348f, -0.024f), M("M_TVScreen"), new Vector3(0, 180, 0), false, true);
            Box(p, "Chin", new Vector3(0.76f, 0.03f, 0.05f), new Vector3(0, 0.125f, -0.002f), M("M_Chrome"));
            Box(p, "Led", new Vector3(0.02f, 0.008f, 0.01f), new Vector3(0.33f, 0.125f, -0.03f), M("M_ScreenGlow"));
            SaleTag(p, new Vector3(0.3f, 0.53f, -0.03f), -14);
        }

        static void BuildConsole(Transform p)
        {
            Box(p, "Body", new Vector3(0.4f, 0.075f, 0.3f), new Vector3(0, 0.0375f, 0), M("M_Black"));
            Box(p, "Top", new Vector3(0.41f, 0.022f, 0.31f), new Vector3(0, 0.086f, 0), M("M_White"));
            Box(p, "Fin", new Vector3(0.41f, 0.012f, 0.12f), new Vector3(0, 0.103f, 0.07f), M("M_White"));
            Box(p, "Light", new Vector3(0.36f, 0.012f, 0.012f), new Vector3(0, 0.05f, -0.152f), M("M_ScreenGlow"));
            Box(p, "Slot", new Vector3(0.16f, 0.008f, 0.012f), new Vector3(-0.08f, 0.028f, -0.152f), M("M_DarkGrey"));
            Cyl(p, "Power", 0.012f, 0.012f, new Vector3(0.15f, 0.028f, -0.152f), M("M_White"), new Vector3(90, 0, 0), 8);
            // oyun kolu
            Box(p, "Pad", new Vector3(0.15f, 0.032f, 0.075f), new Vector3(0.06f, 0.114f, -0.07f), M("M_Purple"));
            Ball(p, "GripL", 0.036f, new Vector3(-0.01f, 0.112f, -0.05f), M("M_Purple"), new Vector3(1, 0.6f, 1.3f));
            Ball(p, "GripR", 0.036f, new Vector3(0.13f, 0.112f, -0.05f), M("M_Purple"), new Vector3(1, 0.6f, 1.3f));
            Cyl(p, "StickL", 0.012f, 0.02f, new Vector3(0.025f, 0.138f, -0.06f), M("M_Black"), default, 8);
            Cyl(p, "StickR", 0.012f, 0.02f, new Vector3(0.085f, 0.138f, -0.085f), M("M_Black"), default, 8);
            Ball(p, "BtnA", 0.008f, new Vector3(0.105f, 0.134f, -0.058f), M("M_Green"));
            Ball(p, "BtnB", 0.008f, new Vector3(0.122f, 0.134f, -0.07f), M("M_Red"));
            Ball(p, "BtnX", 0.008f, new Vector3(0.105f, 0.134f, -0.082f), M("M_Cyan"));
        }

        static void BuildToaster(Transform p)
        {
            Box(p, "Body", new Vector3(0.24f, 0.19f, 0.17f), new Vector3(0, 0.115f, 0), M("M_Red"));
            Cyl(p, "EndL", 0.095f, 0.17f, new Vector3(-0.12f, 0.115f, 0), M("M_Red"), new Vector3(90, 0, 0), 14);
            Cyl(p, "EndR", 0.095f, 0.17f, new Vector3(0.12f, 0.115f, 0), M("M_Red"), new Vector3(90, 0, 0), 14);
            Box(p, "Band", new Vector3(0.42f, 0.03f, 0.176f), new Vector3(0, 0.045f, 0), M("M_Chrome"));
            foreach (var f in new[] { new Vector3(-0.14f, 0.008f, 0.06f), new Vector3(0.14f, 0.008f, 0.06f), new Vector3(-0.14f, 0.008f, -0.06f), new Vector3(0.14f, 0.008f, -0.06f) })
                Cyl(p, "Foot", 0.018f, 0.016f, f, M("M_Black"), default, 8);
            Box(p, "Slot1", new Vector3(0.24f, 0.012f, 0.03f), new Vector3(0, 0.212f, -0.038f), M("M_Black"));
            Box(p, "Slot2", new Vector3(0.24f, 0.012f, 0.03f), new Vector3(0, 0.212f, 0.038f), M("M_Black"));
            Box(p, "Bread1", new Vector3(0.15f, 0.085f, 0.022f), new Vector3(-0.02f, 0.245f, -0.038f), M("M_LightBrown"));
            Box(p, "Crust1", new Vector3(0.155f, 0.02f, 0.024f), new Vector3(-0.02f, 0.292f, -0.038f), M("M_Brown"));
            Box(p, "Bread2", new Vector3(0.15f, 0.05f, 0.022f), new Vector3(0.03f, 0.228f, 0.038f), M("M_LightBrown"));
            Box(p, "LeverSlot", new Vector3(0.01f, 0.1f, 0.02f), new Vector3(0.216f, 0.12f, 0), M("M_Black"));
            Box(p, "Lever", new Vector3(0.04f, 0.025f, 0.05f), new Vector3(0.232f, 0.15f, 0), M("M_Black"));
            Cyl(p, "Knob", 0.025f, 0.02f, new Vector3(0.08f, 0.08f, -0.09f), M("M_Chrome"), new Vector3(90, 0, 0), 10);
            Box(p, "KnobMark", new Vector3(0.006f, 0.02f, 0.006f), new Vector3(0.08f, 0.09f, -0.102f), M("M_Black"));
        }

        static void BuildShoeBox(Transform p)
        {
            // kutu + üstünde teşhir ayakkabısı
            Box(p, "Box", new Vector3(0.36f, 0.12f, 0.22f), new Vector3(0, 0.06f, 0), M("M_Orange"));
            Box(p, "Lid", new Vector3(0.37f, 0.04f, 0.23f), new Vector3(0, 0.135f, 0), M("M_ShoeBoxArt"), default, false, true);
            Box(p, "Logo", new Vector3(0.12f, 0.04f, 0.01f), new Vector3(-0.08f, 0.065f, -0.112f), M("M_Black"), new Vector3(0, 0, 10));
            float y = 0.155f;
            Box(p, "Sole", new Vector3(0.3f, 0.03f, 0.105f), new Vector3(0, y + 0.015f, 0), M("M_White"));
            Box(p, "Mid", new Vector3(0.29f, 0.012f, 0.1f), new Vector3(0, y + 0.034f, 0), M("M_Cyan"));
            Box(p, "Heel", new Vector3(0.12f, 0.085f, 0.095f), new Vector3(0.085f, y + 0.08f, 0), M("M_Orange"));
            Box(p, "Vamp", new Vector3(0.15f, 0.05f, 0.092f), new Vector3(-0.03f, y + 0.066f, 0), M("M_Orange"), new Vector3(0, 0, 14));
            Ball(p, "Toe", 0.05f, new Vector3(-0.105f, y + 0.058f, 0), M("M_White"), new Vector3(1.15f, 0.72f, 0.98f));
            Box(p, "Tongue", new Vector3(0.05f, 0.06f, 0.06f), new Vector3(0.015f, y + 0.115f, 0), M("M_White"), new Vector3(0, 0, 24));
            for (int i = 0; i < 3; i++)
                Box(p, "Lace" + i, new Vector3(0.012f, 0.01f, 0.07f), new Vector3(-0.06f + i * 0.03f, y + 0.086f + i * 0.008f, 0), M("M_White"), new Vector3(0, 0, 14));
            Box(p, "Swoosh", new Vector3(0.13f, 0.02f, 0.098f), new Vector3(0.03f, y + 0.06f, 0), M("M_Black"), new Vector3(0, 0, -16));
            Box(p, "Tab", new Vector3(0.015f, 0.04f, 0.03f), new Vector3(0.148f, y + 0.115f, 0), M("M_Cyan"));
        }

        static void BuildHeadphones(Transform p)
        {
            Cyl(p, "Stand", 0.09f, 0.02f, new Vector3(0, 0.01f, 0), M("M_Black"), default, 16);
            Cyl(p, "Pole", 0.014f, 0.25f, new Vector3(0, 0.145f, 0), M("M_Chrome"), default, 8);
            Ball(p, "Hook", 0.03f, new Vector3(0, 0.275f, 0), M("M_Chrome"), new Vector3(1.6f, 0.6f, 1.2f));
            ArcBoxes(p, "Band", new Vector3(0, 0.185f, 0), 0.125f, 10, 170, 9, new Vector2(0.022f, 0.05f), M("M_Pink"));
            ArcBoxes(p, "Pad", new Vector3(0, 0.185f, 0), 0.112f, 55, 125, 4, new Vector2(0.014f, 0.042f), M("M_Black"));
            foreach (int s in new[] { -1, 1 })
            {
                Box(p, "Yoke", new Vector3(0.02f, 0.07f, 0.03f), new Vector3(s * 0.125f, 0.165f, 0), M("M_Chrome"));
                Cyl(p, "Cup", 0.068f, 0.045f, new Vector3(s * 0.13f, 0.13f, 0), M("M_Pink"), new Vector3(0, 0, 90), 14);
                Cyl(p, "Cushion", 0.062f, 0.025f, new Vector3(s * 0.098f, 0.13f, 0), M("M_Black"), new Vector3(0, 0, 90), 14);
                Cyl(p, "Cap", 0.04f, 0.01f, new Vector3(s * 0.156f, 0.13f, 0), M("M_White"), new Vector3(0, 0, 90), 12);
            }
        }

        static void BuildTeddy(Transform p)
        {
            Ball(p, "Body", 0.14f, new Vector3(0, 0.16f, 0), M("M_Teddy"), new Vector3(1, 1.05f, 0.9f));
            Ball(p, "Belly", 0.095f, new Vector3(0, 0.15f, -0.07f), M("M_LightBrown"), new Vector3(1, 1.1f, 0.6f));
            Ball(p, "Head", 0.105f, new Vector3(0, 0.355f, 0), M("M_Teddy"));
            Ball(p, "Snout", 0.045f, new Vector3(0, 0.335f, -0.085f), M("M_LightBrown"), new Vector3(1.1f, 0.85f, 1));
            Ball(p, "Nose", 0.016f, new Vector3(0, 0.35f, -0.125f), M("M_Black"), new Vector3(1.3f, 0.9f, 1));
            Ball(p, "EyeL", 0.013f, new Vector3(-0.04f, 0.385f, -0.09f), M("M_Black"));
            Ball(p, "EyeR", 0.013f, new Vector3(0.04f, 0.385f, -0.09f), M("M_Black"));
            foreach (int s in new[] { -1, 1 })
            {
                Ball(p, "Ear", 0.04f, new Vector3(s * 0.078f, 0.445f, 0), M("M_Teddy"), new Vector3(1, 1, 0.6f));
                Ball(p, "EarIn", 0.024f, new Vector3(s * 0.078f, 0.445f, -0.018f), M("M_LightBrown"), new Vector3(1, 1, 0.5f));
                Ball(p, "Arm", 0.05f, new Vector3(s * 0.135f, 0.2f, -0.025f), M("M_Teddy"), new Vector3(1, 1.5f, 1));
                Ball(p, "Leg", 0.06f, new Vector3(s * 0.085f, 0.05f, -0.07f), M("M_Teddy"), new Vector3(1, 0.85f, 1.5f));
                Ball(p, "Paw", 0.032f, new Vector3(s * 0.085f, 0.05f, -0.15f), M("M_LightBrown"), new Vector3(1, 1, 0.4f));
                Ball(p, "BowWing", 0.032f, new Vector3(s * 0.04f, 0.268f, -0.09f), M("M_Red"), new Vector3(1.3f, 1, 0.6f));
            }
            Ball(p, "BowKnot", 0.018f, new Vector3(0, 0.268f, -0.1f), M("M_Red"));
        }

        static void BuildBlender(Transform p)
        {
            Cyl(p, "Base", 0.1f, 0.1f, new Vector3(0, 0.05f, 0), M("M_Green"), default, 10);
            Cyl(p, "Collar", 0.075f, 0.03f, new Vector3(0, 0.115f, 0), M("M_Black"), default, 10);
            Box(p, "Panel", new Vector3(0.09f, 0.04f, 0.012f), new Vector3(0, 0.05f, -0.096f), M("M_Black"));
            Cyl(p, "Dial", 0.016f, 0.014f, new Vector3(0, 0.05f, -0.105f), M("M_White"), new Vector3(90, 0, 0), 8);
            Cyl(p, "Jar", 0.078f, 0.21f, new Vector3(0, 0.235f, 0), M("M_Glass"), default, 10);
            Cyl(p, "Juice", 0.064f, 0.11f, new Vector3(0, 0.19f, 0), M("M_Orange"), default, 10);
            Box(p, "Handle", new Vector3(0.022f, 0.15f, 0.03f), new Vector3(0.105f, 0.235f, 0), M("M_Black"));
            Box(p, "HandleTop", new Vector3(0.05f, 0.02f, 0.03f), new Vector3(0.09f, 0.31f, 0), M("M_Black"));
            Box(p, "HandleBot", new Vector3(0.05f, 0.02f, 0.03f), new Vector3(0.09f, 0.165f, 0), M("M_Black"));
            Cyl(p, "Lid", 0.084f, 0.025f, new Vector3(0, 0.352f, 0), M("M_Black"), default, 10);
            Cyl(p, "LidCap", 0.03f, 0.03f, new Vector3(0, 0.375f, 0), M("M_Green"), default, 8);
        }

        static void BuildPhone(Transform p)
        {
            // kutusu + önünde eğik duran telefon
            Box(p, "Box", new Vector3(0.18f, 0.055f, 0.28f), new Vector3(0, 0.0275f, 0), M("M_White"));
            Box(p, "Band", new Vector3(0.185f, 0.057f, 0.04f), new Vector3(0, 0.0275f, 0.1f), M("M_Yellow"));
            Box(p, "Picture", new Vector3(0.1f, 0.004f, 0.15f), new Vector3(0, 0.057f, 0.02f), M("M_DarkGrey"));
            Box(p, "Phone", new Vector3(0.105f, 0.012f, 0.215f), new Vector3(0, 0.075f, -0.03f), M("M_Black"), new Vector3(-14, 0, 0));
            Box(p, "Screen", new Vector3(0.094f, 0.004f, 0.2f), new Vector3(0, 0.083f, -0.032f), M("M_PhoneScreen"), new Vector3(-14, 0, 0), false, true);
            Box(p, "Edge", new Vector3(0.109f, 0.006f, 0.219f), new Vector3(0, 0.072f, -0.03f), M("M_Chrome"), new Vector3(-14, 0, 0));
        }

        static void BuildBag(Transform p)
        {
            Box(p, "Body", new Vector3(0.34f, 0.23f, 0.13f), new Vector3(0, 0.115f, 0), M("M_Magenta"));
            Box(p, "Bottom", new Vector3(0.345f, 0.03f, 0.135f), new Vector3(0, 0.015f, 0), M("M_MagentaDark"));
            Box(p, "Flap", new Vector3(0.35f, 0.11f, 0.137f), new Vector3(0, 0.19f, -0.002f), M("M_MagentaDark"));
            Box(p, "FlapTip", new Vector3(0.12f, 0.05f, 0.01f), new Vector3(0, 0.125f, -0.07f), M("M_MagentaDark"));
            Box(p, "Clasp", new Vector3(0.05f, 0.045f, 0.014f), new Vector3(0, 0.125f, -0.076f), M("M_Gold"));
            foreach (int s in new[] { -1, 1 })
            {
                Ball(p, "Stud", 0.012f, new Vector3(s * 0.14f, 0.21f, -0.07f), M("M_Gold"));
                Ball(p, "Ring", 0.016f, new Vector3(s * 0.1f, 0.245f, 0), M("M_Gold"), new Vector3(1, 1, 0.5f));
                Box(p, "Gusset", new Vector3(0.008f, 0.16f, 0.09f), new Vector3(s * 0.172f, 0.11f, 0), M("M_MagentaDark"));
            }
            ArcBoxes(p, "Handle", new Vector3(0, 0.25f, 0), 0.1f, 0, 180, 8, new Vector2(0.022f, 0.026f), M("M_MagentaDark"));
            Box(p, "Charm", new Vector3(0.03f, 0.05f, 0.008f), new Vector3(0.1f, 0.2f, -0.072f), M("M_Gold"), new Vector3(0, 0, 12));
        }

        static void BuildPerfume(Transform p)
        {
            Box(p, "GiftBox", new Vector3(0.2f, 0.05f, 0.14f), new Vector3(0, 0.025f, 0), M("M_Purple"));
            Box(p, "Ribbon", new Vector3(0.03f, 0.052f, 0.142f), new Vector3(0, 0.025f, 0), M("M_Gold"));
            Box(p, "Ribbon2", new Vector3(0.202f, 0.052f, 0.02f), new Vector3(0, 0.025f, 0), M("M_Gold"));
            Cyl(p, "Bottle", 0.07f, 0.15f, new Vector3(0, 0.125f, 0), M("M_GlassPink"), new Vector3(0, 30, 0), 6);
            Cyl(p, "Liquid", 0.056f, 0.1f, new Vector3(0, 0.107f, 0), M("M_PerfumeLiquid"), new Vector3(0, 30, 0), 6);
            Cyl(p, "Shoulder", 0.045f, 0.015f, new Vector3(0, 0.207f, 0), M("M_Gold"), default, 8);
            Cyl(p, "Neck", 0.018f, 0.03f, new Vector3(0, 0.228f, 0), M("M_Gold"), default, 8);
            Ball(p, "Cap", 0.038f, new Vector3(0, 0.272f, 0), M("M_Gold"));
            Ball(p, "CapGem", 0.014f, new Vector3(0, 0.312f, 0), M("M_Pink"));
            foreach (int s in new[] { -1, 1 })
                Ball(p, "Bow", 0.022f, new Vector3(s * 0.028f, 0.058f, -0.05f), M("M_Gold"), new Vector3(1.4f, 1, 0.6f));
        }

        // =====================================================================
        // 6) Ağ prefabları: oyuncu & NPC
        // =====================================================================
        [MenuItem("BlackFriday/6 - Oyuncu ve NPC prefabları")]
        public static void BuildNetworkPrefabs()
        {
            EnsureFolders();
            NewTempScene();

            // ---- oyuncu
            var root = new GameObject("BF_Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.32f; cc.center = new Vector3(0, 0.87f, 0);
            cc.stepOffset = 0.3f; cc.skinWidth = 0.03f; cc.slopeLimit = 50f;
            root.AddComponent<NetworkIdentity>();
            var nt = root.AddComponent<NetworkTransformUnreliable>();
            nt.target = root.transform;
            nt.syncDirection = SyncDirection.ClientToServer;
            nt.syncScale = false;
            var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
            var vis = root.AddComponent<CharacterVisual>(); vis.modelRoot = model; vis.isNpc = false;
            var anchor = new GameObject("CarryAnchor").transform; anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0, 0.68f, 0.4f);
            anchor.localEulerAngles = new Vector3(0, 180, 0); // ürünün ön yüzü dışarı baksın
            var ringGo = Pipe(root.transform, "Ring", 0.55f, 0.02f, 0.09f, new Vector3(0, 0.03f, 0), M("M_PlayerRing"));
            BakeToAsset(root, "BF_PlayerRing");
            ringGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pl = root.AddComponent<BFPlayer>();
            pl.visual = vis; pl.carryAnchor = anchor; pl.ring = ringGo.GetComponent<Renderer>();
            pl.arcMaterial = M("M_ArcLine");
            SavePrefab(root, R + "/Prefabs/BF_Player.prefab");

            // ---- NPC
            var npc = new GameObject("BF_NPC");
            npc.AddComponent<NetworkIdentity>();
            var nt2 = npc.AddComponent<NetworkTransformUnreliable>();
            nt2.target = npc.transform;
            nt2.syncDirection = SyncDirection.ServerToClient;
            nt2.syncScale = false;
            var agent = npc.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1.7f; agent.speed = 2.2f; agent.acceleration = 10f;
            agent.angularSpeed = 540f; agent.stoppingDistance = 0.3f; agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled = false;
            var cap = npc.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 0.87f, 0); cap.height = 1.7f; cap.radius = 0.3f;
            var rb = npc.AddComponent<Rigidbody>(); rb.isKinematic = true;
            var nmodel = new GameObject("Model").transform; nmodel.SetParent(npc.transform, false);
            var nvis = npc.AddComponent<CharacterVisual>(); nvis.modelRoot = nmodel; nvis.isNpc = true;
            var nanchor = new GameObject("CarryAnchor").transform; nanchor.SetParent(npc.transform, false);
            nanchor.localPosition = new Vector3(0, 0.95f, 0.4f); nanchor.localEulerAngles = new Vector3(0, 180, 0);
            var shopper = npc.AddComponent<NPCShopper>(); shopper.visual = nvis; shopper.carryAnchor = nanchor;
            SavePrefab(npc, R + "/Prefabs/BF_NPC.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[BF] Oyuncu ve NPC prefabları hazır.");
        }

        // =====================================================================
        // 7) Dekor / mağaza prefabları (ProBuilder)
        // =====================================================================
        [MenuItem("BlackFriday/7 - Mağaza prefabları (raf, kasa, masa...)")]
        public static void BuildPropPrefabs()
        {
            EnsureFolders();
            NewTempScene();
            BuildGondola();
            BuildWallShelf();
            BuildDealTable();
            BuildCheckout();
            BuildCart();
            BuildPlant();
            AssetDatabase.SaveAssets();
            Debug.Log("[BF] Mağaza prefabları hazır.");
        }

        static readonly int[] NoTV = { 1, 2, 3, 4, 5, 6, 7 };
        static readonly int[] SmallItems = { 1, 2, 3, 7 };

        // Çift taraflı alçak reyon (kameradan ürünler görünsün): uzunluk X (8m), derinlik Z
        static void BuildGondola()
        {
            var root = new GameObject("Gondola").transform;
            Box(root, "Plinth", new Vector3(8f, 0.15f, 1.2f), new Vector3(0, 0.075f, 0), M("M_WallTrim"), default, true);
            Box(root, "Back", new Vector3(7.9f, 1.0f, 0.08f), new Vector3(0, 0.65f, 0), M("M_ShelfMetal"), default, true);
            Box(root, "Shelf_Mid", new Vector3(7.9f, 0.04f, 1.1f), new Vector3(0, 0.55f, 0), M("M_ShelfBoard"), default, true);
            Box(root, "Shelf_Top", new Vector3(8.0f, 0.05f, 1.2f), new Vector3(0, 1.15f, 0), M("M_ShelfBoard"), default, true);
            Box(root, "LipA", new Vector3(7.9f, 0.07f, 0.03f), new Vector3(0, 0.57f, -0.55f), M("M_Yellow"));
            Box(root, "LipB", new Vector3(7.9f, 0.07f, 0.03f), new Vector3(0, 0.57f, 0.55f), M("M_Yellow"));
            Box(root, "LipC", new Vector3(8.0f, 0.07f, 0.03f), new Vector3(0, 1.16f, -0.6f), M("M_SaleStripes"));
            Box(root, "LipD", new Vector3(8.0f, 0.07f, 0.03f), new Vector3(0, 1.16f, 0.6f), M("M_SaleStripes"));
            Box(root, "EndL", new Vector3(0.08f, 1.2f, 1.2f), new Vector3(-3.98f, 0.6f, 0), M("M_WallTrim"), default, true);
            Box(root, "EndR", new Vector3(0.08f, 1.2f, 1.2f), new Vector3(3.98f, 0.6f, 0), M("M_WallTrim"), default, true);
            Box(root, "EndSignL", new Vector3(0.08f, 0.5f, 0.9f), new Vector3(-3.98f, 1.45f, 0), M("M_Yellow"));
            Box(root, "EndSignR", new Vector3(0.08f, 0.5f, 0.9f), new Vector3(3.98f, 1.45f, 0), M("M_Yellow"));
            BakeToAsset(root.gameObject, "Gondola");
            var t1 = Text3D(root, "%50", new Vector3(-4.03f, 1.45f, 0), new Vector3(0, 90, 0), 0.04f, new Color(0.9f, 0.1f, 0.15f));
            var t2 = Text3D(root, "%70", new Vector3(4.03f, 1.45f, 0), new Vector3(0, -90, 0), 0.04f, new Color(0.9f, 0.1f, 0.15f));
            t1.name = "EndTextL"; t2.name = "EndTextR";

            var spRoot = new GameObject("SpawnPoints").transform; spRoot.SetParent(root, false);
            foreach (float y in new[] { 0.155f, 0.575f, 1.18f })
                foreach (float x in new[] { -3.2f, -1.6f, 0f, 1.6f, 3.2f })
                    foreach (float z in new[] { -0.3f, 0.3f })
                    {
                        var sp = new GameObject("SP").transform; sp.SetParent(spRoot, false);
                        sp.localPosition = new Vector3(x, y, z);
                        sp.localEulerAngles = new Vector3(0, z > 0 ? 180 : 0, 0);
                        var c = sp.gameObject.AddComponent<ShelfSpawnPoint>();
                        if (y < 0.3f) c.allowedProducts = SmallItems;   // alt kat: alçak ürünler
                        else if (y < 1f) c.allowedProducts = NoTV;      // orta kat
                    }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            SavePrefab(root.gameObject, R + "/Prefabs/Props/Gondola.prefab");
        }

        // Tek taraflı duvar rafı: uzunluk X (6m), ön yüz -Z
        static void BuildWallShelf()
        {
            var root = new GameObject("WallShelf").transform;
            Box(root, "Plinth", new Vector3(6f, 0.15f, 0.7f), new Vector3(0, 0.075f, 0), M("M_WallTrim"), default, true);
            Box(root, "Back", new Vector3(6f, 2.4f, 0.08f), new Vector3(0, 1.2f, 0.31f), M("M_ShelfMetal"), default, true);
            foreach (float y in new[] { 0.75f, 1.45f, 2.1f })
                Box(root, "Shelf_" + y, new Vector3(5.9f, 0.04f, 0.62f), new Vector3(0, y, 0), M("M_ShelfBoard"), default, true);
            Box(root, "Lip1", new Vector3(5.9f, 0.08f, 0.03f), new Vector3(0, 0.77f, -0.31f), M("M_Yellow"));
            Box(root, "Lip2", new Vector3(5.9f, 0.08f, 0.03f), new Vector3(0, 1.47f, -0.31f), M("M_Yellow"));
            Box(root, "SideL", new Vector3(0.06f, 2.4f, 0.7f), new Vector3(-2.97f, 1.2f, 0), M("M_WallTrim"), default, true);
            Box(root, "SideR", new Vector3(0.06f, 2.4f, 0.7f), new Vector3(2.97f, 1.2f, 0), M("M_WallTrim"), default, true);
            BakeToAsset(root.gameObject, "WallShelf");
            var spRoot = new GameObject("SpawnPoints").transform; spRoot.SetParent(root, false);
            foreach (float y in new[] { 0.155f, 0.775f, 1.475f })
                foreach (float x in new[] { -2.2f, -0.75f, 0.75f, 2.2f })
                {
                    var sp = new GameObject("SP").transform; sp.SetParent(spRoot, false);
                    sp.localPosition = new Vector3(x, y, -0.05f);
                    sp.gameObject.AddComponent<ShelfSpawnPoint>().allowedProducts = NoTV;
                }
            SavePrefab(root.gameObject, R + "/Prefabs/Props/WallShelf.prefab");
        }

        static void BuildDealTable()
        {
            var root = new GameObject("DealTable").transform;
            var skirt = Cyl(root, "Skirt", 1f, 0.7f, new Vector3(0, 0.35f, 0), M("M_SaleStripes"), default, 24);
            Cyl(root, "Top", 1.08f, 0.06f, new Vector3(0, 0.73f, 0), M("M_Red"), default, 24);
            Cyl(root, "Pole", 0.04f, 1.6f, new Vector3(0, 1.55f, 0), M("M_Chrome"));
            Box(root, "Sign", new Vector3(1.6f, 0.55f, 0.06f), new Vector3(0, 2.4f, 0), M("M_Yellow"));
            BakeToAsset(root.gameObject, "DealTable");
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.38f, 0); col.size = new Vector3(1.6f, 0.76f, 1.6f);
            var pcol = root.gameObject.AddComponent<BoxCollider>();
            pcol.center = new Vector3(0, 1.55f, 0); pcol.size = new Vector3(0.1f, 1.6f, 0.1f);
            Text3D(root, "MEGA İNDİRİM", new Vector3(0, 2.4f, -0.035f), Vector3.zero, 0.022f, new Color(0.85f, 0.1f, 0.15f));
            Text3D(root, "MEGA İNDİRİM", new Vector3(0, 2.4f, 0.035f), new Vector3(0, 180, 0), 0.022f, new Color(0.85f, 0.1f, 0.15f));
            var spRoot = new GameObject("SpawnPoints").transform; spRoot.SetParent(root, false);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2 / 5;
                var sp = new GameObject("SP").transform; sp.SetParent(spRoot, false);
                sp.localPosition = new Vector3(Mathf.Cos(a) * 0.62f, 0.765f, Mathf.Sin(a) * 0.62f);
                sp.localRotation = Quaternion.LookRotation(-new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                sp.gameObject.AddComponent<ShelfSpawnPoint>().hotDeal = true;
            }
            SavePrefab(root.gameObject, R + "/Prefabs/Props/DealTable.prefab");
        }

        // Kasa bankosu: uzunluk Z, müşteri tarafı +X
        static void BuildCheckout()
        {
            var root = new GameObject("Checkout").transform;
            Box(root, "Body", new Vector3(0.9f, 0.92f, 3.2f), new Vector3(0, 0.46f, 0), M("M_CounterBlue"), default, true);
            Box(root, "Top", new Vector3(1.0f, 0.06f, 3.3f), new Vector3(0, 0.95f, 0), M("M_White"), default, true);
            Box(root, "Belt", new Vector3(0.62f, 0.03f, 2.1f), new Vector3(0.05f, 0.995f, 0.45f), M("M_Conveyor"));
            Box(root, "Register", new Vector3(0.42f, 0.22f, 0.38f), new Vector3(-0.12f, 1.09f, -1.15f), M("M_DarkGrey"), default, true);
            Box(root, "Drawer", new Vector3(0.44f, 0.06f, 0.4f), new Vector3(-0.12f, 1.0f, -1.15f), M("M_Black"));
            Box(root, "ScreenStand", new Vector3(0.04f, 0.3f, 0.04f), new Vector3(-0.12f, 1.35f, -1.15f), M("M_Chrome"));
            Box(root, "Screen", new Vector3(0.05f, 0.22f, 0.32f), new Vector3(-0.05f, 1.55f, -1.15f), M("M_ScreenGlow"), new Vector3(0, 0, -10));
            Box(root, "Divider", new Vector3(0.06f, 0.07f, 0.4f), new Vector3(0.05f, 1.02f, 1.4f), M("M_Red"));
            Cyl(root, "LightPole", 0.04f, 2.5f, new Vector3(0.35f, 1.25f, 1.55f), M("M_Chrome"));
            Box(root, "NumberSign", new Vector3(0.1f, 0.5f, 0.5f), new Vector3(0.35f, 2.6f, 1.55f), M("M_NeonYellow"));
            BakeToAsset(root.gameObject, "Checkout");
            var tA = Text3D(root, "KASA", new Vector3(0.41f, 2.6f, 1.55f), new Vector3(0, -90, 0), 0.016f, new Color(0.1f, 0.1f, 0.12f));
            tA.name = "Number";
            var tB = Text3D(root, "KASA", new Vector3(0.29f, 2.6f, 1.55f), new Vector3(0, 90, 0), 0.016f, new Color(0.1f, 0.1f, 0.12f));
            tB.name = "Number2";
            SavePrefab(root.gameObject, R + "/Prefabs/Props/Checkout.prefab");
        }

        static void BuildCart()
        {
            var root = new GameObject("Cart").transform;
            Box(root, "Bottom", new Vector3(0.55f, 0.03f, 0.85f), new Vector3(0, 0.45f, 0), M("M_Red"));
            Box(root, "SideL", new Vector3(0.03f, 0.4f, 0.85f), new Vector3(-0.27f, 0.65f, 0), M("M_Red"));
            Box(root, "SideR", new Vector3(0.03f, 0.4f, 0.85f), new Vector3(0.27f, 0.65f, 0), M("M_Red"));
            Box(root, "Front", new Vector3(0.55f, 0.4f, 0.03f), new Vector3(0, 0.65f, 0.42f), M("M_Red"));
            Box(root, "Back", new Vector3(0.55f, 0.4f, 0.03f), new Vector3(0, 0.65f, -0.42f), M("M_Red"));
            Cyl(root, "Handle", 0.025f, 0.6f, new Vector3(0, 0.95f, -0.55f), M("M_Black"), new Vector3(0, 0, 90));
            Box(root, "HandleBarL", new Vector3(0.03f, 0.3f, 0.03f), new Vector3(-0.27f, 0.85f, -0.5f), M("M_Chrome"), new Vector3(-20, 0, 0));
            Box(root, "HandleBarR", new Vector3(0.03f, 0.3f, 0.03f), new Vector3(0.27f, 0.85f, -0.5f), M("M_Chrome"), new Vector3(-20, 0, 0));
            Box(root, "Frame", new Vector3(0.45f, 0.4f, 0.04f), new Vector3(0, 0.25f, 0), M("M_Chrome"));
            foreach (var w in new[] { new Vector3(-0.22f, 0.06f, 0.33f), new Vector3(0.22f, 0.06f, 0.33f), new Vector3(-0.22f, 0.06f, -0.33f), new Vector3(0.22f, 0.06f, -0.33f) })
                Cyl(root, "Wheel", 0.06f, 0.04f, w, M("M_Black"), new Vector3(0, 0, 90), 10);
            BakeToAsset(root.gameObject, "Cart");
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, -0.05f); col.size = new Vector3(0.6f, 1f, 1.1f);
            SavePrefab(root.gameObject, R + "/Prefabs/Props/Cart.prefab");
        }

        static void BuildPlant()
        {
            var root = new GameObject("Plant").transform;
            Cyl(root, "Pot", 0.3f, 0.5f, new Vector3(0, 0.25f, 0), M("M_Pot"), default, 12);
            Ball(root, "Leaves1", 0.45f, new Vector3(0, 0.85f, 0), M("M_Plant"));
            Ball(root, "Leaves2", 0.3f, new Vector3(0.18f, 1.2f, 0.05f), M("M_Plant"));
            Ball(root, "Leaves3", 0.28f, new Vector3(-0.15f, 1.15f, -0.1f), M("M_Plant"));
            BakeToAsset(root.gameObject, "Plant");
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.6f, 0); col.height = 1.2f; col.radius = 0.35f;
            SavePrefab(root.gameObject, R + "/Prefabs/Props/Plant.prefab");
        }
    }
}
