using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Sanabi.Framework.Game.Managers;

/// <summary>
///     Used to reflect on your actions.
/// </summary>
public static class ReflectionManager
{
    /// <summary>
    ///     Cache of types by their fully qualified type name
    ///         and actual existing type.
    /// </summary>
    private static Dictionary<string, Type> _cachedTypes = new();

    /// <summary>
    ///     Tries to get a type from it's fully qualified type-name.
    ///         Caches type for future calls if found.
    /// </summary>
    /// <param name="qualifiedTypeName">Fully qualified type-name of the specified type; includes it's assembly and namespace.</param>
    /// <returns>True if the type was found.</returns>
    public static bool TryGetTypeByQualifiedName(string qualifiedTypeName, [MaybeNullWhen(false)] out Type type)
    {
        if (_cachedTypes.TryGetValue(qualifiedTypeName, out type))
            return true;

        var typePrefixAssembly = ExtractTypePrefix(qualifiedTypeName);
        if (AssemblyManager.TryGetAssembly(typePrefixAssembly, out var assembly) &&
            assembly.GetType(qualifiedTypeName) is { } foundType)
        {
            _cachedTypes[qualifiedTypeName] = type = foundType;
            return true;
        }

        type = null;
        return false;
    }

    /// <summary>
    ///     Returns the type from it's fully qualified type-name.
    ///         Throws if not possible. Caches type for future calls if found.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the type didn't exist in the cache and could not be found via <see cref="AssemblyManager"/>.</exception>
    /// <inheritdoc cref="TryGetTypeByQualifiedName(string, out Type)"/> // inherit param
    public static Type GetTypeByQualifiedName(string qualifiedTypeName)
    {
        ref var cachedTypeRef = ref CollectionsMarshal.GetValueRefOrAddDefault(_cachedTypes, qualifiedTypeName, out var exists);

        if (!exists)
        {
            var typePrefixAssembly = ExtractTypePrefix(qualifiedTypeName);
            if (!AssemblyManager.TryGetAssembly(typePrefixAssembly, out var assembly))
                throw new InvalidOperationException($"Couldn't locate qualified type \"{qualifiedTypeName}\" in assembly {typePrefixAssembly}!");

            cachedTypeRef = assembly.GetType(qualifiedTypeName);
        }

        return cachedTypeRef!;
    }

    /// <summary>
    ///     Given something like `Content.Client.Admin`,
    ///         this returns `Content.Client`.
    /// </summary>
    private static string ExtractTypePrefix(string path)
    {
        const char separator = '.';

        var split = path.Split(separator);
        return split[0] + separator + split[1];
    }
}
