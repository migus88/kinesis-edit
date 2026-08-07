namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The library's edits: rename, duplicate, delete, assign to a further key, and the propagation
    /// that keeps one logical macro's sites in step. Each one mutates the layout through the model's
    /// existing write paths and then rebuilds the snapshot.
    /// </summary>
    public sealed partial class MacroLibrary
    {
        /// <summary>
        /// Renames every site of <paramref name="entry"/>. A blank name <b>clears</b> the stored
        /// name, so the macro goes back to a derived one and its settings key is removed on the next
        /// save. The name is sanitized (<see cref="MacroNaming.Sanitize"/>).
        /// <para>
        /// Renaming onto a name another entry holds does not fail: if that entry has the same
        /// content the two <b>merge</b> into one logical macro with both sites — which is exactly
        /// "this key runs that macro too" — and if it does not, the rebuild disambiguates
        /// (<c>"Sign-off (2)"</c>). Nothing is ever dropped.
        /// </para>
        /// </summary>
        public MacroLibraryEntry Rename(MacroLibraryEntry entry, string? newName)
        {
            EnsureOwned(entry);

            var canonical = entry.Canonical;

            foreach (var site in entry.Sites)
            {
                site.Macro.Name = MacroNaming.Sanitize(newName);
            }

            Refresh();

            return FindByMacro(canonical)
                   ?? throw new InvalidOperationException("The renamed macro left the layout.");
        }

        /// <summary>
        /// Rewrites every site of <paramref name="entry"/> from its
        /// <see cref="MacroLibraryEntry.Canonical"/> instance — keystrokes (deep-copied), speed,
        /// repeat, co-triggers and the name — so an edit made on one key reaches all of them. The
        /// site's own identity (layer, trigger key, slot) is left alone: that is where the macro
        /// sits, not what it is.
        /// <para>
        /// Co-triggers travel with the content, per 06 §5's "the trigger is the key plus its
        /// co-trigger set". Two sites on the <em>same</em> trigger key therefore end up colliding,
        /// which <see cref="KeyboardLayout.Validate"/> reports as
        /// <see cref="ModelViolationKind.MacroTriggerCollision"/> — reported, never refused, like
        /// every other limit in this model.
        /// </para>
        /// </summary>
        public MacroLibraryEntry Propagate(MacroLibraryEntry entry)
        {
            EnsureOwned(entry);

            var canonical = entry.Canonical;

            foreach (var site in entry.Sites)
            {
                if (ReferenceEquals(site.Macro, canonical))
                {
                    continue;
                }

                CopyContent(canonical, site.Macro);
            }

            Refresh();

            return FindByMacro(canonical)
                   ?? throw new InvalidOperationException("The propagated macro left the layout.");
        }

        /// <summary>
        /// Removes <paramref name="entry"/> from <b>every</b> site it occupies — the tab's delete,
        /// which is why the UI must say what a macro is assigned to before it goes. Slot sites are
        /// emptied with <see cref="KeyboardKey.SetMacro"/>, flat-list sites with
        /// <see cref="KeyboardLayout.RemoveMacro"/>.
        /// </summary>
        public void Delete(MacroLibraryEntry entry)
        {
            EnsureOwned(entry);

            foreach (var site in entry.Sites)
            {
                if (site.Key is KeyboardKey key)
                {
                    if (ReferenceEquals(key.GetMacro(site.SlotNumber), site.Macro))
                    {
                        key.SetMacro(site.SlotNumber, null);
                    }

                    continue;
                }

                Layout.RemoveMacro(site.Macro);
            }

            Refresh();
        }

        /// <summary>
        /// Puts a copy of <paramref name="entry"/> on <paramref name="key"/> — "assigning a named
        /// macro to a second key". The copy carries the canonical instance's keystrokes, speed,
        /// repeat, co-triggers and name, so the result is one more site of the same logical macro.
        /// <para>
        /// <paramref name="slot"/> is 1..5 on a slot device and must be empty;
        /// <see cref="FirstEmptySlot"/> (0) takes the key's first empty slot, the guarded
        /// <see cref="KeyboardKey.AssignMacro"/> path. On a flat-list device the slot argument is
        /// meaningless and must be 0.
        /// </para>
        /// <para>
        /// Returns null — never throws — when the assignment is refused: a position that rejects
        /// macros (06 §2.2), a slot already in use, all five slots full, a non-zero slot on a
        /// flat-list device, or a key that is not in this layout. Device <em>limits</em> (macro
        /// count, budgets) are not consulted; they stay <see cref="KeyboardLayout.Validate"/>'s job.
        /// </para>
        /// </summary>
        public MacroLibraryEntry? AssignTo(MacroLibraryEntry entry, KeyboardKey key, int slot = FirstEmptySlot)
        {
            EnsureOwned(entry);
            ArgumentNullException.ThrowIfNull(key);

            if (slot < FirstEmptySlot || slot > Macro.MaxMacroIndex)
            {
                return null;
            }

            if (FindLayerOf(key) is not KeyboardLayer layer)
            {
                return null;
            }

            var copy = entry.Canonical.Clone();

            copy.LayerIndex = layer.Index;
            copy.TriggerKey = key.TriggerKey.Code;

            return Place(copy, key, slot) ? RefreshTo(copy) : null;
        }

        /// <summary>
        /// Creates an independent second macro with the same content, named uniquely
        /// (<see cref="CreateUniqueName"/>), on the same key and layer as
        /// <see cref="MacroLibraryEntry.Canonical"/>: the next empty slot on a slot device, a new
        /// flat-list entry on the Advantage360. Returns null when there is no room — all five slots
        /// full, or a position that rejects macros.
        /// <para>
        /// The copy is always <b>explicitly named</b>, even when the original is not. A duplicate
        /// that stayed unnamed would derive the same name from the same content and be grouped back
        /// into the original on the next rebuild, i.e. the duplicate would vanish.
        /// </para>
        /// <para>
        /// A duplicate sits on the trigger its original does, with the same co-triggers, so it is a
        /// duplicate trigger by construction (06 §5) and <see cref="KeyboardLayout.Validate"/> says
        /// so. That is the honest report of a state the user asked for; move it with
        /// <see cref="AssignTo"/> and <see cref="Delete"/>.
        /// </para>
        /// </summary>
        public MacroLibraryEntry? Duplicate(MacroLibraryEntry entry)
        {
            EnsureOwned(entry);

            var source = entry.Sites[0];
            var copy = entry.Canonical.Clone();

            copy.Name = CreateUniqueName(entry.Name);
            copy.LayerIndex = source.LayerIndex;
            copy.TriggerKey = source.TriggerKeyCode;

            return Place(copy, source.Key, FirstEmptySlot) ? RefreshTo(copy) : null;
        }

        private static void CopyContent(Macro source, Macro target)
        {
            target.Name = source.Name;
            target.Speed = source.Speed;
            target.RepeatFrequency = source.RepeatFrequency;

            target.ClearKeystrokes();

            foreach (var keystroke in source.Keystrokes)
            {
                target.AddKeystroke(keystroke.Clone());
            }

            target.ClearCoTriggers();

            foreach (var coTrigger in source.CoTriggers)
            {
                target.AddCoTrigger(coTrigger);
            }
        }

        private bool Place(Macro macro, KeyboardKey? key, int slot)
        {
            if (UsesFlatMacroList)
            {
                if (slot != FlatListSlot)
                {
                    return false;
                }

                macro.MacroIndex = FlatListSlot;
                Layout.AddMacro(macro);

                return true;
            }

            if (key is null || !key.CanAssignMacro || !key.UsesMacroSlots)
            {
                return false;
            }

            if (slot == FirstEmptySlot)
            {
                return key.AssignMacro(macro) != 0;
            }

            if (key.GetMacro(slot) is not null)
            {
                return false;
            }

            key.SetMacro(slot, macro);

            return true;
        }

        private MacroLibraryEntry? RefreshTo(Macro macro)
        {
            Refresh();

            return FindByMacro(macro);
        }
    }
}
