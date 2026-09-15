using System.Collections.Generic;
using System.IO;
using SevenDoctors.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SevenDoctors.EditorTools
{
    /// <summary>
    /// Game 씬을 한 번에 만들어줍니다.
    ///
    /// 씬에 들어가는 건 카메라와 GameBootstrap 오브젝트뿐입니다.
    /// UI도 매니저도 전부 런타임에 코드로 생기기 때문에, 씬 파일이 거의 비어 있고
    /// 그래서 셋이 같이 작업해도 씬 충돌이 나지 않습니다.
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        [MenuItem("Tools/일곱 박사/게임 씬 만들기", false, 20)]
        public static void BuildGameScene()
        {
            if (File.Exists(ScenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "게임 씬 만들기",
                    "Game.unity 가 이미 있습니다. 새로 만들면 기존 씬 내용이 사라집니다.\n계속할까요?",
                    "새로 만들기", "취소");
                if (!overwrite) return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            camGo.transform.position = new Vector3(0, 0, -10);

            // 부트스트랩
            var bootGo = new GameObject("GameBootstrap");
            bootGo.AddComponent<GameBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);

            AssetDatabase.Refresh();
            Selection.activeGameObject = bootGo;

            Debug.Log($"[SceneBuilder] {ScenePath} 생성 완료. 그대로 Play 를 누르면 됩니다.");
            EditorUtility.DisplayDialog("완료",
                "Game.unity 를 만들고 빌드 설정에 등록했습니다.\n\n" +
                "이제 Play 버튼을 누르면 프롤로그가 돌아갑니다.", "확인");
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var s in scenes)
                if (s.path == path) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("Tools/일곱 박사/현재 씬에 부트스트랩 추가", false, 21)]
        public static void AddBootstrapToCurrentScene()
        {
            if (Object.FindFirstObjectByType<GameBootstrap>() != null)
            {
                EditorUtility.DisplayDialog("안내", "이 씬에는 이미 GameBootstrap 이 있습니다.", "확인");
                return;
            }

            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Add GameBootstrap");
            Selection.activeGameObject = go;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[SceneBuilder] 현재 씬에 GameBootstrap 을 추가했습니다.");
        }
    }
}
