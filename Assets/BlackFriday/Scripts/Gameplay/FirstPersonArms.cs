using UnityEngine;
using UnityEngine.Rendering;

namespace BlackFriday
{
    /// <summary>
    /// FPS el modeli (viewmodel). Oyuncunun kendi karakterinin bir kopyası kameraya bağlanır,
    /// baş ve bacaklar kemik ölçeğiyle gizlenir; sadece kollar/eller görünür.
    /// Ayrı katmanda (FPArms) overlay kamerayla çizilir → duvar/rafa girmez.
    /// Taşınan ürün ekranın alt-ortasına sığacak şekilde ölçeklenir; eller IK ile ürünün yanlarını kavrar.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class FirstPersonArms : MonoBehaviour
    {
        public static FirstPersonArms Instance;
        public const string LayerName = "FPArms";

        public RuntimeAnimatorController controller;
        public Vector3 modelOffset = new Vector3(0f, -1.52f, -0.16f);
        public Vector3 headOffset = new Vector3(0f, -0.03f, -0.04f);  // baş kemiğinin kameraya göre yeri
        public Vector3 itemPos = new Vector3(0.12f, -0.1f, 0.36f);   // taşınan ürün: sağ alt, tek avuçta (kollar 0.47 m)
        public Vector3 maxItemSize = new Vector3(0.15f, 0.12f, 0.13f);
        public float handScale = 0.82f;                              // stilize büyük eller görüntüyü kaplamasın
        public float bobAmount = 0.022f;
        public float swayAmount = 0.02f;

        Transform rig;
        GameObject model;
        Animator anim;
        HandIK ik;
        int skin = -1;
        Transform itemAnchor;
        Transform head;
        float bobTime;
        Vector3 swayPos;
        Vector3 lastPlayerPos;
        bool visible = true;
        ProductItem currentItem;
        float itemScale = 1f;
        float windup, kick, prevWindup;
        Vector3 itemLag, itemLagVel;

        static readonly int CarryHash = Animator.StringToHash("Carry");

        public Transform ItemAnchor => itemAnchor;
        public float ItemScale => itemScale;
        public static int Layer => LayerMask.NameToLayer(LayerName);

        void Awake()
        {
            Instance = this;
            rig = new GameObject("ArmsRig").transform;
            rig.SetParent(transform, false);
            itemAnchor = new GameObject("ItemAnchor").transform;
            itemAnchor.SetParent(rig, false);
            itemAnchor.localPosition = itemPos;
            itemAnchor.localRotation = Quaternion.Euler(0, 180, 0);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetVisible(bool v)
        {
            visible = v;
            if (model != null) model.SetActive(v);
        }

        public void SetSkin(int index)
        {
            if (index == skin && model != null) return;
            var lib = CharacterLibrary.Instance;
            if (lib == null || lib.playerModels.Count == 0) return;
            index = ((index % lib.playerModels.Count) + lib.playerModels.Count) % lib.playerModels.Count;
            skin = index;
            if (model != null) Destroy(model);
            model = Instantiate(lib.playerModels[index], rig);
            model.name = "FPArmsModel";
            model.transform.localPosition = modelOffset;
            model.transform.localRotation = Quaternion.identity;
            int layer = Layer;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (layer >= 0) t.gameObject.layer = layer;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            anim = model.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
                ik = anim.gameObject.GetComponent<HandIK>();
                if (ik == null) ik = anim.gameObject.AddComponent<HandIK>();
                ik.ikLayer = 0;
                ik.elbowOut = 0.05f;
                ik.fpMode = true;
                ik.fingerDown = 0.1f;
                ik.body = rig;
                ik.holdPoint = itemAnchor;
            }
            model.SetActive(visible);
        }

        public void Play(string trigger)
        {
            if (anim != null && anim.isActiveAndEnabled) anim.SetTrigger(trigger);
        }

        /// <summary>Taşınan ürünü ekrana sığdır.</summary>
        void ConfigureItem(ProductItem item)
        {
            currentItem = item;
            if (item == null) return;
            Vector3 s = item.LocalSize;
            itemScale = Mathf.Min(1f, maxItemSize.x / Mathf.Max(s.x, 0.01f), maxItemSize.y / Mathf.Max(s.y, 0.01f), maxItemSize.z / Mathf.Max(s.z, 0.01f));
            if (ik != null) ik.itemSize = s * itemScale;
        }

        void Update()
        {
            var p = BFPlayer.Local;
            if (p == null || model == null) return;

            var held = p.HeldItem;
            if (held != currentItem) ConfigureItem(held);
            bool carrying = held != null;
            windup = Mathf.MoveTowards(windup, p.ThrowPending ? 1f : 0f, Time.deltaTime * (p.ThrowPending ? 1f / p.throwWindup : 10f));
            if (prevWindup > 0.4f && !p.ThrowPending && !carrying) kick = 1f;   // bırakma anı: kollar ileri savrulur
            prevWindup = windup;
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 3.5f);

            if (anim != null) anim.SetBool(CarryHash, carrying);
            if (ik != null) ik.weight = carrying ? 1f : 0f;

            // modeli baş kemiğine göre kameraya hizala (animasyondan ÖNCE: IK hedefleriyle tutarlı kalsın)
            if (head != null)
            {
                Vector3 desired = rig.TransformPoint(headOffset);
                model.transform.position += desired - head.position;
            }
        }

        void LateUpdate()
        {
            var p = BFPlayer.Local;
            if (p == null || model == null) return;

            // baş/bacakları gizle
            if (anim != null && anim.isHuman)
            {
                Hide(HumanBodyBones.Head);
                Hide(HumanBodyBones.LeftUpperLeg);
                Hide(HumanBodyBones.RightUpperLeg);
                // FPS'te sadece sağ el: sol kol omuzdan söndürülür (görüş alanına hiç girmez)
                Hide(HumanBodyBones.LeftUpperArm);
                Scale(HumanBodyBones.RightHand, handScale);
            }

            float dt = Time.deltaTime;
            Vector3 pv = p.transform.position - lastPlayerPos; pv.y = 0;
            lastPlayerPos = p.transform.position;
            float speed = dt > 0 ? Mathf.Min(pv.magnitude / dt, 8f) : 0;
            bobTime += dt * (2f + speed * 1.6f);
            float bobAmp = bobAmount * Mathf.Clamp01(speed / 4f);
            Vector3 bob = new Vector3(Mathf.Sin(bobTime) * bobAmp, Mathf.Abs(Mathf.Cos(bobTime)) * bobAmp * 1.2f, 0);
            bob.y += Mathf.Sin(Time.time * 1.6f) * 0.004f;

            Vector2 look = BFInput.Look();
            Vector3 targetSway = Vector3.ClampMagnitude(new Vector3(-look.x, -look.y, 0) * swayAmount * 0.15f, 0.05f);
            swayPos = Vector3.Lerp(swayPos, targetSway, 1f - Mathf.Exp(-10f * dt));

            float kk = Mathf.Sin(kick * Mathf.PI);
            rig.localPosition = bob + swayPos + new Vector3(0.07f * kk, -0.02f * kk, 0.07f * kk);   // bırakışta kol sağda kalır, ekranı kapatmaz
            rig.localRotation = Quaternion.Euler(-swayPos.y * 60f - 7f * kk, swayPos.x * 60f, swayPos.x * 40f);

            // ürün ağırlığı: hızlanınca / dönünce ürün yay gibi geride kalır
            Vector3 lagT = new Vector3(Mathf.Clamp(-look.x * 0.004f, -0.03f, 0.03f), Mathf.Clamp(look.y * 0.003f, -0.02f, 0.02f) - bob.y * 0.6f, 0);
            itemLagVel += (lagT - itemLag) * (120f * dt) - itemLagVel * Mathf.Min(1f, 12f * dt);
            itemLag += itemLagVel * dt;

            // fırlatma hazırlığı: ürün geri ve yukarı çekilir
            float wu = windup * windup * (3f - 2f * windup);
            // hazırlık: ürün sağ omza geri-yukarı çekilir (tek elle atış)
            itemAnchor.localPosition = itemPos + itemLag + new Vector3(0.06f, 0.09f, -0.12f) * wu;
            itemAnchor.localRotation = Quaternion.Euler(-30f * wu + itemLag.y * 200f, 165f - 20f * wu, -itemLag.x * 260f - 10f * wu);
        }

        void Scale(HumanBodyBones b, float s)
        {
            var t = anim.GetBoneTransform(b);
            if (t != null) t.localScale = Vector3.one * s;
        }

        void Hide(HumanBodyBones b)
        {
            var t = anim.GetBoneTransform(b);
            if (t != null) t.localScale = Vector3.zero;
        }
    }
}
