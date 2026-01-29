SanabiLauncher is a fork of the launcher for SS14.

Features include:
- Patchloader, with mod template here https://github.com/LaCumbiaDelCoronavirus/TemplateSanabiMod
- Compatible with [Marseyloader](https://github.com/ValidHunters/Marseyloader) patches [(example here)](https://github.com/ValidHunters/SubversionExamplePatch)
- - Comes with built-in patches including but not limited to:
- - - HWId spoof patch
    - All-commands-enabled patch
    - Ahelp-menu-popup-disabled patch (todo: fix)
- - Support for externally-loaded mod assemblies (`.dll`s, see button to open patch directory somewhere in Sanabi tab), can be selectively loaded (maximum of 64 mods in directory)
  - Suspicious launcher-related assemblies (e.g. patch assemblies) are hidden from the game
- Gives you access to tent for camouflage against drones
- - Account tokens are updated only for individual accounts, and only when they are in use
  - You can use the launcher when logged-out as if you are logged in
  - Option to start the launcher from the login page if you don't want to fetch statuses of servers on favourites tab and hubserver for whatever reason
  - Allows you to change the default hub API used for the server tabs (however it defaults to the [wizden hub *mirror*](https://github.com/LaCumbiaDelCoronavirus/SanabiLauncher/blob/9d340ad0998191e7b3b7f21a19bca162e6679af9/SS14.Launcher/ConfigConstants.cs#L43-L49))
  - Per-account settings; you can have settings be different values for specific accounts
  - - Account seed:
    - - When the HWId spoofing patch is enabled and active, the randomly generated HWId uses this seed to stay the same if the seed is the same.
      - Also used for spoofing of the launcher fingerprint; the unique header which the launcher sends in every HTTP request, that can be used as a vector of detection.
  - Options to disable aforementioned launcher fingerprint

Info for mod development:
- Entrypoint
- - You must have a static class with no namespace in your main assembly. This is your entry-point class.
  - The entry class must have one of the following names, otherwise it won't be recognised:
  - - PatchEntry
    - ModEntry
    - MarseyEntry
  - The loader recognises two different optional main entry-point methods on the entry class:
  - - 1. Static method named `Entry` that takes a parameter of type `Dictionary<string, Assembly>`; this dictionary will be populated with every loaded game-assembly known by the launcher thus far, with the string being the assembly's name. This will normally include:
    - - - Content.Client
        - Content.Shared
        - Robust.Client
        - Robust.Shared
    - 2. Static method named `Entry` that takes no parameters
  - The loader recognises one more optional method, that being a static method named `AfterEntitySystemsLoaded` on the entry class that takes no parameters. This method is called immediately after every one of the game's `EntitySystem`s have finished loading. This is necessary as you can't resolve any entity systems before this point, otherwise an exception is thrown.
- The mod assembly is loaded like any other game assembly.
