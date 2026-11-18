using System.Collections.Generic;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>Kasa alanı. İçine ürünle giren (ya da ürün fırlatan) oyuncu teslim eder.</summary>
    public class CheckoutZone : MonoBehaviour
    {
        public static readonly List<CheckoutZone> All = new List<CheckoutZone>();
        public Vector3 size = new Vector3(2.5f, 3f, 3f);
        public Transform highlightRoot;      // yanıp sönen çerçeve
        public Renderer highlight;           // (eski sürüm uyumluluğu)

        Renderer[] rends;
        MaterialPropertyBlock mpb;
        float pulse;
        float nextUpdate;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            if (highlightRoot != null) rends = highlightRoot.GetComponentsInChildren<Renderer>();
            else if (highlight != null) rends = new[] { highlight };
            else rends = new Renderer[0];
            mpb = new MaterialPropertyBlock();
        }

        public bool Contains(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            return Mathf.Abs(local.x) <= size.x * 0.5f && local.y >= -0.5f && local.y <= size.y && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        public static bool AnyContains(Vector3 p)
        {
            foreach (var z in All) if (z.Contains(p)) return true;
            return false;
        }

        public void Flash() { pulse = 1f; nextUpdate = 0; }

        void Update()
        {
            if (rends.Length == 0) return;
            // ekonomik: ~30 Hz güncelle
            if (Time.time < nextUpdate && pulse <= 0) return;
            nextUpdate = Time.time + 0.033f;
            pulse = Mathf.MoveTowards(pulse, 0, Time.deltaTime * 2f);
            float idle = 0.6f + 0.25f * Mathf.Sin(Time.time * 3f);
            var c = Color.Lerp(new Color(0.25f, 1f, 0.5f, idle), new Color(1f, 1f, 1f, 1f), pulse);
            mpb.SetColor("_BaseColor", c);
            foreach (var r in rends) if (r != null) r.SetPropertyBlock(mpb);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.up * size.y * 0.5f, size);
        }
    }
}
