using System.Collections.Generic;
using UnityEngine;

namespace BlackFriday
{
    [System.Serializable]
    public class ProductDef
    {
        public int id;
        public string displayName;
        public Color uiColor = Color.white;
        public GameObject prefab;
        [Tooltip("Taşırken hız çarpanı (1 = yavaşlatmaz)")] public float carrySpeed = 1f;
        public int points = 100;
    }

    [CreateAssetMenu(menuName = "BlackFriday/Product Database")]
    public class ProductDatabase : ScriptableObject
    {
        public List<ProductDef> products = new List<ProductDef>();

        static ProductDatabase instance;
        public static ProductDatabase Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<ProductDatabase>("BF_ProductDatabase");
                return instance;
            }
        }

        public ProductDef Get(int id)
        {
            foreach (var p in products) if (p.id == id) return p;
            return null;
        }
    }
}
