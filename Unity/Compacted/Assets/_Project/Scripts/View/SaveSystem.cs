using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Compacted.Sim;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Compacted.View
{
    /// <summary>One save file: format version, the world, and the view state to restore.</summary>
    public sealed class SaveFile
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public string SavedAt;
        public WorldSave World;
        public int CurrentDim;
        public float CameraX, CameraY;
        public string[] Toolbar;
    }

    public readonly struct SaveEntry
    {
        public readonly string Name;
        public readonly string Path;
        public readonly DateTime Time;
        public SaveEntry(string name, string path, DateTime time) { Name = name; Path = path; Time = time; }
    }

    /// <summary>Save files under persistentDataPath/Saves (requirements §8.2). Many saves per world; interval autosave.</summary>
    public static class SaveSystem
    {
        public const string AutosaveName = "autosave";
        const string AutosaveKey = "compacted.autosaveMinutes";

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter() },
        };

        public static string Folder => System.IO.Path.Combine(Application.persistentDataPath, "Saves");

        /// <summary>Autosave interval in minutes; 0 = off. Default 10. Stored per machine.</summary>
        public static int AutosaveMinutes
        {
            get => PlayerPrefs.GetInt(AutosaveKey, 10);
            set { PlayerPrefs.SetInt(AutosaveKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Write a save. name = null makes a new timestamped file. Returns the file name, or null on failure.</summary>
        public static string Save(GameController g, string name = null)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                name ??= "save-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var file = new SaveFile
                {
                    SavedAt = DateTime.Now.ToString("o"),
                    World = g.World.ToSave(),
                    CurrentDim = g.CurrentDim,
                    CameraX = g.CameraOffset.x,
                    CameraY = g.CameraOffset.y,
                    Toolbar = (string[])g.Toolbar.Clone(),
                };
                File.WriteAllText(System.IO.Path.Combine(Folder, name + ".json"), JsonConvert.SerializeObject(file, Settings));
                return name;
            }
            catch (Exception e)
            {
                Debug.LogError("Couldn't save: " + e.Message);
                return null;
            }
        }

        /// <summary>Load a save into the controller. Returns an error message, or null on success.</summary>
        public static string Load(GameController g, string path)
        {
            try
            {
                var file = JsonConvert.DeserializeObject<SaveFile>(File.ReadAllText(path), Settings);
                if (file == null) return "The save file is empty.";
                if (file.Version != SaveFile.CurrentVersion) return $"Save format version {file.Version} isn't supported.";
                var world = Sim.World.FromSave(g.Content, file.World);
                g.LoadWorld(world, file.CurrentDim, new Vector2(file.CameraX, file.CameraY), file.Toolbar);
                return null;
            }
            catch (Exception e)
            {
                return "Couldn't load the save: " + e.Message;
            }
        }

        /// <summary>Saves, newest first.</summary>
        public static List<SaveEntry> List()
        {
            if (!Directory.Exists(Folder)) return new List<SaveEntry>();
            return Directory.GetFiles(Folder, "*.json")
                .Select(p => new SaveEntry(System.IO.Path.GetFileNameWithoutExtension(p), p, File.GetLastWriteTime(p)))
                .OrderByDescending(e => e.Time).ToList();
        }
    }
}
