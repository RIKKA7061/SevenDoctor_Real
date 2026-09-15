using System.IO;
using SevenDoctors.UI;
using UnityEditor;
using UnityEngine;

namespace SevenDoctors.EditorTools
{
    /// <summary>
    /// UI 계층 전체를 진짜 GameObject 로 만들어 프리팹으로 저장합니다.
    ///
    /// 만드는 코드는 UIRoot.CreateFromCode() 하나뿐입니다.
    /// 여기서는 그걸 에디터에서 한 번 실행시켜 결과물을 프리팹으로 굳힐 뿐이라,
    /// 코드 경로와 프리팹 경로의 구조가 갈라질 일이 없습니다.
    ///
    /// 만들고 나면:
    ///   · Hierarchy / Inspector 에서 위치·색·크기를 눈으로 보고 고칠 수 있습니다
    ///   · 씬 파일에는 여전히 카메라와 GameBootstrap 만 들어 있어서 깃 충돌이 안 납니다
    ///     (UI 는 프리팹 파일 하나에 모여 있으므로, 건드리는 사람이 겹칠 때만 충돌합니다)
    ///
    /// 코드로 UI 구조를 바꾼 뒤에는 이 메뉴를 다시 눌러 프리팹을 갱신하세요.
    /// 프리팹이 코드와 안 맞으면 런타임이 알아서 코드 생성으로 되돌아가고 경고를 남깁니다.
    /// </summary>
    public static class UIPrefabBuilder
    {
        const string Folder = "Assets/_Project/Resources/UI";
        const string PrefabPath = Folder + "/UIRootCanvas.prefab";

        [MenuItem("Tools/일곱 박사/UI 프리팹 만들기", false, 10)]
        public static void BuildPrefab()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("안내", "플레이 중에는 만들 수 없습니다. 먼저 Play 를 멈춰주세요.", "확인");
                return;
            }

            if (File.Exists(PrefabPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "UI 프리팹 만들기",
                    "UIRootCanvas 프리팹이 이미 있습니다.\n다시 만들면 Inspector 에서 손으로 고친 값이 전부 초기화됩니다.\n\n계속할까요?",
                    "새로 만들기", "취소");
                if (!overwrite) return;
            }

            Directory.CreateDirectory(Folder);

            GameObject temp = null;
            GameObject canvasGo = null;

            try
            {
                // 1) 코드로 한 번 만든다
                temp = new GameObject("~UIPrefabTemp");
                var ui = temp.AddComponent<UIRoot>();
                ui.CreateFromCode();

                canvasGo = ui.Canvas.gameObject;

                // 2) Canvas 를 떼어내 프리팹 루트로 삼는다
                canvasGo.transform.SetParent(null, false);
                canvasGo.name = "UIRootCanvas";

                // 3) 저장
                var saved = PrefabUtility.SaveAsPrefabAsset(canvasGo, PrefabPath, out bool success);
                if (!success || saved == null)
                {
                    Debug.LogError("[UIPrefabBuilder] 프리팹 저장에 실패했습니다.");
                    return;
                }

                AssetDatabase.Refresh();
                Selection.activeObject = saved;
                EditorGUIUtility.PingObject(saved);

                int count = saved.GetComponentsInChildren<Transform>(true).Length;
                Debug.Log($"[UIPrefabBuilder] {PrefabPath} 생성 완료 — GameObject {count}개.\n" +
                          "이제 Play 하면 이 프리팹에서 UI 를 불러옵니다. Inspector 에서 고친 값이 그대로 반영됩니다.");

                EditorUtility.DisplayDialog("완료",
                    $"UI 프리팹을 만들었습니다. (GameObject {count}개)\n\n" +
                    "Project 창에서 UIRootCanvas 를 더블클릭하면 Hierarchy 에서 편집할 수 있습니다.\n" +
                    "Play 하면 이 프리팹이 그대로 쓰입니다.", "확인");
            }
            finally
            {
                // 씬에 임시 오브젝트를 남기지 않는다
                if (canvasGo != null) Object.DestroyImmediate(canvasGo);
                if (temp != null) Object.DestroyImmediate(temp);
            }
        }

        /// <summary>대화상자 없이 프리팹을 만듭니다. 씬 생성기가 이어서 부릅니다.</summary>
        public static void BuildPrefabSilent()
        {
            if (Application.isPlaying) return;
            Directory.CreateDirectory(Folder);

            GameObject temp = null, canvasGo = null;
            try
            {
                temp = new GameObject("~UIPrefabTemp");
                var ui = temp.AddComponent<UIRoot>();
                ui.CreateFromCode();

                canvasGo = ui.Canvas.gameObject;
                canvasGo.transform.SetParent(null, false);
                canvasGo.name = "UIRootCanvas";

                PrefabUtility.SaveAsPrefabAsset(canvasGo, PrefabPath, out bool success);
                if (success) AssetDatabase.Refresh();
                else Debug.LogError("[UIPrefabBuilder] 프리팹 저장에 실패했습니다.");
            }
            finally
            {
                if (canvasGo != null) Object.DestroyImmediate(canvasGo);
                if (temp != null) Object.DestroyImmediate(temp);
            }
        }

        [MenuItem("Tools/일곱 박사/UI 프리팹 열기", false, 11)]
        public static void OpenPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("안내",
                    "아직 UI 프리팹이 없습니다.\nTools → 일곱 박사 → UI 프리팹 만들기 를 먼저 실행하세요.", "확인");
                return;
            }

            AssetDatabase.OpenAsset(prefab);
        }

        [MenuItem("Tools/일곱 박사/UI 프리팹 열기", true)]
        static bool OpenPrefabValidate() => File.Exists(PrefabPath);

        public static bool PrefabExists => File.Exists(PrefabPath);
    }
}
