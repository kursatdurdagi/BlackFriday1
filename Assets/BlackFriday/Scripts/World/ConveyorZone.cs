using System.Collections.Generic;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>Yürüyen merdiven / bant: içindeki oyuncuyu yönünde taşır.</summary>
    public class ConveyorZone : MonoBehaviour
    {
        public static readonly List<ConveyorZone> All = new List<ConveyorZone>();
        public Vector3 size = new Vector3(2f, 7f, 9f);       // yerel kutu (pivot: taban merkezi)
        public Vector3 localVelocity = new Vector3(0, 0, 1.7f);

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static Vector3 VelocityAt(Vector3 worldPos)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var z = All[i];
                Vector3 l = z.transform.InverseTransformPoint(worldPos);
                if (Mathf.Abs(l.x) <= z.size.x * 0.5f && Mathf.Abs(l.z) <= z.size.z * 0.5f && l.y >= -0.2f && l.y <= z.size.y)
                    return z.transform.TransformDirection(z.localVelocity);
            }
            return Vector3.zero;
        }
    }
}
