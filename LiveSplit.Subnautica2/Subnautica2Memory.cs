using LiveSplit.ComponentUtil;
using LiveSplit.Model;
using LiveSplit.Subnautica2.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Voxif.AutoSplitter;
using Voxif.Helpers.Unity;
using Voxif.IO;
using Voxif.Memory;

namespace LiveSplit.Subnautica2
{
    public class Subnautica2Memory : Memory
    {
        protected override string[] ProcessNames => new string[] { "Subnautica2" };

        public Subnautica2Split CurrentSplitToCheck { get; set; }

        public bool startedTimerBefore = false;
        public bool isInMainMenu = false;
        private readonly Stopwatch _duringLoad = new Stopwatch();
        private readonly Stopwatch _afterLoad = new Stopwatch();
        private int prePortalDelayMs = 0;
        private int postPortalDelayMs = 0;
        private const int maxInventoryTimeWithoutChangingMs = 1000;
        public bool pointersInitialized;
        public GameVersion gameVersion;

        public readonly Dictionary<SplitName, Func<bool>> splitConditions;
        public readonly Dictionary<SplitName, Func<bool>> subConditions;

        private readonly Subnautica2Settings settings;

        #region Pointer stuff
        
        #endregion

        public Subnautica2Memory(LiveSplitState state, Subnautica2Component component, Logger logger, Subnautica2Settings settings) : base(logger)
        {            
            OnHook += () =>
            {
                GetGameVersion();
            };

            OnExit += () => {
                
            };

            this.settings = settings;

            subConditions = new Dictionary<SplitName, Func<bool>>
            {
                
            };

            splitConditions = new Dictionary<SplitName, Func<bool>>
            {
                
            };
        }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            if (!pointersInitialized || game == null)
                return false;

            UpdateMemoryWatchers();

            isInMainMenu = IsInMainMenu();
            if (isInMainMenu)
                startedTimerBefore = false;

            return true;
        }

        #region Memory stuff
        private void GetGameVersion()
        {
            System.Diagnostics.ProcessModule firstModule = game.Process.Modules.Cast<System.Diagnostics.ProcessModule>().FirstOrDefault();
            if (firstModule == null) return;
            int moduleLen = firstModule.ModuleMemorySize;
            switch (moduleLen)
            {
                default:
                    gameVersion = GameVersion.113109;
                    MessageBox.Show($"Module length {moduleLen} does not match a version, defaulting to most recent (113109)",
                                    "Subnautica2 Autosplitter",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    break;
            }
        }

        private void InitPointers()
        {
            #region Memory Watchers
            switch (gameVersion)
            {
                default: // GameVersion.113109
                    break;
            }

            #endregion Memory Watchers 

            logger.Log("Pointers initialized");
            pointersInitialized = true;
        }

        private void UpdateMemoryWatchers()
        {
            
        }

        private bool Needs(params SplitName[] required)
        {
            if (settings?.Splits == null || settings.Splits.Count == 0)
                return false;

            var usedSplitNames = new HashSet<SplitName>();

            foreach (var split in settings.Splits)
            {
                usedSplitNames.Add(split.SplitName);

                foreach (var conditionSplit in Subnautica2Component.GetAllConditions(split))
                    usedSplitNames.Add(conditionSplit.SplitName);
            }
            return required.Any(usedSplitNames.Contains);
        }
        #endregion Memory stuff
        #region World/Player Checks
        public bool IsInMainMenu() => false;

        private bool IsWithinBounds(float[] bounds, bool old = false)
        {
            /*float x = old ? posX.Old : posX.Current;
            float y = old ? posY.Old : posY.Current;
            float z = old ? posZ.Old : posZ.Current;
            if (x >= Math.Min(bounds[0], bounds[1]) && x <= Math.Max(bounds[0], bounds[1]) &&
                y >= Math.Min(bounds[2], bounds[3]) && y <= Math.Max(bounds[2], bounds[3]) &&
                z >= Math.Min(bounds[4], bounds[5]) && z <= Math.Max(bounds[4], bounds[5]))
                return true;
            else
                return false;*/
        }

        public bool ShouldPause()
        {
            return false;
        }
        #endregion
        #region Bounds
        // xmin, xmax, ymin, ymax, zmin, zmax
        private readonly float[] exampleBounds = { -212f, 27f, -100f, 100f, 159f, 177f };
        #endregion
    }

    public class InvChangeInfo
    {
        public int Count { get; set; }
        public Stopwatch ElapsedTime { get; }

        public InvChangeInfo(int count, Stopwatch elapsedTime)
        {
            Count = count;
            ElapsedTime = elapsedTime ?? throw new ArgumentNullException(nameof(elapsedTime));
        }
    }
}
