using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace GDGC {
    internal static class PrisonerManagementPanelCompat {
        private const string MarkerDefName = "GDGC_MugbGoblinPolicyRace";
        private const string RaceUtilsTypeName = "PrisonerManagementPanel.Utils.RaceUtils";
        private const string StorageTypeName = "PrisonerManagementPanel.Surgery.PawnSurgeryPolicyStorage";

        private static readonly FieldInfo ThingDefField = AccessTools.Field(typeof(Thing), nameof(Thing.def));
        private static readonly MethodInfo PawnRaceMatchMethod =
            AccessTools.Method(typeof(PrisonerManagementPanelCompat), nameof(IsPawnRaceMatch));

        private static MethodInfo prisonerManagementPanelRaceMatchMethod;

        internal static void Apply(Harmony harmony) {
            var raceUtilsType = AccessTools.TypeByName(RaceUtilsTypeName);
            var storageType = AccessTools.TypeByName(StorageTypeName);
            if (raceUtilsType == null || storageType == null) {
                return;
            }

            var getAllRacesMethod = AccessTools.Method(raceUtilsType, "GetAllRaces");
            prisonerManagementPanelRaceMatchMethod = AccessTools.Method(raceUtilsType, "IsRaceMatch");
            var setPolicyMethod = AccessTools.Method(storageType, "SetPolicyForPawn");
            var updatePawnsMethod = AccessTools.Method(storageType, "UpdatePawnsWithPolicy");
            var transpilerMethod =
                AccessTools.Method(typeof(PrisonerManagementPanelCompat), nameof(RaceCheckTranspiler));
            var racesPostfixMethod =
                AccessTools.Method(typeof(PrisonerManagementPanelCompat), nameof(GetAllRacesPostfix));

            if (getAllRacesMethod == null || prisonerManagementPanelRaceMatchMethod == null ||
                setPolicyMethod == null || updatePawnsMethod == null) {
                GDGCLog.Warning("[GDGC] Prisoner Management Panel was found, but its race-selection API is incompatible.");
                return;
            }

            try {
                var transpiler = new HarmonyMethod(transpilerMethod);
                harmony.Patch(setPolicyMethod, transpiler: transpiler);
                harmony.Patch(updatePawnsMethod, transpiler: transpiler);
                harmony.Patch(getAllRacesMethod, postfix: new HarmonyMethod(racesPostfixMethod));
                GDGCLog.Message("[GDGC] Prisoner Management Panel compatibility loaded for MUGB goblins and hobgoblins.");
            } catch (Exception exception) {
                GDGCLog.Error($"[GDGC] Failed to patch Prisoner Management Panel: {exception}");
            }
        }

        private static IEnumerable<CodeInstruction> RaceCheckTranspiler(IEnumerable<CodeInstruction> instructions,
                                                                        MethodBase __originalMethod) {
            var patchedInstructions = new List<CodeInstruction>(instructions);
            var replacementCount = 0;

            for (var index = 0; index < patchedInstructions.Count; index++) {
                if (!patchedInstructions[index].Calls(prisonerManagementPanelRaceMatchMethod)) {
                    continue;
                }

                for (var argumentIndex = index - 1; argumentIndex >= Math.Max(0, index - 8); argumentIndex--) {
                    var instruction = patchedInstructions[argumentIndex];
                    if (instruction.opcode != OpCodes.Ldfld || !Equals(instruction.operand, ThingDefField)) {
                        continue;
                    }

                    instruction.opcode = OpCodes.Nop;
                    instruction.operand = null;
                    patchedInstructions[index].operand = PawnRaceMatchMethod;
                    replacementCount++;
                    break;
                }
            }

            if (replacementCount != 1) {
                throw new InvalidOperationException(
                    $"Expected one prisoner race check in {__originalMethod.Name}, found {replacementCount}.");
            }

            return patchedInstructions;
        }

        private static bool IsPawnRaceMatch(Pawn pawn, ThingDef selectedRace) {
            var marker = DefDatabase<ThingDef>.GetNamedSilentFail(MarkerDefName);
            return selectedRace == marker ? GoblinExemption.IsMugbGoblin(pawn)
                                          : pawn != null && pawn.def == selectedRace;
        }

        private static void GetAllRacesPostfix(ref IEnumerable<ThingDef> __result) {
            var marker = DefDatabase<ThingDef>.GetNamedSilentFail(MarkerDefName);
            if (marker != null && __result != null) {
                __result = AppendMarker(__result, marker);
            }
        }

        private static IEnumerable<ThingDef> AppendMarker(IEnumerable<ThingDef> races, ThingDef marker) {
            var containsMarker = false;
            foreach (var race in races) {
                containsMarker |= race == marker;
                yield return race;
            }

            if (!containsMarker) {
                yield return marker;
            }
        }
    }
}
