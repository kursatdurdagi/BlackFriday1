using UnityEditor;
using UnityEngine;

namespace BlackFriday.EditorTools
{
    /// <summary>Arayüz dokuları (kâğıt, karton, bant, LCD...): mipmap yok, sıkıştırma yok, tekrar eden sarma.</summary>
    public class BFUITextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("BlackFriday/UI/Tex/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.sRGBTexture = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Bilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
