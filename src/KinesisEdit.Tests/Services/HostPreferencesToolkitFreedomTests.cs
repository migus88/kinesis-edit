using System.Reflection;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// app-shell.md invariant 8, for this module: the host-preferences model, its store, its file
    /// engine and its path seam are <b>toolkit-free</b>, and only the two appliers touch Avalonia.
    /// <para>
    /// It is asserted rather than remembered because the temptation is real — a
    /// <c>ThemeVariant</c> on the model would be one line and would make the whole store
    /// untestable without a UI runtime, and it would put a preference file's schema at the mercy of
    /// a toolkit type's name.
    /// </para>
    /// </summary>
    public class HostPreferencesToolkitFreedomTests
    {
        private const string ToolkitAssemblyPrefix = "Avalonia";

        public static TheoryData<Type> ToolkitFreeTypes =>
        [
            typeof(HostPreferences),
            typeof(WindowGeometry),
            typeof(AppThemePreference),
            typeof(MotionPreference),
            typeof(IHostPreferencesStore),
            typeof(JsonHostPreferencesStore),
            typeof(HostPreferencesParser),
            typeof(HostPreferencesSerializer),
            typeof(IHostPreferencesPathProvider),
            typeof(HostPreferencesPathProvider),
            typeof(IMotionSettings),
            typeof(MotionSettings)
        ];

        [Theory]
        [MemberData(nameof(ToolkitFreeTypes))]
        public void Type_ReferencesNoToolkitType(Type type)
        {
            var offenders = ReferencedTypes(type)
                .Where(IsToolkitType)
                .Select(referenced => referenced.FullName ?? referenced.Name)
                .Distinct()
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"{type.Name} references Avalonia: {string.Join(", ", offenders)}");
        }

        [Fact]
        public void TheAppliers_AreTheOnlyOnesThatTouchAvalonia()
        {
            // Stated positively so the exception is deliberate rather than a gap: these two are the
            // bridging services invariant 8 permits, and they exist precisely so nothing else has
            // to be one.
            Assert.Contains(ReferencedTypes(typeof(ThemeApplier)), IsToolkitType);
            Assert.Contains(ReferencedTypes(typeof(MotionPreferenceApplier)), IsToolkitType);
        }

        private static bool IsToolkitType(Type type)
        {
            var assembly = type.Assembly.GetName().Name;

            return assembly is not null && assembly.StartsWith(ToolkitAssemblyPrefix, StringComparison.Ordinal);
        }

        private static IReadOnlyList<Type> ReferencedTypes(Type type)
        {
            const BindingFlags Everything = BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.DeclaredOnly;

            var referenced = new List<Type>();

            if (type.BaseType is not null)
            {
                referenced.Add(type.BaseType);
            }

            referenced.AddRange(type.GetInterfaces());

            foreach (var constructor in type.GetConstructors(Everything))
            {
                referenced.AddRange(constructor.GetParameters().Select(parameter => parameter.ParameterType));
            }

            foreach (var method in type.GetMethods(Everything))
            {
                referenced.Add(method.ReturnType);
                referenced.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
            }

            foreach (var property in type.GetProperties(Everything))
            {
                referenced.Add(property.PropertyType);
            }

            foreach (var field in type.GetFields(Everything))
            {
                referenced.Add(field.FieldType);
            }

            foreach (var declaredEvent in type.GetEvents(Everything))
            {
                if (declaredEvent.EventHandlerType is not null)
                {
                    referenced.Add(declaredEvent.EventHandlerType);
                }
            }

            return referenced.SelectMany(Flatten).ToArray();
        }

        /// <summary>
        /// A type plus everything inside it — <c>Func&lt;Avalonia.Something&gt;</c> hides its
        /// toolkit type in a generic argument, and an array hides it in its element type.
        /// </summary>
        private static IEnumerable<Type> Flatten(Type type)
        {
            yield return type;

            if (type.HasElementType && type.GetElementType() is { } element)
            {
                foreach (var inner in Flatten(element))
                {
                    yield return inner;
                }
            }

            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var inner in Flatten(argument))
                {
                    yield return inner;
                }
            }
        }
    }
}
