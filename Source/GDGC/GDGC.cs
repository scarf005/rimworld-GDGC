using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GDGC
{
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            Harmony harmony = new Harmony("local.goblinsdontdeservegenevaconvention");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            DynamicVictimContextPatches.Apply(harmony);
            Log.Message("[GDGC] Goblin-exception logic loaded. The exemption requires the GDGC meme and an MUGB goblin target.");
        }
    }

    internal static class GoblinExemption
    {
        internal const string MemeDefName = "GDGC_GoblinExceptionalism";

        private static readonly HashSet<string> GoblinXenotypes = new HashSet<string>
        {
            "MUGB_Goblin",
            "MUGB_Hobgoblin"
        };

        private static readonly HashSet<string> GoblinCoreGenes = new HashSet<string>
        {
            "MUGB_Gene_GoblinCore",
            "MUGB_Gene_HobgoblinFrame"
        };

        private static readonly HashSet<string> GoblinFoodThoughts = new HashSet<string>
        {
            "MUGB_AteGoblinMeatDirect",
            "MUGB_AteGoblinMeatDirectCannibal",
            "MUGB_AteGoblinMeatAsIngredient",
            "MUGB_AteGoblinMeatAsIngredientCannibal"
        };

        private static readonly HashSet<string> ExplicitMoralThoughts = new HashSet<string>
        {
            "MUGB_PerformedLiveButchery",
            "ButcheredHumanlikeCorpse",
            "KnowButcheredHumanlikeCorpse",
            "KnowColonistOrganHarvested",
            "KnowGuestOrganHarvested",
            "KnowPrisonerOrganHarvested",
            "KnowColonistExecuted",
            "KnowPrisonerExecuted",
            "KnowPrisonerDiedInnocent",
            "ColonistOrganHarvested",
            "PrisonerOrganHarvested"
        };

        private static readonly string[] MoralNameFragments =
        {
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
            "Tortur",
            "Vivisect",
            "Mutilat",
            "LiveButcher"
        };

        internal static bool IsMugbGoblin(Pawn pawn)
        {
            if (pawn == null || pawn.genes == null)
            {
                return false;
            }

            XenotypeDef xenotype = pawn.genes.Xenotype;
            if (xenotype != null && GoblinXenotypes.Contains(xenotype.defName))
            {
                return true;
            }

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                Gene gene = genes[i];
                if (gene != null && gene.Active && gene.def != null && GoblinCoreGenes.Contains(gene.def.defName))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasGoblinExceptionalism(Pawn pawn)
        {
            Ideo ideo = pawn == null ? null : pawn.Ideo;
            if (ideo == null)
            {
                return false;
            }

            // Reflection keeps this source tolerant of minor Ideo API changes.
            MemeDef meme = DefDatabase<MemeDef>.GetNamedSilentFail(MemeDefName);
            if (meme == null)
            {
                return false;
            }

            MethodInfo hasMeme = AccessTools.Method(typeof(Ideo), "HasMeme", new Type[] { typeof(MemeDef) });
            if (hasMeme != null)
            {
                object result = hasMeme.Invoke(ideo, new object[] { meme });
                if (result is bool)
                {
                    return (bool)result;
                }
            }

            FieldInfo memesField = AccessTools.Field(typeof(Ideo), "memes");
            if (memesField != null)
            {
                IEnumerable<MemeDef> memes = memesField.GetValue(ideo) as IEnumerable<MemeDef>;
                if (memes != null && memes.Contains(meme))
                {
                    return true;
                }
            }

            PropertyInfo memesProperty = AccessTools.Property(typeof(Ideo), "MemesListForReading");
            if (memesProperty != null)
            {
                IEnumerable<MemeDef> memes = memesProperty.GetValue(ideo, null) as IEnumerable<MemeDef>;
                if (memes != null && memes.Contains(meme))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TreatsGoblinAsGuilty(Pawn receiver, Pawn victim)
        {
            return HasGoblinExceptionalism(receiver) && IsMugbGoblin(victim) && receiver != victim;
        }

        internal static bool ShouldSuppress(Pawn receiver, ThoughtDef thoughtDef, Pawn victim)
        {
            if (receiver == null || thoughtDef == null || !HasGoblinExceptionalism(receiver))
            {
                return false;
            }

            // MUGB's four goblin-meat thoughts already encode that the consumed ingredient was goblin meat.
            if (GoblinFoodThoughts.Contains(thoughtDef.defName))
            {
                return IsNegativeMoodThought(thoughtDef);
            }

            // Every MUGB goblin/hobgoblin is ideologically guilty to followers of this meme.
            // Keep this observer-relative instead of changing RimWorld's global guilt tracker.
            if (!TreatsGoblinAsGuilty(receiver, victim))
            {
                return false;
            }

            if (!IsNegativeMoodThought(thoughtDef))
            {
                return false;
            }

            if (ExplicitMoralThoughts.Contains(thoughtDef.defName))
            {
                return true;
            }

            for (int i = 0; i < MoralNameFragments.Length; i++)
            {
                if (thoughtDef.defName.IndexOf(MoralNameFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNegativeMoodThought(ThoughtDef thoughtDef)
        {
            if (thoughtDef.stages == null)
            {
                return false;
            }

            for (int i = 0; i < thoughtDef.stages.Count; i++)
            {
                ThoughtStage stage = thoughtDef.stages[i];
                if (stage != null && stage.baseMoodEffect < 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // MUGB goblins are Biotech xenotypes on the vanilla Human ThingDef, so a normal
    // ThingDef/category filter cannot distinguish their corpses from other human corpses.
    // These workers expose the distinction as SpecialThingFilterDefs in the bill UI.
    public sealed class SpecialThingFilterWorker_MugbGoblinCorpse : SpecialThingFilterWorker
    {
        public override bool Matches(Thing thing)
        {
            Corpse corpse = thing as Corpse;
            return corpse != null && GoblinExemption.IsMugbGoblin(corpse.InnerPawn);
        }
    }

    public sealed class SpecialThingFilterWorker_OtherHumanlikeCorpse : SpecialThingFilterWorker
    {
        public override bool Matches(Thing thing)
        {
            Corpse corpse = thing as Corpse;
            Pawn pawn = corpse == null ? null : corpse.InnerPawn;
            return pawn != null && pawn.RaceProps != null && pawn.RaceProps.Humanlike && !GoblinExemption.IsMugbGoblin(pawn);
        }
    }

    internal static class VictimContext
    {
        [ThreadStatic]
        private static Stack<Pawn> victims;

        internal static Pawn Current
        {
            get
            {
                return victims != null && victims.Count > 0 ? victims.Peek() : null;
            }
        }

        internal static Pawn PushIfGoblin(Pawn pawn)
        {
            if (!GoblinExemption.IsMugbGoblin(pawn))
            {
                return null;
            }

            if (victims == null)
            {
                victims = new Stack<Pawn>();
            }

            victims.Push(pawn);
            return pawn;
        }

        internal static void Pop(Pawn state)
        {
            if (state == null || victims == null || victims.Count == 0)
            {
                return;
            }

            victims.Pop();
        }
    }

    [HarmonyPatch]
    internal static class MemoryThoughtHandler_TryGainMemory_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(MemoryThoughtHandler)).Where(method => method.Name == "TryGainMemory");
        }

        private static bool Prefix(MemoryThoughtHandler __instance, object[] __args)
        {
            Pawn receiver = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            ThoughtDef thoughtDef = null;
            Pawn victim = null;

            for (int i = 0; i < __args.Length; i++)
            {
                ThoughtDef suppliedDef = __args[i] as ThoughtDef;
                if (suppliedDef != null)
                {
                    thoughtDef = suppliedDef;
                }

                Thought_Memory suppliedMemory = __args[i] as Thought_Memory;
                if (suppliedMemory != null)
                {
                    thoughtDef = suppliedMemory.def;
                }

                Pawn suppliedPawn = __args[i] as Pawn;
                if (suppliedPawn != null && suppliedPawn != receiver)
                {
                    victim = suppliedPawn;
                }
            }

            // During death/butchery/organ-harvest processing, the action context identifies the
            // actual victim. Prefer it over TryGainMemory's optional "other pawn" argument, which
            // can instead be the butcher, executioner, or another participant.
            Pawn contextualVictim = VictimContext.Current;
            if (contextualVictim != null)
            {
                victim = contextualVictim;
            }

            return !GoblinExemption.ShouldSuppress(receiver, thoughtDef, victim);
        }
    }

    // Corpse.ButcherProducts is an iterator. A normal prefix/postfix only surrounds iterator creation,
    // while humanlike-butchery memories are generated during enumeration. Keep the goblin victim
    // context active until the enumeration is disposed.
    [HarmonyPatch]
    internal static class Corpse_ButcherProducts_VictimContext_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Corpse))
                .Where(method => method.Name == "ButcherProducts"
                    && typeof(IEnumerable<Thing>).IsAssignableFrom(method.ReturnType));
        }

        private static void Postfix(Corpse __instance, ref IEnumerable<Thing> __result)
        {
            Pawn victim = __instance == null ? null : __instance.InnerPawn;
            if (__result == null || !GoblinExemption.IsMugbGoblin(victim))
            {
                return;
            }

            __result = EnumerateWithVictimContext(__result, victim);
        }

        private static IEnumerable<Thing> EnumerateWithVictimContext(IEnumerable<Thing> source, Pawn victim)
        {
            Pawn state = VictimContext.PushIfGoblin(victim);
            try
            {
                foreach (Thing thing in source)
                {
                    yield return thing;
                }
            }
            finally
            {
                VictimContext.Pop(state);
            }
        }
    }

    internal static class DynamicVictimContextPatches
    {
        private static readonly HarmonyMethod PrefixMethod = new HarmonyMethod(typeof(DynamicVictimContextPatches), "ContextPrefix");
        private static readonly HarmonyMethod PostfixMethod = new HarmonyMethod(typeof(DynamicVictimContextPatches), "ContextPostfix");
        private static readonly HarmonyMethod FinalizerMethod = new HarmonyMethod(typeof(DynamicVictimContextPatches), "ContextFinalizer");
        private static readonly HarmonyMethod PreserveHumanlikeThoughtsPrefixMethod = new HarmonyMethod(typeof(DynamicVictimContextPatches), "PreserveHumanlikeThoughtsPrefix");

        internal static void Apply(Harmony harmony)
        {
            HashSet<MethodBase> patched = new HashSet<MethodBase>();

            // Innocent-prisoner/responsibility memories are generated in the death path.
            PatchNamedMethod(harmony, patched, typeof(Pawn), "Kill");
            PatchNamedMethod(harmony, patched, typeof(ThoughtUtility), "GiveThoughtsForPawnOrganHarvested");

            Type executionUtility = AccessTools.TypeByName("RimWorld.ExecutionThoughtsUtility");
            PatchMethodsContaining(harmony, patched, executionUtility, "Executed");
            PatchMethodsContaining(harmony, patched, executionUtility, "Execution");

            Type mugbLiveButchery = AccessTools.TypeByName("MUGB.Recipe_ExtractFleshChunks");
            PatchNamedMethod(harmony, patched, mugbLiveButchery, "ApplyLiveButcheryConsequences");

            // Keep ordinary human/HAR cannibalism memories intact for followers of the GDGC meme (KO: "단, 고블린은 제외").
            // MUGB's goblin-specific food memories are suppressed separately above.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.GetName().Name.StartsWith("MUGB", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).ToArray();
                }

                for (int i = 0; i < types.Length; i++)
                {
                    MethodInfo method = AccessTools.Method(types[i], "RemoveVanillaHumanlikeMeatThoughts");
                    if (method != null)
                    {
                        harmony.Patch(method, prefix: PreserveHumanlikeThoughtsPrefixMethod);
                    }
                }
            }
        }

        private static void PatchNamedMethod(Harmony harmony, HashSet<MethodBase> patched, Type type, string methodName)
        {
            if (type == null)
            {
                return;
            }

            List<MethodInfo> methods = AccessTools.GetDeclaredMethods(type).Where(method => method.Name == methodName).ToList();
            for (int i = 0; i < methods.Count; i++)
            {
                PatchContextMethod(harmony, patched, methods[i]);
            }
        }

        private static void PatchMethodsContaining(Harmony harmony, HashSet<MethodBase> patched, Type type, string fragment)
        {
            if (type == null)
            {
                return;
            }

            List<MethodInfo> methods = AccessTools.GetDeclaredMethods(type)
                .Where(method => method.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            for (int i = 0; i < methods.Count; i++)
            {
                PatchContextMethod(harmony, patched, methods[i]);
            }
        }

        private static void PatchContextMethod(Harmony harmony, HashSet<MethodBase> patched, MethodBase method)
        {
            if (method == null || !patched.Add(method))
            {
                return;
            }

            harmony.Patch(method, prefix: PrefixMethod, postfix: PostfixMethod, finalizer: FinalizerMethod);
        }

        private static void ContextPrefix(object __instance, object[] __args, ref Pawn __state)
        {
            Pawn victim = ExtractVictim(__instance, __args);
            __state = VictimContext.PushIfGoblin(victim);
        }

        private static void ContextPostfix(Pawn __state)
        {
            VictimContext.Pop(__state);
        }

        private static Exception ContextFinalizer(Exception __exception, Pawn __state)
        {
            // Harmony normally runs postfixes on successful completion. The finalizer clears context after exceptions.
            if (__exception != null)
            {
                VictimContext.Pop(__state);
            }

            return __exception;
        }

        private static Pawn ExtractVictim(object instance, object[] args)
        {
            Pawn directPawn = instance as Pawn;
            if (directPawn != null && GoblinExemption.IsMugbGoblin(directPawn))
            {
                return directPawn;
            }

            Corpse corpse = instance as Corpse;
            if (corpse != null)
            {
                return corpse.InnerPawn;
            }

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    Pawn pawn = args[i] as Pawn;
                    if (pawn != null && GoblinExemption.IsMugbGoblin(pawn))
                    {
                        return pawn;
                    }

                    Corpse suppliedCorpse = args[i] as Corpse;
                    if (suppliedCorpse != null && GoblinExemption.IsMugbGoblin(suppliedCorpse.InnerPawn))
                    {
                        return suppliedCorpse.InnerPawn;
                    }
                }
            }

            return null;
        }

        private static bool PreserveHumanlikeThoughtsPrefix(object __instance, object[] __args)
        {
            Pawn pawn = ExtractAnyPawn(__instance, __args);
            return !GoblinExemption.HasGoblinExceptionalism(pawn);
        }

        private static Pawn ExtractAnyPawn(object instance, object[] args)
        {
            Pawn directPawn = instance as Pawn;
            if (directPawn != null)
            {
                return directPawn;
            }

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    Pawn pawn = args[i] as Pawn;
                    if (pawn != null)
                    {
                        return pawn;
                    }
                }
            }

            if (instance != null)
            {
                FieldInfo pawnField = AccessTools.Field(instance.GetType(), "pawn");
                if (pawnField != null)
                {
                    return pawnField.GetValue(instance) as Pawn;
                }
            }

            return null;
        }
    }
}
