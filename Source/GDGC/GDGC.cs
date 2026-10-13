using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GDGC;

[StaticConstructorOnStartup]
public static class Bootstrap {
    static Bootstrap() {
        var harmony = new Harmony("local.goblinsdontdeservegenevaconvention");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        DynamicVictimContextPatches.Apply(harmony);
        GoblinPheromonePreferenceCompat.Apply(harmony);
        Wce2Compat.Apply(harmony);
        PrisonerManagementPanelCompat.Apply(harmony);
        GDGCLog.Message("[GDGC] Goblin-exception logic loaded. The exemption requires the GDGC meme and an MUGB goblin target.");
    }
}

internal static class GoblinExemption {
    internal const string MemeDefName = "GDGC_GoblinExceptionalism";

    private static readonly HashSet<string> GoblinXenotypes = ["MUGB_Goblin", "MUGB_Hobgoblin"];

    private static readonly HashSet<string> GoblinCoreGenes = ["MUGB_Gene_GoblinCore", "MUGB_Gene_HobgoblinFrame"];

    private static readonly HashSet<string> GoblinFoodThoughts = [
        "MUGB_AteGoblinMeatDirect",
        "MUGB_AteGoblinMeatDirectCannibal",
        "MUGB_AteGoblinMeatAsIngredient",
        "MUGB_AteGoblinMeatAsIngredientCannibal",
    ];

    private static readonly HashSet<string> AlienExaltedThoughts = ["HAR_AlienRaces_Exalted", "HAR_AlienRaces_Exalted_Social"];

    private static readonly HashSet<string> ExplicitMoralThoughts = [
        "MUGB_PerformedLiveButchery",
        "ButcheredHumanlikeCorpse",
        "KnowButcheredHumanlikeCorpse",
        "KnowColonistOrganHarvested",
        "KnowGuestOrganHarvested",
        "KnowPrisonerOrganHarvested",
        "KnowColonistExecuted",
        "KnowPrisonerExecuted",
        "KnowPrisonerDiedInnocent",
        "PrisonerBanishedToDie",
        "SoldSlave_Know_Abhorrent_Mood",
        "SoldSlave_Horrible",
        "SoldSlave_Know_Horrible_Mood",
        "SoldSlave_Disapproved",
        "SoldSlave_Know_Disapproved_Mood",
        "ColonistOrganHarvested",
        "PrisonerOrganHarvested",
    ];

    private static readonly string[] MoralNameFragments = [
        "OrganHarvest",
        "HarvestedOrgan",
        "Butcher",
        "Cannibal",
        "HumanMeat",
        "HumanlikeMeat",
        "Executed",
        "Execution",
        "InnocentPrisoner",
        "PrisonerDied",
        "PrisonerDeath",
        // Charity-refusal memories are raised while Pawn.Kill still supplies the goblin context.
        "CharityRefused",
        "Tortur",
        "Vivisect",
        "Mutilat",
        "LiveButcher",
        "SoldSlave",
    ];

    internal static bool IsMugbGoblin(Pawn? pawn) =>
        pawn?.genes is { } genes
        && (genes.Xenotype is { } xenotype && GoblinXenotypes.Contains(xenotype.defName)
            || genes.GenesListForReading.Any(gene => gene is { Active: true, def: { } def } && GoblinCoreGenes.Contains(def.defName)));

    internal static bool HasGoblinExceptionalism(Pawn? pawn) =>
        pawn?.Ideo is { } ideo
        && DefDatabase<MemeDef>.GetNamedSilentFail(MemeDefName) is { } meme
        && ideo.HasMeme(meme);

    internal static bool TreatsGoblinAsGuilty(Pawn? receiver, Pawn? victim) =>
        HasGoblinExceptionalism(receiver) && IsMugbGoblin(victim) && receiver != victim;

    internal static bool ShouldSuppress(Pawn? receiver, ThoughtDef? thoughtDef, Pawn? victim) {
        if (receiver is null || thoughtDef is null || !HasGoblinExceptionalism(receiver)) {
            return false;
        }

        // MUGB's four goblin-meat thoughts already encode that the consumed ingredient was goblin meat.
        if (GoblinFoodThoughts.Contains(thoughtDef.defName)) {
            return IsNegativeMoodThought(thoughtDef);
        }

        // Every MUGB goblin/hobgoblin is ideologically guilty to followers of this meme.
        // Keep this observer-relative instead of changing RimWorld's global guilt tracker.
        return TreatsGoblinAsGuilty(receiver, victim)
            && IsNegativeMoodThought(thoughtDef)
            && (ExplicitMoralThoughts.Contains(thoughtDef.defName)
                || MoralNameFragments.Any(fragment => thoughtDef.defName.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    internal static bool ShouldSuppressAlienExalted(Pawn? receiver, ThoughtDef? thoughtDef) =>
        IsAlienExaltedFor(receiver, thoughtDef) && IsMugbGoblin(receiver);

    internal static bool ShouldSuppressAlienExaltedSocial(Pawn? receiver, ThoughtDef? thoughtDef, Pawn? otherPawn) =>
        IsAlienExaltedFor(receiver, thoughtDef) && IsMugbGoblin(otherPawn);

    private static bool IsAlienExaltedFor(Pawn? receiver, ThoughtDef? thoughtDef) =>
        thoughtDef is not null && AlienExaltedThoughts.Contains(thoughtDef.defName) && HasGoblinExceptionalism(receiver);

    private static bool IsNegativeMoodThought(ThoughtDef thoughtDef) =>
        thoughtDef.stages?.Any(stage => stage is { baseMoodEffect: < 0f }) == true;
}

[HarmonyPatch]
internal static class AlienExaltedThought_Patch {
    [HarmonyPatch(typeof(ThoughtWorker), nameof(ThoughtWorker.CurrentState))]
    [HarmonyPostfix]
    private static void CurrentStatePostfix(ThoughtWorker __instance, Pawn p, ref ThoughtState __result) {
        if (GoblinExemption.ShouldSuppressAlienExalted(p, __instance.def)) {
            __result = ThoughtState.Inactive;
        }
    }

    [HarmonyPatch(typeof(ThoughtWorker), nameof(ThoughtWorker.CurrentSocialState))]
    [HarmonyPostfix]
    private static void CurrentSocialStatePostfix(ThoughtWorker __instance, Pawn p, Pawn otherPawn, ref ThoughtState __result) {
        if (GoblinExemption.ShouldSuppressAlienExaltedSocial(p, __instance.def, otherPawn)) {
            __result = ThoughtState.Inactive;
        }
    }
}

// MUGB goblins are Biotech xenotypes on the vanilla Human ThingDef, so a normal
// ThingDef/category filter cannot distinguish their corpses from other human corpses.
// These workers expose the distinction as SpecialThingFilterDefs in the bill UI.
public sealed class SpecialThingFilterWorker_MugbGoblinCorpse : SpecialThingFilterWorker {
    public override bool Matches(Thing thing) => thing is Corpse corpse && GoblinExemption.IsMugbGoblin(corpse.InnerPawn);
}

public sealed class SpecialThingFilterWorker_OtherHumanlikeCorpse : SpecialThingFilterWorker {
    public override bool Matches(Thing thing) =>
        thing is Corpse { InnerPawn: { RaceProps.Humanlike: true } pawn } && !GoblinExemption.IsMugbGoblin(pawn);
}

internal static class VictimContext {
    [ThreadStatic]
    private static Stack<Pawn>? victims;

    internal static Pawn? Current => victims is { Count: > 0 } stack ? stack.Peek() : null;

    internal static Pawn? PushIfGoblin(Pawn? pawn) {
        if (pawn is null || !GoblinExemption.IsMugbGoblin(pawn)) {
            return null;
        }

        (victims ??= []).Push(pawn);
        return pawn;
    }

    internal static void Pop(Pawn? state) {
        if (state is not null && victims is { Count: > 0 } stack) {
            stack.Pop();
        }
    }
}

[HarmonyPatch]
internal static class MemoryThoughtHandler_TryGainMemory_Patch {
    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.GetDeclaredMethods(typeof(MemoryThoughtHandler)).Where(method => method.Name == nameof(MemoryThoughtHandler.TryGainMemory));

    private static bool Prefix(MemoryThoughtHandler __instance, object?[] __args) {
        var receiver = __instance.pawn;
        ThoughtDef? thoughtDef = null;
        Pawn? victim = null;

        foreach (var arg in __args) {
            switch (arg) {
                case ThoughtDef def:
                    thoughtDef = def;
                    break;
                case Thought_Memory memory:
                    thoughtDef = memory.def;
                    break;
                case Pawn pawn when pawn != receiver:
                    victim = pawn;
                    break;
            }
        }

        // During death/butchery/organ-harvest processing, the action context identifies the
        // actual victim. Prefer it over TryGainMemory's optional "other pawn" argument, which
        // can instead be the butcher, executioner, or another participant.
        return !GoblinExemption.ShouldSuppress(receiver, thoughtDef, VictimContext.Current ?? victim);
    }
}

// Corpse.ButcherProducts is an iterator. A normal prefix/postfix only surrounds iterator creation,
// while humanlike-butchery memories are generated during enumeration. Keep the goblin victim
// context active until the enumeration is disposed.
[HarmonyPatch]
internal static class Corpse_ButcherProducts_VictimContext_Patch {
    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.GetDeclaredMethods(typeof(Corpse))
            .Where(method => method.Name == nameof(Corpse.ButcherProducts) && typeof(IEnumerable<Thing>).IsAssignableFrom(method.ReturnType));

    private static void Postfix(Corpse? __instance, ref IEnumerable<Thing>? __result) {
        var victim = __instance?.InnerPawn;
        if (__result is not null && GoblinExemption.IsMugbGoblin(victim)) {
            __result = EnumerateWithVictimContext(__result, victim);
        }
    }

    private static IEnumerable<Thing> EnumerateWithVictimContext(IEnumerable<Thing> source, Pawn? victim) {
        var state = VictimContext.PushIfGoblin(victim);
        try {
            foreach (var thing in source) {
                yield return thing;
            }
        } finally {
            VictimContext.Pop(state);
        }
    }
}

internal static class DynamicVictimContextPatches {
    private static readonly HarmonyMethod PrefixMethod = new(typeof(DynamicVictimContextPatches), nameof(ContextPrefix));
    private static readonly HarmonyMethod PostfixMethod = new(typeof(DynamicVictimContextPatches), nameof(ContextPostfix));
    private static readonly HarmonyMethod FinalizerMethod = new(typeof(DynamicVictimContextPatches), nameof(ContextFinalizer));
    private static readonly HarmonyMethod PreserveHumanlikeThoughtsPrefixMethod =
        new(typeof(DynamicVictimContextPatches), nameof(PreserveHumanlikeThoughtsPrefix));

    internal static void Apply(Harmony harmony) {
        HashSet<MethodBase> patched = [];

        // Innocent-prisoner/responsibility memories are generated in the death path.
        PatchNamedMethod(harmony, patched, typeof(Pawn), nameof(Pawn.Kill));
        PatchNamedMethod(harmony, patched, typeof(ThoughtUtility), nameof(ThoughtUtility.GiveThoughtsForPawnOrganHarvested));

        // Selling a pawn records SoldSlave after GiveSoldThingToTrader returns, so the
        // pawn cannot be recovered from the history event itself. Keep the sold pawn
        // in context for the resulting ideology memories.
        PatchNamedMethod(harmony, patched, typeof(Tradeable_Pawn), nameof(Tradeable_Pawn.ResolveTrade));

        PatchNamedMethod(harmony, patched, AccessTools.TypeByName("MUGB.Recipe_ExtractFleshChunks"), "ApplyLiveButcheryConsequences");

        // Keep ordinary human/HAR cannibalism memories intact for followers of the GDGC meme (KO: "단, 고블린은 제외").
        // MUGB's goblin-specific food memories are suppressed separately above.
        var removeVanillaThoughts = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("MUGB", StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(LoadableTypes)
            .Select(type => AccessTools.Method(type, "RemoveVanillaHumanlikeMeatThoughts"))
            .OfType<MethodInfo>();
        foreach (var method in removeVanillaThoughts) {
            harmony.Patch(method, prefix: PreserveHumanlikeThoughtsPrefixMethod);
        }
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly) {
        try {
            return assembly.GetTypes();
        } catch (ReflectionTypeLoadException exception) {
            return exception.Types.OfType<Type>();
        }
    }

    private static void PatchNamedMethod(Harmony harmony, HashSet<MethodBase> patched, Type? type, string methodName) {
        if (type is null) {
            return;
        }

        foreach (var method in AccessTools.GetDeclaredMethods(type).Where(method => method.Name == methodName)) {
            if (patched.Add(method)) {
                harmony.Patch(method, prefix: PrefixMethod, postfix: PostfixMethod, finalizer: FinalizerMethod);
            }
        }
    }

    private static void ContextPrefix(object? __instance, object?[]? __args, ref Pawn? __state) =>
        __state = VictimContext.PushIfGoblin(ExtractVictim(__instance, __args));

    private static void ContextPostfix(Pawn? __state) => VictimContext.Pop(__state);

    private static Exception? ContextFinalizer(Exception? __exception, Pawn? __state) {
        // Harmony normally runs postfixes on successful completion. The finalizer clears context after exceptions.
        if (__exception is not null) {
            VictimContext.Pop(__state);
        }

        return __exception;
    }

    private static Pawn? ExtractVictim(object? instance, object?[]? args) =>
        instance switch {
            Pawn pawn when GoblinExemption.IsMugbGoblin(pawn) => pawn,
            Corpse corpse => corpse.InnerPawn,
            Tradeable_Pawn { thingsColony: { } things } when things.OfType<Pawn>().FirstOrDefault() is { } traded => traded,
            _ => args?.Select(arg => arg switch {
                Pawn pawn => pawn,
                Corpse corpse => corpse.InnerPawn,
                _ => null,
            }).FirstOrDefault(GoblinExemption.IsMugbGoblin),
        };

    private static bool PreserveHumanlikeThoughtsPrefix(object? __instance, object?[]? __args) =>
        !GoblinExemption.HasGoblinExceptionalism(ExtractAnyPawn(__instance, __args));

    private static Pawn? ExtractAnyPawn(object? instance, object?[]? args) =>
        instance as Pawn
        ?? args?.OfType<Pawn>().FirstOrDefault()
        ?? (instance is null ? null : AccessTools.Field(instance.GetType(), "pawn")?.GetValue(instance) as Pawn);
}

// MUGB tracks repeated goblin-meat consumption as pheromone conditioning, then promotes
// it to pheromone adaptation. Followers of this meme never gain either state.
internal static class GoblinPheromonePreferenceCompat {
    private const string PreferenceUtilityTypeName = "MUGB.GoblinPheromonePreferenceUtility";
    private const string ConditioningCompTypeName = "MUGB.HediffComp_GoblinPheromoneConditioning";
    private const string ConditioningDefName = "MUGB_GoblinPheromoneConditioning";
    private const string PreferenceDefName = "MUGB_GoblinPheromonePreference";

    private static readonly HarmonyMethod GainConditioningPrefixMethod = new(typeof(GoblinPheromonePreferenceCompat), nameof(GainConditioningPrefix));
    private static readonly HarmonyMethod ConditioningGainPrefixMethod = new(typeof(GoblinPheromonePreferenceCompat), nameof(ConditioningGainPrefix));

    internal static void Apply(Harmony harmony) {
        if (AccessTools.TypeByName(PreferenceUtilityTypeName) is not { } preferenceUtilityType
            || AccessTools.TypeByName(ConditioningCompTypeName) is not { } conditioningCompType) {
            return;
        }

        PatchNamedMethods(harmony, preferenceUtilityType, GainConditioningPrefixMethod,
            "TryGainConditioning", "ForceGainConditioning", "NotifyPreferenceGoblinFoodEaten");
        PatchNamedMethods(harmony, conditioningCompType, ConditioningGainPrefixMethod, "AddConditioning", "AddForcedConditioning");
        GDGCLog.Message("[GDGC] MUGB goblin pheromone adaptation is disabled for followers of the GDGC meme.");
    }

    internal static void RemovePheromoneAdaptation(Pawn? pawn) {
        if (pawn?.health?.hediffSet is null || !GoblinExemption.HasGoblinExceptionalism(pawn)) {
            return;
        }

        RemoveHediff(pawn, PreferenceDefName);
        RemoveHediff(pawn, ConditioningDefName);
    }

    private static bool GainConditioningPrefix(object?[]? __args) => AllowConditioning(__args?.OfType<Pawn>().FirstOrDefault());

    private static bool ConditioningGainPrefix(HediffComp? __instance) => AllowConditioning(__instance?.Pawn);

    private static bool AllowConditioning(Pawn? pawn) {
        if (!GoblinExemption.HasGoblinExceptionalism(pawn)) {
            return true;
        }

        RemovePheromoneAdaptation(pawn);
        return false;
    }

    private static void PatchNamedMethods(Harmony harmony, Type type, HarmonyMethod prefix, params string[] methodNames) {
        foreach (var method in AccessTools.GetDeclaredMethods(type).Where(method => methodNames.Contains(method.Name))) {
            harmony.Patch(method, prefix: prefix);
        }
    }

    private static void RemoveHediff(Pawn pawn, string defName) {
        if (DefDatabase<HediffDef>.GetNamedSilentFail(defName) is { } def && pawn.health.hediffSet.GetFirstHediffOfDef(def) is { } hediff) {
            pawn.health.RemoveHediff(hediff);
        }
    }
}

// Existing adaptation is a migration concern, not a per-tick condition. GameComponent.LoadedGame
// runs after every map and world pawn has finished loading, so this scans each saved pawn once.
public sealed class GameComponent_GoblinPheromoneAdaptationCleanup : GameComponent {
    // RimWorld instantiates game components through a Game constructor.
    public GameComponent_GoblinPheromoneAdaptationCleanup(Game game) { }

    public override void LoadedGame() {
        foreach (var pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive) {
            GoblinPheromonePreferenceCompat.RemovePheromoneAdaptation(pawn);
        }
    }
}

// Optional War Crimes Expanded 2 compatibility. No WCE2 assembly reference is required:
// the helper and MUGB recipe are resolved at runtime when both mods are active.
internal static class Wce2Compat {
    private const string TortureHelperTypeName = "WarCrimesExpanded2.WCE2_ThoughtHelper";
    private const string TortureHelperMethodName = "WCE2_GiveThoughtsForPawnTortured";
    private const string FleshExtractionRecipeTypeName = "MUGB.Recipe_ExtractFleshChunks";
    private const string RespectedGuiltyPreceptDefName = "WCE2_Torture_RespectedGuilty";
    private const string RespectedGuiltyDoerThoughtDefName = "WCE2_TorturedPrisonerGuiltyResponsibleRespected";

    private static MethodInfo? giveTortureThoughts;
    private static bool invoking;
    private static bool invocationErrorLogged;

    internal static void Apply(Harmony harmony) {
        if (AccessTools.TypeByName(TortureHelperTypeName) is not { } tortureHelperType
            || AccessTools.TypeByName(FleshExtractionRecipeTypeName) is not { } fleshExtractionType) {
            return;
        }

        giveTortureThoughts = AccessTools.Method(tortureHelperType, TortureHelperMethodName, [typeof(Pawn), typeof(Pawn)]);
        if (giveTortureThoughts is null) {
            GDGCLog.Warning("[GDGC] WCE2 was found, but WCE2_GiveThoughtsForPawnTortured(Pawn, Pawn) was not found. Flesh extraction torture compatibility is disabled.");
            return;
        }

        var methods = AccessTools.GetDeclaredMethods(fleshExtractionType).Where(method => method.Name == "ApplyOnPawn").ToList<MethodInfo>();
        if (methods.Count == 0 && AccessTools.Method(fleshExtractionType, "ApplyOnPawn") is { } inheritedMethod) {
            methods.Add(inheritedMethod);
        }

        var postfix = new HarmonyMethod(typeof(Wce2Compat), nameof(FleshExtractionPostfix));
        foreach (var target in methods) {
            harmony.Patch(target, postfix: postfix);
        }

        if (methods.Count > 0) {
            GDGCLog.Message("[GDGC] WCE2 compatibility loaded: MUGB flesh extraction counts as torture.");
        }
    }

    private static void FleshExtractionPostfix(MethodBase? __originalMethod, object?[]? __args) {
        if (invoking || giveTortureThoughts is null) {
            return;
        }

        var (victim, torturer) = ExtractSurgeryPawns(__originalMethod, __args);
        if (victim is null) {
            return;
        }

        var state = VictimContext.PushIfGoblin(victim);
        invoking = true;
        try {
            // Make MUGB_ExtractFleshChunks use WCE2's own torture-thought pipeline.
            // Non-goblins therefore keep WCE2's normal innocence/guilt result.
            giveTortureThoughts.Invoke(null, [victim, torturer]);

            // The GDGC meme treats every MUGB goblin as guilty for that believer only.
            // Do not mutate Pawn_GuiltTracker because that would affect non-believers.
            if (GoblinExemption.TreatsGoblinAsGuilty(torturer, victim) && victim.guilt?.IsGuilty != true) {
                GiveRespectedGuiltyDoerThought(torturer);
            }
        } catch (Exception exception) {
            if (!invocationErrorLogged) {
                invocationErrorLogged = true;
                GDGCLog.Error($"[GDGC] WCE2 flesh-extraction compatibility failed: {(exception as TargetInvocationException)?.InnerException ?? exception}");
            }
        } finally {
            invoking = false;
            VictimContext.Pop(state);
        }
    }

    private static (Pawn? victim, Pawn? torturer) ExtractSurgeryPawns(MethodBase? method, object?[]? args) {
        if (args is null) {
            return (null, null);
        }

        Pawn? victim = null;
        Pawn? torturer = null;
        if (method?.GetParameters() is { } parameters && parameters.Length == args.Length) {
            foreach (var (parameter, arg) in parameters.Zip(args)) {
                if (arg is not Pawn pawn) {
                    continue;
                }

                var name = parameter.Name ?? "";
                if (NameHas(name, "victim", "patient")) {
                    victim = pawn;
                } else if (NameHas(name, "torturer", "surgeon", "doctor")) {
                    torturer = pawn;
                }
            }
        }

        // RimWorld surgery workers conventionally pass victim first and surgeon/torturer second.
        // Use that ordering if parameter names were stripped or changed.
        foreach (var pawn in args.OfType<Pawn>()) {
            if (victim is null) {
                victim = pawn;
            } else if (torturer is null && pawn != victim) {
                torturer = pawn;
            }
        }

        return (victim, torturer);
    }

    private static bool NameHas(string name, params string[] fragments) =>
        fragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static void GiveRespectedGuiltyDoerThought(Pawn? torturer) {
        if (torturer is not { Ideo: { } ideo, needs.mood: { } mood }
            || DefDatabase<PreceptDef>.GetNamedSilentFail(RespectedGuiltyPreceptDefName) is not { } preceptDef
            || DefDatabase<ThoughtDef>.GetNamedSilentFail(RespectedGuiltyDoerThoughtDefName) is not { } thoughtDef
            || ideo.GetPrecept(preceptDef) is not { } precept) {
            return;
        }

        mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(thoughtDef, precept));
    }
}
