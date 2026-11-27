using System.Linq;
using Compacted.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Compacted.Editor
{
    /// <summary>Adds GameAudio to the Game scene and assigns the clips from Assets/_Project/Audio by file name.</summary>
    public static class GameAudioSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        [MenuItem("Compacted/Setup/Add Audio To Game Scene")]
        public static void AddToGameScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<GameController>();
            if (game == null) { Debug.LogError("No GameController in the Game scene. Run Compacted → Setup → Create Game Scene first."); return; }
            Configure(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Add or update the GameAudio component on the controller's object and assign clips.</summary>
        public static void Configure(GameController game)
        {
            // Unity only outputs sound through an AudioListener; it lives on the camera.
            var cam = game.view != null ? game.view.cam : Camera.main;
            if (cam != null && cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            var audio = game.GetComponent<GameAudio>() ?? game.gameObject.AddComponent<GameAudio>();
            audio.game = game;
            audio.ambience = Clips("Assets/_Project/Audio/Ambience");
            var sfx = Clips("Assets/_Project/Audio/SFX");
            AudioClip Find(string part) => sfx.FirstOrDefault(c => c.name.Contains(part));
            audio.techComplete = Find("positive-blip");
            audio.manualPickup = Find("radar");
            audio.placeBlock = Find("short-ping");
            audio.craftDone = Find("bliq");
            EditorUtility.SetDirty(audio);
            var missing = new[] { ("tech complete", audio.techComplete), ("manual pickup", audio.manualPickup), ("place block", audio.placeBlock), ("craft done", audio.craftDone) }
                .Where(p => p.Item2 == null).Select(p => p.Item1).ToList();
            Debug.Log($"GameAudio set up: {audio.ambience.Length} ambience track(s)" + (missing.Count > 0 ? $"; no clip found for: {string.Join(", ", missing)}" : "; all sound effects assigned."));
        }

        static AudioClip[] Clips(string folder) =>
            AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).OrderBy(c => c.name).ToArray();
    }
}
