using System.Reflection;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// app-shell.md invariant 8, for this module: the demo drive, its fixture reader, its file
    /// service and its device provider are <b>toolkit-free</b>.
    /// <para>
    /// It is asserted rather than remembered because the shortcut is right there: the fixtures are
    /// assets, and Avalonia's <c>AssetLoader</c> over an <c>AvaloniaResource</c> would read them in
    /// one line. It would also make a service that <see cref="Core.Profiles.ProfileSession"/> runs
    /// on depend on a UI toolkit, and put the demo drive out of reach of every test that does not
    /// boot one. <see cref="Assembly.GetManifestResourceStream(string)"/> is the whole reason the
    /// fixtures are <c>EmbeddedResource</c> items.
    /// </para>
    /// </summary>
    public class DemoVDriveToolkitFreedomTests
    {
        private const string ToolkitAssemblyPrefix = "Avalonia";

        public static TheoryData<Type> ToolkitFreeTypes =>
        [
            typeof(DemoVDrive),
            typeof(DemoVDriveFixtures),
            typeof(DemoVDriveFileService),
            typeof(DemoVDriveWriteException),
            typeof(IDemoDeviceProvider),
            typeof(DemoDeviceProvider)
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
        public void TheFixtures_AreReadableWithNoUiRuntime()
        {
            // The behavioural half of the assertion above: this test class carries no
            // [AvaloniaFact], so if reading a fixture needed a UI runtime it could not run here.
            Assert.NotEmpty(DemoVDriveFixtures.Default.Paths);
            Assert.NotEmpty(DemoVDriveFixtures.Default.ReadLines(DemoVDriveFixtures.Default.Paths.First()));
        }

        private static bool IsToolkitType(Type type)
        {
            var assembly = type.Assembly.GetName().Name;

            return assembly is not null && assembly.StartsWith(ToolkitAssemblyPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Every type reachable from <paramref name="type"/>'s declared surface — base, interfaces,
        /// constructors, methods, properties and fields — flattened through generic arguments and
        /// element types, since a <c>Func&lt;Avalonia.Something&gt;</c> hides its toolkit type
        /// inside one. Mirrors <see cref="HostPreferencesToolkitFreedomTests"/>, whose walk this is.
        /// </summary>
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

            return referenced.SelectMany(Flatten).ToArray();
        }

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
