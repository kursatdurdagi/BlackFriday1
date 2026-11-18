using System.Collections.Generic;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>Otomatik sürgülü kapı: yakında oyuncu / müşteri varsa kanatlar açılır (görsel, yerel).</summary>
    public class AutoDoor : MonoBehaviour
    {
        public Transform leafA, leafB;
        public Vector3 slideA = new Vector3(0, 0, -1.9f), slideB = new Vector3(0, 0, 1.9f);
        public float range = 5.5f;

        Vector3 homeA, homeB; float open, nextCheck; bool wantOpen;

        void Awake() { if (leafA) homeA = leafA.localPosition; if (leafB) homeB = leafB.localPosition; }

        void Update()
        {
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.2f;
                wantOpen = false;
                Vector3 p = transform.position; float r2 = range * range;
                foreach (var pl in BFPlayer.All) if ((pl.transform.position - p).sqrMagnitude < r2) { wantOpen = true; break; }
                if (!wantOpen) foreach (var n in NPCShopper.All) if ((n.transform.position - p).sqrMagnitude < r2) { wantOpen = true; break; }
            }
            float target = wantOpen ? 1f : 0f;
            if (Mathf.Approximately(open, target)) return;
            open = Mathf.MoveTowards(open, target, Time.deltaTime * 2.2f);
            float e = open * open * (3f - 2f * open);
            if (leafA) leafA.localPosition = homeA + slideA * e;
            if (leafB) leafB.localPosition = homeB + slideB * e;
        }
    }
}
