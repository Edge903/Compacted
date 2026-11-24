using System.Linq;
using Compacted.Data;
using UnityEditor;
using UnityEngine;

namespace Compacted.Editor
{
    public static class ContentMenu
    {
        /// <summary>Validate every ContentDatabase in the project and log the result.</summary>
        [MenuItem("Compacted/Content/Validate Content")]
        public static void ValidateAll()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ContentDatabase));
            if (guids.Length == 0) { Debug.LogWarning("No ContentDatabase asset found."); return; }
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(path);
                var errors = db.Validate();
                if (errors.Count == 0) Debug.Log($"Content is valid: {path}", db);
                else Debug.LogError($"{errors.Count} content problem(s) in {path}:\n• " + string.Join("\n• ", errors), db);
            }
        }

        /// <summary>Add every definition asset under Assets/_Project/Content that the database is missing.</summary>
        [MenuItem("Compacted/Content/Collect Definitions Into Database")]
        public static void Collect()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(EarlyGameContentGenerator.DatabasePath);
            if (db == null) { Debug.LogWarning($"No database at {EarlyGameContentGenerator.DatabasePath}."); return; }
            int added = 0;
            added += AddMissing(db.items);
            added += AddMissing(db.fluids);
            added += AddMissing(db.buildings);
            added += AddMissing(db.recipes);
            added += AddMissing(db.multiblocks);
            added += AddMissing(db.tech);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"Added {added} definition(s) to the database.", db);
        }

        static int AddMissing<T>(System.Collections.Generic.List<T> list) where T : Definition
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { EarlyGameContentGenerator.Root }))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null && !list.Contains(a)) { list.Add(a); n++; }
            }
            list.RemoveAll(x => x == null);
            return n;
        }
    }
}
