using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels.Advisories
{
    /// <summary>
    /// Everything the editor has noticed about the open profile and is reporting without having
    /// changed anything — the model behind the amber strip, the per-key flag and the save toast's
    /// count.
    /// <para>
    /// <b>Nothing here blocks.</b> Every source is already a report: <see cref="KeyboardLayout"/>'s
    /// limits are reported, never enforced (docs/app/keyboard-model.md, invariant 1), and
    /// <see cref="DuplicateKeyScan"/> is an observation about a legal layout. This type reads them
    /// and says them; it never refuses an edit or a save, and there is deliberately no path from
    /// here into a <c>CanExecute</c>.
    /// </para>
    /// <para>
    /// <b>Immutable, and rebuilt whole.</b> Core announces no change, so an advisory set that
    /// mutated in place could never tell anybody. <see cref="Build"/> is called from the editor's
    /// <c>RefreshCounters()</c> — the funnel every layout-mutation path already ends in — and the
    /// new instance replaces the old. That is also why <see cref="Build"/> is not called from a
    /// property getter: <see cref="KeyboardLayout.Validate"/> walks every key of every layer.
    /// </para>
    /// </summary>
    public sealed class EditorAdvisories
    {
        /// <summary>An empty set: what a device with no model, and a clean profile, both have.</summary>
        public static EditorAdvisories Empty { get; } = new([]);

        /// <summary>
        /// Reads <paramref name="layout"/> and reports what it finds. Two sources, both of which
        /// already report rather than enforce:
        /// <list type="bullet">
        /// <item><see cref="KeyboardLayout.Validate"/>'s four <em>budget</em> breaches —
        /// <see cref="ModelViolationKind.TapAndHoldCountExceeded"/>,
        /// <see cref="ModelViolationKind.MacroLengthExceeded"/>,
        /// <see cref="ModelViolationKind.MacroKeystrokeBudgetExceeded"/> and
        /// <see cref="ModelViolationKind.MacroCoTriggerLimitExceeded"/>. The other violation kinds
        /// are deliberately skipped: they describe a model that cannot be written faithfully (a
        /// remap on a locked position, a macro on a board with no macros), which is a failure the
        /// save path reports, not an advisory.</item>
        /// <item><see cref="DuplicateKeyScan"/>, one advisory <b>per position</b> of each finding —
        /// so every cap in the group carries the note and "3 keys carry advisory notes on this
        /// layer" counts what it says it counts.</item>
        /// </list>
        /// </summary>
        public static EditorAdvisories Build(KeyboardLayout? layout)
        {
            if (layout is null)
            {
                return Empty;
            }

            var advisories = new List<AdvisoryViewModel>();

            AddBudgetAdvisories(layout, advisories);
            AddDuplicateAdvisories(layout, advisories);

            return new EditorAdvisories(advisories);
        }

        /// <summary>Every advisory, in the order the sources reported them.</summary>
        public IReadOnlyList<AdvisoryViewModel> All { get; }

        /// <summary>How many there are across the whole profile — the save toast's count.</summary>
        public int Total => All.Count;

        private readonly List<AdvisoryViewModel> _advisories;

        private EditorAdvisories(List<AdvisoryViewModel> advisories)
        {
            _advisories = advisories;
            All = advisories;
        }

        /// <summary>
        /// The advisories one section shows. With <paramref name="layerIndex"/> the Layout tab is
        /// narrowed to that layer — plus the layout-wide ones, which are true on every layer and so
        /// are shown on every layer. Every other tab ignores the layer: the macro list shows the
        /// whole profile.
        /// </summary>
        public IReadOnlyList<AdvisoryViewModel> ForTab(EditorTab tab, int? layerIndex = null)
        {
            var matches = new List<AdvisoryViewModel>();

            foreach (var advisory in _advisories)
            {
                if (advisory.Tab != tab)
                {
                    continue;
                }

                if (tab == EditorTab.Keys
                    && layerIndex is int layer
                    && advisory.LayerIndex is int advisoryLayer
                    && advisoryLayer != layer)
                {
                    continue;
                }

                matches.Add(advisory);
            }

            return matches;
        }

        /// <summary>How many advisories a section carries, across every layer.</summary>
        public int CountForTab(EditorTab tab)
        {
            return ForTab(tab).Count;
        }

        /// <summary>
        /// How many <b>positions</b> of one layer carry an advisory — the count the Layout tab's
        /// sentence names. Layout-wide advisories are not positions and are not counted.
        /// </summary>
        public int CountForLayer(int layerIndex)
        {
            var keys = new HashSet<int>();

            foreach (var advisory in _advisories)
            {
                if (advisory.LayerIndex == layerIndex && advisory.KeyIndex is int keyIndex)
                {
                    keys.Add(keyIndex);
                }
            }

            return keys.Count;
        }

        /// <summary>The advisories on one key position.</summary>
        public IReadOnlyList<AdvisoryViewModel> ForKey(int layerIndex, int keyIndex)
        {
            var matches = new List<AdvisoryViewModel>();

            foreach (var advisory in _advisories)
            {
                if (advisory.LayerIndex == layerIndex && advisory.KeyIndex == keyIndex)
                {
                    matches.Add(advisory);
                }
            }

            return matches;
        }

        /// <summary>Whether one key position carries an advisory (<c>KeyboardKeyViewModel.HasAdvisory</c>).</summary>
        public bool HasAdvisoryForKey(int layerIndex, int keyIndex)
        {
            foreach (var advisory in _advisories)
            {
                if (advisory.LayerIndex == layerIndex && advisory.KeyIndex == keyIndex)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether one row of the macro list carries an advisory. Identity is the slot's:
        /// layer + key + 1-based slot, exactly what <see cref="ModelViolation"/> reports for a
        /// slot-based macro (06 §1).
        /// <para>
        /// <b>A flat-list macro never matches.</b> The Advantage360 keeps its macros in one list and
        /// <see cref="KeyboardLayout.Validate"/> anchors a finding there by layer alone — there is
        /// no per-macro identity in the report to match a row against, and marking every row of that
        /// layer would say "this macro is over budget" about macros that are not. Those advisories
        /// are still counted, and still shown in the strip; only the row dot is missing, on the one
        /// board whose editor is issue #41.
        /// </para>
        /// </summary>
        public bool HasAdvisoryForMacro(int? layerIndex, int? keyIndex, int slot)
        {
            if (keyIndex is null)
            {
                return false;
            }

            foreach (var advisory in _advisories)
            {
                if (advisory.Tab == EditorTab.Macros
                    && advisory.LayerIndex == layerIndex
                    && advisory.KeyIndex == keyIndex
                    && advisory.MacroIndex == slot)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The one calm sentence the summary strip shows for a section, or an empty string when it
        /// has nothing to say. A single advisory speaks for itself; several are summarised, and the
        /// Layout tab's summary counts <b>positions</b> because that is what mockup 1e's sentence
        /// says it counts.
        /// </summary>
        public string SummaryFor(EditorTab tab, int? layerIndex = null)
        {
            var matches = ForTab(tab, layerIndex);

            if (matches.Count == 0)
            {
                return string.Empty;
            }

            if (matches.Count == 1)
            {
                return matches[0].Message;
            }

            var detail = matches[0].Detail;

            return tab == EditorTab.Keys && layerIndex is int layer
                ? AdvisoryText.LayerSummary(CountForLayer(layer), detail)
                : AdvisoryText.SectionSummary(matches.Count, detail);
        }

        private static void AddBudgetAdvisories(KeyboardLayout layout, List<AdvisoryViewModel> advisories)
        {
            foreach (var violation in layout.Validate())
            {
                var actual = violation.ActualValue ?? 0;
                var limit = violation.Limit ?? 0;

                switch (violation.Kind)
                {
                    case ModelViolationKind.TapAndHoldCountExceeded:
                        advisories.Add(Create(
                            EditorTab.Keys,
                            violation,
                            AdvisoryText.TapAndHoldCount(actual, limit),
                            AdvisoryText.TapAndHoldCountDetail(actual, limit)));

                        break;

                    case ModelViolationKind.MacroLengthExceeded:
                        advisories.Add(Create(
                            EditorTab.Macros,
                            violation,
                            AdvisoryText.MacroCharacters(actual, limit),
                            AdvisoryText.MacroCharactersDetail(actual, limit)));

                        break;

                    case ModelViolationKind.MacroKeystrokeBudgetExceeded:
                        advisories.Add(Create(
                            EditorTab.Macros,
                            violation,
                            AdvisoryText.LayoutKeystrokeBudget(actual, limit),
                            AdvisoryText.LayoutKeystrokeBudgetDetail(actual, limit)));

                        break;

                    case ModelViolationKind.MacroCoTriggerLimitExceeded:
                        advisories.Add(Create(
                            EditorTab.Macros,
                            violation,
                            AdvisoryText.CoTriggers(actual, limit)));

                        break;
                }
            }
        }

        private static void AddDuplicateAdvisories(KeyboardLayout layout, List<AdvisoryViewModel> advisories)
        {
            // The layout's own dialect, never the scan's fallback: with TokenDialect.None the
            // reported token is spelled by whichever dialect the entry happens to carry first,
            // which would print a token the user's file does not contain.
            foreach (var finding in DuplicateKeyScan.Find(layout))
            {
                var message = AdvisoryText.DuplicateKey(finding.Token, finding.KeyIndexes);
                var detail = AdvisoryText.DuplicateKeyDetail(finding.Token, finding.KeyIndexes);

                foreach (var keyIndex in finding.KeyIndexes)
                {
                    advisories.Add(new AdvisoryViewModel(
                        new AdvisoryAnchor
                        {
                            Tab = EditorTab.Keys,
                            LayerIndex = finding.LayerIndex,
                            KeyIndex = keyIndex
                        },
                        message,
                        detail));
                }
            }
        }

        private static AdvisoryViewModel Create(
            EditorTab tab,
            ModelViolation violation,
            string message,
            string? detail = null)
        {
            return new AdvisoryViewModel(
                new AdvisoryAnchor
                {
                    Tab = tab,
                    LayerIndex = violation.LayerIndex,
                    KeyIndex = violation.KeyIndex,
                    MacroIndex = violation.MacroIndex
                },
                message,
                detail);
        }
    }
}
