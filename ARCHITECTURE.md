# Eci Flow Connector 架构

插件以 UPM 包形式维护，业务代码不再放在项目的 `Assets` 中。编辑器入口只负责组合依赖，各功能按职责独立。

```text
Packages/net.eciflow.unity/
├─ Editor/
│  ├─ App/                         # 窗口入口、应用编排、共享会话
│  ├─ Domain/
│  │  ├─ Entries/                 # 词条标识与元数据
│  │  └─ Versioning/              # 版本及内容差异模型与比较规则
│  ├─ Features/
│  │  ├─ TableBrowser/            # Table 浏览与选择
│  │  ├─ EntryDetails/            # Char Limit、Comment 等详情
│  │  ├─ Screenshots/             # 截图与图片加载
│  │  ├─ Import/                  # 拉取、差异处理与合并流程
│  │  ├─ Export/                  # 推送流程
│  │  ├─ Highlighter/             # 运行时 UI 查找与标记
│  │  └─ Settings/                # 服务配置与高亮设置
│  ├─ Infrastructure/
│  │  ├─ Api/                     # HTTP 客户端及协议 DTO
│  │  ├─ Images/                  # 截图内存缓存
│  │  ├─ Localization/            # Unity Localization 数据访问
│  │  └─ Persistence/             # 本地元数据持久化
│  └─ UI/
│     ├─ Components/              # 可复用 UI Toolkit 组件
│     ├─ Styles/                  # 按功能拆分的 USS
│     └─ MainWindow.uxml          # 主窗口结构
├─ Runtime/                       # Play Mode 截图执行器
├─ Documentation~/               # 架构与使用文档
└─ package.json
```

## 依赖方向

`App` 负责创建对象并连接事件；`Features` 承担用例和界面行为；`Domain` 保存不依赖界面的规则与数据；`Infrastructure` 隔离网络、Unity Localization 和持久化细节；`UI` 只保存声明式布局、样式与通用控件。

新增功能应优先建立自己的 `Features/<FeatureName>` 目录。跨功能共享的业务规则进入 `Domain`，外部系统访问进入 `Infrastructure`，不要重新把逻辑堆回窗口类。

## 使用方式

从 `gitlab` 拉取到项目后，确保项目根目录在Unity项目的`Packages` 文件夹下即可使Unity加载到插件

## 演示场景

路径添加正确后在Unity的包管理器中能找到 `Eci Flow Connector` 点击 `Samples` 并导入演示场景。如果修改了演示场景，需要手动将演示场景所在的文件夹粘回到项目文件夹中的`Samples~`中

## 关于发布

如果包管理器中没有创建包或发布包的选项，可以尝试按包名添加包 `com.unity.upm.develop`