using System.Globalization;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One line of <c>pedals.txt</c> that named an input but could not be applied
    /// (specs/12-savant-elite.md §4.2). Unparseable lines are shown to the user, never silently
    /// dropped — this is the row the pedal view lists them as.
    /// </summary>
    public sealed class PedalInvalidLineViewModel : ViewModelBase
    {
        /// <summary>Prefix of <see cref="LineCaption"/>.</summary>
        public const string LineCaptionPrefix = "Line ";

        /// <summary>1-based line number in the loaded file.</summary>
        public int LineNumber { get; }

        /// <summary>The line number as the view shows it.</summary>
        public string LineCaption { get; }

        /// <summary>Caption of the input the line addressed; empty when it names none.</summary>
        public string InputName { get; }

        /// <summary>The line exactly as it appears in the file.</summary>
        public string Text { get; }

        /// <summary>Creates the row for <paramref name="line"/>.</summary>
        public PedalInvalidLineViewModel(PedalInvalidLine line)
        {
            ArgumentNullException.ThrowIfNull(line);

            LineNumber = line.LineNumber;
            LineCaption = LineCaptionPrefix + line.LineNumber.ToString(CultureInfo.InvariantCulture);
            InputName = line.Input == PedalInputId.None ? string.Empty : PedalInputs.GetDisplayName(line.Input);
            Text = line.Text;
        }
    }
}
