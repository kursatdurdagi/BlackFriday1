using System.Collections.Generic;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>Rafta ürün çıkabilecek nokta.</summary>
    public class ShelfSpawnPoint : MonoBehaviour
    {
        [Tooltip("Boşsa her ürün çıkabilir")] public int[] allowedProducts = new int[0];
        [Tooltip("Mega indirim masası gibi öncelikli noktalar")] public bool hotDeal;

        public bool Allows(int id)
        {
            if (allowedProducts == null || allowedProducts.Length == 0) return true;
            foreach (var a in allowedProducts) if (a == id) return true;
            return false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = hotDeal ? Color.red : Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.15f, new Vector3(0.3f, 0.3f, 0.3f));
        }
    }
}
