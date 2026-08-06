using Avalonia.Input;
using KinesisEdit.Input;

using NavigationDirection = KinesisEdit.Core.Geometry.Visual.NavigationDirection;

namespace KinesisEdit.Tests.Input
{
    /// <summary>
    /// The editor's keyboard grammar as a table (docs/design/mockups.md <c>2b</c>): the whole
    /// shortcut set under <b>both</b> platform mappings, the strictness that keeps ⌘⇧S from
    /// reading as ⌘S, and the two projections the view dispatches through.
    /// <para>
    /// Plain <c>[Fact]</c>/<c>[Theory]</c> deliberately: <see cref="EditorShortcuts"/> touches
    /// nothing but its arguments, so no Avalonia runtime is involved — and
    /// <c>isMacOs</c> is a parameter precisely so the Windows half is exercised on a Mac and the
    /// macOS half in CI.
    /// </para>
    /// </summary>
    public class EditorShortcutsTests
    {
        public static TheoryData<Key, EditorShortcut> Arrows()
        {
            return new TheoryData<Key, EditorShortcut>
            {
                { Key.Up, EditorShortcut.MoveUp },
                { Key.Down, EditorShortcut.MoveDown },
                { Key.Left, EditorShortcut.MoveLeft },
                { Key.Right, EditorShortcut.MoveRight }
            };
        }

        public static TheoryData<Key, EditorShortcut> LayerKeys()
        {
            return new TheoryData<Key, EditorShortcut>
            {
                { Key.D1, EditorShortcut.SelectLayer1 },
                { Key.D2, EditorShortcut.SelectLayer2 },
                { Key.D3, EditorShortcut.SelectLayer3 },
                { Key.D4, EditorShortcut.SelectLayer4 },
                { Key.D5, EditorShortcut.SelectLayer5 }
            };
        }

        public static TheoryData<Key> ArrowKeys()
        {
            return [Key.Up, Key.Down, Key.Left, Key.Right];
        }

        public static TheoryData<Key> CommandLetters()
        {
            return [Key.F, Key.S, Key.W];
        }

        public static TheoryData<Key, EditorShortcut> CommandKeys()
        {
            return new TheoryData<Key, EditorShortcut>
            {
                { Key.F, EditorShortcut.FocusSearch },
                { Key.S, EditorShortcut.Save },
                { Key.W, EditorShortcut.GoHome }
            };
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CommandModifier_IsMetaOnMacOsAndControlEverywhereElse(bool isMacOs)
        {
            // handoff.md § Interactions: "map ⌘→Ctrl and ⌥→Alt on Windows/Linux".
            Assert.Equal(
                isMacOs ? KeyModifiers.Meta : KeyModifiers.Control,
                EditorShortcuts.CommandModifier(isMacOs));
        }

        [Theory]
        [MemberData(nameof(Arrows))]
        public void Map_AnUnmodifiedArrow_IsAMove(Key key, EditorShortcut expected)
        {
            Assert.Equal(expected, EditorShortcuts.Map(key, KeyModifiers.None, isMacOs: true));
            Assert.Equal(expected, EditorShortcuts.Map(key, KeyModifiers.None, isMacOs: false));
        }

        [Theory]
        [MemberData(nameof(ArrowKeys))]
        public void Map_AnArrowWithAnyModifier_IsNotAMove(Key key)
        {
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(key, KeyModifiers.Shift, isMacOs: true));
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(key, KeyModifiers.Alt, isMacOs: true));
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(key, KeyModifiers.Meta, isMacOs: true));
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(key, KeyModifiers.Control, isMacOs: false));
        }

        [Theory]
        [MemberData(nameof(LayerKeys))]
        public void Map_AltAndADigit_SelectsThatLayerOnEveryPlatform(Key key, EditorShortcut expected)
        {
            // ⌥ is Alt on every platform — Alt *is* Option on macOS — so only the spelling on the
            // layer chip changes, never which physical key the grammar names.
            Assert.Equal(expected, EditorShortcuts.Map(key, KeyModifiers.Alt, isMacOs: true));
            Assert.Equal(expected, EditorShortcuts.Map(key, KeyModifiers.Alt, isMacOs: false));
        }

        [Fact]
        public void Map_AltAndADigitPastFive_IsNotInTheGrammar()
        {
            Assert.Equal(5, EditorShortcuts.MaxLayerNumber);
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(Key.D6, KeyModifiers.Alt, isMacOs: true));
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(Key.D0, KeyModifiers.Alt, isMacOs: true));
        }

        [Fact]
        public void Map_ADigitWithoutAlt_IsNotALayerShortcut()
        {
            // The digits are ordinary typing; only ⌥ makes them a layer jump.
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(Key.D1, KeyModifiers.None, isMacOs: true));
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(Key.D1, KeyModifiers.Meta, isMacOs: true));
        }

        [Theory]
        [MemberData(nameof(CommandKeys))]
        public void Map_TheCommandKeys_FollowThePlatformMapping(Key key, EditorShortcut expected)
        {
            Assert.Equal(expected, EditorShortcuts.Map(key, KeyModifiers.Meta, isMacOs: true));
            Assert.Equal(expected, EditorShortcuts.Map(key, KeyModifiers.Control, isMacOs: false));
        }

        [Theory]
        [MemberData(nameof(CommandLetters))]
        public void Map_TheCommandKeys_IgnoreTheOtherPlatformsModifier(Key key)
        {
            // Ctrl+S on a Mac belongs to whatever else wants it, and Meta (the Windows key) is not
            // an app accelerator on Windows or Linux.
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(key, KeyModifiers.Control, isMacOs: true));
            Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(key, KeyModifiers.Meta, isMacOs: false));
        }

        [Theory]
        [MemberData(nameof(CommandLetters))]
        public void Map_AnExtraModifier_IsNotTheShortcut(Key key)
        {
            // The set is matched exactly, never masked: ⌘⇧S is not ⌘S. A shortcut that fires on a
            // superset steals combinations the user meant for something else.
            Assert.Equal(
                EditorShortcut.None,
                EditorShortcuts.Map(key, KeyModifiers.Meta | KeyModifiers.Shift, isMacOs: true));
            Assert.Equal(
                EditorShortcut.None,
                EditorShortcuts.Map(key, KeyModifiers.Meta | KeyModifiers.Alt, isMacOs: true));
            Assert.Equal(
                EditorShortcut.None,
                EditorShortcuts.Map(key, KeyModifiers.Control | KeyModifiers.Shift, isMacOs: false));
        }

        [Theory]
        [InlineData(Key.A)]
        [InlineData(Key.Escape)]
        [InlineData(Key.Tab)]
        [InlineData(Key.Enter)]
        [InlineData(Key.Back)]
        [InlineData(Key.Space)]
        public void Map_AKeyOutsideTheTable_IsNone(Key key)
        {
            // Escape most of all: it is a remappable key, not a shortcut (keyboard-editor.md,
            // invariant 6), so it must never come out of this table.
            foreach (var modifiers in new[] { KeyModifiers.None, KeyModifiers.Alt, KeyModifiers.Meta, KeyModifiers.Control })
            {
                Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(key, modifiers, isMacOs: true));
                Assert.Equal(EditorShortcut.None, EditorShortcuts.Map(key, modifiers, isMacOs: false));
            }
        }

        [Fact]
        public void ToDirection_MapsTheFourMovesAndNothingElse()
        {
            Assert.Equal(NavigationDirection.Up, EditorShortcuts.ToDirection(EditorShortcut.MoveUp));
            Assert.Equal(NavigationDirection.Down, EditorShortcuts.ToDirection(EditorShortcut.MoveDown));
            Assert.Equal(NavigationDirection.Left, EditorShortcuts.ToDirection(EditorShortcut.MoveLeft));
            Assert.Equal(NavigationDirection.Right, EditorShortcuts.ToDirection(EditorShortcut.MoveRight));

            // Never `None` for a move: KeyAdjacency.Next throws on it.
            foreach (var shortcut in Enum.GetValues<EditorShortcut>())
            {
                var isMove = shortcut is EditorShortcut.MoveUp or EditorShortcut.MoveDown
                    or EditorShortcut.MoveLeft or EditorShortcut.MoveRight;

                Assert.Equal(isMove, EditorShortcuts.ToDirection(shortcut) != NavigationDirection.None);
            }
        }

        [Fact]
        public void ToLayerNumber_IsOneToFiveForTheLayerShortcutsAndZeroOtherwise()
        {
            Assert.Equal(1, EditorShortcuts.ToLayerNumber(EditorShortcut.SelectLayer1));
            Assert.Equal(5, EditorShortcuts.ToLayerNumber(EditorShortcut.SelectLayer5));
            Assert.Equal(0, EditorShortcuts.ToLayerNumber(EditorShortcut.Save));
            Assert.Equal(0, EditorShortcuts.ToLayerNumber(EditorShortcut.MoveLeft));
            Assert.Equal(0, EditorShortcuts.ToLayerNumber(EditorShortcut.None));
        }

        [Fact]
        public void TheGrammar_HasExactlyTheTwelveIntentsTheDesignLists()
        {
            // 2b's table verbatim: four arrows, five layer jumps, ⌘F, ⌘S, ⌘W. A thirteenth intent
            // is a design change, not a refactor.
            Assert.Equal(13, Enum.GetValues<EditorShortcut>().Length);
        }
    }
}
