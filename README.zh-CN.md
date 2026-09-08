<p align="center">
  <img src="Documentation~/Images/flow-unity-connector-logo.png" alt="Flow Unity Connector Logo" width="120">
</p>

<p align="center">
  <a href="README.md">English</a> · 中文
</p>

# Flow Unity Connector（中文）

Flow Unity Connector 是一款 Unity 插件，用于对齐 Unity Localization 中的内容与 Flow 项目中的内容。

借助 Flow Unity Connector，您可以将游戏中需要本地化的内容推送到 Flow 平台，并一并提交相关配置要求、备注和截图。

## 安装方式

### 安装前准备

- Unity 2022.3 或更高版本
- 建议先在项目中安装并初始化 [Unity Localization](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html)。插件会自动拉取 `com.unity.localization` 1.5.3。
- 本机已安装 Git，且可以在终端中直接运行 `git`。通过 Git URL 安装时必需。
- 本仓库不提供 `.unitypackage` 或 GitHub Release，请勿使用 **Assets > Import Package**。

以下依赖会由 Unity Package Manager 自动解析：

- `com.unity.localization` 1.5.3
- `com.unity.nuget.newtonsoft-json` 3.2.2

### 推荐方式：Package Manager + Git URL

1. 使用 Unity 2022.3 或更高版本打开项目。
2. 选择菜单 **Window > Package Manager**。
3. 点击左上角 **+ > Add package from git URL...**。
4. 粘贴以下地址：

   ```text
   https://github.com/eci-products/eci-unity-flow-connector.git
   ```

5. 点击 **Add**，等待 Unity 编译完成。
6. 选择菜单 **Tools > ECI Flow Connector** 打开窗口。

如果找不到 Flow Connector，请尝试重启 Unity。如有疑问，请联系 [flowsupport@ecinnovations.com](mailto:flowsupport@ecinnovations.com)。

## 文档

代码结构和扩展约定见 [ARCHITECTURE.md](ARCHITECTURE.md)。
