# Coding Conventions

The purpose of this document is to provide guidelines for Naming Conventions and Coding Style of this project.

## Naming

- Use Pascal case (e.g. `ExampleDeviceController`, `MaxHealth`, etc.) unless noted otherwise
- Use camel case (e.g. `exampleDeviceController`, `maxHealth`, etc.) for local variables, parameters
- Use camel case (e.g. `exampleDeviceController`, `maxHealth`, etc.) with `_` prefix for private variables
- Avoid [snake_case](https://en.wikipedia.org/wiki/Snake_case), [kebab-case](https://www.theserverside.com/definition/Kebab-case) and [Hungarian Notations](https://en.wikipedia.org/wiki/Hungarian_notation)
- The file name must match the name of the main type it contains

## Formatting

- Use [Allman](https://en.wikipedia.org/wiki/Indentation_style#Allman_style) style braces
- Use a single space before flow control conditions, e.g. `while (x == y)` and not `while(x == y)`
- Avoid spaces inside brackets, e.g. `x = dataArray[index]` and not `x = dataArray[ index ]`
- Use a single space after a comma between function arguments, e.g. `Invoke(param1, param2);` and not `Invoke(param1,param2);`
- Don’t add a space after the parenthesis and function arguments, e.g. `CollectItem(myObject, 0, 1);` and not `CollectItem( myObject, 0, 1 );`
- Use vertical spacing (extra blank line) for visual separation.

## Ordering

### Order of access modifiers

1. Public
2. Protected
3. Private

This means that in a Properties section, we will first display the public, then the protected and then the private properties

### Order of sections in *.cs file

1. Usings
2. Namespace
3. Class/Interface/Struct/Record declaration
4. Constants
5. Static Members
6. Properties
7. Events
8. Fields
9. Readonly Fields
10. Constructors
11. Public Methods
12. Private Methods
13. `Dispose` methods
14. Destructor
15. Child classes, structs, enums, etc.

## Fields and encapsulation

1. No public fields allowed — expose state through properties.

## Class Example

```csharp
// - Organize your usings and remove those that are not in use
using System;
using System.Collections.Generic;

// NAMESPACES:
// - Pascal case, without special symbols or underscores.
// - Add using line at the top to avoid typing namespace repeatedly.
// - Create sub-namespaces with the dot (.) operator, e.g. MyApplication.Devices, MyApplication.Parsing, etc.
// - Namespace should correspond to default assembly namespace followed by folder structure
namespace StyleSheetExample
{

    // ENUMS:
    // - Use a singular type name.
    // - No prefix or suffix.
    // - Assign numeric values to all enums (keeps persisted/serialized values stable)
    // - Leave 0 index in Enums for values like None/Empty/etc.
    public enum Direction
    {
        North = 1,
        South = 2,
        East = 3,
        West = 4,
    }

    // FLAGS ENUMS:
    // - Use a plural type name
    // - No prefix or suffix.
    // - Use bit shifting values in Flags Enums
    [Flags]
    public enum AttackModes
    {
        None = 0,
        Melee = 1 << 0,
        Ranged = 1 << 1,
        Special = 1 << 2,

        MeleeAndSpecial = Melee | Special
    }

    // INTERFACES:
    // - Use the 'I' prefix.
    public interface IDamageable
    {
        string DamageTypeName { get; }
        float DamageValue { get; }

        // METHODS:
        // - Start a methods name with a verbs or verb phrases to show an action.
        // - Parameter names are camelCase.
        bool ApplyDamage(string description, float damage, int numberOfHits);
    }

    public interface IDamageable<T> : IDamageable
    {
        void Damage(T damageTaken);
    }

    // CLASSES or STRUCTS:
    // - Name them with nouns or noun phrases.
    // - Avoid prefixes.
    // - The source file name must match the main type it contains.
    public class StyleExample
    {

        // PROPERTIES:
        // - Preferable to a public field.
        // - Pascal case, without special characters.
        // - Use the Auto-Implemented Property for a public property without a backing field.

        // the private backing field
        private int _maxHealth;

        // read-only, returns backing field
        public int MaxHealthReadOnly => _maxHealth;

        // equivalent to:
        // public int MaxHealth { get; private set; }

        // explicitly implementing getter and setter
        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        // auto-implemented property without backing field
        public string DescriptionName { get; set; } = "Fireball";

        // FIELDS:
        // - Avoid special characters (backslashes, symbols, Unicode characters); these can interfere with command line tools.
        // - Use nouns for names, but prefix booleans with a verb.
        // - Use meaningful names. Make names searchable and pronounceable. Don’t abbreviate (unless it’s math).
        // - Fields are private; use camel case with an underscore (_) prefix to differentiate from local variables.
        private int _elapsedTimeInDays;

        // Booleans ask a question that can be answered true or false.
        private bool _isConnectionLost;

        // EVENTS:
        // - Name with a verb phrase.
        // - Present participle means "before" and past participle mean "after."
        // - Use System.Action delegate for most events (can take 0 to 16 parameters).
        // - Event name should describe the event and handling should append the `On` prefix
        // - e.g. event/action = "DoorOpened" and event handling method = "OnDoorOpened"

        // event before
        public event Action OpeningDoor;

        // event after
        public event Action DoorOpened;

        public event Action<int> PointsScored;
        public event Action<CustomEventArgs> ThingHappened;

        // This is a custom EventArg made from a struct.
        public struct CustomEventArgs
        {
            public int ObjectId { get; }
            public string Description { get; }

            public CustomEventArgs(int objectId, string description)
            {
                this.ObjectId = objectId;
                this.Description = description;
            }
        }

        // METHODS:
        // - Start a methods name with a verbs or verb phrases to show an action.
        // - Parameter names are camel case.

        // Methods start with a verb.
        public void SetInitialHealth(int maxHealth)
        {
            _maxHealth = maxHealth;
        }

        // Methods ask a question when they return bool.
        public bool IsNewDescription(string newDescription)
        {
            return DescriptionName == newDescription;
        }

        private void FormatExamples(int someExpression)
        {
            // VAR:
            // - Use var if it helps readability, especially with long type names.
            // - Avoid var if it makes the type ambiguous.
            var powerUps = new List<PlayerStats>();
            var dict = new Dictionary<string, List<PlayerStats>>();

            // BRACES:
            // - Always use braces
            // - Never use a single line statement
            for (int i = 0; i < 100; i++)
            {
                DoSomething(i);
            }

            // Don't remove the braces here.
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    DoSomething(j);
                }
            }
        }

        private void DoSomething(int x)
        {
            // ..
        }
    }

    // OTHER CLASSES:
    // - Define as many other helper classes in your file as needed,
    //   as long as the file stays focused on one main type.
    public struct PlayerStats
    {
        public int MovementSpeed { get; set; }
        public int HitPoints { get; set; }
        public bool HasHealthPotion { get; set; }
    }

}

```
