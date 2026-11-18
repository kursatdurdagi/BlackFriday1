using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace BlackFriday
{
    /// <summary>
    /// Koli kulesi: itince, koşarak çarpınca ya da ürün fırlatınca devrilir. Devrilirken yakındaki müşterileri
    /// yere serer (kapıcıları durdurmanın eğlenceli yolu). Koliler her istemcide yerel fizikle saçılır
    /// (ağ yükü yok); kule bir süre sonra yeniden dizilir.
    /// </summary>
    public class BoxTower : NetworkBehaviour
    {
        public static readonly List<BoxTower> All = new List<BoxTower>();
        public float respawnSeconds = 28f;
        public float knockRadius = 2.6f;

        [SyncVar(hook = nameof(OnToppled))] bool toppled;
        [SyncVar] Vector3 toppleDir;

        Transform[] boxes; Vector3[] homePos; Quaternion[] homeRot;
        Collider solid; NavMeshObstacle obstacle;
        double resetAt;

        public bool Toppled => toppled;

        void Awake()
        {
            solid = GetComponent<Collider>();
            obstacle = GetComponent<NavMeshObstacle>();
            var list = new List<Transform>();
            foreach (Transform t in transform) if (t.GetComponent<MeshRenderer>() != null) list.Add(t);
            boxes = list.ToArray();
            homePos = new Vector3[boxes.Length]; homeRot = new Quaternion[boxes.Length];
            for (int i = 0; i < boxes.Length; i++) { homePos[i] = boxes[i].localPosition; homeRot[i] = boxes[i].localRotation; }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public override void OnStartClient() { if (toppled) Apply(true, false); }

        [Server]
        public void ServerTopple(Vector3 dir, BFPlayer by)
        {
            if (toppled) return;
            dir.y = 0;
            toppleDir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            toppled = true;
            resetAt = NetworkTime.time + respawnSeconds;
            int hit = 0;
            Vector3 c = transform.position + toppleDir * 0.8f;
            foreach (var npc in NPCShopper.All)
            {
                if (npc.IsStunned) continue;
                Vector3 d = npc.transform.position - c; d.y = 0;
                if (d.magnitude > knockRadius) continue;
                npc.ServerHitByItem(d.sqrMagnitude > 0.01f ? d.normalized : toppleDir);
                hit++;
            }
            foreach (var p in BFPlayer.All)
            {
                if (p == by) continue;
                Vector3 d = p.transform.position - c; d.y = 0;
                if (d.magnitude < knockRadius * 0.7f) p.ServerPush((d.sqrMagnitude > 0.01f ? d.normalized : toppleDir) * 6f);
            }
            var mode = GameModeBase.Current;
            if (mode != null && hit > 0)
                mode.ServerFeed($"{(by != null ? by.playerName : "Biri")} koli kulesini devirdi: {hit} müşteri yerde!", FeedKind.Info);
        }

        [ServerCallback]
        void Update()
        {
            if (toppled && NetworkTime.time >= resetAt) toppled = false;
        }

        [ServerCallback]
        void OnCollisionEnter(Collision c)
        {
            if (toppled || c.rigidbody == null || c.relativeVelocity.magnitude < 3f) return;
            var item = c.rigidbody.GetComponent<ProductItem>();
            var prop = c.rigidbody.GetComponent<PushableProp>();
            if (item == null && prop == null) return;
            ServerTopple(-c.relativeVelocity, null);
        }

        void OnToppled(bool _, bool now) { Apply(now, true); }

        void Apply(bool down, bool fx)
        {
            if (solid != null) solid.enabled = !down;
            if (obstacle != null) obstacle.enabled = !down;
            if (boxes == null) return;
            if (down)
            {
                var rng = new System.Random((int)netId * 31 + 7);
                for (int i = 0; i < boxes.Length; i++)
                {
                    var b = boxes[i];
                    var bc = b.GetComponent<BoxCollider>(); if (bc == null) bc = b.gameObject.AddComponent<BoxCollider>();
                    bc.enabled = true;
                    var rb = b.GetComponent<Rigidbody>(); if (rb == null) rb = b.gameObject.AddComponent<Rigidbody>();
                    rb.mass = 0.6f; rb.isKinematic = false;
                    b.gameObject.layer = 2; // Ignore Raycast: kamera / nişan / zemin ışınlarını etkilemesin
                    float h = Mathf.Clamp01(homePos[i].y / 2f);
                    Vector3 jitter = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() - 0.5f);
                    rb.linearVelocity = toppleDir * (2f + h * 4.5f) + jitter * 2f;
                    rb.angularVelocity = jitter * 9f;
                }
                if (fx)
                {
                    var l = BFPlayer.Local;
                    if (l == null || Vector3.Distance(l.transform.position, transform.position) < 25f) Sfx.Play(SfxId.Bump, 0.8f);
                }
            }
            else
            {
                for (int i = 0; i < boxes.Length; i++)
                {
                    var b = boxes[i];
                    var rb = b.GetComponent<Rigidbody>(); if (rb != null) Destroy(rb);
                    var bc = b.GetComponent<BoxCollider>(); if (bc != null) bc.enabled = false;
                    b.localPosition = homePos[i]; b.localRotation = homeRot[i];
                }
            }
        }
    }
}
