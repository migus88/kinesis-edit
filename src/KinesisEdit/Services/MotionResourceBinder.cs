using Avalonia;
using Avalonia.Controls;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Points the motion alias resources at one of the two sets in Themes/Motion.axaml.
    /// <para>
    /// The mechanism is deliberately the smallest thing that works. Motion.axaml declares each
    /// motion twice — <c>&lt;Name&gt;TransitionsFull</c> and <c>&lt;Name&gt;TransitionsReduced</c>
    /// — and never declares the bare <c>&lt;Name&gt;Transitions</c> key that views and styles
    /// actually bind with <c>{DynamicResource}</c>. This runs once at startup and writes that bare
    /// key into <see cref="Application.Resources"/> itself, aliasing it to whichever set
    /// <see cref="IMotionSettings"/> chose. An entry a resource dictionary owns outranks anything
    /// in its merged dictionaries, so the alias always wins, and because the views bind
    /// dynamically, calling this again later re-points every consumer live.
    /// </para>
    /// <para>
    /// There is deliberately no alias for the tab/layer swap: the design pins it at 0 ms, and a
    /// zero-duration transition is not the same thing as no transition. Those containers get no
    /// <c>Transitions</c> at all.
    /// </para>
    /// </summary>
    public static class MotionResourceBinder
    {
        /// <summary>
        /// Every motion the app animates, as the alias key a view binds and the two concrete keys
        /// it can point at. Adding a motion means adding both resources to Themes/Motion.axaml and
        /// one row here.
        /// </summary>
        private static readonly MotionAlias[] Aliases =
        [
            new MotionAlias("StatusFillTransitions", "StatusFillTransitionsFull", "StatusFillTransitionsReduced"),
            new MotionAlias("ListInsertTransitions", "ListInsertTransitionsFull", "ListInsertTransitionsReduced"),
            new MotionAlias("PopoverTransitions", "PopoverTransitionsFull", "PopoverTransitionsReduced"),
            new MotionAlias("ModalTransitions", "ModalTransitionsFull", "ModalTransitionsReduced"),
            new MotionAlias("ScrimTransitions", "ScrimTransitionsFull", "ScrimTransitionsReduced"),
            new MotionAlias("ToastTransitions", "ToastTransitionsFull", "ToastTransitionsReduced"),
        ];

        /// <summary>
        /// Resolves every alias against <paramref name="motionSettings"/>. Safe to call more than
        /// once; an alias whose source resource is missing is skipped rather than nulled out, so a
        /// typo in Motion.axaml costs an animation, never a crash.
        /// </summary>
        public static void Apply(Application application, IMotionSettings motionSettings)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(motionSettings);

            foreach (var alias in Aliases)
            {
                var sourceKey = motionSettings.ReduceMotion ? alias.ReducedKey : alias.FullKey;

                if (application.TryFindResource(sourceKey, out var value) && value is not null)
                {
                    application.Resources[alias.AliasKey] = value;
                }
            }
        }

        /// <summary>One motion: the key views bind, and the two sets it can resolve to.</summary>
        private sealed record MotionAlias(string AliasKey, string FullKey, string ReducedKey);
    }
}
