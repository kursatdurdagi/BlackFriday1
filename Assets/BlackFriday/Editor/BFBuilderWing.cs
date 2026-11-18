using System.Collections.Generic;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace BlackFriday.EditorTools
{
    /// <summary>
    /// AVM genişlemesi ve çevre etkileşimi:
    /// - Doğu kanadı: Yemek Alanı (6 büfe, masalar) + MEGA MARKET (16 reyon, 3 kasa, fırsat masaları)
    /// - Üst kat: iki köprü, balkonlarda fırsat masaları
    /// - Etkileşim: itilebilir alışveriş arabaları, devrilen koli kuleleri, kaygan zemin, yürüyen merdiven bandı,
    ///   otomatik giriş kapısı, koridor boyunca bayrak süsleri
    /// </summary>
    public static partial class BFBuilder
    {
        const float WingX0 = 40.4f, WingX1 = 78.4f, WingZ = 25.3f, WingH = 7f, MarketX = 57f;

        // ---------------------------------------------------------------- kasa grubu (müşteri tarafı yerel +X)
        static void AddCheckout(Transform parent, Vector3 pos, float yaw, CharacterLibrary lib, int skin)
        {
            var g = new GameObject("CheckoutGroup").transform;
            g.SetParent(parent, false); g.localPosition = pos; g.localEulerAngles = new Vector3(0, yaw, 0);
            var co = Place(P("Props/Checkout"), g, Vector3.zero, 0);
            foreach (var tm in co.GetComponentsInChildren<TextMesh>()) tm.text = "KASA";
            var zoneGo = new GameObject("CheckoutZone");
            zoneGo.transform.SetParent(g, false);
            zoneGo.transform.localPosition = new Vector3(1.75f, 0, 0);
            var zone = zoneGo.AddComponent<CheckoutZone>();
            zone.size = new Vector3(2.3f, 3f, 3.4f);
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
                var cashier = (GameObject)PrefabUtility.InstantiatePrefab(lib.npcModels[skin % lib.npcModels.Count]);
                cashier.name = "Cashier";
                cashier.transform.SetParent(g, false);
                cashier.transform.localPosition = new Vector3(-1.1f, 0, 1.0f);
                cashier.transform.localEulerAngles = new Vector3(0, 90, 0);
                foreach (var an in cashier.GetComponentsInChildren<Animator>()) an.cullingMode = AnimatorCullingMode.CullCompletely;
            }
        }

        // ---------------------------------------------------------------- yemek alanı parçaları
        static void CafeTable(Transform parent, Vector3 pos, string stoolMat)
        {
            var t = new GameObject("CafeTable").transform; t.SetParent(parent, false); t.localPosition = pos;
            Cyl(t, "Top", 0.55f, 0.05f, new Vector3(0, 0.74f, 0), M("M_White"), default, 20);
            Cyl(t, "Leg", 0.05f, 0.7f, new Vector3(0, 0.37f, 0), M("M_Chrome"), default, 8);
            Cyl(t, "Foot", 0.28f, 0.04f, new Vector3(0, 0.02f, 0), M("M_Chrome"), default, 14);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 0.82f, 0, Mathf.Sin(a) * 0.82f);
                Cyl(t, "Stool", 0.19f, 0.07f, p + Vector3.up * 0.47f, M(stoolMat), default, 12);
                Cyl(t, "StoolLeg", 0.035f, 0.44f, p + Vector3.up * 0.22f, M("M_Chrome"), default, 6);
            }
            // tepsi + bardak
            Box(t, "Tray", new Vector3(0.36f, 0.02f, 0.26f), new Vector3(0.1f, 0.775f, 0.05f), M("M_Red"));
            Cyl(t, "Cup", 0.045f, 0.13f, new Vector3(0.02f, 0.85f, 0.02f), M("M_White"), default, 8);
            var col = t.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.39f, 0); col.size = new Vector3(1.15f, 0.78f, 1.15f);
        }

        static void FoodStall(Transform parent, float x, int side, string name, string mat, string accent)
        {
            // side: +1 kuzey duvarı, -1 güney duvarı; tezgâh koridora bakar
            var s = new GameObject("Stall_" + name).transform; s.SetParent(parent, false);
            s.localPosition = new Vector3(x, 0, side * (WingZ - 0.2f));
            s.localEulerAngles = new Vector3(0, side > 0 ? 180 : 0, 0);   // yerel +Z = salonun içi
            Box(s, "Back", new Vector3(4.6f, 4.2f, 0.12f), new Vector3(0, 2.1f, 0.08f), M(mat));
            Box(s, "Counter", new Vector3(4.4f, 1.05f, 0.9f), new Vector3(0, 0.525f, 2.2f), M("M_White"), default, true);
            Box(s, "CounterTop", new Vector3(4.5f, 0.06f, 1.0f), new Vector3(0, 1.08f, 2.2f), M("M_Wood"));
            NoShadow(Box(s, "CounterStripe", new Vector3(4.42f, 0.18f, 0.92f), new Vector3(0, 0.3f, 2.2f), M(accent)));
            Box(s, "SideL", new Vector3(0.14f, 3.4f, 2.6f), new Vector3(-2.3f, 1.7f, 1.4f), M(mat), default, true);
            Box(s, "SideR", new Vector3(0.14f, 3.4f, 2.6f), new Vector3(2.3f, 1.7f, 1.4f), M(mat), default, true);
            Box(s, "Awning", new Vector3(4.9f, 0.22f, 1.9f), new Vector3(0, 3.35f, 2.3f), M(accent), new Vector3(-8, 0, 0));
            Box(s, "SignBoard", new Vector3(4.2f, 0.95f, 0.14f), new Vector3(0, 4.15f, 2.9f), M("M_Fascia"));
            Text3D(s, name, new Vector3(0, 4.15f, 2.98f), new Vector3(0, 180, 0), 0.045f, new Color(1f, 0.86f, 0.25f), true);
            // menü panoları + tezgâh üstü
            for (int i = -1; i <= 1; i++)
                NoShadow(Box(s, "Menu", new Vector3(1.1f, 0.7f, 0.05f), new Vector3(i * 1.35f, 2.6f, 0.18f), M("M_ScreenGlow")));
            Box(s, "Register", new Vector3(0.4f, 0.3f, 0.35f), new Vector3(1.5f, 1.26f, 2.2f), M("M_DarkGrey"));
            for (int i = 0; i < 3; i++)
                Cyl(s, "CupStack", 0.06f, 0.22f + i * 0.05f, new Vector3(-1.6f + i * 0.2f, 1.22f + i * 0.025f, 2.3f), M("M_White"), default, 8);
            Box(s, "Display", new Vector3(1.2f, 0.4f, 0.5f), new Vector3(-0.2f, 1.31f, 2.25f), M("M_Glass"));
        }

        static void Flags(Transform parent, Vector3 a, Vector3 b, int count, int seed)
        {
            string[] mats = { "M_Red", "M_Yellow", "M_Cyan", "M_Pink", "M_Orange", "M_Green", "M_Purple" };
            Vector3 d = b - a; float len = d.magnitude;
            var g = new GameObject("Flags").transform; g.SetParent(parent, false);
            g.localPosition = a; g.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            NoShadow(Box(g, "Cable", new Vector3(0.015f, 0.015f, len), new Vector3(0, 0, len * 0.5f), M("M_Black")));
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count; float sag = Mathf.Sin(t * Mathf.PI) * 0.0f;
                NoShadow(Box(g, "Flag", new Vector3(0.02f, 0.42f, 0.32f), new Vector3(0, -0.23f - sag, t * len), M(mats[(i + seed) % mats.Length]), new Vector3(0, 0, 0)));
            }
        }

        // ---------------------------------------------------------------- doğu kanadı
        static List<Transform> BuildEastWing(Transform mall, Transform lights, Transform probeRoot, List<ReflectionProbe> probes, CharacterLibrary lib)
        {
            var chunks = new List<Transform>();
            float cx = (WingX0 + WingX1) / 2, len = WingX1 - WingX0;

            // ---- kabuk
            var shell = new GameObject("Wing_Shell").transform; shell.SetParent(mall, false); chunks.Add(shell);
            Box(shell, "Floor", new Vector3(len, 0.2f, WingZ * 2), new Vector3(cx, -0.1f, 0), M("M_Marble"), default, true);
            NoShadow(Box(shell, "FoodFloor", new Vector3(MarketX - WingX0 - 1.6f, 0.012f, 38f), new Vector3((WingX0 + MarketX) / 2, 0.006f, 0), M("M_WoodFloor")));
            NoShadow(Box(shell, "MarketFloor", new Vector3(WingX1 - MarketX - 0.4f, 0.012f, WingZ * 2 - 0.6f), new Vector3((MarketX + WingX1) / 2, 0.006f, 0), M("M_Floor")));
            foreach (int s in new[] { 1, -1 })
                Box(shell, "Wall_" + s, new Vector3(len, WingH + 0.4f, 0.4f), new Vector3(cx, WingH / 2 + 0.2f, s * (WingZ + 0.2f)), M("M_MallWall"), default, true);
            Box(shell, "Wall_E", new Vector3(0.4f, WingH + 0.4f, WingZ * 2 + 0.8f), new Vector3(WingX1 + 0.2f, WingH / 2 + 0.2f, 0), M("M_MallWall"), default, true);
            Box(shell, "Ceiling", new Vector3(len, 0.3f, WingZ * 2 + 0.8f), new Vector3(cx, WingH + 0.15f, 0), M("M_CeilingTile"));
            // tavan ışık panelleri + ışıklar
            foreach (float x in new[] { 45f, 51f, 61f, 67f, 73f })
                foreach (float z in new[] { -18f, -9f, 0f, 9f, 18f })
                    NoShadow(Box(shell, "LightPanel", new Vector3(3.2f, 0.05f, 1.1f), new Vector3(x, WingH - 0.03f, z), M("M_LightPanel")));
            foreach (float x in new[] { 45.5f, 52.5f })
                foreach (float z in new[] { -13f, 0f, 13f })
                    SpotL(lights, new Vector3(x, WingH - 0.2f, z), new Vector3(x, 0, z + 0.01f), new Color(1f, 0.86f, 0.66f), 8.5f, 10f, 128f, false).innerSpotAngle = 75f;
            foreach (float x in new[] { 62f, 68f, 74f })
                foreach (float z in new[] { -16f, 0f, 16f })
                    SpotL(lights, new Vector3(x, WingH - 0.2f, z), new Vector3(x, 0, z + 0.01f), new Color(0.92f, 0.96f, 1f), 9f, 11f, 132f, false).innerSpotAngle = 80f;
            Probe(probeRoot, new Vector3((WingX0 + MarketX) / 2, 3f, 0), new Vector3(MarketX - WingX0, WingH, WingZ * 2), probes);
            Probe(probeRoot, new Vector3((MarketX + WingX1) / 2, 3f, 0), new Vector3(WingX1 - MarketX, WingH, WingZ * 2), probes);

            // market ayırıcı duvar + giriş tabelası
            foreach (int s in new[] { 1, -1 })
                Box(shell, "Divider_" + s, new Vector3(0.3f, WingH, WingZ - 9f), new Vector3(MarketX, WingH / 2, s * (9f + (WingZ - 9f) / 2)), M("M_Red"), default, true);
            Box(shell, "MarketFascia", new Vector3(0.5f, 1.9f, 18f), new Vector3(MarketX, WingH - 0.95f, 0), M("M_Fascia"));
            NoShadow(Box(shell, "MarketNeon", new Vector3(0.54f, 0.1f, 18f), new Vector3(MarketX, WingH - 1.95f, 0), M("M_NeonYellow")));
            Text3D(shell, "MEGA MARKET", new Vector3(MarketX - 0.27f, WingH - 0.85f, 0), new Vector3(0, 90, 0), 0.085f, new Color(1f, 0.84f, 0.2f), true);
            Text3D(shell, "HER ŞEY %90 İNDİRİMLİ  •  3 KASA AÇIK", new Vector3(MarketX - 0.27f, WingH - 1.55f, 0), new Vector3(0, 90, 0), 0.026f, Color.white);
            Text3D(shell, "TEŞEKKÜRLER  •  YİNE BEKLERİZ", new Vector3(MarketX + 0.27f, WingH - 1.0f, 0), new Vector3(0, -90, 0), 0.04f, new Color(1f, 0.84f, 0.2f), true);
            foreach (float z in new[] { -9f, 9f })
                Box(shell, "GatePost", new Vector3(0.5f, WingH, 0.5f), new Vector3(MarketX, WingH / 2, z), M("M_Yellow"), default, true);
            // market duvar şeritleri
            foreach (int s in new[] { 1, -1 })
            {
                NoShadow(Box(shell, "WallStripe", new Vector3(WingX1 - MarketX, 0.7f, 0.05f), new Vector3((MarketX + WingX1) / 2, 4.6f, s * (WingZ - 0.03f)), M("M_Red")));
                Text3D(shell, "MEGA MARKET  •  BLACK FRIDAY  •  %90", new Vector3((MarketX + WingX1) / 2, 4.6f, s * (WingZ - 0.07f)), new Vector3(0, s > 0 ? 0 : 180, 0), 0.035f, Color.white, true);
            }
            NoShadow(Box(shell, "WallStripeE", new Vector3(0.05f, 0.7f, WingZ * 2), new Vector3(WingX1 - 0.03f, 4.6f, 0), M("M_Red")));
            // doğu duvarı: dev ekran duvarı
            Box(shell, "ScreenFrame", new Vector3(0.2f, 3.2f, 13f), new Vector3(WingX1 - 0.12f, 2.4f, 0), M("M_Black"));
            NoShadow(Box(shell, "ScreenWall", new Vector3(0.06f, 2.9f, 12.6f), new Vector3(WingX1 - 0.24f, 2.4f, 0), M("M_ScreenWall")));
            Text3D(shell, "FLAŞ İNDİRİMLERİ KAÇIRMA!", new Vector3(WingX1 - 0.3f, 4.6f, 0), new Vector3(0, 90, 0), 0.04f, new Color(1f, 0.84f, 0.2f), true);

            // ---- yemek alanı
            var food = new GameObject("Wing_FoodCourt").transform; food.SetParent(mall, false); chunks.Add(food);
            var stalls = new (string n, string m, string a)[]
            {
                ("BURGER", "M_Wall_Terracotta", "M_Red"), ("KAHVE", "M_Brown", "M_LightBrown"), ("DONDURMA", "M_Wall_Blush", "M_Pink"),
                ("PİZZA", "M_Wall_Green", "M_Orange"), ("DÖNER", "M_Wall_Navy", "M_Yellow"), ("TATLI", "M_Wall_Plum", "M_Purple"),
            };
            float[] sx = { 43.6f, 48.8f, 54f };
            for (int i = 0; i < 6; i++)
                FoodStall(food, sx[i % 3], i < 3 ? 1 : -1, stalls[i].n, stalls[i].m, stalls[i].a);
            string[] stoolMats = { "M_Red", "M_Yellow", "M_Cyan", "M_Orange" };
            int ti = 0;
            foreach (float x in new[] { 44.5f, 48.6f, 52.7f })
                foreach (float z in new[] { -17f, -12.8f, -8.6f, 8.6f, 12.8f, 17f })
                    CafeTable(food, new Vector3(x + ((ti & 1) == 0 ? 0.4f : -0.4f), 0, z), stoolMats[ti++ % stoolMats.Length]);
            Box(food, "HangSign", new Vector3(0.14f, 1.3f, 8f), new Vector3(48.7f, 5.5f, 0), M("M_Fascia"));
            NoShadow(Box(food, "HangSignNeon", new Vector3(0.16f, 0.08f, 8f), new Vector3(48.7f, 4.82f, 0), M("M_Neon_Gold")));
            foreach (int s in new[] { 1, -1 })
            {
                Text3D(food, "YEMEK ALANI", new Vector3(48.7f + s * 0.08f, 5.6f, 0), new Vector3(0, s > 0 ? -90 : 90, 0), 0.06f, new Color(1f, 0.84f, 0.2f), true);
                Text3D(food, "MOLA VER, SONRA YİNE KOŞ", new Vector3(48.7f + s * 0.08f, 5.1f, 0), new Vector3(0, s > 0 ? -90 : 90, 0), 0.022f, Color.white);
                Cyl(food, "HangCable", 0.02f, 1f, new Vector3(48.7f, 6.6f, s * 3.6f), M("M_Black"), default, 6);
            }
            foreach (var p in new[] { new Vector3(41.6f, 0, -8.6f), new Vector3(41.6f, 0, 8.6f), new Vector3(55.6f, 0, -10.6f), new Vector3(55.6f, 0, 10.6f) })
                Place(P("Props/Tree"), food, p, 0);
            Place(P("Props/Bench"), food, new Vector3(44f, 0, -4.2f), 180);
            Place(P("Props/Bench"), food, new Vector3(53f, 0, 4.2f), 0);
            Place(P("Props/DealTable"), food, new Vector3(49f, 0, 0.3f), 30);

            // ---- MEGA MARKET
            var market = new GameObject("Wing_MEGA_MARKET").transform; market.SetParent(mall, false); chunks.Add(market);
            foreach (float z in new[] { -21.6f, -17.6f, -13.6f, -9.8f, 9.8f, 13.6f, 17.6f, 21.6f })
                foreach (float x in new[] { 66.2f, 74.3f })
                    Place(P("Props/Gondola"), market, new Vector3(x, 0, z), 0);
            // reyon tabelaları
            string[] aisle = { "ELEKTRONİK", "OYUNCAK", "MUTFAK", "SPOR", "MODA", "KOZMETİK", "EV", "FIRSAT" };
            int ai = 0;
            foreach (float z in new[] { -19.6f, -11.7f, 11.7f, 19.6f })
            {
                Box(market, "AisleSign", new Vector3(0.1f, 0.6f, 2.6f), new Vector3(62.4f, 5.2f, z), M("M_Fascia"));
                Cyl(market, "AisleCable", 0.015f, 1.3f, new Vector3(62.4f, 6.15f, z), M("M_Black"), default, 6);
                Text3D(market, aisle[ai++], new Vector3(62.34f, 5.2f, z), new Vector3(0, 90, 0), 0.03f, new Color(1f, 0.84f, 0.2f), true);
                Text3D(market, aisle[ai++], new Vector3(62.46f, 5.2f, z), new Vector3(0, -90, 0), 0.03f, new Color(1f, 0.84f, 0.2f), true);
            }
            // orta alan: fırsat masaları + teşhir masaları
            foreach (var p in new[] { new Vector3(66f, 0, 0f), new Vector3(71.5f, 0, -3.6f), new Vector3(71.5f, 0, 3.6f) })
                Place(P("Props/DealTable"), market, p, Random.Range(0, 360));
            foreach (var p in new[] { new Vector3(75.6f, 0, -3.2f), new Vector3(75.6f, 0, 3.2f), new Vector3(67.5f, 0, -5.4f), new Vector3(67.5f, 0, 5.4f) })
                Place(P("Props/DisplayTable"), market, p, p.x > 74 ? 90 : 0);
            // 3 kasa (müşteri şeridi kasanın güneyinde)
            int k = 0;
            foreach (float z in new[] { -4.2f, 1f, 6.2f })
                AddCheckout(market, new Vector3(60.4f, 0, z), 90, lib, 7 + k++ * 3);
            Box(market, "ExitRail", new Vector3(3.4f, 0.9f, 0.08f), new Vector3(60.4f, 0.45f, 8.6f), M("M_Chrome"), default, true);
            BuildStoreStock(market, "MEGA MARKET", 41);
            return chunks;
        }

        // ---------------------------------------------------------------- üst kat köprüleri + balkon fırsatları
        static void BuildBridges(Transform shell, Transform conc)
        {
            foreach (float x in new[] { -28f, 28f })
            {
                Box(shell, "Bridge", new Vector3(2.6f, 0.3f, 14f), new Vector3(x, 5.15f, 0), M("M_MallWall"), default, true);
                NoShadow(Box(shell, "BridgeFloor", new Vector3(2.6f, 0.02f, 14f), new Vector3(x, 5.31f, 0), M("M_Marble")));
                NoShadow(Box(shell, "BridgeStripe", new Vector3(0.5f, 0.022f, 14f), new Vector3(x, 5.312f, 0), M("M_Gold")));
                foreach (float o in new[] { -1.3f, 1.3f })
                {
                    NoShadow(Box(shell, "BridgeGlass", new Vector3(0.05f, 1.0f, 14f), new Vector3(x + o, 5.85f, 0), M("M_Glass"), default, true));
                    Box(shell, "BridgeRail", new Vector3(0.12f, 0.06f, 14f), new Vector3(x + o, 6.38f, 0), M("M_Chrome"));
                    Box(shell, "BridgeEdge", new Vector3(0.12f, 0.3f, 14f), new Vector3(x + o, 5.15f, 0), M("M_MallTrim"));
                }
                NoShadow(Box(shell, "BridgeNeon", new Vector3(2.6f, 0.06f, 0.1f), new Vector3(x, 5.0f, 0), M("M_Neon_Gold")));
            }
            // balkonlarda sıcak fırsat masaları: yukarı çıkan ödüllenir
            foreach (var p in new[] { new Vector3(-18f, 5.3f, 9f), new Vector3(18f, 5.3f, -9f), new Vector3(35f, 5.3f, 9f), new Vector3(-35f, 5.3f, -9f) })
                Place(P("Props/DealTable"), conc, p, Random.Range(0, 360));
            foreach (var p in new[] { new Vector3(-8f, 5.3f, 9.6f), new Vector3(8f, 5.3f, -9.6f) })
                Place(P("Props/Bench"), conc, p, p.z > 0 ? 180 : 0);
            // koridor boyunca bayrak süsleri
            int seed = 0;
            foreach (float x in new[] { -36f, -30f, -20f, 20f, 30f, 36f })
                Flags(conc, new Vector3(x, 4.7f, -6.9f), new Vector3(x, 4.7f, 6.9f), 18, seed++);
        }

        // ---------------------------------------------------------------- etkileşimli nesneler (birleştirilmez, statik değil)
        static Mesh CartonMeshAsset(int cell)
        {
            string path = $"{R}/Meshes/Carton_{cell}.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m != null) return m;
            m = Object.Instantiate(CartonMesh(cell)); m.name = "Carton_" + cell;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static PhysicsMaterial SlideMat()
        {
            string path = $"{R}/Materials/PM_Slide.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm != null) return pm;
            pm = new PhysicsMaterial("PM_Slide") { dynamicFriction = 0.12f, staticFriction = 0.15f, bounciness = 0.1f, frictionCombine = PhysicsMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        static void Unstatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        static void PushCart(Transform parent, Vector3 pos, float yaw)
        {
            var root = new GameObject("PushCart");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos + Vector3.up * 0.02f, Quaternion.Euler(0, yaw, 0));
            var vis = Place(P("Props/Cart"), root.transform, Vector3.zero, 0);
            Unstatic(vis);
            foreach (var c in vis.GetComponentsInChildren<Collider>()) c.sharedMaterial = SlideMat();
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 14f; rb.linearDamping = 0.9f; rb.angularDamping = 2.5f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<NetworkIdentity>();
            var nt = root.AddComponent<NetworkTransformUnreliable>();
            nt.target = root.transform; nt.syncDirection = SyncDirection.ServerToClient; nt.syncScale = false;
            root.AddComponent<PushableProp>();
        }

        static void Tower(Transform parent, Vector3 pos, float yaw, int[] cells, int seed)
        {
            var root = new GameObject("BoxTower");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var rng = new System.Random(seed * 131 + 5);
            var mat = StockMaterial();
            float y = 0.02f; int layer = 0;
            // 2x2 taban, yukarı doğru daralan 5 kat
            int[] perLayer = { 4, 4, 4, 1, 1 };
            foreach (int n in perLayer)
            {
                float h = 0.42f + (float)rng.NextDouble() * 0.08f;
                for (int i = 0; i < n; i++)
                {
                    float s = n == 1 ? 0.62f : 0.54f;
                    Vector3 lp = n == 1 ? new Vector3(0, y + h * 0.5f, 0)
                                        : new Vector3((i % 2 == 0 ? -1 : 1) * 0.285f, y + h * 0.5f, (i / 2 == 0 ? -1 : 1) * 0.285f);
                    var b = new GameObject("Carton");
                    b.transform.SetParent(root.transform, false);
                    b.transform.localPosition = lp;
                    b.transform.localEulerAngles = new Vector3(0, ((float)rng.NextDouble() - 0.5f) * 14f + (layer % 2) * 90f, 0);
                    b.transform.localScale = new Vector3(s, h, s);
                    b.AddComponent<MeshFilter>().sharedMesh = CartonMeshAsset(cells[rng.Next(cells.Length)]);
                    var r = b.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                }
                y += h; layer++;
            }
            var col = root.AddComponent<BoxCollider>(); col.center = new Vector3(0, y * 0.5f, 0); col.size = new Vector3(1.15f, y, 1.15f);
            var ob = root.AddComponent<NavMeshObstacle>(); ob.shape = NavMeshObstacleShape.Box; ob.center = col.center; ob.size = col.size; ob.carving = true;
            root.AddComponent<NetworkIdentity>();
            root.AddComponent<BoxTower>();
        }

        static void WetFloor(Transform parent, Transform deco, Vector3 pos)
        {
            var z = new GameObject("SlipZone");
            z.transform.SetParent(parent, false); z.transform.position = pos;
            z.AddComponent<SlipZone>().size = new Vector2(3.2f, 3.2f);
            NoShadow(Cyl(deco, "Puddle", 1.5f, 0.012f, pos + Vector3.up * 0.016f, M("M_Glass"), default, 24));
            NoShadow(Cyl(deco, "Puddle2", 0.8f, 0.012f, pos + new Vector3(0.9f, 0.017f, -0.7f), M("M_Glass"), default, 18));
            // sarı "kaygan zemin" levhası
            var s = new GameObject("WetSign").transform; s.SetParent(deco, false); s.localPosition = pos + new Vector3(1.75f, 0, 1.2f);
            Box(s, "A", new Vector3(0.34f, 0.7f, 0.03f), new Vector3(0, 0.33f, 0.1f), M("M_Yellow"), new Vector3(-16, 0, 0));
            Box(s, "B", new Vector3(0.34f, 0.7f, 0.03f), new Vector3(0, 0.33f, -0.1f), M("M_Yellow"), new Vector3(16, 0, 0));
            Text3D(s, "!", new Vector3(0, 0.4f, 0.14f), new Vector3(-16, 180, 0), 0.05f, new Color(0.1f, 0.1f, 0.1f));
            Text3D(s, "!", new Vector3(0, 0.4f, -0.14f), new Vector3(16, 0, 0), 0.05f, new Color(0.1f, 0.1f, 0.1f));
        }

        static void BuildInteractives(Transform deco)
        {
            var root = new GameObject("Interactive").transform;

            foreach (var (p, yaw) in new[] { (new Vector3(-30f, 0, -2.2f), 80f), (new Vector3(-17f, 0, 4.2f), 200f), (new Vector3(6.5f, 0, 5f), 130f),
                                             (new Vector3(18f, 0, -4.4f), 20f), (new Vector3(34f, 0, 1.5f), 265f), (new Vector3(46.5f, 0, 2.6f), 100f),
                                             (new Vector3(63.2f, 0, 3.2f), 95f), (new Vector3(63.2f, 0, -2.4f), 80f), (new Vector3(69.5f, 0, 7.4f), 10f) })
                PushCart(root, p, yaw);

            int seed = 0;
            foreach (var (p, cells) in new[] { (new Vector3(-12f, 0, 2.8f), new[] { 0, 1, 2 }), (new Vector3(12f, 0, -2.8f), new[] { 3, 4, 5 }),
                                               (new Vector3(-34.5f, 0, -3.2f), new[] { 9, 10, 11 }), (new Vector3(34.5f, 0, -3.6f), new[] { 6, 7, 8 }),
                                               (new Vector3(55.8f, 0, 0.4f), new[] { 3, 10, 6 }), (new Vector3(64f, 0, 0.3f), new[] { 0, 9, 4 }),
                                               (new Vector3(73.5f, 0, 0f), new[] { 1, 5, 8 }), (new Vector3(70.3f, 0, -11.7f), new[] { 12, 14, 2 }) })
                Tower(root, p, seed * 23f, cells, seed++);

            foreach (var p in new[] { new Vector3(16f, 0, 3.2f), new Vector3(-23f, 0, -2.4f), new Vector3(51f, 0, -3.4f), new Vector3(68f, 0, 15.6f) })
                WetFloor(root, deco, p);

            // yürüyen merdiven bantları (Escalator ile aynı yerleşim)
            foreach (int s in new[] { -1, 1 })
            {
                var c = new GameObject("Conveyor_" + (s < 0 ? "S" : "N"));
                c.transform.SetParent(root, false);
                c.transform.position = new Vector3(s < 0 ? -14f : 14f, 0, s < 0 ? -5.1f : 5.1f);
                c.transform.eulerAngles = new Vector3(0, s < 0 ? 90f : -90f, 0);
                var pivot = new GameObject("Zone").transform; pivot.SetParent(c.transform, false); pivot.localPosition = new Vector3(0.9f, 0, 4.5f);
                var cz = pivot.gameObject.AddComponent<ConveyorZone>();
                cz.size = new Vector3(1.9f, 7f, 9.2f); cz.localVelocity = new Vector3(0, 0, 1.7f);
            }

            // otomatik giriş kapısı
            var door = new GameObject("EntranceDoor");
            door.transform.SetParent(root, false); door.transform.position = new Vector3(-40.45f, 0, 0);
            var a = NoShadow(Box(door.transform, "LeafA", new Vector3(0.06f, 4f, 2f), new Vector3(0, 2.05f, -1f), M("M_Glass")));
            var b = NoShadow(Box(door.transform, "LeafB", new Vector3(0.06f, 4f, 2f), new Vector3(0, 2.05f, 1f), M("M_Glass")));
            Box(a.transform, "Bar", new Vector3(0.09f, 4f, 0.08f), new Vector3(0, 0, 0.96f), M("M_Chrome"));
            Box(b.transform, "Bar", new Vector3(0.09f, 4f, 0.08f), new Vector3(0, 0, -0.96f), M("M_Chrome"));
            var ad = door.AddComponent<AutoDoor>();
            ad.leafA = a.transform; ad.leafB = b.transform; ad.range = 6.5f;
            ad.slideA = new Vector3(0, 0, -1.95f); ad.slideB = new Vector3(0, 0, 1.95f);
            Unstatic(root.gameObject);
        }
    }
}
