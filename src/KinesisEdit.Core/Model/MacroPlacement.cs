namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Putting a macro on a key and taking one off again — the two write paths a macro editor needs
    /// beyond editing the instance it already has.
    /// <para>
    /// <b>A copy is a copy.</b> <see cref="CopyTo"/> clones the source and re-stamps the clone's
    /// layer and trigger, so the destination gets an independent macro (05 §1.5: copies, never
    /// shared instances). Editing either one afterwards leaves the other alone, which is exactly
    /// what the file already says — every slot holds its own copy (06 §1).
    /// </para>
    /// <para>
    /// <b>Refusals return, they do not throw.</b> A restricted position, a full key, an occupied
    /// slot and a key from another layout are all states a user can reach by clicking, so they are
    /// answers (null / false), not exceptions. Only genuine programming errors — a null argument —
    /// throw, per the model's "limits are reported, never enforced" rule. Device <em>limits</em>
    /// (macro counts, keystroke budgets) are not consulted here either; they stay
    /// <see cref="KeyboardLayout.Validate"/>'s job.
    /// </para>
    /// </summary>
    public static class MacroPlacement
    {
        /// <summary>
        /// Puts an independent copy of <paramref name="source"/> on <paramref name="target"/>,
        /// carrying its keystrokes, speed, repeat, co-triggers and name, and returns that copy.
        /// <para>
        /// <paramref name="slot"/> is <see cref="MacroSites.FirstEmptySlot"/> (the default) for the
        /// key's first empty slot — the guarded <see cref="KeyboardKey.AssignMacro"/> path — or an
        /// explicit <see cref="Macro.MinMacroIndex"/>..<see cref="Macro.MaxMacroIndex"/> slot, which
        /// must be empty. On a flat-list device (06 §1) the copy is appended to
        /// <see cref="KeyboardLayout.Macros"/> and the only slots that mean anything are
        /// <see cref="MacroSites.FirstEmptySlot"/> and <see cref="MacroSites.FlatListSlot"/>; an
        /// explicit per-key slot names a store that device does not have.
        /// </para>
        /// <para>
        /// Returns <b>null</b> — never throws — when the copy is refused: a slot number outside the
        /// range above, a key that is not in <paramref name="layout"/>, a position that rejects
        /// macros (06 §2.2), a slot already in use, or every slot full.
        /// </para>
        /// </summary>
        public static Macro? CopyTo(
            KeyboardLayout layout,
            Macro source,
            KeyboardKey target,
            int slot = MacroSites.FirstEmptySlot)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            if (slot != MacroSites.FirstEmptySlot
                && (slot < MacroSites.FlatListSlot || slot > Macro.MaxMacroIndex))
            {
                return null;
            }

            if (FindLayerOf(layout, target) is not KeyboardLayer layer)
            {
                return null;
            }

            var copy = source.Clone();

            // The copy sits where it landed, not where it came from: the destination's layer and
            // trigger identity (05 §1.3, never the position key) are what the save writes.
            copy.LayerIndex = layer.Index;
            copy.TriggerKey = target.TriggerKey.Code;

            return Place(layout, copy, target, slot) ? copy : null;
        }

        /// <summary>
        /// Takes <paramref name="macro"/> off one place: the flat list on a Gen2 layout, or slot
        /// <paramref name="slot"/> of <paramref name="key"/> on every other device. Returns whether
        /// anything was removed.
        /// <para>
        /// <b>Only that one place.</b> The slot is cleared only when it really holds
        /// <paramref name="macro"/> (by reference), so a stale slot number cannot empty somebody
        /// else's macro, and other slots on the same key — and every other key carrying a macro that
        /// merely looks the same — are untouched.
        /// </para>
        /// </summary>
        public static bool Remove(KeyboardLayout layout, KeyboardKey? key, Macro macro, int slot)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(macro);

            if (layout.UsesFlatMacroList)
            {
                return layout.RemoveMacro(macro);
            }

            if (key is null || slot < Macro.MinMacroIndex || slot > Macro.MaxMacroIndex)
            {
                return false;
            }

            if (!ReferenceEquals(key.GetMacro(slot), macro))
            {
                return false;
            }

            key.SetMacro(slot, null);

            return true;
        }

        private static KeyboardLayer? FindLayerOf(KeyboardLayout layout, KeyboardKey key)
        {
            foreach (var layer in layout.Layers)
            {
                foreach (var candidate in layer.Keys)
                {
                    if (ReferenceEquals(candidate, key))
                    {
                        return layer;
                    }
                }
            }

            return null;
        }

        private static bool Place(KeyboardLayout layout, Macro macro, KeyboardKey target, int slot)
        {
            if (layout.UsesFlatMacroList)
            {
                if (slot != MacroSites.FirstEmptySlot && slot != MacroSites.FlatListSlot)
                {
                    return false;
                }

                macro.MacroIndex = MacroSites.FlatListSlot;
                layout.AddMacro(macro);

                return true;
            }

            if (!target.CanAssignMacro || !target.UsesMacroSlots)
            {
                return false;
            }

            if (slot == MacroSites.FirstEmptySlot)
            {
                return target.AssignMacro(macro) != 0;
            }

            // MacroSites.FlatListSlot on a slot device names no slot at all, and GetMacro would
            // throw for it — a refusal, like every other "there is nowhere to put this".
            if (slot < Macro.MinMacroIndex || target.GetMacro(slot) is not null)
            {
                return false;
            }

            target.SetMacro(slot, macro);

            return true;
        }
    }
}
