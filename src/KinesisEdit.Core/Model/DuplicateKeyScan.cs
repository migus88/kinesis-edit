using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Finds tokens the user's edits left on more than one position of a layer — the editor's
    /// duplicate-key advisory. A pure function over the model, in the same UI-free spirit as
    /// <see cref="TapAndHoldPrecheck"/>: it answers a question every editor would otherwise
    /// re-derive, and it decides nothing.
    /// <para>
    /// <b>A finding needs a user assignment.</b> A group of positions sharing a token is reported
    /// only when at least one of them got that token from a remap. Factory layouts legitimately
    /// repeat tokens (both Shifts, both Ctrls, the hotkey defaults), so scanning every position's
    /// factory action would flood a stock board with advisories the user did not cause — the noise
    /// that makes an advisory system get ignored. Reported duplicates are therefore exactly the
    /// ones an edit created or joined.
    /// </para>
    /// <para>
    /// <b>Deliberately outside <see cref="KeyboardLayout.Validate"/>.</b> A duplicate is not a
    /// device limit: the firmware accepts the file, the save succeeds, and the layout is legal.
    /// <c>Validate()</c> reports what breaches a catalog limit, and adding an editor observation to
    /// it would put an advisory in the save-time violation list, where callers treat entries as
    /// reasons to warn about the write. So this lives beside the model, not inside it, and
    /// <see cref="ModelViolationKind"/> is untouched.
    /// </para>
    /// </summary>
    public static class DuplicateKeyScan
    {
        /// <summary>
        /// Scans one layer. <paramref name="dialect"/> selects the token spelling; pass the
        /// owning layout's <see cref="KeyboardLayout.Dialect"/>. With
        /// <see cref="TokenDialect.None"/> the first spelling the entry has (Gen1, then Gen2, then
        /// Legacy) is used, which keeps a layer scanned on its own comparable but is not what any
        /// file carries. Positions whose current action has no token in the dialect are skipped —
        /// they cannot reach the file, so they cannot duplicate anything in it.
        /// <para>
        /// A position's token is <see cref="KeyboardKey.ModifiedOrOriginalKey"/>, so the
        /// <c>fn1s</c>/<c>keyt</c> exception of 05 §5.3 holds: those keys act as their layer-switch
        /// action even when remapped, and a remap of one neither creates nor joins a duplication.
        /// Edge-lighting zones (<see cref="KeyboardLayer.EdgeKeys"/>) are LEDs, not key positions,
        /// and are not scanned.
        /// </para>
        /// </summary>
        /// <returns>Findings ordered by their first key index; <c>KeyIndexes</c> ascending.</returns>
        public static IReadOnlyList<DuplicateKeyFinding> Find(KeyboardLayer layer, TokenDialect dialect = TokenDialect.None)
        {
            ArgumentNullException.ThrowIfNull(layer);

            var groups = new List<TokenGroup>();
            var groupsByToken = new Dictionary<string, TokenGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in layer.Keys)
            {
                var token = ResolveToken(key, dialect);

                if (token.Length == 0)
                {
                    continue;
                }

                if (!groupsByToken.TryGetValue(token, out var group))
                {
                    group = new TokenGroup(token);
                    groupsByToken.Add(token, group);
                    groups.Add(group);
                }

                group.Add(key.Index, IsUserAssigned(key));
            }

            var findings = new List<DuplicateKeyFinding>();

            foreach (var group in groups)
            {
                if (!group.IsDuplication)
                {
                    continue;
                }

                findings.Add(new DuplicateKeyFinding
                {
                    Token = group.Token,
                    LayerIndex = layer.Index,
                    KeyIndexes = group.KeyIndexes
                });
            }

            return findings;
        }

        /// <summary>
        /// Scans every layer of a layout in the device's dialect.
        /// </summary>
        /// <returns>
        /// Findings ordered by layer index, then by first key index; <c>KeyIndexes</c> ascending.
        /// </returns>
        public static IReadOnlyList<DuplicateKeyFinding> Find(KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            var findings = new List<DuplicateKeyFinding>();

            foreach (var layer in layout.Layers)
            {
                findings.AddRange(Find(layer, layout.Dialect));
            }

            findings.Sort(static (left, right) => left.LayerIndex != right.LayerIndex
                ? left.LayerIndex.CompareTo(right.LayerIndex)
                : left.KeyIndexes[0].CompareTo(right.KeyIndexes[0]));

            return findings;
        }

        /// <summary>
        /// Whether the position resolves to its token because the user remapped it. A remap that
        /// the model stores but never acts on — <c>fn1s</c>/<c>keyt</c>, whose
        /// <see cref="KeyboardKey.ModifiedOrOriginalKey"/> stays the layer-switch action (05 §5.3)
        /// — does not count: the file's behaviour did not change.
        /// </summary>
        private static bool IsUserAssigned(KeyboardKey key)
        {
            return key.IsModified && !ReferenceEquals(key.ModifiedOrOriginalKey, key.OriginalKey);
        }

        private static string ResolveToken(KeyboardKey key, TokenDialect dialect)
        {
            var definition = key.ModifiedOrOriginalKey;

            if (dialect != TokenDialect.None)
            {
                return definition.GetToken(dialect);
            }

            if (definition.Gen1Token.Length > 0)
            {
                return definition.Gen1Token;
            }

            return definition.Gen2Token.Length > 0 ? definition.Gen2Token : definition.LegacyToken;
        }

        /// <summary>Positions collected for one token, in the layer's ascending index order.</summary>
        private sealed class TokenGroup
        {
            /// <summary>The spelling of the first position that carried the token.</summary>
            public string Token { get; }

            /// <summary>Whether the token sits on more than one position, one of them user-assigned.</summary>
            public bool IsDuplication => _keyIndexes.Count > 1 && _hasUserAssignment;

            /// <summary>The positions carrying the token, ascending.</summary>
            public IReadOnlyList<int> KeyIndexes => _keyIndexes;

            private bool _hasUserAssignment;

            private readonly List<int> _keyIndexes = [];

            public TokenGroup(string token)
            {
                Token = token;
            }

            public void Add(int keyIndex, bool isUserAssigned)
            {
                _keyIndexes.Add(keyIndex);
                _hasUserAssignment |= isUserAssigned;
            }
        }
    }
}
