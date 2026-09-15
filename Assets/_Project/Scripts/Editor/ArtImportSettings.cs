using UnityEditor;
using UnityEngine;

namespace SevenDoctors.EditorTools
{
    /// <summary>
    /// Resources/Art 아래로 들어오는 이미지의 임포트 설정을 자동으로 맞춥니다.
    ///
    /// 이게 없으면 아트 담당이 PNG 를 넣을 때마다 Inspector 에서 Texture Type 을
    /// Sprite 로 바꿔줘야 하고, 한 번만 깜빡해도 Resources.Load&lt;Sprite&gt; 가 null 을 돌려줘서
    /// "왜 배경이 안 나오지" 를 한참 헤매게 됩니다. 그 사고를 원천 차단하는 장치입니다.
    ///
    /// 아트 담당에게는 이렇게만 전하면 됩니다 — "PNG 를 폴더에 넣기만 하세요."
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        const string ArtRoot = "Assets/_Project/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;

            // 이미 한 번 처리한 에셋은 건드리지 않습니다 (아트가 손으로 조정한 값 보존)
            if (!string.IsNullOrEmpty(importer.userData) && importer.userData.Contains("sd-art")) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;

            // 폴더별 해상도 상한 — 배경은 크게, 아이콘은 작게
            if (assetPath.Contains("/Backgrounds/"))      importer.maxTextureSize = 2048;
            else if (assetPath.Contains("/Characters/"))  importer.maxTextureSize = 2048;
            else if (assetPath.Contains("/Faces/"))       importer.maxTextureSize = 2048;
            else if (assetPath.Contains("/Evidence/"))    importer.maxTextureSize = 512;
            else                                          importer.maxTextureSize = 1024;

            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.userData = "sd-art";
        }

        [MenuItem("Tools/일곱 박사/아트 임포트 설정 다시 적용", false, 30)]
        static void Reimport()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Resources/Art" });
            int n = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.userData = "";           // 강제로 다시 적용되게
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                n++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ArtImportSettings] 이미지 {n}개의 임포트 설정을 다시 적용했습니다.");
        }
    }
}
