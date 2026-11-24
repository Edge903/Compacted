using System;
using System.Collections.Generic;
using Compacted.Data;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.View
{
    /// <summary>
    /// Owns the World and everything the player is doing with it right now: current dimension, camera offset,
    /// toolbar, selection, destroy mode, pause and speed. Runs the fixed-step simulation and autosave.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public ContentDatabase database;
        public WorldView view;

        public static readonly string[] DefaultToolbar =
            { "belt", "pipe", "wall_chipper", "compressor", "condenser", "mixer", "furnace", "wall_miner", "assembler" };

        public ContentSet Content { get; private set; }
        public World World { get; private set; }

        // ---- view state (saved with the game) ----
        public int CurrentDim;
        /// <summary>Camera target offset from the current dimension's centre, in tiles. The camera eases toward it.</summary>
        public Vector2 CameraOffset;
        /// <summary>Camera zoom target: 1 = the dimension fills the view (closest), 0.5 = half that size (furthest).</summary>
        public float Zoom = DefaultZoom;
        public const float MinZoom = 0.5f, MaxZoom = 1f, DefaultZoom = 0.5f;

        public void SetZoom(float z) => Zoom = Mathf.Clamp(z, MinZoom, MaxZoom);
        public readonly string[] Toolbar = new string[9];
        public int Slot = -1;

        // ---- transient state ----
        public Dir PreviewDir = Dir.E;
        public bool DestroyMode;
        public bool Paused;
        public float Speed = 1;
        public Cell? Hover;
        public RectInt? DragRect;
        public int SelectedEntity;   // entity whose info popup is open (0 = none)

        /// <summary>Raised when toolbar, selection, mode, dimension or world identity changes (not every tick).</summary>
        public event Action Changed;
        public void NotifyChanged() => Changed?.Invoke();

        double accumulator;
        float autosaveTimer;

        void Awake()
        {
            Content = database.Build();
            var errors = database.Validate();
            if (errors.Count > 0) Debug.LogError("Content problems:\n• " + string.Join("\n• ", errors), database);
            NewGame();
        }

        public void NewGame()
        {
            World = new World(Content);
            CurrentDim = World.RootId;
            CameraOffset = Vector2.zero;
            Zoom = DefaultZoom;
            if (view != null) view.SnapCamera();
            for (int i = 0; i < Toolbar.Length; i++) Toolbar[i] = i < DefaultToolbar.Length && Content.Buildings.ContainsKey(DefaultToolbar[i]) ? DefaultToolbar[i] : null;
            Slot = -1; DestroyMode = false; SelectedEntity = 0; Paused = false;
            autosaveTimer = 0;
            NotifyChanged();
        }

        void Update()
        {
            if (!Paused)
            {
                accumulator += Time.deltaTime * Speed;
                int steps = 0;
                while (accumulator >= World.Dt && steps < 400) { World.Step(); accumulator -= World.Dt; steps++; }
                if (steps >= 400) accumulator = 0;   // don't spiral if the frame rate collapses
            }
            int minutes = SaveSystem.AutosaveMinutes;
            if (minutes > 0)
            {
                autosaveTimer += Time.unscaledDeltaTime;
                if (autosaveTimer >= minutes * 60f) { autosaveTimer = 0; SaveSystem.Save(this, SaveSystem.AutosaveName); }
            }
        }

        // ---------- selection & modes ----------
        public string SelectedType => Slot >= 0 ? Toolbar[Slot] : null;
        public BuildingDef SelectedDef => SelectedType != null && Content.Buildings.TryGetValue(SelectedType, out var d) ? d : null;

        public void SelectSlot(int i) { Slot = i; DestroyMode = false; DragRect = null; NotifyChanged(); }
        public void Deselect() { Slot = -1; NotifyChanged(); }
        public void SetDestroyMode(bool on) { DestroyMode = on; DragRect = null; if (on) { Slot = -1; SelectedEntity = 0; } NotifyChanged(); }
        public void AssignSlot(int i, string type) { Toolbar[i] = type; NotifyChanged(); }
        public void Rotate(int quarterTurns) => PreviewDir = PreviewDir.Rotate(quarterTurns);

        // ---------- navigation ----------
        public void Enter(ControllerEntity c) => GoToDim(c.Inner, zoomIn: true);

        public void Leave()
        {
            int p = World.ParentDimOf(CurrentDim);
            if (p >= 0) GoToDim(p, zoomIn: false);
        }

        public void GoToDim(int dimId, bool zoomIn)
        {
            if (dimId == CurrentDim || !World.Dims.ContainsKey(dimId)) return;
            CurrentDim = dimId;
            CameraOffset = Vector2.zero;
            SelectedEntity = 0;
            DragRect = null;
            if (view != null) { view.StartTransition(zoomIn); view.SnapCamera(); }
            NotifyChanged();
        }

        /// <summary>Root first, current dimension last.</summary>
        public List<int> DimPath()
        {
            var path = new List<int>();
            for (int d = CurrentDim; d >= 0; d = World.ParentDimOf(d)) path.Insert(0, d);
            return path;
        }

        public string DimName(int dimId)
        {
            var dim = World.Dims[dimId];
            return dim.IsRoot ? "Root" : World.Get<ControllerEntity>(dim.ControllerId).Name;
        }

        // ---------- save / load ----------
        public void LoadWorld(World w, int dim, Vector2 camOffset, string[] toolbar, float zoom = DefaultZoom)
        {
            World = w;
            CurrentDim = w.Dims.ContainsKey(dim) ? dim : w.RootId;
            CameraOffset = camOffset;
            SetZoom(zoom);
            if (view != null) view.SnapCamera();
            if (toolbar != null) for (int i = 0; i < Toolbar.Length; i++) Toolbar[i] = i < toolbar.Length && toolbar[i] != null && Content.Buildings.ContainsKey(toolbar[i]) ? toolbar[i] : null;
            Slot = -1; DestroyMode = false; SelectedEntity = 0; DragRect = null;
            accumulator = 0;
            NotifyChanged();
        }

        /// <summary>Debug: complete every tech node, in prerequisite order.</summary>
        public void CompleteAllTech()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var id in Content.TechOrder)
                    if (World.StateOf(id) == TechState.Open) { World.CompleteTech(id, force: true); changed = true; }
            }
            NotifyChanged();
        }
    }
}
