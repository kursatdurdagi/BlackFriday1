using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>
    /// Ağ oyuncusu (TPS/FPS).
    /// TPS normal: yönlü hareket (karakter gittiği yöne döner).
    /// TPS + SAĞ TIK veya FPS: strafe (karakter kameraya bakar), nişan + fırlatma yayı.
    /// Hareket istemci yetkili (NetworkTransform ClientToServer); al/bırak/fırlat/it/teslim sunucu yetkili.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class BFPlayer : NetworkBehaviour, ICarrier
    {
        public const float HitStunTime = 2.1f;   // fırlatılan ürün isabeti (düşme + kalkma animasyonu)
        public const float PushTime = 0.45f;     // itilince sendeleme
        public static readonly List<BFPlayer> All = new List<BFPlayer>();
        public static BFPlayer Local;

        public static readonly Color[] PlayerColors =
        {
            new Color(1f, 0.83f, 0.1f), new Color(0.2f, 0.85f, 1f), new Color(1f, 0.35f, 0.75f), new Color(0.45f, 1f, 0.35f)
        };

        [Header("Refs")]
        public Transform carryAnchor;
        public Renderer ring;
        public CharacterVisual visual;
        public Material arcMaterial;

        [Header("Hareket")]
        public float runSpeed = 4.4f;
        public float sprintSpeed = 6.5f;          // = sprint klibinin gerçek hızı (kayma yok)
        public float aimSpeed = 2.3f;             // = koşu-strafe klibinin gerçek hızı
        public float strafeSideMul = 0.85f, strafeBackMul = 0.7f;
        public float accel = 30f;
        public float turnSpeed = 720f;
        public float gravity = -25f;
        public float staminaMax = 100f, staminaDrain = 30f, staminaRegen = 22f;
        public float jumpHeight = 1.1f;
        public float airControl = 0.35f;
        public float coyoteTime = 0.12f;

        [Header("Etkileşim")]
        public float pickupRange = 2.0f;
        public float shoveRange = 1.8f;
        public float shoveCooldown = 0.9f;
        public float throwSpeed = 11f;
        public float throwLift = 2.2f;
        public float throwWindup = 0.24f;

        [SyncVar] public string playerName = "Oyuncu";
        [SyncVar(hook = nameof(OnSkinChanged))] public int skinIndex = -1;
        [SyncVar(hook = nameof(OnIndexChanged))] public int playerIndex;
        [SyncVar] public uint heldItemId;
        [SyncVar] public double stunnedUntil;
        [SyncVar] public double pushedUntil;
        [SyncVar] public int deliveredCount;

        CharacterController cc;
        Vector3 velocity;
        Vector3 knockback;
        float verticalVel;
        double lastActionTime, lastBumpTime;
        LineRenderer arc;
        readonly Vector3[] arcPts = new Vector3[48];

        public float Stamina { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsStunned => NetworkTime.time < stunnedUntil;
        public bool IsPushed => NetworkTime.time < pushedUntil;
        public Transform CarryAnchor => visual != null ? visual.HoldPoint : carryAnchor;
        public bool ThrowPending => throwPending;
        bool throwPending;
        float throwFaceUntil;
        float lastGroundedTime, jumpBufferTime = -10f;
        public bool IsCarrying => heldItemId != 0;
        public Color PlayerColor => PlayerColors[Mathf.Clamp(playerIndex, 0, PlayerColors.Length - 1)];

        public ProductItem HeldItem
        {
            get { var id = NetUtil.Find(heldItemId); return id != null ? id.GetComponent<ProductItem>() : null; }
        }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Stamina = staminaMax;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); if (Local == this) Local = null; }

        public override void OnStartServer()
        {
            for (int i = 0; i < 8; i++)
            {
                bool used = false;
                foreach (var p in All) if (p != this && p.playerIndex == i) used = true;
                if (!used) { playerIndex = i; break; }
            }
        }

        public override void OnStartClient()
        {
            if (skinIndex >= 0) visual.SetModel(skinIndex);
            ApplyRingColor();
        }

        public override void OnStartLocalPlayer()
        {
            Local = this;
            CmdSetProfile(GameSettings.PlayerName, GameSettings.SkinIndex);
            var cam = PlayerCamera.Instance != null ? PlayerCamera.Instance : FindFirstObjectByType<PlayerCamera>();
            if (cam != null) cam.SetTarget(transform);
            if (ring != null) ring.enabled = false; // kendi halkamız TPS'te dikkat dağıtmasın

            var go = new GameObject("ThrowArc");
            go.transform.SetParent(transform, false);
            arc = go.AddComponent<LineRenderer>();
            arc.material = arcMaterial;
            arc.widthMultiplier = 0.05f;
            arc.numCapVertices = 2;
            arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arc.receiveShadows = false;
            arc.useWorldSpace = true;
            arc.enabled = false;
        }

        void OnSkinChanged(int _, int n)
        {
            visual.SetModel(n);
            if (isLocalPlayer && PlayerCamera.Instance != null) visual.SetFirstPersonHidden(PlayerCamera.Instance.FirstPerson);
        }

        void OnIndexChanged(int _, int __) { ApplyRingColor(); }

        void ApplyRingColor()
        {
            if (ring == null) return;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", PlayerColor);
            ring.SetPropertyBlock(mpb);
        }

        [Command]
        void CmdSetProfile(string n, int skin)
        {
            if (string.IsNullOrWhiteSpace(n)) n = "Oyuncu";
            playerName = n.Length > 16 ? n.Substring(0, 16) : n;
            var lib = CharacterLibrary.Instance;
            int count = lib != null ? Mathf.Max(1, lib.playerModels.Count) : 1;
            skinIndex = Mathf.Abs(skin) % count;
        }

        // ================================================================ update
        void Update()
        {
            if (visual != null)
            {
                visual.stunned = IsStunned;
                visual.pushed = IsPushed;
                visual.SetCarried(HeldItem);
                visual.aiming = isLocalPlayer && IsAiming;
                visual.windup = Mathf.MoveTowards(visual.windup, throwPending ? 1f : 0f, Time.deltaTime * (throwPending ? 1f / throwWindup : 9f));
                var mode = GameModeBase.Current;
                visual.celebrate = mode != null && mode.state == RoundState.Ended && mode.success;
            }

            if (isServer) ServerTick();
            if (!isLocalPlayer) return;

            LocalMove();
            LocalActions();
            UpdateArc();
        }

        void LocalMove()
        {
            float dt = Time.deltaTime;
            var mode = GameModeBase.Current;
            var cam = PlayerCamera.Instance;
            bool canMove = !IsStunned && !IsPushed && (mode == null || mode.PlayersCanMove);
            bool fps = cam != null && cam.FirstPerson;

            IsAiming = canMove && BFInput.Aim();
            if (cam != null) cam.Aiming = IsAiming;
            // fırlatırken (hazırlık + bırakış) karakter nişan yönüne döner: kol ileri, ürün de ileri gider
            bool strafe = fps || IsAiming || throwPending || Time.time < throwFaceUntil;

            Vector2 input = canMove ? BFInput.Move() : Vector2.zero;
            float camYaw = cam != null ? cam.Yaw : transform.eulerAngles.y;
            Quaternion yawRot = Quaternion.Euler(0, camYaw, 0);
            Vector3 dir = yawRot * new Vector3(input.x, 0, input.y);

            bool sprinting = !IsAiming && BFInput.Sprint() && dir.sqrMagnitude > 0.01f && Stamina > 1f && input.y > -0.2f;
            Stamina = sprinting ? Mathf.Max(0, Stamina - staminaDrain * dt) : Mathf.Min(staminaMax, Stamina + staminaRegen * dt);

            float speed = IsAiming ? aimSpeed : (sprinting ? sprintSpeed : runSpeed);
            var held = HeldItem;
            if (held != null && held.Def != null) speed *= held.Def.carrySpeed;
            // strafe modunda yana / geriye daha yavaş (doğal his + animasyonla uyum)
            if (strafe && !IsAiming && input.sqrMagnitude > 0.01f)
            {
                Vector2 n = input.normalized;
                float mul = n.y >= 0 ? Mathf.Lerp(strafeSideMul, 1f, n.y) : Mathf.Lerp(strafeSideMul, strafeBackMul, -n.y);
                speed *= mul;
            }

            bool groundedNow = cc.isGrounded;
            if (groundedNow) lastGroundedTime = Time.time;
            bool canJumpGround = Time.time - lastGroundedTime <= coyoteTime;

            // kaygan zemin: ivme düşer (kayarsın), havadayken daha az kontrol
            bool slipping = groundedNow && SlipZone.All.Count > 0 && SlipZone.Contains(transform.position);
            if (slipping && !wasSlipping && velocity.magnitude > 3f) Sfx.Play(SfxId.Wrong, 0.25f);
            wasSlipping = slipping;
            velocity = Vector3.MoveTowards(velocity, dir * speed, accel * (groundedNow ? (slipping ? 0.07f : 1f) : airControl) * dt);
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 18f * dt);

            if (groundedNow && verticalVel < 0) verticalVel = -2f;
            if (BFInput.JumpPressed()) jumpBufferTime = Time.time;
            if (canMove && canJumpGround && Time.time - jumpBufferTime < 0.15f && verticalVel <= 0f)
            {
                float h = jumpHeight * (held != null && held.Def != null && held.Def.carrySpeed < 0.9f ? 0.7f : 1f);
                verticalVel = Mathf.Sqrt(2f * -gravity * h);
                jumpBufferTime = -10f;
                lastGroundedTime = -10f;
            }
            verticalVel += gravity * dt;

            Vector3 belt = groundedNow && ConveyorZone.All.Count > 0 ? ConveyorZone.VelocityAt(transform.position) : Vector3.zero;
            belt.y = 0;
            Vector3 move = (velocity + knockback + belt) * dt;
            move.y = verticalVel * dt;
            cc.Move(move);

            // dönüş: strafe modunda kameraya bak, normalde gidilen yöne
            if (canMove && strafe)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, yawRot, turnSpeed * 2f * dt);
            else if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), turnSpeed * dt);

            if (transform.position.y < -10f) TeleportTo(new Vector3(0, 0.2f, -11f));
        }

        bool wasSlipping;
        float lastPropPush;

        /// <summary>Yürürken / koşarken çevreye çarpma: arabaları it, koli kulelerini devir.</summary>
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!isLocalPlayer || hit.rigidbody == null && hit.collider.GetComponent<BoxTower>() == null) return;
            if (hit.moveDirection.y < -0.4f || Time.time - lastPropPush < 0.1f) return;
            float spd = new Vector3(velocity.x, 0, velocity.z).magnitude;
            var prop = hit.collider.GetComponentInParent<PushableProp>();
            if (prop != null && spd > 0.6f)
            {
                lastPropPush = Time.time;
                CmdPushProp(prop.netIdentity, hit.moveDirection, Mathf.Clamp(spd / runSpeed, 0.3f, 1.6f));
                return;
            }
            var tower = hit.collider.GetComponentInParent<BoxTower>();
            if (tower != null && !tower.Toppled && spd > runSpeed * 1.15f)
            {
                lastPropPush = Time.time;
                CmdTopple(tower.netIdentity, hit.moveDirection);
            }
        }

        [Command]
        void CmdPushProp(NetworkIdentity id, Vector3 dir, float strength)
        {
            if (id == null || IsStunned) return;
            var prop = id.GetComponent<PushableProp>();
            if (prop == null || (prop.transform.position - transform.position).sqrMagnitude > 9f) return;
            prop.ServerPush(dir, Mathf.Clamp(strength, 0f, 1.6f));
        }

        [Command]
        void CmdTopple(NetworkIdentity id, Vector3 dir)
        {
            if (id == null || IsStunned) return;
            var tower = id.GetComponent<BoxTower>();
            if (tower == null || (tower.transform.position - transform.position).sqrMagnitude > 16f) return;
            tower.ServerTopple(dir, this);
        }

        void LocalActions()
        {
            if (IsStunned || IsPushed || throwPending) return;
            var mode = GameModeBase.Current;
            bool cooldownOk = NetworkTime.time - lastActionTime > 0.3f;

            if (BFInput.Primary() && cooldownOk)
            {
                lastActionTime = NetworkTime.time;
                if (IsCarrying) StartCoroutine(ThrowRoutine());
                else DoShove();
            }
            if (BFInput.Shove() && cooldownOk && !IsCarrying)
            {
                lastActionTime = NetworkTime.time;
                DoShove();
            }
            if (BFInput.Interact() && cooldownOk)
            {
                lastActionTime = NetworkTime.time;
                if (IsCarrying)
                {
                    if (IsAiming) StartCoroutine(ThrowRoutine());
                    else CmdDrop();
                }
                else if (mode == null || mode.CanPickUp)
                {
                    var target = FindTargetItem();
                    if (target != null)
                    {
                        PlayLocalAction("Grab");
                        CmdPickUp(target.netId);
                    }
                }
            }
        }

        void DoShove()
        {
            PlayLocalAction("Shove");
            CmdShove();
        }

        /// <summary>Fırlatma: önce kollar geri (hazırlık), sonra bırak.</summary>
        System.Collections.IEnumerator ThrowRoutine()
        {
            // hazırlık IK ile (ürün omuz üstüne kalkar); kol savurma klibi bırakma anında başlar
            throwPending = true;
            throwFaceUntil = Time.time + throwWindup + 0.4f;
            CmdThrowWindup();
            yield return new WaitForSeconds(throwWindup);
            throwPending = false;
            if (IsCarrying && !IsStunned)
            {
                PlayLocalAction("Throw");
                CmdThrow(ComputeThrowDir());
            }
        }

        // diğer oyuncular da hazırlığı görsün (ürün omuza kalkar)
        [Command] void CmdThrowWindup() { RpcThrowWindup(); }
        [ClientRpc] void RpcThrowWindup() { if (!isLocalPlayer) StartCoroutine(RemoteWindup()); }
        System.Collections.IEnumerator RemoteWindup()
        {
            throwPending = true;
            yield return new WaitForSeconds(throwWindup + 0.05f);
            throwPending = false;
        }

        /// <summary>Test/otomasyon: fırlatmayı başlat.</summary>
        public void DebugThrow() { if (IsCarrying && !throwPending) StartCoroutine(ThrowRoutine()); }

        void PlayLocalAction(string trigger)
        {
            if (visual != null) visual.PlayAction(trigger);
            var cam = PlayerCamera.Instance;
            if (cam != null && cam.FirstPerson && FirstPersonArms.Instance != null) FirstPersonArms.Instance.Play(trigger);
        }

        Vector3 ThrowOrigin()
        {
            var held = HeldItem;
            if (held != null) return held.transform.position;
            return carryAnchor != null ? carryAnchor.position : transform.position + Vector3.up;
        }

        Vector3 ComputeThrowDir()
        {
            var cam = PlayerCamera.Instance;
            if (cam == null) return (transform.forward + Vector3.up * 0.25f).normalized;
            Vector3 aimPoint = cam.AimPoint();
            Vector3 d = (aimPoint - ThrowOrigin());
            if (d.sqrMagnitude < 0.25f) d = cam.transform.forward;
            return d.normalized;
        }

        Vector3 ThrowVelocity(Vector3 dir) => dir.normalized * throwSpeed + Vector3.up * throwLift;

        void UpdateArc()
        {
            if (arc == null) return;
            bool show = IsAiming && IsCarrying && !IsStunned;
            arc.enabled = show;
            if (!show) return;
            Vector3 p = ThrowOrigin();
            Vector3 v = ThrowVelocity(ComputeThrowDir());
            const float step = 0.035f;
            int count = 0;
            arcPts[count++] = p;
            for (int i = 1; i < arcPts.Length; i++)
            {
                Vector3 next = p + v * step;
                v += Physics.gravity * step;
                if (Physics.Linecast(p, next, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<BFPlayer>() == null && hit.collider.GetComponentInParent<ProductItem>() == null)
                {
                    arcPts[count++] = hit.point;
                    break;
                }
                arcPts[count++] = next;
                p = next;
            }
            arc.positionCount = count;
            arc.SetPositions(arcPts);
        }

        void TeleportTo(Vector3 p)
        {
            cc.enabled = false;
            transform.position = p;
            cc.enabled = true;
            velocity = Vector3.zero;
        }

        // ================================================================ hedef seçimi
        /// <summary>Alınabilecek ürün: önce kameranın baktığı, yoksa en yakın.</summary>
        public ProductItem FindTargetItem()
        {
            var cam = PlayerCamera.Instance;
            ProductItem best = null;
            float bestScore = float.MaxValue;
            Vector3 pos = transform.position;
            foreach (var it in ProductItem.All)
            {
                if (it == null || it.IsCarried) continue;
                Vector3 d = it.transform.position - pos;
                if (d.y < -0.6f || d.y > 2.4f) continue;
                Vector3 flat = d; flat.y = 0;
                float dist = flat.magnitude;
                if (dist > pickupRange) continue;
                float score;
                if (cam != null && isLocalPlayer)
                {
                    Vector3 toItem = (it.transform.position + Vector3.up * 0.15f) - cam.transform.position;
                    float angle = Vector3.Angle(cam.transform.forward, toItem);
                    if (angle > 50f) continue;
                    score = angle * 0.05f + dist * 0.5f;
                }
                else
                {
                    float facing = dist > 0.01f ? Vector3.Dot(transform.forward, flat / dist) : 1f;
                    score = dist - facing * 0.6f;
                }
                if (score < bestScore) { bestScore = score; best = it; }
            }
            return best;
        }

        // ================================================================ komutlar (sunucu)
        [Command]
        void CmdPickUp(uint itemId)
        {
            if (IsStunned || IsCarrying) return;
            var mode = GameModeBase.Current;
            if (mode != null && !mode.CanPickUp) return;
            var ni = NetUtil.Find(itemId);
            var item = ni != null ? ni.GetComponent<ProductItem>() : null;
            if (item == null || item.IsCarried) return;
            Vector3 d = item.transform.position - transform.position; d.y = 0;
            if (d.magnitude > pickupRange + 0.8f) return;
            item.ServerPickUp(this);
            heldItemId = item.netId;
            RpcAction(ActGrab);
            RpcSfx(SfxId.Pickup);
        }

        [Command]
        void CmdDrop()
        {
            if (!IsCarrying) return;
            ServerDropHeld(transform.forward * 1.6f + Vector3.up * 1.2f);
        }

        [Command]
        void CmdThrow(Vector3 dir)
        {
            if (!IsCarrying || IsStunned) return;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            var held = HeldItem;
            heldItemId = 0;
            if (held == null) return;
            held.ServerRelease(ThrowVelocity(dir), cc, true);
            RpcAction(ActThrow);
            RpcSfx(SfxId.Drop);
        }

        /// <summary>Test/otomasyon: ürünü doğrudan ele ver.</summary>
        [Server]
        public void ServerDebugGive(ProductItem item)
        {
            if (item == null || item.IsCarried || IsCarrying) return;
            item.ServerPickUp(this);
            heldItemId = item.netId;
        }

        [Server]
        public void ServerDropHeld(Vector3 vel)
        {
            var held = HeldItem;
            heldItemId = 0;
            if (held == null) return;
            held.ServerRelease(vel, cc, false);
            RpcSfx(SfxId.Drop);
        }

        [Command]
        void CmdShove()
        {
            if (IsStunned) return;
            RpcAction(ActShove);
            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;
            foreach (var other in All)
            {
                if (other == this) continue;
                Vector3 d = other.transform.position - pos; d.y = 0;
                if (d.magnitude > shoveRange || Vector3.Dot(fwd, d.normalized) < 0.35f) continue;
                other.ServerPush(d.normalized * 7f);
            }
            foreach (var npc in NPCShopper.All)
            {
                Vector3 d = npc.transform.position - pos; d.y = 0;
                if (d.magnitude > shoveRange || Vector3.Dot(fwd, d.normalized) < 0.35f) continue;
                npc.ServerPush(d.normalized);
            }
            // çevre: arabayı ittir, koli kulesini devir
            foreach (var col in Physics.OverlapSphere(pos + Vector3.up * 0.8f + fwd * 1.0f, 1.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                var prop = col.GetComponentInParent<PushableProp>();
                if (prop != null) { prop.ServerPush(fwd, 2.2f); continue; }
                var tower = col.GetComponentInParent<BoxTower>();
                if (tower != null) tower.ServerTopple(fwd, this);
            }
        }

        /// <summary>Fırlatılan ürün isabeti: oyuncu düşer (sersemler), elindekini düşürür.</summary>
        [Server]
        public void ServerKnock(Vector3 dir, float duration, float force)
        {
            if (IsStunned || NetworkTime.time - lastBumpTime < 0.5f) return;
            lastBumpTime = NetworkTime.time;
            stunnedUntil = NetworkTime.time + duration;
            if (IsCarrying) ServerDropHeld(dir * 2.5f + Vector3.up * 3f);
            TargetKnockback(dir * force);
            RpcSfx(SfxId.Bump);
        }

        /// <summary>İtme: sadece geri savrulur ve kısa sendeler (sersemlemez, ürün düşmez).</summary>
        [Server]
        public void ServerPush(Vector3 impulse)
        {
            if (IsStunned || IsPushed) return;
            pushedUntil = NetworkTime.time + PushTime;
            TargetKnockback(impulse);
            RpcSfx(SfxId.Bump);
        }

        const byte ActThrow = 1, ActGrab = 2, ActShove = 3;

        [TargetRpc]
        void TargetKnockback(Vector3 v)
        {
            knockback = v;
            velocity = Vector3.zero;
        }

        [ClientRpc]
        void RpcSfx(SfxId id)
        {
            if (isLocalPlayer || Local == null || Vector3.Distance(Local.transform.position, transform.position) < 12f)
                Sfx.Play(id, isLocalPlayer ? 0.6f : 0.3f);
        }

        [ClientRpc]
        void RpcAction(byte act)
        {
            if (isLocalPlayer) return; // yerelde girişte zaten oynatıldı
            string t = act == ActThrow ? "Throw" : act == ActGrab ? "Grab" : "Shove";
            if (visual != null) visual.PlayAction(t);
        }

        [Server]
        void ServerTick()
        {
            if (!IsCarrying) return;
            var held = HeldItem;
            if (held == null) { heldItemId = 0; return; }
            var mode = GameModeBase.Current;
            if (mode == null || mode.state != RoundState.Playing) return;
            foreach (var z in CheckoutZone.All)
            {
                if (z.Contains(transform.position))
                {
                    mode.ServerOnDeliver(this, held, z);
                    break;
                }
            }
        }

        public override void OnStopServer()
        {
            if (IsCarrying) ServerDropHeld(Vector3.up);
        }
    }
}
