# Demo 初始化

1. 安装 FlowConnector 及其 Localization/Addressables 依赖。
2. 在 Package Manager 中导入 Demo Scene Sample，等待编译完成。
3. 退出 Play Mode，执行 `Tools > ECI Flow Connector > Initialize Demo`。
4. 打开 `FLowDemoScene.unity` 并运行。

初始化会补齐语言注册、表集合关联（含西班牙语表）以及语言表和 Shared Data 的 Addressables 注册。重复执行可修复注册，不会重复添加相同语言。

项目没有 Localization Settings 时启用示例 Settings，并设置 en-US 后备语言；已有设置时保留其语言选择策略。若已有同名的其他表集合，初始化将报告冲突，请先处理冲突，不要删除用户数据。

工具使用导入后的脚本位置定位资源。仅安装包而未导入 Sample 时没有此菜单。若存在多份 Sample，先在 Project 中选择目标 Demo 内的资源；多份同名程序集可能导致编译冲突，因此更新 Sample 前应移走旧副本（先保存自己的修改）。

工具不会修改 Addressables Profiles、构建路径或 Play Mode Script。使用 Use Existing Build 或构建游戏时，需要自行构建目标平台的 Addressables。编辑器快速验证可使用 Use Asset Database。

当前使用 Unity 2022.3.62f3c1 / Localization 1.5.3 的程序集完成编译验证，尚未完成新项目运行验证或其他版本测试，不声明支持所有版本。初始化只验证资源注册，不替代场景运行和 UI 绑定测试。
