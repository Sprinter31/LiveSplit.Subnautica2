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
using Voxif.Helpers.Unreal;
using Voxif.IO;
using Voxif.Memory;

namespace LiveSplit.Subnautica2
{
    public class Subnautica2Memory : Memory
    {
        protected override string[] ProcessNames => new string[] { "Subnautica2-Win64-Shipping" };

        public Subnautica2Split CurrentSplitToCheck { get; set; }

        public bool startedTimerBefore = false;
        public bool isInMainMenu = false;
        private const int maxInventoryTimeWithoutChangingMs = 1000;
        public bool pointersInitialized;
        public GameVersion gameVersion;

        public readonly Dictionary<SplitName, Func<bool>> splitConditions;
        public readonly Dictionary<SplitName, Func<bool>> subConditions;

        private readonly Subnautica2Settings settings;

        #region Pointer stuff
        private UnrealNestedPointerFactory pointerFactory;
        private Pointer<float> gameDurationSeconds;
        private Pointer<float> sessionDurationSeconds;
        private Pointer<float> oxygenCurrentValue;
        private Pointer<float> oxygenMaxValue;
        private DateTime nextGameplayTimeProbeLog = DateTime.MinValue;

        #endregion

        UnrealHelperTask unrealTask;

        public Subnautica2Memory(LiveSplitState state, Subnautica2Component component, Logger logger, Subnautica2Settings settings) : base(logger)
        {            
            OnHook += () =>
            {
                GetGameVersion();
                unrealTask = new UnrealHelperTask(game, logger);
                unrealTask.Run(Version.Parse("5.0.0"), InitPointers);
            };

            OnExit += () => {
                if (unrealTask != null)
                {
                    pointersInitialized = false;
                    pointerFactory = null;
                    gameDurationSeconds = null;
                    sessionDurationSeconds = null;
                    oxygenCurrentValue = null;
                    oxygenMaxValue = null;
                    unrealTask.Dispose();
                    unrealTask = null;
                }
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
                case 232562688:
                    gameVersion = GameVersion.v113109;
                    break;
                default:
                    gameVersion = GameVersion.v113109;
                    MessageBox.Show($"Module length {moduleLen} does not match a version, defaulting to most recent (113109)",
                                    "Subnautica2 Autosplitter",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    break;
            }
        }

        private void InitPointers(IUnrealHelper unrealHelper)
        {
            logger.Log("Unreal helper initialized; running diagnostics");
            unrealHelper.LogDiagnostics();
            logger.Log("Unreal diagnostics finished");

            pointerFactory = new UnrealNestedPointerFactory(game, unrealHelper);
            InitGameplayTimeProbe(unrealHelper);
            InitOxygenProbe(unrealHelper);

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
            if (oxygenCurrentValue != null)
            {
                try
                {
                    float oxygen = oxygenCurrentValue.New;
                    float maxOxygen = oxygenMaxValue?.New ?? 0f;
                    logger.Log($"Oxygen tick: CurrentValue={oxygen:F1}, MaxValue={maxOxygen:F1}");
                }
                catch (Exception ex)
                {
                    logger.Log($"Oxygen probe failed: {ex.Message}");
                    oxygenCurrentValue = null;
                    oxygenMaxValue = null;
                }
            }

            if (gameDurationSeconds == null)
                return;

            try
            {
                float gameDuration = gameDurationSeconds.New;
                float sessionDuration = sessionDurationSeconds?.New ?? 0f;

                if (DateTime.Now >= nextGameplayTimeProbeLog)
                {
                    logger.Log($"Gameplay time probe: GameDurationSeconds={gameDuration:F1}, SessionDurationSeconds={sessionDuration:F1}");
                    nextGameplayTimeProbeLog = DateTime.Now.AddSeconds(5);
                }
            }
            catch (Exception ex)
            {
                logger.Log($"Gameplay time probe failed: {ex.Message}");
                gameDurationSeconds = null;
                sessionDurationSeconds = null;
            }
        }

        private void InitGameplayTimeProbe(IUnrealHelper unrealHelper)
        {
            try
            {
                gameDurationSeconds = pointerFactory.Make<float>("UWEGameplayTimeComponent", "GameDurationSeconds");
                sessionDurationSeconds = pointerFactory.Make<float>("UWEGameplayTimeComponent", "SessionDurationSeconds");
                logger.Log($"Gameplay time probe initialized through Unreal pointer factory: GameDurationSeconds={gameDurationSeconds.New:F1}, SessionDurationSeconds={sessionDurationSeconds.New:F1}");
            }
            catch (Exception ex)
            {
                logger.Log($"Gameplay time probe not initialized: {ex.Message}");
            }
        }

        private void InitOxygenProbe(IUnrealHelper unrealHelper)
        {
            try
            {
                oxygenCurrentValue = pointerFactory.Make<float>("SN2PlayerOxygenViewModel", "CurrentValue");
                oxygenMaxValue = pointerFactory.Make<float>("SN2PlayerOxygenViewModel", "MaxValue");
                logger.Log($"Oxygen probe initialized through Unreal pointer factory: CurrentValue={oxygenCurrentValue.New:F1}, MaxValue={oxygenMaxValue.New:F1}");
            }
            catch (Exception ex)
            {
                logger.Log($"Oxygen probe not initialized: {ex.Message}");
            }
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
            return false;
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
