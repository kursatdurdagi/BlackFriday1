using Mirror;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>
    /// İtilebilir fizik nesnesi (alışveriş arabası, uyarı levhası). Fizik sunucuda; oyuncular çarpınca / itince
    /// BFPlayer üzerinden komut gelir. Hızlı giden araba müşterilere çarparsa onları sendeletir.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PushableProp : NetworkBehaviour
    {
        public float pushForce = 3.2f;
        public float maxSpeed = 7f;
        public bool knocksNpcs = true;

        Rigidbody rb;
        Vector3 startPos; Quaternion startRot;
        double lastPush;

        void Awake() { rb = GetComponent<Rigidbody>(); startPos = transform.position; startRot = transform.rotation; }

        public override void OnStartClient() { if (!isServer) rb.isKinematic = true; }

        [Server]
        public void ServerPush(Vector3 dir, float strength)
        {
            if (NetworkTime.time - lastPush < 0.08) return;
            lastPush = NetworkTime.time;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) return;
            rb.WakeUp();
            rb.AddForce(dir.normalized * pushForce * strength, ForceMode.VelocityChange);
            var v = rb.linearVelocity; float y = v.y; v.y = 0;
            if (v.magnitude > maxSpeed) v = v.normalized * maxSpeed;
            v.y = y; rb.linearVelocity = v;
        }

        [ServerCallback]
        void FixedUpdate()
        {
            if (transform.position.y < -5f)
            {
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                transform.SetPositionAndRotation(startPos, startRot);
            }
        }

        [ServerCallback]
        void OnCollisionEnter(Collision c)
        {
            if (!knocksNpcs || rb.linearVelocity.magnitude < 3.2f) return;
            var npc = c.collider.GetComponentInParent<NPCShopper>();
            if (npc != null && !npc.IsStunned)
            {
                Vector3 d = rb.linearVelocity; d.y = 0;
                npc.ServerPush(d.normalized);
                rb.linearVelocity *= 0.4f;
            }
        }
    }
}
