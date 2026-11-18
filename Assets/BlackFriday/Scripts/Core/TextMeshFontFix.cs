using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackFriday
{
    /// <summary>Dinamik font dokusu yenilendiğinde 3D yazı materyalinin dokusunu günceller.</summary>
    public static class TextMeshFontFix
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Font.textureRebuilt += _ => Apply();
            SceneManager.sceneLoaded += (_, __) => Apply();
            Apply();
        }

        static void Apply()
        {
            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if (tm.font == null) continue;
                var r = tm.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader.name != "BlackFriday/Text3D") continue;
                r.sharedMaterial.mainTexture = tm.font.material.mainTexture;
            }
        }
    }
}
