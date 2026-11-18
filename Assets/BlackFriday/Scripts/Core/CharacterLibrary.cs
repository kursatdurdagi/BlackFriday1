using System.Collections.Generic;
using UnityEngine;

namespace BlackFriday
{
    [CreateAssetMenu(menuName = "BlackFriday/Character Library")]
    public class CharacterLibrary : ScriptableObject
    {
        [Tooltip("Oyuncuların seçebileceği karakterler")] public List<GameObject> playerModels = new List<GameObject>();
        public List<string> playerModelNames = new List<string>();
        [Tooltip("NPC müşteriler için karakterler")] public List<GameObject> npcModels = new List<GameObject>();
        public RuntimeAnimatorController controller;

        static CharacterLibrary instance;
        public static CharacterLibrary Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<CharacterLibrary>("BF_CharacterLibrary");
                return instance;
            }
        }

        public string PlayerModelName(int i)
        {
            if (i >= 0 && i < playerModelNames.Count) return playerModelNames[i];
            return "Karakter " + (i + 1);
        }
    }
}
