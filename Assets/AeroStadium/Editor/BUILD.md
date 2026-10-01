Unity 6000.3.25f1 (URP 17.3.0) is the reference editor for this prototype.

Use the AeroStadium menu in Unity, or invoke these static methods in batch mode:

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$projectDirectory = (Resolve-Path '.').Path
.\Tools\build.ps1 -Action Prepare -UnityEditor $unityEditor
.\Tools\build.ps1 -Action Validation -UnityEditor $unityEditor
.\Tools\build.ps1 -UnityEditor $unityEditor
```

Run from the AeroStadium project directory. Close its editor before another batch instance uses the same project.

The wrapper waits for the Unity editor process explicitly, avoiding both a detached GUI command and waiting indefinitely on persistent child services.

Preparation imports the local Pokémon models through LocalModelImporter, then creates the Arena scene and URP assets without replacing an existing scene. Runtime presentation is provided by GameBootstrap. Input handling uses Both to support the new Input System and editor templates.

Validation loads Resources/Data/catalog.json and checks it with Catalog.Validate(). Windows builds run preparation and validation first, then produce Builds/Windows/AeroStadium.exe using the Mono backend. The executable needs its neighbouring data and Unity files.

Results are recorded in Builds/Reports/catalog-validation.json and Builds/Reports/build-windows.json. A failed validation or build throws an exception, which causes Unity's batch invocation to fail. Consult the corresponding Unity log for compiler and build diagnostics.
