using UnityEngine;

namespace BlackFriday
{
    /// <summary>
    /// Taşınan ürünü iki elle kavratır.
    /// - Konum: Humanoid IK (bilek hedefi + dirsek ipucu). Animatör katmanında "IK Pass" açık olmalı.
    /// - Yön: IK dönüş hedefleri rig'e göre farklı eksenlerle çalıştığı için kullanılmaz; bunun yerine animasyon + IK
    ///   bittikten sonra (LateUpdate) el kemiği, parmak ve avuç yönü istenen yöne bakacak şekilde döndürülür.
    ///   Parmak/avuç yönü kemik konumlarından ölçülür → her rig'de aynı sonuç, kalibrasyon gerekmez.
    /// Kavrama türleri: yanlardan (orta boy), arkadan sarılarak (geniş: TV), alttan avuçla (küçük: telefon, parfüm).
    /// </summary>
    public class HandIK : MonoBehaviour
    {
        public int ikLayer = 1;
        [HideInInspector] public Transform holdPoint;   // ürünün merkezi (dünya)
        [HideInInspector] public Transform body;        // yön referansı (karakter kökü / FPS'te kamera)
        [HideInInspector] public Vector3 itemSize;      // ölçeklenmiş ürün boyutu
        [HideInInspector] public float weight;
        [HideInInspector] public float elbowOut = 0.12f;  // dirseklerin yana açıklığı
        [HideInInspector] public float fingerDown = 0.15f;
        [HideInInspector] public bool underGrip;          // (eski) kullanılmıyor
        [HideInInspector] public bool fpMode;             // FPS el modeli: avuç altta
        [HideInInspector] public float windup;            // 0..1 fırlatma hazırlığı (TPS)

        Animator anim;
        Transform lHand, rHand, lMid, rMid, lIdx, rIdx, lPinky, rPinky;
        bool hasFingers;
        float appliedWeight;
        Vector3 wantFL, wantPL, wantFR, wantPR;   // istenen parmak / avuç yönleri (dünya)
        Vector3 tL, tR; string mode = "-";

        public string DebugReport()
        {
            float dl = lHand != null ? Vector3.Distance(lHand.position, tL) : -1f;
            float dr = rHand != null ? Vector3.Distance(rHand.position, tR) : -1f;
            float aL = -1f;
            if (hasFingers && appliedWeight > 0f) aL = Vector3.Angle(rMid.position - rHand.position, wantFR);
            return $"w={weight:F2} mode={mode} size={itemSize} dL={dl:F3} dR={dr:F3} fingerErrL={aL:F1}";
        }

        void Awake()
        {
            anim = GetComponent<Animator>();
            if (anim == null || !anim.isHuman) return;
            lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            lMid = anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            rMid = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            lIdx = anim.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            rIdx = anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            lPinky = anim.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            rPinky = anim.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            hasFingers = lHand && rHand && lMid && rMid && lIdx && rIdx && lPinky && rPinky;
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (anim == null || layerIndex != ikLayer) return;

            float w = holdPoint != null && body != null ? Mathf.Clamp01(weight) : 0f;
            appliedWeight = w;
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, w * 0.7f);
            if (w <= 0f) return;

            Vector3 c = holdPoint.position;
            Vector3 right = body.right, up = body.up, fwd = body.forward;
            float hh = itemSize.y * 0.5f;

            // tek el: sol kol serbest (TPS'te yürüyüş sallanması, FPS'te aşağıda)
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            leftActive = false;

            if (fpMode)
            {
                // FPS: sağ avuç ürünün altında, parmaklar ileri-içe
                mode = "palm";
                Vector3 hand = c - up * (hh + 0.025f) - fwd * 0.02f;
                SetRight(hand, fwd - right * 0.3f, up);
                anim.SetIKHintPosition(AvatarIKHint.RightElbow, hand + right * 0.18f - up * 0.25f - fwd * 0.2f);
                return;
            }

            // TPS: bavul tutar gibi — el ürünün üst kenarında, avuç gövdeye dönük, ürün yanda sallanır
            mode = "hang";
            Vector3 iu = holdPoint.up;
            Vector3 top = c + iu * (hh + HangGap);
            float wu = Mathf.Clamp01(windup);
            // fırlatma hazırlığı: el omzun üst-arkasında, ürün elin arkasında; avuç ürüne, dirsek yana-aşağı
            Vector3 fingers = Vector3.Lerp(-iu * 0.75f + fwd * 0.35f, up * 0.8f - iu * 0.3f, wu);
            Vector3 palm = Vector3.Lerp(-right, -iu, wu);
            SetRight(top, fingers, palm);
            Vector3 hint = Vector3.Lerp(top + right * 0.1f + up * 0.3f - fwd * 0.14f, top + right * 0.24f - up * 0.22f + fwd * 0.04f, wu);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, hint);
        }

        /// <summary>Elin ürünün üst kenarından yukarıdaki mesafesi (bilek → avuç).</summary>
        public const float HangGap = 0.07f;
        bool leftActive;

        void SetRight(Vector3 pos, Vector3 fingers, Vector3 palm)
        {
            anim.SetIKPosition(AvatarIKGoal.RightHand, tR = pos);
            wantFR = fingers.normalized; wantPR = palm.normalized;
            tL = pos;
        }

        void Set(Vector3 l, Vector3 r, Vector3 fl, Vector3 pl, Vector3 fr, Vector3 pr)
        {
            anim.SetIKPosition(AvatarIKGoal.LeftHand, tL = l);
            anim.SetIKPosition(AvatarIKGoal.RightHand, tR = r);
            wantFL = fl.normalized; wantPL = pl.normalized;
            wantFR = fr.normalized; wantPR = pr.normalized;
        }

        void Hints(Vector3 c, Vector3 right, Vector3 up, Vector3 fwd, float side, float down, float backOff)
        {
            anim.SetIKHintPosition(AvatarIKHint.LeftElbow, c - right * side - up * down - fwd * backOff);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, c + right * side - up * down - fwd * backOff);
        }

        void LateUpdate()
        {
            if (!hasFingers || appliedWeight <= 0f) return;
            if (leftActive) Orient(lHand, lMid, lIdx, lPinky, true, wantFL, wantPL, appliedWeight);
            Orient(rHand, rMid, rIdx, rPinky, false, wantFR, wantPR, appliedWeight);
        }

        /// <summary>El kemiğini, parmaklar F'ye ve avuç P'ye bakacak şekilde döndürür (kemik konumlarından ölçerek).</summary>
        static void Orient(Transform hand, Transform mid, Transform idx, Transform pinky, bool left, Vector3 F, Vector3 P, float w)
        {
            Vector3 f = mid.position - hand.position;
            Vector3 side = idx.position - pinky.position;
            if (f.sqrMagnitude < 1e-6f || side.sqrMagnitude < 1e-6f) return;
            f.Normalize(); side.Normalize();
            // sol el: avuç = yan x parmak, sağ el: parmak x yan (T-pozda ikisi de aşağı)
            Vector3 p = left ? Vector3.Cross(side, f) : Vector3.Cross(f, side);
            if (p.sqrMagnitude < 1e-4f) return;
            Vector3 pw = Vector3.ProjectOnPlane(P, F);
            if (pw.sqrMagnitude < 1e-4f) return;
            Quaternion cur = Quaternion.LookRotation(f, p);
            Quaternion want = Quaternion.LookRotation(F, pw);
            Quaternion delta = want * Quaternion.Inverse(cur);
            hand.rotation = Quaternion.Slerp(Quaternion.identity, delta, w) * hand.rotation;
        }
    }
}
