using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// Shared helpers for the model tests: key-table lookups by token and rendering of a layer's
    /// original/position/trigger identities as one space-separated string, so whole layers can be
    /// asserted against the listings of specs/05-key-model.md §4.
    /// </summary>
    internal static class ModelTokens
    {
        public static KeyDefinition Key(string token, TokenDialect dialect = TokenDialect.Gen1)
        {
            return KeyRegistry.FindByToken(token, dialect)
                   ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve in {dialect}.");
        }

        public static KeyDefinition KeyByCode(int code)
        {
            return KeyRegistry.FindByCode(code)
                   ?? throw new InvalidOperationException($"Key code {code} is not registered.");
        }

        public static KeyboardKey CreateKey(
            string defaultToken,
            TokenDialect dialect = TokenDialect.Gen1,
            string? positionToken = null,
            int index = 0,
            bool canEdit = true,
            bool canAssignMacro = true,
            bool supportsMultiModifiers = false,
            bool usesMacroSlots = true)
        {
            var position = new KeyPosition(index, defaultToken, positionToken, canEdit, canAssignMacro);

            return new KeyboardKey(
                position,
                Key(defaultToken, dialect),
                positionToken is null ? null : Key(positionToken, dialect),
                supportsMultiModifiers,
                usesMacroSlots);
        }

        public static string RenderOriginals(KeyboardLayer layer, TokenDialect dialect)
        {
            return Render(layer, key => key.OriginalKey.GetToken(dialect));
        }

        public static string RenderPositions(KeyboardLayer layer, TokenDialect dialect)
        {
            return Render(layer, key => key.PositionKey.GetToken(dialect));
        }

        public static string RenderTriggers(KeyboardLayer layer, TokenDialect dialect)
        {
            return Render(layer, key => key.TriggerKey.GetToken(dialect));
        }

        private static string Render(KeyboardLayer layer, Func<KeyboardKey, string> tokenSelector)
        {
            return string.Join(
                " ",
                layer.Keys.Select(key => tokenSelector(key) is { Length: > 0 } token ? token : "(none)"));
        }
    }
}
