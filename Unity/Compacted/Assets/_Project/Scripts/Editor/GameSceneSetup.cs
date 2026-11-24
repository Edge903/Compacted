using Compacted.Data;
using Compacted.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Compacted.Editor
{
    /// <summary>Builds Assets/_Project/Scenes/Game.unity and the assets it needs. Safe to run again.</summary>
    public static class GameSceneSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string PanelPath = "Assets/_Project/UI/GamePanelSettings.asset";
        const string ThemePath = "Assets/_Project/UI/DefaultTheme.tss";
        const string StylePath = "Assets/_Project/UI/Game.uss";
        const string MaterialPath = "Assets/_Project/Art/ShapeMaterial.mat";

        [MenuItem("Compacted/Setup/Create Game Scene")]
        public static void CreateGameScene()
        {
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder("Assets/_Project/Art");

            // Material for the flat vertex-colored shapes (URP 2D unlit sprite shader uses vertex colors).
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                mat = new Material(shader) { name = "ShapeMaterial" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            // UI Toolkit panel settings with the default runtime theme, scaled for 1600×900.
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.082f, 0.09f, 0.11f);
            camGo.transform.position = new Vector3(0, 0, -10);

            var gameGo = new GameObject("Game");
            var doc = gameGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var controller = gameGo.AddComponent<GameController>();
            controller.database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(EarlyGameContentGenerator.DatabasePath);
            var view = gameGo.AddComponent<WorldView>();
            view.game = controller;
            view.cam = cam;
            view.shapeMaterial = mat;
            controller.view = view;
            var ui = gameGo.AddComponent<GameUI>();
            ui.document = doc;
            ui.game = controller;
            ui.view = view;
            ui.style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            var input = gameGo.AddComponent<GameInput>();
            input.game = controller;
            input.view = view;
            input.ui = ui;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            if (controller.database == null) Debug.LogError("Game scene created, but no ContentDatabase was found. Run Compacted → Content → Generate Early-Game Content.");
            else if (panel.themeStyleSheet == null || ui.style == null) Debug.LogError("Game scene created, but the UI theme or stylesheet is missing.");
            else Debug.Log($"Game scene created at {ScenePath} and set as the only build scene.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
