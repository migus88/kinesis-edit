using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// The programming of one pedal input (specs/12-savant-elite.md §1): "one single action (key
    /// or mouse button) or one macro". Mutable POCO like the rest of the runtime model — no
    /// change notification, no validation; the SE2 has no documented per-input limits.
    /// <para>
    /// <see cref="Mode"/>, <see cref="Action"/> and <see cref="Macro"/> are kept consistent by
    /// the setters: a null action or macro means the input is unassigned
    /// (<see cref="PedalInputMode.None"/>), which the serializer writes as <c>[input]&gt;</c>.
    /// </para>
    /// </summary>
    public sealed class PedalInput
    {
        /// <summary>Which of the seven inputs this is (12 §1).</summary>
        public PedalInputId Id { get; }

        /// <summary>Whether the input carries a single action, a macro, or nothing (12 §4.2).</summary>
        public PedalInputMode Mode { get; private set; }

        /// <summary>The single key/mouse action, non-null exactly when <see cref="Mode"/> is <see cref="PedalInputMode.Single"/>.</summary>
        public KeyDefinition? Action { get; private set; }

        /// <summary>The macro, non-null exactly when <see cref="Mode"/> is <see cref="PedalInputMode.Macro"/>; may be empty (an <c>{input}&gt;</c> line).</summary>
        public Macro? Macro { get; private set; }

        /// <summary>Whether the input actually fires something — an empty macro does not (12 §4.1).</summary>
        public bool IsAssigned => Mode switch
        {
            PedalInputMode.Single => Action is not null,
            PedalInputMode.Macro => Macro is { IsEmpty: false },
            _ => false
        };

        /// <summary>Creates an unassigned input.</summary>
        public PedalInput(PedalInputId input)
        {
            Id = input;
        }

        /// <summary>
        /// Puts the input in single-action mode with <paramref name="action"/>; a null action
        /// clears the input instead (12 §4.3: single mode writes <c>[token]</c>, empty when there
        /// is no key).
        /// </summary>
        public void SetSingleAction(KeyDefinition? action)
        {
            Mode = action is null ? PedalInputMode.None : PedalInputMode.Single;
            Action = action;
            Macro = null;
        }

        /// <summary>
        /// Puts the input in macro mode with <paramref name="macro"/>; a null macro clears the
        /// input. An empty macro is kept as macro mode — that is the <c>{input}&gt;</c> line.
        /// </summary>
        public void SetMacro(Macro? macro)
        {
            Mode = macro is null ? PedalInputMode.None : PedalInputMode.Macro;
            Action = null;
            Macro = macro;
        }

        /// <summary>Clears the input (12 §5 "Clear").</summary>
        public void Clear()
        {
            Mode = PedalInputMode.None;
            Action = null;
            Macro = null;
        }

        /// <summary>
        /// Deep copy — the backup the editor takes before an edit begins (12 §5 step 1, restored
        /// by Cancel). Key-table entries are immutable and stay shared; the macro is cloned.
        /// </summary>
        public PedalInput Clone()
        {
            return new PedalInput(Id)
            {
                Mode = Mode,
                Action = Action,
                Macro = Macro?.Clone()
            };
        }
    }
}
