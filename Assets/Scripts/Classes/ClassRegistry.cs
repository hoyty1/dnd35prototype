using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry for all character class definitions.
/// Provides lookup by class name and exposes the full list of available classes.
/// New classes can be added by simply creating a new ICharacterClass implementation
/// and registering it in the Init() method.
/// </summary>
public static class ClassRegistry
{
    private static Dictionary<string, ICharacterClass> _classes = new Dictionary<string, ICharacterClass>();
    private static List<ICharacterClass> _classList = new List<ICharacterClass>();
    private static bool _initialized = false;

    /// <summary>All registered class names in registration order.</summary>
    public static string[] ClassNames { get; private set; } = new string[0];

    /// <summary>
    /// Initialize the registry with all available character classes.
    /// Safe to call multiple times — only initializes once.
    /// To add a new class, simply instantiate it and call Register() here.
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        Register(new FighterClass());
        Register(new RogueClass());
        Register(new MonkClass());
        Register(new BarbarianClass());
        Register(new WizardClass());
        Register(new ClericClass());

        ClassNames = new string[_classList.Count];
        for (int i = 0; i < _classList.Count; i++)
            ClassNames[i] = _classList[i].ClassName;

        Debug.Log($"[ClassRegistry] Initialized with {_classList.Count} classes: {string.Join(", ", ClassNames)}");
    }

    /// <summary>Register a character class definition.</summary>
    private static void Register(ICharacterClass classDef)
    {
        if (_classes.ContainsKey(classDef.ClassName))
        {
            Debug.LogWarning($"[ClassRegistry] Duplicate class registration: {classDef.ClassName}");
            return;
        }
        _classes[classDef.ClassName] = classDef;
        _classList.Add(classDef);
    }

    /// <summary>
    /// Get a class definition by name. Returns null if not found.
    /// </summary>
    public static ICharacterClass GetClass(string className)
    {
        Init();
        if (_classes.TryGetValue(className, out ICharacterClass classDef))
            return classDef;
        Debug.LogWarning($"[ClassRegistry] Class not found: {className}");
        return null;
    }

    /// <summary>
    /// Get all registered class definitions in order.
    /// </summary>
    public static List<ICharacterClass> GetAllClasses()
    {
        Init();
        return new List<ICharacterClass>(_classList);
    }

    /// <summary>
    /// Get the number of registered classes.
    /// </summary>
    public static int Count
    {
        get
        {
            Init();
            return _classList.Count;
        }
    }

    /// <summary>
    /// Get a class definition by index (registration order).
    /// </summary>
    public static ICharacterClass GetClassByIndex(int index)
    {
        Init();
        if (index >= 0 && index < _classList.Count)
            return _classList[index];
        Debug.LogWarning($"[ClassRegistry] Class index out of range: {index}");
        return null;
    }
}
