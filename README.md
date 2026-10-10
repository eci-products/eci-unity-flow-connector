<p align="center">
  <img src="Documentation~/Images/flow-unity-connector-logo.png" alt="Flow Unity Connector logo" width="120">
</p>

<p align="center">
  English · <a href="README.zh-CN.md">中文</a>
</p>

# Flow Unity Connector

Flow Unity Connector is a Unity plugin designed to align content in Unity Localization with content in Flow projects.

With Flow Unity Connector, you can push in-game content that requires localization to the Flow platform, together with related configuration requirements, comments, and screenshots.

## Installation

### Before you begin

- Unity 2022.3 or later
- We recommend installing and initializing [Unity Localization](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html) in your project before installing the connector. The connector will automatically resolve `com.unity.localization` 1.5.3.
- Git installed locally and available as `git` from your terminal. This is required when installing with a Git URL.
- This repository does not provide a `.unitypackage` or GitHub Release. Do not use **Assets > Import Package**.

The Unity Package Manager resolves these dependencies automatically:

- `com.unity.localization` 1.5.3
- `com.unity.nuget.newtonsoft-json` 3.2.2

### Recommended: Package Manager + Git URL

1. Open your project with Unity 2022.3 or later.
2. Go to **Window > Package Manager**.
3. Select **+ > Add package from git URL...**.
4. Paste the following URL:

   ```text
   https://github.com/eci-products/eci-unity-flow-connector.git
   ```

5. Select **Add** and wait for Unity to finish compiling.
6. Open **Tools > ECI Flow Connector** to launch the window.

If you cannot find Flow Connector, restart Unity. For help, contact [flowsupport@ecinnovations.com](mailto:flowsupport@ecinnovations.com).

## Documentation

See [ARCHITECTURE.md](ARCHITECTURE.md) for the code structure and extension conventions.
