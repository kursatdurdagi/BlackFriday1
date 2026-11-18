using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BlackFriday
{
    [System.Serializable]
    public struct ListEntry
    {
        public int productId;
        public int required;
        public int delivered;
        public bool Done => delivered >= required;
    }

    /// <summary>
    /// MOD 1 — Alışveriş Listesi: Ortak listedeki ürünleri süre bitmeden kasaya taşıyın.
    /// Listede olmayan ürün teslim etmek puan kaybettirir.
    /// </summary>
    public class ShoppingListMode : GameModeBase
    {
        [Header("Alışveriş Listesi")]
        public int baseListEntries = 4;
        public int extraEntriesPerPlayer = 1;
        public int maxPerEntry = 3;
        public int decoyItems = 110;
        public int wrongItemPenalty = 30;
        public int timeBonusPerSecond = 5;

        [Header("NPC")]
        public GameObject npcPrefab;
        public int npcCount = 18;
        public Transform npcSpawnArea;

        [Header("Kurgu: kombo + flaş indirim")]
        public float comboWindow = 15f;      // art arda teslimler arası en fazla süre
        public int maxCombo = 4;
        public float flashInterval = 38f;    // flaş indirimler arası
        public float flashDuration = 18f;

        public readonly SyncList<ListEntry> list = new SyncList<ListEntry>();

        [SyncVar] public int combo = 1;              // bir sonraki teslimin çarpanı
        [SyncVar] public double comboEndTime;
        [SyncVar] public int flashProductId = -1;    // şu an çift puan veren ürün
        [SyncVar] public double flashEndTime;
        [SyncVar] public int timeBonus;
        [SyncVar] public int bonusPoints;
        [SyncVar] public int penaltyTotal;

        public bool FlashActive => flashProductId >= 0 && NetworkTime.time < flashEndTime;
        public int ComboNow => NetworkTime.time < comboEndTime ? Mathf.Max(1, combo) : 1;
        public float ComboLeft => Mathf.Max(0f, (float)(comboEndTime - NetworkTime.time));

        public const string Story =
            "Yılın indirimi başlıyor: Mega AVM'de her şey %90 ucuz! Ekibinle ortak listeyi topla, ürünleri mağaza kasalarına " +
            "taşı ya da kasa alanına fırlat. Müşteriler aynı ürünlerin peşinde — kaparlarsa bir şey fırlatıp düşür. " +
            "Art arda teslim = kombo, FLAŞ İNDİRİM'deki ürün = çift puan.";

        double nextFlashTime;
        bool announcedHalf, announcedLast;

        public override string ModeName => "Alışveriş Listesi";
        public override string ModeHint => Story;

        readonly List<GameObject> spawnedItems = new List<GameObject>();
        bool npcsSpawned;

        public int TotalRequired { get { int t = 0; foreach (var e in list) t += e.required; return t; } }
        public int TotalDelivered { get { int t = 0; foreach (var e in list) t += Mathf.Min(e.delivered, e.required); return t; } }

        // ------------------------------------------------------------ server
        protected override void ServerOnLobby()
        {
            ServerSpawnNpcs();
            // lobide raflar boş görünmesin diye vitrin ürünleri
            ServerBuildList();
            ServerSpawnProducts();
        }

        protected override void ServerPrepareRound()
        {
            ServerBuildList();
            ServerSpawnProducts();
            combo = 1; comboEndTime = 0; flashProductId = -1; flashEndTime = 0;
            timeBonus = 0; bonusPoints = 0; penaltyTotal = 0;
            announcedHalf = announcedLast = false;
            NPCShopper.MaxGrabbers = 3;
            ServerFeed("Yeni liste geldi! Kapılar açılınca koşun!", FeedKind.Info);
        }

        protected override void ServerOnRoundBegin()
        {
            nextFlashTime = NetworkTime.time + 16f;
            ServerFeed("ANONS: Kapılar açıldı! Black Friday başladı!", FeedKind.Announce);
        }

        /// <summary>Tur akışı: flaş indirimler, kalabalık dalgası, son saniye anonsu.</summary>
        protected override void ServerTickRound()
        {
            double now = NetworkTime.time;
            if (flashProductId >= 0 && now >= flashEndTime) flashProductId = -1;
            if (flashProductId < 0 && now >= nextFlashTime && TimeLeft > flashDuration + 6f)
            {
                var open = new List<int>();
                foreach (var e in list) if (!e.Done) open.Add(e.productId);
                if (open.Count > 0)
                {
                    flashProductId = open[Random.Range(0, open.Count)];
                    flashEndTime = now + flashDuration;
                    var d = ProductDatabase.Instance != null ? ProductDatabase.Instance.Get(flashProductId) : null;
                    ServerFeed($"FLAŞ İNDİRİM: {(d != null ? d.displayName : "Ürün")} şimdi çift puan!", FeedKind.Announce);
                }
                nextFlashTime = now + flashInterval;
            }
            if (!announcedHalf && TimeLeft <= roundSeconds * 0.5f)
            {
                announcedHalf = true;
                NPCShopper.MaxGrabbers = 5;
                ServerFeed("ANONS: Kalabalık artıyor! Kapıcılara dikkat edin.", FeedKind.Announce);
            }
            if (!announcedLast && TimeLeft <= 30f)
            {
                announcedLast = true;
                ServerFeed("ANONS: Kasalar 30 saniye sonra kapanıyor!", FeedKind.Announce);
            }
        }

        protected override bool ServerIsComplete()
        {
            foreach (var e in list) if (!e.Done) return false;
            return list.Count > 0;
        }

        protected override int ServerComputeStars(bool won)
        {
            int total = TotalRequired;
            float ratio = total > 0 ? TotalDelivered / (float)total : 0;
            if (won && TimeLeft >= roundSeconds * 0.3f) return 3;
            if (won) return 2;
            if (ratio >= 0.5f) return 1;
            return 0;
        }

        public override void ServerOnDeliver(BFPlayer player, ProductItem item, CheckoutZone zone)
        {
            if (state != RoundState.Playing || item == null) return;
            var def = item.Def;
            string itemName = def != null ? def.displayName : "Ürün";
            string who = player != null ? player.playerName : "Takım";
            int idx = -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i].productId == item.productId && !list[i].Done) { idx = i; break; }

            item.ServerMarkDelivered();
            if (player != null && player.heldItemId == item.netId) player.heldItemId = 0;
            NetworkServer.Destroy(item.gameObject);
            RpcZoneFlash(CheckoutZone.All.IndexOf(zone), idx >= 0);

            if (idx >= 0)
            {
                var e = list[idx];
                e.delivered++;
                list[idx] = e; // SyncList'e geri yaz
                int basePts = def != null ? def.points : 100;
                int mult = ComboNow;
                bool flash = FlashActive && flashProductId == item.productId;
                int pts = basePts * mult * (flash ? 2 : 1);
                score += pts;
                bonusPoints += pts - basePts;
                combo = Mathf.Min(maxCombo, mult + 1);
                comboEndTime = NetworkTime.time + comboWindow;
                if (flash) flashProductId = -1;
                if (player != null) player.deliveredCount++;
                string extra = (flash ? "  FLAŞ x2" : "") + (mult > 1 ? $"  KOMBO x{mult}" : "");
                ServerFeed($"{who}: {itemName} kasada! +{pts}{extra}", FeedKind.Good);
                if (ServerIsComplete())
                {
                    int bonus = Mathf.RoundToInt(TimeLeft) * timeBonusPerSecond;
                    timeBonus = bonus;
                    score += bonus;
                    ServerFeed($"LİSTE TAMAM! Süre bonusu +{bonus}", FeedKind.Good);
                    ServerEndRound(true);
                }
            }
            else
            {
                int pen = Mathf.Min(score, wrongItemPenalty);
                penaltyTotal += pen;
                combo = 1; comboEndTime = 0;
                score -= pen;
                ServerFeed($"{itemName} listede yok! -{wrongItemPenalty}", FeedKind.Bad);
            }
        }

        [ClientRpc]
        void RpcZoneFlash(int zoneIndex, bool good)
        {
            if (zoneIndex >= 0 && zoneIndex < CheckoutZone.All.Count) CheckoutZone.All[zoneIndex].Flash();
            Sfx.Play(good ? SfxId.Deliver : SfxId.Wrong, 0.5f);
        }

        void ServerBuildList()
        {
            list.Clear();
            var db = ProductDatabase.Instance;
            if (db == null || db.products.Count == 0) return;
            int players = Mathf.Max(1, BFPlayer.All.Count);
            int entries = Mathf.Min(db.products.Count, baseListEntries + extraEntriesPerPlayer * (players - 1));
            var pool = new List<ProductDef>(db.products);
            Shuffle(pool);
            for (int i = 0; i < entries; i++)
            {
                int maxCount = Mathf.Clamp(1 + players / 2, 1, maxPerEntry);
                list.Add(new ListEntry { productId = pool[i].id, required = Random.Range(1, maxCount + 1), delivered = 0 });
            }
        }

        void ServerSpawnProducts()
        {
            foreach (var go in spawnedItems) if (go != null) NetworkServer.Destroy(go);
            spawnedItems.Clear();
            foreach (var p in BFPlayer.All) p.heldItemId = 0;
            foreach (var n in NPCShopper.All) n.heldItemId = 0;

            var db = ProductDatabase.Instance;
            var points = new List<ShelfSpawnPoint>(ShelfSpawnPointCache.Points);
            if (db == null || points.Count == 0) { Debug.LogWarning("[BF] Ürün ya da spawn noktası yok"); return; }
            Shuffle(points);

            var toSpawn = new List<int>();
            foreach (var e in list)
            {
                int extra = Random.Range(1, 3);
                for (int i = 0; i < e.required + extra; i++) toSpawn.Add(e.productId);
            }
            for (int i = 0; i < decoyItems; i++) toSpawn.Add(db.products[Random.Range(0, db.products.Count)].id);

            // en pahalı listedeki ürün mega indirim masasına
            var hot = points.FindAll(p => p.hotDeal);
            int pi = 0;
            foreach (int id in toSpawn)
            {
                var def = db.Get(id);
                if (def == null || def.prefab == null) continue;
                ShelfSpawnPoint sp = null;
                if (hot.Count > 0 && Random.value < 0.25f)
                {
                    sp = hot[Random.Range(0, hot.Count)];
                    hot.Remove(sp); points.Remove(sp);
                }
                else
                {
                    for (int tries = 0; tries < points.Count && sp == null; tries++)
                    {
                        var cand = points[(pi + tries) % points.Count];
                        if (cand.Allows(id)) { sp = cand; points.Remove(cand); }
                    }
                }
                if (sp == null) break;
                pi++;
                var go = Instantiate(def.prefab, sp.transform.position + Vector3.up * 0.02f,
                    sp.transform.rotation * Quaternion.Euler(0, Random.Range(-15f, 15f), 0));
                NetworkServer.Spawn(go);
                spawnedItems.Add(go);
            }
        }

        void ServerSpawnNpcs()
        {
            if (npcsSpawned || npcPrefab == null) return;
            npcsSpawned = true;
            var lib = CharacterLibrary.Instance;
            int skins = lib != null ? Mathf.Max(1, lib.npcModels.Count) : 1;
            var points = ShelfSpawnPointCache.Points;
            for (int i = 0; i < npcCount; i++)
            {
                Vector3 pos = points.Count > 0 ? points[Random.Range(0, points.Count)].transform.position : Vector3.zero;
                pos.y = 0;
                var go = Instantiate(npcPrefab, pos + Random.insideUnitSphere * 1.5f, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                var npc = go.GetComponent<NPCShopper>();
                npc.skinIndex = Random.Range(0, skins);
                NetworkServer.Spawn(go);
            }
        }

        static void Shuffle<T>(List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); }
        }
    }
}
