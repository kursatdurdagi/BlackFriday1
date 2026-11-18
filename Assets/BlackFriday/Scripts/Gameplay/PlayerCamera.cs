using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BlackFriday
{
    /// <summary>
    /// TPS / FPS kamera. TPS: omuz üstü orbit kamera, sağ tıkta yakınlaşıp nişan alır.
    /// FPS: göz hizası. V / R3 ile geçiş. Duvar/raf içine girmemek için çarpışma kontrolü yapar.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-100)] // kollar ve taşınan ürün kameradan sonra konumlansın
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera Instance;

        [Header("TPS")]
        public float tpsDistance = 3.6f;
        public float tpsMinDistance = 1.8f, tpsMaxDistance = 6f;
        public float tpsPivotHeight = 1.55f;
        public float tpsShoulder = 0.45f;
        public float aimDistance = 1.9f;
        public float aimShoulder = 0.65f;
        public float tpsFov = 60f, aimFov = 45f;
        public Vector2 tpsPitchLimits = new Vector2(-40f, 70f);

        [Header("FPS")]
        public float eyeHeight = 1.55f;
        public float eyeForward = 0.12f;
        public float fpsFov = 70f, fpsAimFov = 55f;
        public Vector2 fpsPitchLimits = new Vector2(-80f, 80f);

        [Header("Genel")]
        public float collisionRadius = 0.25f;
        public RuntimeAnimatorController fpArmsController;
        public float armsFov = 58f;

        public Transform target;
        public bool FirstPerson { get; private set; }
        public bool Aiming { get; set; }
        public float Yaw => yaw;
        public float Pitch => pitch;

        Camera cam;
        Camera armsCam;
        FirstPersonArms arms;
        float yaw, pitch = 12f;
        float curDist, curShoulder;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            FirstPerson = GameSettings.FirstPerson;
            curDist = tpsDistance;
            curShoulder = tpsShoulder;
            SetupArms();
        }

        /// <summary>FPS kolları için overlay kamera (URP camera stack) + viewmodel.</summary>
        void SetupArms()
        {
            int layer = FirstPersonArms.Layer;
            if (layer < 0) { Debug.LogWarning("[BF] FPArms katmanı yok"); return; }
            cam.cullingMask &= ~(1 << layer);

            var go = new GameObject("FPArmsCamera");
            go.transform.SetParent(transform, false);
            armsCam = go.AddComponent<Camera>();
            armsCam.cullingMask = 1 << layer;
            armsCam.nearClipPlane = 0.01f;
            armsCam.farClipPlane = 5f;
            armsCam.fieldOfView = armsFov;
            armsCam.useOcclusionCulling = false;
            var data = armsCam.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderShadows = false;
            var baseData = cam.GetUniversalAdditionalCameraData();
            baseData.cameraStack.Add(armsCam);

            var ag = new GameObject("FPArms");
            ag.transform.SetParent(transform, false);
            arms = ag.AddComponent<FirstPersonArms>();
            arms.controller = fpArmsController;
            UpdateArmsVisibility();
        }

        void UpdateArmsVisibility()
        {
            bool on = FirstPerson && target != null;
            if (armsCam != null) armsCam.enabled = on;
            if (arms != null) arms.SetVisible(on);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetTarget(Transform t)
        {
            target = t;
            if (t != null) yaw = t.eulerAngles.y;
            ApplyFirstPersonVisual();
            UpdateArmsVisibility();
        }

        public void SetFirstPerson(bool fps)
        {
            FirstPerson = fps;
            GameSettings.FirstPerson = fps;
            ApplyFirstPersonVisual();
            UpdateArmsVisibility();
        }

        void ApplyFirstPersonVisual()
        {
            if (target == null) return;
            var vis = target.GetComponent<CharacterVisual>();
            if (vis != null) vis.SetFirstPersonHidden(FirstPerson);
        }

        public void SetAngles(float newYaw, float newPitch) { yaw = newYaw; pitch = newPitch; }

        /// <summary>Kamera merkezinden nişan ışını.</summary>
        public Ray AimRay => new Ray(transform.position, transform.forward);

        /// <summary>Kameranın baktığı dünya noktası (oyuncunun kendisi hariç).</summary>
        public Vector3 AimPoint(float maxDist = 40f)
        {
            var ray = AimRay;
            int n = Physics.RaycastNonAlloc(ray, hits, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float best = maxDist; Vector3 p = ray.origin + ray.direction * maxDist;
            for (int i = 0; i < n; i++)
            {
                if (IsIgnored(hits[i].collider)) continue;
                if (hits[i].distance < best && hits[i].distance > 0.3f) { best = hits[i].distance; p = hits[i].point; }
            }
            return p;
        }

        bool IsIgnored(Collider c)
        {
            if (c == null) return true;
            if (target != null && c.transform.IsChildOf(target)) return true;
            if (c.GetComponentInParent<BFPlayer>() != null) return true;
            if (c.GetComponentInParent<NPCShopper>() != null) return true;
            if (c.GetComponentInParent<ProductItem>() != null) return true;
            return false;
        }

        void LateUpdate()
        {
            if (target == null && BFPlayer.Local != null) SetTarget(BFPlayer.Local.transform);
            if (arms != null && BFPlayer.Local != null && BFPlayer.Local.skinIndex >= 0) arms.SetSkin(BFPlayer.Local.skinIndex);
            if (BFInput.ToggleCamera()) SetFirstPerson(!FirstPerson);

            Vector2 look = BFInput.Look();
            yaw += look.x;
            pitch -= look.y;
            var lim = FirstPerson ? fpsPitchLimits : tpsPitchLimits;
            pitch = Mathf.Clamp(pitch, lim.x, lim.y);

            if (target == null) return;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-14f * dt);

            if (FirstPerson)
            {
                Vector3 flatFwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                transform.position = target.position + Vector3.up * eyeHeight + flatFwd * eyeForward;
                transform.rotation = rot;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Aiming ? fpsAimFov : fpsFov, k);
                cam.nearClipPlane = 0.05f;
                return;
            }

            tpsDistance = Mathf.Clamp(tpsDistance + BFInput.Zoom(), tpsMinDistance, tpsMaxDistance);
            curDist = Mathf.Lerp(curDist, Aiming ? aimDistance : tpsDistance, k);
            curShoulder = Mathf.Lerp(curShoulder, Aiming ? aimShoulder : tpsShoulder, k);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Aiming ? aimFov : tpsFov, k);
            cam.nearClipPlane = 0.1f;

            Vector3 pivot = target.position + Vector3.up * tpsPivotHeight;
            Vector3 shoulder = rot * Vector3.right * curShoulder;
            Vector3 start = pivot;
            Vector3 desired = pivot + shoulder - rot * Vector3.forward * curDist;
            Vector3 dir = desired - start;
            float len = dir.magnitude;
            float allowed = len;
            int n = Physics.SphereCastNonAlloc(start, collisionRadius, dir / Mathf.Max(len, 0.001f), hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (IsIgnored(hits[i].collider)) continue;
                if (hits[i].distance < allowed) allowed = hits[i].distance;
            }
            transform.position = start + dir.normalized * Mathf.Max(0.2f, allowed);
            transform.rotation = rot;
        }
    }
}
