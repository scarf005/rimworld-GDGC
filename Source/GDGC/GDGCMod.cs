using UnityEngine;
using Verse;

namespace GDGC {
    public sealed class GDGCMod : Mod {
        internal static GDGCSettings CurrentSettings { get; private set; }

        private GDGCSettings settings;

        public GDGCMod(ModContentPack content) : base(content) {
            settings = GetSettings<GDGCSettings>();
            CurrentSettings = settings;
        }

        public override string SettingsCategory() {
            return "GDGC_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect) {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "GDGC_DebugLogging".Translate(),
                ref settings.debugLogging,
                "GDGC_DebugLoggingTip".Translate());
            listing.End();
        }
    }

    public sealed class GDGCSettings : ModSettings {
        public bool debugLogging;

        public override void ExposeData() {
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
        }
    }

    internal static class GDGCLog {
        private static bool Enabled => GDGCMod.CurrentSettings != null && GDGCMod.CurrentSettings.debugLogging;

        internal static void Message(string message) {
            if (Enabled) {
                Log.Message(message);
            }
        }

        internal static void Warning(string message) {
            if (Enabled) {
                Log.Warning(message);
            }
        }

        internal static void Error(string message) {
            if (Enabled) {
                Log.Error(message);
            }
        }
    }
}
