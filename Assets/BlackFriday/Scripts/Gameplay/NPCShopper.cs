using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace BlackFriday
{
    /// <summary>
    /// AVM'de koşuşturan NPC müşteri (sunucuda NavMeshAgent).
    /// Tur sırasında bazıları "kapıcı" olur: listedeki ürünlerden birini kapıp kaçar.
    /// Fırlatılan bir ürün isabet ederse düşer ve elindekini bırakır. İtilirse sendeler.
    /// </summary>
    public class NPCShopper : NetworkBehaviour, ICarrier
    {
        public static readonly List<NPCShopper> All = new List<NPCShopper>();
        public const float HitStunTime = 3.0f;
        public static int MaxGrabbers = 3;   // tur ilerledikçe mod artırır (kalabalık dalgası)

        public CharacterVisual visual;
        public Transform carryAnchor;
        public float walkSpeed = 1.6f;
        public float rushSpeed = 4.6f;
        public float fleeSpeed = 3.6f;

        [SyncVar(hook = nameof(OnSkin))] public int skinIndex = -1;
        [SyncVar] public double stunnedUntil;
        [SyncVar] public double pushedUntil;
        [SyncVar] public uint heldItemId;

        enum Mode { Wander, GoGrab, Flee }
        Mode mode = Mode.Wander;
        ProductItem grabTarget;
        float fleeTimer;

        NavMeshAgent agent;
        float waitTimer;
        float rushTimer;
        float stuckTimer;
        Vector3 lastPos;

        public bool IsStunned => NetworkTime.time < stunnedUntil;
        public bool IsPushed => NetworkTime.time < pushedUntil;
        public bool IsCarrying => heldItemId != 0;
        public Transform CarryAnchor => visual != null ? visual.HoldPoint : carryAnchor;

        void Awake() { agent = GetComponent<NavMeshAgent>(); }
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public override void OnStartServer()
        {
            if (agent == null) return;
            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 4f, NavMesh.AllAreas)) agent.Warp(hit.position);
            agent.speed = walkSpeed * Random.Range(0.85f, 1.25f);
            waitTimer = Random.Range(0f, 2f);
        }

        public override void OnStartClient()
        {
            if (!isServer && agent != null) agent.enabled = false;
            if (skinIndex >= 0) visual.SetModel(skinIndex);
        }

        void OnSkin(int _, int n) { visual.SetModel(n); }

        void Update()
        {
            if (visual != null)
            {
                visual.stunned = IsStunned;
                visual.pushed = IsPushed;
                var hid = NetUtil.Find(heldItemId);
                visual.SetCarried(hid != null ? hid.GetComponent<ProductItem>() : null);
            }
            if (isServer) ServerTick();
        }

        // ------------------------------------------------------------ sunucu: darbeler
        /// <summary>Fırlatılan ürün isabet etti: düşer, elindekini bırakır.</summary>
        [Server]
        public void ServerHitByItem(Vector3 dir)
        {
            stunnedUntil = NetworkTime.time + HitStunTime;
            if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(-dir, Vector3.up); // isabet yönüne dönük, geriye düşer
            ServerDropHeld(dir * 1.5f + Vector3.up * 3f);
            mode = Mode.Wander;
            grabTarget = null;
        }

        [Server]
        public void ServerPush(Vector3 dir)
        {
            if (IsStunned) return;
            pushedUntil = NetworkTime.time + 0.5f;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.velocity = dir * 4f;
                agent.Move(dir * 0.4f);
            }
        }

        [Server]
        void ServerDropHeld(Vector3 vel)
        {
            var id = NetUtil.Find(heldItemId);
            heldItemId = 0;
            var item = id != null ? id.GetComponent<ProductItem>() : null;
            if (item != null) item.ServerRelease(vel, GetComponent<Collider>(), false);
        }

        // ------------------------------------------------------------ sunucu: yapay zeka
        [Server]
        void ServerTick()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            float dt = Time.deltaTime;

            if (IsStunned || IsPushed) { agent.isStopped = true; return; }
            agent.isStopped = false;

            var gm = GameModeBase.Current;
            bool playing = gm != null && gm.state == RoundState.Playing;

            if (rushTimer > 0)
            {
                rushTimer -= dt;
                if (rushTimer <= 0 && mode == Mode.Wander) agent.speed = walkSpeed * Random.Range(0.85f, 1.25f);
            }

            switch (mode)
            {
                case Mode.GoGrab: TickGrab(); break;
                case Mode.Flee: TickFlee(dt, playing); break;
                default:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                    {
                        waitTimer -= dt;
                        if (waitTimer <= 0) PickDestination(playing);
                    }
                    break;
            }

            // sıkışma kontrolü
            if ((transform.position - lastPos).sqrMagnitude < 0.0004f && agent.hasPath) stuckTimer += dt; else stuckTimer = 0;
            lastPos = transform.position;
            if (stuckTimer > 3f) { stuckTimer = 0; mode = IsCarrying ? Mode.Flee : Mode.Wander; PickDestination(playing); }
        }

        void TickGrab()
        {
            if (grabTarget == null || grabTarget.IsCarried || grabTarget.IsInFlight) { mode = Mode.Wander; grabTarget = null; return; }
            Vector3 d = grabTarget.transform.position - transform.position; d.y = 0;
            if (d.magnitude < 1.3f)
            {
                grabTarget.ServerPickUp(this);
                heldItemId = grabTarget.netId;
                RpcGrab();
                var gm = GameModeBase.Current;
                if (gm != null && IsOnList(grabTarget.productId)) gm.ServerFeed($"Bir müşteri {grabTarget.DisplayName} kaptı! Bir şey fırlatıp düşür!", FeedKind.Bad);
                grabTarget = null;
                mode = Mode.Flee;
                fleeTimer = Random.Range(14f, 22f);
                agent.speed = fleeSpeed;
                PickFleeDestination();
                return;
            }
            if (!agent.pathPending && (agent.destination - grabTarget.transform.position).sqrMagnitude > 1f) SetDest(grabTarget.transform.position);
        }

        void TickFlee(float dt, bool playing)
        {
            fleeTimer -= dt;
            if (!IsCarrying) { mode = Mode.Wander; return; }
            if (fleeTimer <= 0 || !playing)
            {
                // sıkıldı: ürünü bulunduğu yere bırakır
                ServerDropHeld(transform.forward * 1.2f + Vector3.up * 1.5f);
                mode = Mode.Wander;
                agent.speed = walkSpeed;
                return;
            }
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f) PickFleeDestination();
        }

        [ClientRpc] void RpcGrab() { if (visual != null) visual.PlayAction("Grab"); }

        static bool IsOnList(int productId)
        {
            var slm = GameModeBase.Current as ShoppingListMode;
            if (slm == null) return false;
            foreach (var e in slm.list) if (e.productId == productId && !e.Done) return true;
            return false;
        }

        static int GrabberCount()
        {
            int n = 0;
            foreach (var npc in All) if (npc.mode != Mode.Wander || npc.IsCarrying) n++;
            return n;
        }

        [Server]
        void PickDestination(bool playing)
        {
            waitTimer = Random.Range(0.5f, 3f);

            // kapıcı ol: yakındaki (tercihen listedeki) bir ürüne yönel
            if (playing && Random.value < 0.22f && GrabberCount() < MaxGrabbers)
            {
                ProductItem best = null; float bestScore = float.MaxValue;
                foreach (var it in ProductItem.All)
                {
                    if (it == null || it.IsCarried || it.IsInFlight) continue;
                    float dist = Vector3.Distance(it.transform.position, transform.position);
                    if (dist > 16f) continue;
                    float score = dist - (IsOnList(it.productId) ? 10f : 0f);
                    if (score < bestScore) { bestScore = score; best = it; }
                }
                if (best != null)
                {
                    grabTarget = best;
                    mode = Mode.GoGrab;
                    agent.speed = rushSpeed * Random.Range(0.9f, 1.1f);
                    SetDest(best.transform.position);
                    return;
                }
            }

            var points = ShelfSpawnPointCache.Points;
            Vector3 target;
            if (points.Count > 0)
            {
                var sp = points[Random.Range(0, points.Count)];
                if (playing && Random.value < 0.2f)
                {
                    var hot = ShelfSpawnPointCache.HotPoints;
                    if (hot.Count > 0) sp = hot[Random.Range(0, hot.Count)];
                    agent.speed = rushSpeed * Random.Range(0.9f, 1.1f);
                    rushTimer = 5f;
                    waitTimer = Random.Range(0.2f, 1f);
                }
                target = sp.transform.position + Random.insideUnitSphere * 1.5f;
            }
            else target = transform.position + Random.insideUnitSphere * 8f;
            SetDest(target);
        }

        void PickFleeDestination()
        {
            // oyunculardan uzak bir nokta seç
            var points = ShelfSpawnPointCache.Points;
            Vector3 best = transform.position; float bestScore = float.MinValue;
            for (int i = 0; i < 8 && points.Count > 0; i++)
            {
                Vector3 c = points[Random.Range(0, points.Count)].transform.position;
                float minToPlayer = 999f;
                foreach (var p in BFPlayer.All) minToPlayer = Mathf.Min(minToPlayer, Vector3.Distance(p.transform.position, c));
                float score = minToPlayer + Random.Range(0f, 4f);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            SetDest(best + Random.insideUnitSphere * 1.5f);
        }

        void SetDest(Vector3 target)
        {
            if (NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        public override void OnStopServer()
        {
            if (IsCarrying) ServerDropHeld(Vector3.up);
        }
    }

    public static class ShelfSpawnPointCache
    {
        static List<ShelfSpawnPoint> points = new List<ShelfSpawnPoint>();
        static List<ShelfSpawnPoint> hot = new List<ShelfSpawnPoint>();
        static UnityEngine.SceneManagement.Scene scene;
        static bool built;

        static void Refresh()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (built && scene == active && points.Count > 0 && points[0] != null) return;
            scene = active; built = true;
            points = new List<ShelfSpawnPoint>(Object.FindObjectsByType<ShelfSpawnPoint>(FindObjectsSortMode.None));
            hot = points.FindAll(p => p.hotDeal);
        }

        public static List<ShelfSpawnPoint> Points { get { Refresh(); return points; } }
        public static List<ShelfSpawnPoint> HotPoints { get { Refresh(); return hot; } }
    }
}
