using UnityEngine;
using UnityEngine.Rendering;

namespace BlackFriday
{
    /// <summary>
    /// ToonPeople modelini oluşturur ve animatörü sürer (ağdaki herkes için yerel olarak çalışır).
    /// - Hız (MoveX/MoveZ) ve dikey hız transform hareketinden hesaplanır → ek ağ senkronu gerekmez.
    /// - Ayak kayması: blend parametreleri o yöndeki klibin gerçek hızına kırpılır, fazlası oynatma hızıyla karşılanır.
    /// - Taşıma: ürün boyutuna göre göğüs önünde bir tutma noktası hesaplanır, eller IK ile ürünün yanlarını kavrar.
    /// - Zıplama: zemin ışını + dikey hızla Jump/InAir/Land durumları.
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        // Synty kliplerinin ToonPeople üzerindeki ölçülmüş gerçek hızları (m/s)
        public const float WalkClipSpeed = 1.30f;
        public const float RunClipSpeed = 2.31f;
        public const float SprintClipSpeed = 6.47f;

        public Transform modelRoot;
        public bool isNpc;

        [HideInInspector] public bool stunned;   // fırlatılan ürün isabeti: düşer
        [HideInInspector] public bool pushed;    // itildi: sendeler
        [HideInInspector] public bool carrying;
        [HideInInspector] public bool celebrate;
        [HideInInspector] public float windup;   // 0..1 fırlatma hazırlığı (ürün baş üstüne kalkar, gövde geriye yaylanır)
        [HideInInspector] public bool aiming;    // nişan: ürün göğüs hizasına kalkar

        Animator anim;
        HandIK ik;
        GameObject current;
        int currentIndex = -1;
        Vector3 lastPos;
        Vector3 smoothLocalVel;
        float smoothVy;
        float carryWeight;
        bool wasStunned, wasPushed;
        bool fpHidden;
        bool grounded = true, jumpFired;
        float prevWindup;
        bool wasCarrying;
        float groundedTimer, airTimer;

        Transform holdPoint;
        ProductItem carriedItem;
        Vector3 holdBase, windupHand = new Vector3(0.3f, 1.56f, -0.08f);
        bool heavy;
        float carryHH = 0.15f;
        // tutma hissi: adım sallantısı + yay gecikmesi; fırlatma: gövde yaylanması
        Vector3 springPos, springVel, prevLocalVel;
        float bobPhase, releaseKick, aimW, lean, crouch;

        static readonly int MoveXHash = Animator.StringToHash("MoveX");
        static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int StunHash = Animator.StringToHash("Stunned");
        static readonly int CelebrateHash = Animator.StringToHash("Celebrate");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int PushedHash = Animator.StringToHash("Pushed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int VertHash = Animator.StringToHash("VertSpeed");
        static readonly int JumpHash = Animator.StringToHash("Jump");

        static int groundMask = -1;

        public Animator Animator => anim;
        public int CurrentIndex => currentIndex;
        public bool Grounded => grounded;

        /// <summary>Taşınan ürünün merkezinin durduğu nokta.</summary>
        public Transform HoldPoint
        {
            get
            {
                if (holdPoint == null)
                {
                    holdPoint = new GameObject("HoldPoint").transform;
                    holdPoint.SetParent(transform, false);
                    holdPoint.localPosition = holdBase = new Vector3(0.25f, 0.6f, 0.06f);
                    holdPoint.localRotation = Quaternion.Euler(0, -90, 0);
                }
                return holdPoint;
            }
        }

        public void SetModel(int index)
        {
            var lib = CharacterLibrary.Instance;
            if (lib == null) return;
            var list = isNpc ? lib.npcModels : lib.playerModels;
            if (list.Count == 0) return;
            index = ((index % list.Count) + list.Count) % list.Count;
            if (index == currentIndex && current != null) return;
            if (current != null) Destroy(current);
            currentIndex = index;
            if (modelRoot == null) modelRoot = transform;
            current = Instantiate(list[index], modelRoot);
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
            anim = current.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = lib.controller;
                anim.applyRootMotion = false;
                // NPC görünmüyorsa iskelet güncellenmez (büyük CPU kazancı)
                anim.cullingMode = isNpc ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.AlwaysAnimate;
                anim.SetFloat("CycleOffset", Random.value);
                ik = anim.gameObject.GetComponent<HandIK>();
                if (ik == null) ik = anim.gameObject.AddComponent<HandIK>();
                ik.ikLayer = 1;
                ik.body = transform;
                ik.holdPoint = HoldPoint;
            }
            lastPos = transform.position;
            ApplyHidden();
        }

        /// <summary>FPS'te kendi modelimizi gizle (gölgesi kalsın).</summary>
        public void SetFirstPersonHidden(bool hidden)
        {
            fpHidden = hidden;
            ApplyHidden();
        }

        void ApplyHidden()
        {
            if (current == null) return;
            foreach (var r in current.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = fpHidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        /// <summary>Elde taşınan ürünü bildir (her kare çağrılabilir).</summary>
        public void SetCarried(ProductItem item)
        {
            carrying = item != null;
            if (item == carriedItem) return;
            carriedItem = item;
            if (item == null) return;
            Vector3 size = item.LocalSize;
            // tek el, bavul gibi: sağ el bel hizasında, ürün sağ yanda asılı (uzun kenarı ileri bakar)
            carryHH = size.y * 0.5f;
            float lateral = Mathf.Clamp(0.17f + size.z * 0.5f, 0.2f, 0.32f);
            float handY = Mathf.Max(0.86f, carryHH * 2f + HandIK.HangGap + 0.06f);   // uzun ürün yere sürtmesin
            holdBase = new Vector3(lateral, handY - carryHH - HandIK.HangGap, 0.06f);
            HoldPoint.localPosition = holdBase;
            heavy = item.Def != null && item.Def.carrySpeed < 0.9f;
            // fırlatma hazırlığı: el omzun üstüne-arkasına kalkar (tek elle omuz üstü atış)
            windupHand = new Vector3(0.3f, 1.56f, -0.08f);
            springPos = Vector3.zero; springVel = Vector3.zero;
            if (ik != null) ik.itemSize = size;
        }

        /// <summary>Kol hareketi: "Throw", "Grab", "Shove".</summary>
        public void PlayAction(string trigger)
        {
            if (anim != null) anim.SetTrigger(trigger);
            if (trigger == "Shove") releaseKick = Mathf.Max(releaseKick, 0.6f);
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 p = transform.position;
            Vector3 d = p - lastPos;
            lastPos = p;
            float vy = d.y / dt;
            d.y = 0;
            Vector3 vel = d / dt;
            if (vel.magnitude > 15f) { vel = Vector3.zero; vy = 0; } // ışınlanma
            Vector3 local = transform.InverseTransformDirection(vel);
            float k = 1f - Mathf.Exp(-14f * dt);
            smoothLocalVel = Vector3.Lerp(smoothLocalVel, local, k);
            smoothVy = Mathf.Lerp(smoothVy, Mathf.Clamp(vy, -20f, 20f), 1f - Mathf.Exp(-20f * dt));

            // zemin
            if (groundMask == -1)
            {
                groundMask = ~0;
                int fp = LayerMask.NameToLayer(FirstPersonArms.LayerName);
                if (fp >= 0) groundMask &= ~(1 << fp);
                groundMask &= ~(1 << 2); // Ignore Raycast
            }
            bool hitGround = isNpc || Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.42f, groundMask, QueryTriggerInteraction.Ignore);
            if (hitGround) { groundedTimer += dt; airTimer = 0; } else { airTimer += dt; groundedTimer = 0; }
            grounded = hitGround;

            // --- tutma noktası: adım sallantısı + ağırlık gecikmesi (yay) + nişan + fırlatma hazırlığı
            float spdNow = smoothLocalVel.magnitude;
            float wu = windup * windup * (3f - 2f * windup);
            if (wasCarrying && !carrying && prevWindup > 0.3f) releaseKick = 1f;   // ürün elden çıktı: ileri savrul
            prevWindup = windup;
            wasCarrying = carrying;
            releaseKick = Mathf.MoveTowards(releaseKick, 0f, dt * 3.2f);
            aimW = Mathf.MoveTowards(aimW, aiming && carrying ? 1f : 0f, dt * 6f);
            if (holdPoint != null)
            {
                bobPhase += dt * (2.5f + spdNow * 2.4f);
                float amp = Mathf.Clamp01(spdNow / 3.5f) * (heavy ? 1.7f : 1f);
                Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * 0.012f, Mathf.Abs(Mathf.Sin(bobPhase)) * -0.022f, 0) * amp;
                bob.y += Mathf.Sin(Time.time * 1.7f) * 0.005f;                                    // nefes
                Vector3 accel = (smoothLocalVel - prevLocalVel) / dt;
                prevLocalVel = smoothLocalVel;
                Vector3 lagTarget = Vector3.ClampMagnitude(-accel * (heavy ? 0.0045f : 0.003f), 0.07f);
                lagTarget.y = Mathf.Clamp(-smoothVy * 0.012f, -0.06f, 0.06f);                       // zıplarken ürün geride kalır
                springVel += (lagTarget - springPos) * (90f * dt) - springVel * Mathf.Min(1f, 11f * dt);
                springPos += springVel * dt;
                // nişanda ürün hafif öne-yukarı (atışa hazır)
                Vector3 basePos = holdBase + new Vector3(-0.03f, heavy ? 0.05f : 0.12f, 0.12f) * aimW;
                Vector3 pos = basePos + bob * 0.6f + springPos;
                float hang = carryHH + HandIK.HangGap;
                // el (pivot): normalde ürünün üstünde; hazırlıkta omzun üst-arkasına, dışarıdan yay çizerek kalkar (kafadan geçmez)
                Vector3 pivot = Vector3.Lerp(pos + Vector3.up * hang, windupHand, wu) + new Vector3(0.16f, 0f, 0.04f) * Mathf.Sin(Mathf.PI * wu);
                float swingK = 1f - wu;
                // sarkaç: ürün elin altında, adımla ve ivmeyle sallanır; hazırlıkta ürün elin arkasına yatar
                float swing = (Mathf.Sin(bobPhase) * 8f * amp + springPos.z * 260f) * swingK + 78f * wu;
                float roll = (-springPos.x * 220f - Mathf.Sin(bobPhase * 0.5f) * 3f * amp) * swingK;
                var sw = Quaternion.Euler(swing, 0f, roll);
                holdPoint.localPosition = pivot + sw * new Vector3(0f, -hang, 0f);
                holdPoint.localRotation = sw * Quaternion.Euler(0f, -90f, 0f);
            }
            // --- gövde: hazırlıkta geriye yaylan + çök, bırakınca öne savrul (ayaklar yerde kalır)
            if (current != null)
            {
                float k2 = Mathf.Sin(releaseKick * Mathf.PI) * (releaseKick > 0.5f ? 1f : 0.6f);
                float targetLean = -9f * wu + 13f * k2 + (heavy && carrying && !stunned ? -3.5f : 0f);
                lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-22f * dt));
                crouch = Mathf.Lerp(crouch, 0.05f * wu + 0.03f * k2, 1f - Mathf.Exp(-22f * dt));
                current.transform.localRotation = Quaternion.Euler(stunned ? 0f : lean, 0, 0);
                current.transform.localPosition = new Vector3(0, stunned ? 0f : -crouch, 0);
            }

            if (anim == null) return;

            // --- ayak kaymasını önle
            float spd = smoothLocalVel.magnitude;
            Vector3 param = Vector3.zero;
            float playback = 1f;
            if (spd > 0.05f)
            {
                float fwd = Mathf.Clamp01(smoothLocalVel.z / spd);
                float maxDir = Mathf.Lerp(RunClipSpeed, SprintClipSpeed, Mathf.Pow(fwd, 6f));
                float clamped = Mathf.Min(spd, maxDir);
                param = smoothLocalVel / spd * clamped;
                if (spd > maxDir) playback = Mathf.Min(spd / maxDir, 1.9f);
            }
            anim.speed = (stunned || pushed || !grounded) ? 1f : playback;

            anim.SetFloat(MoveXHash, param.x);
            anim.SetFloat(MoveZHash, param.z);
            anim.SetFloat(SpeedHash, spd);
            anim.SetBool(GroundedHash, grounded || airTimer < 0.12f);
            anim.SetFloat(VertHash, smoothVy);
            if (!grounded && vy > 1.5f && !jumpFired && !stunned)
            {
                anim.SetTrigger(JumpHash);
                jumpFired = true;
            }
            if (grounded && groundedTimer > 0.05f) jumpFired = false;

            anim.SetBool(StunHash, stunned);
            if (stunned && !wasStunned) anim.SetTrigger(HitHash);
            if (pushed && !wasPushed && !stunned) anim.SetTrigger(PushedHash);
            wasStunned = stunned;
            wasPushed = pushed;
            anim.SetBool(CelebrateHash, celebrate);
            carryWeight = Mathf.MoveTowards(carryWeight, carrying && !stunned ? 1f : 0f, dt * 7f);
            if (anim.layerCount > 1) anim.SetLayerWeight(1, carryWeight);
            // eller hazırlık boyunca üründe kalır; ürün çıkınca hemen bırakır (fırlatma klibi devralır)
            if (!carrying) carryWeight = Mathf.MoveTowards(carryWeight, 0f, dt * 14f);
            if (ik != null) { ik.weight = carrying && !stunned ? carryWeight : 0f; ik.windup = wu; }
        }
    }
}
