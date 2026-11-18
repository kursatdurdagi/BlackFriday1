using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>Ürün taşıyabilen her şey (oyuncu, NPC).</summary>
    public interface ICarrier
    {
        Transform CarryAnchor { get; }
    }

    /// <summary>
    /// Raflardan alınıp kasaya taşınan / fırlatılan ürün. Fizik sunucu yetkili.
    /// - Fırlatılan ürün kasa alanına düşerse teslim sayılır (fırlatana yazılır).
    /// - Fırlatılan ürün uçarken bir oyuncuya ya da NPC'ye isabet ederse onu sersemletir (düşürür).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ProductItem : NetworkBehaviour
    {
        public static readonly List<ProductItem> All = new List<ProductItem>();

        public int productId;
        public float hitSpeed = 4.5f;   // bu hızın üstünde isabet sersemletir

        [SyncVar(hook = nameof(OnCarrierChanged))] public uint carrierId;

        Rigidbody rb;
        Collider[] cols;
        Vector3 spawnPos;
        Quaternion spawnRot;
        uint lastHolderId;     // sadece sunucu
        uint thrownById;       // sadece sunucu: uçuş sırasında isabet sahibi
        double thrownAt;
        bool delivered;
        int defaultLayer;
        Vector3 centerLocal;   // tutulma noktası: ürünün merkezi (pivot tabanda)
        Vector3 localSize = new Vector3(0.3f, 0.3f, 0.3f);

        /// <summary>Ürünün (ölçeksiz) boyutu.</summary>
        public Vector3 LocalSize => localSize;

        public ProductDef Def => ProductDatabase.Instance != null ? ProductDatabase.Instance.Get(productId) : null;
        public string DisplayName => Def != null ? Def.displayName : name;
        public bool IsCarried => carrierId != 0;
        public bool IsInFlight => thrownById != 0;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            cols = GetComponentsInChildren<Collider>();
            defaultLayer = gameObject.layer;
            var bc = GetComponent<BoxCollider>();
            centerLocal = bc != null ? bc.center : Vector3.up * 0.15f;
            if (bc != null) localSize = bc.size;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public override void OnStartServer()
        {
            spawnPos = transform.position;
            spawnRot = transform.rotation;
            ApplyPhysicsState();
        }

        public override void OnStartClient() { ApplyPhysicsState(); }

        void OnCarrierChanged(uint _, uint __)
        {
            ApplyPhysicsState();
            if (carrierId == 0) SetLayer(defaultLayer);   // ölçek LateUpdate'te yumuşakça 1'e döner (FPS'ten fırlatılan ürün ekranı kaplamasın)
        }

        void ApplyPhysicsState()
        {
            bool carried = carrierId != 0;
            foreach (var c in cols) if (c) c.enabled = !carried;
            if (rb == null) return;
            if (isServer)
            {
                rb.isKinematic = carried;
                rb.interpolation = carried ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            }
            else
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }

        void SetLayer(int layer)
        {
            if (layer < 0 || gameObject.layer == layer) return;
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        ICarrier Carrier
        {
            get
            {
                var id = NetUtil.Find(carrierId);
                return id != null ? id.GetComponent<ICarrier>() : null;
            }
        }

        void LateUpdate()
        {
            if (carrierId == 0)
            {
                // FPS el modelindeki küçültülmüş ürün: uçarken gerçek boyuna büyür
                float sc = transform.localScale.x;
                if (sc != 1f) transform.localScale = Vector3.one * Mathf.MoveTowards(sc, 1f, Time.deltaTime * 4.5f);
                return;
            }
            var c = Carrier;
            if (c == null || c.CarryAnchor == null) return;

            // FPS'te kendi taşıdığımız ürün: el modelinin arasında, kol katmanında çizilir
            var local = BFPlayer.Local;
            var cam = PlayerCamera.Instance;
            var arms = FirstPersonArms.Instance;
            if (local != null && carrierId == local.netId && cam != null && cam.FirstPerson && arms != null)
            {
                SetLayer(FirstPersonArms.Layer);
                float s = arms.ItemScale;
                transform.localScale = Vector3.one * s;
                PlaceCentered(arms.ItemAnchor, s);
                return;
            }
            SetLayer(defaultLayer);
            if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
            PlaceCentered(c.CarryAnchor, 1f);
        }

        void PlaceCentered(Transform anchor, float scale)
        {
            Quaternion r = anchor.rotation;
            transform.SetPositionAndRotation(anchor.position - r * (centerLocal * scale), r);
        }

        [ServerCallback]
        void FixedUpdate()
        {
            if (carrierId != 0 || delivered) return;
            if (transform.position.y < -5f)
            {
                rb.linearVelocity = Vector3.zero;
                transform.SetPositionAndRotation(spawnPos, spawnRot);
                thrownById = 0;
                return;
            }

            if (thrownById != 0) ServerFlightHits();

            if (lastHolderId == 0) return;
            var mode = GameModeBase.Current;
            if (mode == null || mode.state != RoundState.Playing) return;
            foreach (var z in CheckoutZone.All)
            {
                if (!z.Contains(transform.position)) continue;
                var holder = NetUtil.Find(lastHolderId);
                var player = holder != null ? holder.GetComponent<BFPlayer>() : null;
                if (holder != null && player == null) continue; // NPC'nin düşürdüğü ürün teslim sayılmaz
                delivered = true;
                mode.ServerOnDeliver(player, this, z);
                return;
            }
        }

        /// <summary>Uçan ürün birine çarptı mı? (fizik olaylarına güvenmeden, kapsül yaklaşık testi)</summary>
        [Server]
        void ServerFlightHits()
        {
            if (NetworkTime.time - thrownAt > 2.5 || rb.linearVelocity.magnitude < hitSpeed)
            {
                if (NetworkTime.time - thrownAt > 0.15) thrownById = 0;
                return;
            }
            Vector3 p = transform.position;
            Vector3 v = rb.linearVelocity;
            foreach (var pl in BFPlayer.All)
            {
                if (pl.netId == thrownById || pl.IsStunned) continue;
                if (!HitsCapsule(pl.transform.position, p)) continue;
                pl.ServerKnock(new Vector3(v.x, 0, v.z).normalized, BFPlayer.HitStunTime, 3f);
                ServerOnHit(pl.playerName);
                return;
            }
            foreach (var npc in NPCShopper.All)
            {
                if (npc.IsStunned) continue;
                if (!HitsCapsule(npc.transform.position, p)) continue;
                npc.ServerHitByItem(new Vector3(v.x, 0, v.z).normalized);
                ServerOnHit(null);
                return;
            }
        }

        static bool HitsCapsule(Vector3 feet, Vector3 p)
        {
            if (p.y < feet.y - 0.1f || p.y > feet.y + 1.9f) return false;
            Vector2 d = new Vector2(p.x - feet.x, p.z - feet.z);
            return d.magnitude < 0.62f;
        }

        [Server]
        void ServerOnHit(string victim)
        {
            var thrower = NetUtil.Find(thrownById);
            var tp = thrower != null ? thrower.GetComponent<BFPlayer>() : null;
            var mode = GameModeBase.Current;
            if (mode != null && tp != null)
                mode.ServerFeed(victim != null ? $"{tp.playerName}, {victim} oyuncusunu {DisplayName} ile devirdi!" : $"{tp.playerName} bir müşteriyi {DisplayName} ile devirdi!", victim != null ? FeedKind.Bad : FeedKind.Info);
            thrownById = 0;
            rb.linearVelocity *= -0.25f; // sekme
            RpcHitFx(transform.position);
        }

        [ClientRpc]
        void RpcHitFx(Vector3 pos)
        {
            var l = BFPlayer.Local;
            if (l == null || Vector3.Distance(l.transform.position, pos) < 18f) Sfx.Play(SfxId.Bump, 0.6f);
        }

        // ------------------------------------------------------------ sunucu API
        [Server]
        public void ServerPickUp(NetworkBehaviour carrier)
        {
            carrierId = carrier.netId;
            lastHolderId = carrier.netId;
            thrownById = 0;
            ApplyPhysicsState();
        }

        [Server]
        public void ServerRelease(Vector3 velocity, Collider ignore, bool thrown)
        {
            uint holder = carrierId;
            carrierId = 0;
            ApplyPhysicsState();
            if (!thrown) transform.localScale = Vector3.one;
            rb.linearVelocity = velocity;
            // fırlatılan ürün uçuş yönünde takla atar
            Vector3 spinAxis = Vector3.Cross(Vector3.up, velocity);
            rb.angularVelocity = thrown && spinAxis.sqrMagnitude > 0.01f ? spinAxis.normalized * 8f + Random.insideUnitSphere * 2f
                                                                          : Random.insideUnitSphere * 3f;
            if (thrown) { thrownById = holder; thrownAt = NetworkTime.time; }
            if (ignore != null) StartCoroutine(IgnoreFor(ignore, 0.4f));
        }

        IEnumerator IgnoreFor(Collider other, float t)
        {
            foreach (var c in cols) if (c && other) Physics.IgnoreCollision(c, other, true);
            yield return new WaitForSeconds(t);
            foreach (var c in cols) if (c && other) Physics.IgnoreCollision(c, other, false);
        }

        [Server]
        public void ServerMarkDelivered() { delivered = true; }
    }

    public static class NetUtil
    {
        public static NetworkIdentity Find(uint id)
        {
            if (id == 0) return null;
            if (NetworkServer.active && NetworkServer.spawned.TryGetValue(id, out var a)) return a;
            if (NetworkClient.active && NetworkClient.spawned.TryGetValue(id, out var b)) return b;
            return null;
        }
    }
}
