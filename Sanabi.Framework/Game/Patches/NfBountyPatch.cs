using Sanabi.Framework.Game.Managers;
using HarmonyLib;
using Sanabi.Framework.Patching;
using System.Reflection;

namespace Sanabi.Framework.Game.Patches;

/// <summary>
///     On frontier-forks: removes clientside restrictions on making bounties.
/// </summary>
public static class NfBountyPatch
{
    [PatchEntry(PatchRunLevel.Content)]
    public static void Patch()
    {
        if (!ReflectionManager.TryGetTypeByQualifiedName("Content.Client._NF.BountyContracts.UI.BountyContractUiFragmentCreate", out var uiClassType))
            return;

        PatchHelpers.PatchMethod(
            uiClassType,
            "UpdateDisclaimer",
            Prefix,
            HarmonyPatchType.Prefix
        );
    }

    private static bool Prefix(ref object __instance)
    {
        var createButton = __instance.GetType().GetProperty("CreateButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(__instance);
        var createButtonDisabled = createButton!.GetType().GetProperty("Disabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        createButtonDisabled!.SetValue(createButton, false);

        return false;
    }
}
