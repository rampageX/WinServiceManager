# WinServiceManager

轻量 Windows 服务管理器，基于 .NET Framework 4.8 / WinForms，无第三方 UI 依赖。

## 本版功能（v1.1 预览）

- 参考 Film Grain Studio 配色的全暗黑界面（包含 Windows 10/11 深色标题栏、列表和右键菜单）
- 同时显示服务名称、显示名称、状态
- 点击任意列标题切换升序/降序；刷新后保留排序、多选与滚动位置
- 双击服务或右键“查看详情”，查看描述、启动类型、可执行路径、登录账户、依赖关系
- 详情页只允许编辑“显示名称”和标准 Windows 服务的“启动类型”；其他字段只读
- 启动类型下拉框、箭头和全部弹出项均为暗色自绘，四个选项无需滚动
- 详情面板不出现滚动条，多行说明可用鼠标滚轮滚动；更改仅在点击“确定”后写入 SCM
- 禁用服务有暗色二次确认；驱动及引导/系统启动类型不允许由本界面修改
- 按名称或显示名称进行包含匹配；可隐藏已停止的服务
- 批量启动、停止、重启；服务操作后台执行
- 服务扫描移至后台线程，每 2.5 秒自动刷新；只重绘有变化的行
- F5 刷新，Ctrl+F 聚焦搜索框

## 修复记录

- 修复：打开“启动类型”暗色下拉菜单后点击空白处，`ContextMenuStrip` 在 `Closed` 回调中提前 `Dispose` 导致 `ObjectDisposedException` 闪退；改为控件拥有并重用菜单，控件销毁时才释放。主列表右键菜单也同步修复。
- 新增 Windows Actions GUI 回归测试：打开、模拟空白点击关闭、重新打开、选择选项、控件销毁资源回收。

## 窗口位置及版本元数据

- 服务详情窗口在主窗口**右半区域**居中打开，并限制在主窗口所在显示器的工作区域内（多显示器可用），不再默认靠左。
- EXE 的原有 `icon.ico` 与主窗口内嵌资源图标逐字节一致；详情页使用主窗口图标的独立副本。
- Windows 资源管理器悬停 EXE / 文件属性包含：文件说明 WinServiceManager、文件版本 1.1.1.0、产品版本 1.1.1-preview、公司 rampageX。

## 运行

Windows 10 / 11，已安装 .NET Framework 4.8。

查看服务无需管理员权限。启动、停止、重启及修改显示名称、启动类型可能需要以管理员身份运行，程序不会自动提权。

服务配置变更通过 Windows 服务控制管理器的 ChangeServiceConfigW / ChangeServiceConfig2W 完成，不直接改注册表。关闭或取消详情窗口不写入更改。修改启动类型不会立即停止或启动运行中的服务；带依赖关系的系统服务不要随意禁用。延迟自动启动由两次 SCM 调用配置，第二步若失败会明确报告，需重新进入详情核对实际配置。

从 Actions 中下载 **WinServiceManager-Dark-Preview** 构建产物，解压运行 WinServiceManager.exe。

## 源码构建

Visual Studio 2022（安装 .NET 桌面开发工作负载及 .NET Framework 4.8 targeting pack），打开 WinServiceManager.sln，选择 Release / Any CPU 编译。

## 兼容说明

原始项目目标框架为 .NET Framework 4.5，本次升级至 4.8，以匹配当前 Windows 10/11 桌面使用场景。正式发布前请先做 Win11 实机外观及服务操作验收。
