# WinServiceManager

轻量 Windows 服务管理器，基于 .NET Framework 4.8 / WinForms，无第三方 UI 依赖。

## 本版功能（v1.1 预览）

- 参考 Film Grain Studio 配色的全暗黑界面（包含 Windows 10/11 深色标题栏、列表和右键菜单）
- 同时显示服务名称、显示名称、状态
- 点击任意列标题切换升序/降序；刷新后保留排序、多选与滚动位置
- 双击服务或右键“查看详情”，查看描述、启动类型、可执行路径、登录账户、依赖关系等只读资料
- 按名称或显示名称进行包含匹配；可隐藏已停止的服务
- 批量启动、停止、重启；服务操作后台执行
- 服务扫描移至后台线程，每 2.5 秒自动刷新；只重绘有变化的行
- F5 刷新，Ctrl+F 聚焦搜索框

## 运行

Windows 10 / 11，已安装 .NET Framework 4.8。

查看服务无需管理员权限。启动、停止、重启服务可能需要以管理员身份运行。

从 Actions 中下载 **WinServiceManager-Dark-Preview** 构建产物，解压运行 WinServiceManager.exe。

## 源码构建

Visual Studio 2022（安装 .NET 桌面开发工作负载及 .NET Framework 4.8 targeting pack），打开 WinServiceManager.sln，选择 Release / Any CPU 编译。

## 兼容说明

原始项目目标框架为 .NET Framework 4.5，本次升级至 4.8，以匹配当前 Windows 10/11 桌面使用场景。正式发布前请先做 Win11 实机外观及服务操作验收。
