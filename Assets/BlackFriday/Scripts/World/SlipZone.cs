using System.Collections.Generic;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>Kaygan zemin (ıslak paspas): içinde koşan oyuncu kayar — ivme ve dönüş düşer.</summary>
    public class SlipZone : MonoBehaviour
    {
        public static readonly List<SlipZone> All = new List<SlipZone>();
        public Vector2 size = new Vector2(3f, 3f);   // x, z (yerel)

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static bool Contains(Vector3 worldPos)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var z = All[i];
                Vector3 l = z.transform.InverseTransformPoint(worldPos);
                if (Mathf.Abs(l.x) <= z.size.x * 0.5f && Mathf.Abs(l.z) <= z.size.y * 0.5f && l.y > -0.3f && l.y < 0.6f) return true;
            }
            return false;
        }
    }
}
