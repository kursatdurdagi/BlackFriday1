using System.Collections.Generic;

namespace BlackFriday
{
    /// <summary>
    /// Bölüm ve mod listesi. Yeni bölüm / mod eklemek için buraya bir kayıt ekle,
    /// mod için de GameModeBase'den türeyen bir sınıf yaz ve sahneye koy.
    /// </summary>
    public static class GameCatalog
    {
        public class LevelInfo
        {
            public string id, displayName, description, scenePath;
            public bool available;
        }

        public class ModeInfo
        {
            public string id, displayName, description;
            public bool available;
        }

        public static readonly List<LevelInfo> Levels = new List<LevelInfo>
        {
            new LevelInfo { id = "megaavm", displayName = "Bölüm 1: Mega AVM", description = "Dev AVM: mağazalar, yemek alanı, yürüyen merdivenler. Kapılar açılıyor!", scenePath = "Assets/BlackFriday/Scenes/Level01_MegaMart.unity", available = true },
            new LevelInfo { id = "outlet", displayName = "Bölüm 2: Outlet Çarşı", description = "Yakında", available = false },
            new LevelInfo { id = "warehouse", displayName = "Bölüm 3: Depo", description = "Yakında", available = false },
        };

        public static readonly List<ModeInfo> Modes = new List<ModeInfo>
        {
            new ModeInfo { id = "shoppinglist", displayName = "Alışveriş Listesi", description = "Ortak listeyi süre bitmeden kasaya yetiştir. Kombo ve flaş indirimle puanı katla.", available = true },
            new ModeInfo { id = "grabrace", displayName = "Kapış Kapış", description = "Yakında", available = false },
            new ModeInfo { id = "survival", displayName = "İzdiham", description = "Yakında", available = false },
        };

        public static int SelectedLevel = 0;
        public static int SelectedMode = 0;
    }
}
