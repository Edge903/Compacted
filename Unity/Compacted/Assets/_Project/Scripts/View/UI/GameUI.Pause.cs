using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Compacted.View
{
    // Pause menu: Resume, Save, Load, Settings, Quit (requirements §8.1, §8.2).
    public sealed partial class GameUI
    {
        VisualElement pauseBox, settingsBox, savesBox, quitConfirm;

        void BuildPause()
        {
            pauseLayer = Div(root, "layer", "pause-dim");
            pauseLayer.style.display = DisplayStyle.None;
            pauseLayer.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            pauseBox = Div(pauseLayer, "box", "pause");
            Text(pauseBox, "Paused", "pause-title");
            Btn(pauseBox, "Resume", () => SetPaused(false));
            Btn(pauseBox, "Save", () =>
            {
                var name = SaveSystem.Save(game);
                Toast(name != null ? $"Saved as {name}" : "Couldn’t save");
                if (savesBox.style.display == DisplayStyle.Flex) RenderSaves();
            });
            Btn(pauseBox, "Load", () => Toggle(savesBox, RenderSaves));
            savesBox = Add(pauseBox, new ScrollView(), "saves");
            savesBox.style.display = DisplayStyle.None;
            Btn(pauseBox, "Settings", () => Toggle(settingsBox, RenderSettings));
            settingsBox = Div(pauseBox, "settings");
            settingsBox.style.display = DisplayStyle.None;
            Btn(pauseBox, "Quit", () => Toggle(quitConfirm, null));
            quitConfirm = Div(pauseBox, "settings");
            quitConfirm.style.display = DisplayStyle.None;
            var q = Text(quitConfirm, "Quit the game? Unsaved progress is lost.", "warn");
            var qa = Div(quitConfirm, "acts");
            Btn(qa, "Cancel", () => quitConfirm.style.display = DisplayStyle.None);
            Btn(qa, "Quit", Quit, "danger");
        }

        static void Toggle(VisualElement el, System.Action render)
        {
            bool show = el.style.display != DisplayStyle.Flex;
            if (show) render?.Invoke();
            el.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetPaused(bool paused)
        {
            game.Paused = paused;
            if (!paused) { var a = Audio; if (a != null) a.SetVolumes(a.CurrentAmbienceSetting, a.CurrentSfxSetting, save: true); }
            pauseLayer.style.display = paused ? DisplayStyle.Flex : DisplayStyle.None;
            if (paused)
            {
                settingsBox.style.display = DisplayStyle.None;
                savesBox.style.display = DisplayStyle.None;
                quitConfirm.style.display = DisplayStyle.None;
            }
        }

        void RenderSaves()
        {
            savesBox.Clear();
            var list = SaveSystem.List();
            if (list.Count == 0) { Text(savesBox, "No saves yet.", "muted"); return; }
            foreach (var s in list)
            {
                var r = Div(savesBox, "save-row");
                var info = Div(r);
                Text(info, s.Name);
                Text(info, s.Time.ToString("yyyy-MM-dd HH:mm"), "muted", "small");
                var path = s.Path;
                Btn(r, "Load", () =>
                {
                    var err = SaveSystem.Load(game, path);
                    if (err != null) { Toast(err); return; }
                    ClosePopup(); CloseBrowser(); CloseConfirm();
                    if (OpenPanel != Panel.None) TogglePanel(OpenPanel);
                    SetPaused(false);
                    Toast("Game loaded");
                });
            }
        }

        GameAudio Audio => game.GetComponent<GameAudio>();

        /// <summary>A 0–100% volume slider. Applies while dragging; saves when released.</summary>
        void VolumeSlider(string label, float value, System.Action<float> apply, System.Action onRelease)
        {
            var row = Div(settingsBox, "row");
            var slider = Add(row, new Slider(label, 0, 100) { value = value * 100 }, "volume");
            var readout = Text(row, Mathf.RoundToInt(value * 100) + "%", "bold", "volume-value");
            slider.RegisterValueChangedCallback(ev =>
            {
                readout.text = Mathf.RoundToInt(ev.newValue) + "%";
                apply(ev.newValue / 100f);
            });
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => onRelease());
        }

        void RenderSettings()
        {
            settingsBox.Clear();
            var audio = Audio;
            if (audio != null)
            {
                VolumeSlider("Ambience", audio.CurrentAmbienceSetting,
                    v => audio.SetVolumes(v, audio.CurrentSfxSetting, save: false),
                    () => audio.SetVolumes(audio.CurrentAmbienceSetting, audio.CurrentSfxSetting, save: true));
                VolumeSlider("Sound effects", audio.CurrentSfxSetting,
                    v => audio.SetVolumes(audio.CurrentAmbienceSetting, v, save: false),
                    () =>
                    {
                        audio.SetVolumes(audio.CurrentAmbienceSetting, audio.CurrentSfxSetting, save: true);
                        audio.Play(audio.craftDone);   // a sample at the new level
                    });
            }
            var free = Add(settingsBox, new Toggle("Free build (debug)") { value = W.FreeBuild });
            free.RegisterValueChangedCallback(ev => { W.FreeBuild = ev.newValue; game.NotifyChanged(); });

            var speeds = new List<string> { "1×", "2×", "4×", "10×" };
            var speedValues = new[] { 1f, 2f, 4f, 10f };
            int si = System.Array.IndexOf(speedValues, game.Speed);
            var speed = Add(settingsBox, new DropdownField("Simulation speed", speeds, si < 0 ? 0 : si));
            speed.RegisterValueChangedCallback(ev => game.Speed = speedValues[speeds.IndexOf(ev.newValue)]);

            var intervals = new List<string> { "Off", "5 min", "10 min", "15 min", "30 min" };
            var minutes = new[] { 0, 5, 10, 15, 30 };
            int ai = System.Array.IndexOf(minutes, SaveSystem.AutosaveMinutes);
            var auto = Add(settingsBox, new DropdownField("Autosave", intervals, ai < 0 ? 2 : ai));
            auto.RegisterValueChangedCallback(ev => SaveSystem.AutosaveMinutes = minutes[intervals.IndexOf(ev.newValue)]);

            var debug = Div(settingsBox, "acts");
            Btn(debug, "Complete all tech (debug)", () => { game.CompleteAllTech(); Toast("All tech completed (debug)"); });
            Btn(debug, "New game", () => { game.NewGame(); ClosePopup(); SetPaused(false); Toast("New game"); });
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
