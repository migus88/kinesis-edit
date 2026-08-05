using System.Collections.ObjectModel;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// The in-memory model of <c>active/pedals.txt</c> (specs/12-savant-elite.md §4): the seven
    /// programmable inputs, always all present, in the file order of <see cref="PedalInputs.All"/>.
    /// There are no layers, no lighting, and no remap-vs-macro distinction here (12 §7) — which is
    /// why the SE2 never builds a <c>KeyboardLayout</c>.
    /// </summary>
    public sealed class PedalConfiguration
    {
        /// <summary>The seven inputs in file/save order (12 §4.6).</summary>
        public IReadOnlyList<PedalInput> Inputs { get; }

        /// <summary>Whether no input carries an action (12 §4.1: a freshly created file).</summary>
        public bool IsEmpty
        {
            get
            {
                foreach (var input in Inputs)
                {
                    if (input.IsAssigned)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>The input with the given id; throws for <see cref="PedalInputId.None"/>.</summary>
        public PedalInput this[PedalInputId input] => Get(input);

        /// <summary>Creates a configuration with all seven inputs unassigned.</summary>
        public PedalConfiguration()
        {
            var inputs = new PedalInput[PedalInputs.Count];

            for (var i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new PedalInput(PedalInputs.All[i]);
            }

            Inputs = new ReadOnlyCollection<PedalInput>(inputs);
        }

        /// <summary>The input with the given id; throws for <see cref="PedalInputId.None"/>.</summary>
        public PedalInput Get(PedalInputId input)
        {
            var index = (int)input - 1;

            if (index < 0 || index >= Inputs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(input), input, "Not a programmable pedal input.");
            }

            return Inputs[index];
        }

        /// <summary>Clears every input (12 §5 "Clear").</summary>
        public void Clear()
        {
            foreach (var input in Inputs)
            {
                input.Clear();
            }
        }

        /// <summary>Deep copy — the editor's undo/cancel backup of the whole file (12 §5 step 1).</summary>
        public PedalConfiguration Clone()
        {
            var clone = new PedalConfiguration();

            for (var i = 0; i < Inputs.Count; i++)
            {
                var source = Inputs[i];

                if (source.Mode == PedalInputMode.Macro)
                {
                    clone.Inputs[i].SetMacro(source.Macro?.Clone());
                }
                else
                {
                    clone.Inputs[i].SetSingleAction(source.Action);
                }
            }

            return clone;
        }
    }
}
