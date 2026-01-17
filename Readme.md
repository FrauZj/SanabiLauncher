SanabiLauncher is a fork of the launcher for SS14.

Features include:
- Patchloader, with mod template here https://github.com/LaCumbiaDelCoronavirus/TemplateSanabiMod
- Compatible with [Marseyloader](https://github.com/ValidHunters/Marseyloader) patches [(example here)](https://github.com/ValidHunters/SubversionExamplePatch)
- - Comes with built-in patches including but not limited to:
- - - HWId spoof patch
    - All-commands-enabled patch
    - Ahelp-menu-popup-disabled patch (todo: fix)
- - Support for externally-loaded `.dll` patches (see button to open patch directory somewhere in settings), can be selectively loaded (maximum of 64 mods in directory)
  - Suspicious launcher-related assemblies (e.g. patch assemblies) are hidden from the game
- Gives you access to tent for camouflage against drones
- - Account tokens are updated only for individual accounts, and only when they are in use
  - You can use the launcher when logged-out as if you are logged in
  - Option to start the launcher from the login page, and defer *all* web API calls until off of the login page (this option exists if you are paranoid *and know what you're doing*)
  - Allows you to change the default hub API used for the server tabs (however it defaults to the [wizden hub *mirror*](https://cdn.spacestationmultiverse.com/wizden-hub-mirror/))
  - Per-account settings; you can have settings be different values for specific accounts
  - - HWId seed: when the HWId spoofing patch is enabled and active, the randomly generated HWId uses this seed to stay the same if the seed is the same.
  - Options to either disable or spoof (with a random value) the unique header which the launcher sends in every HTTP request, that can be used as a vector of detection.
