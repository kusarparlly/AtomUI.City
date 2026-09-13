# Desktop Dogfood GUI 人工检查表

本清单用于系统级自动化通过后的 30 分钟人工复核。开始前关闭可能覆盖窗口的置顶应用，保持系统输入法、显示缩放和窗口主题为日常使用状态。

## 启动

```powershell
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -c Release -- --profile manual --seed 20260911
```

记录 Windows 版本、显示器数量、每台显示器分辨率/缩放、输入法和开始时间。出现系统 crash dialog、空白窗口或窗口无响应立即判定失败。

## 0-10 分钟：窗口与核心操作

- 确认主窗口、Order Workbench、Support Workspace、Diagnostics 四个窗口均非空，标题和 Outlet 内容正确。
- 在四个窗口间切换焦点，最小化和恢复辅助窗口；关闭辅助窗口后主窗口仍可操作。
- 依次点击六个导航项，检查主内容和底部状态按点击顺序更新。
- 使用鼠标和 Tab/Shift+Tab 遍历操作面板，焦点不得消失或跳入不可见控件。
- 搜索空字符串、英文和中文 IME 文本；分别使用 Enter 和 Search 按钮；长搜索执行中点击 Cancel。

## 10-20 分钟：跨模块与布局

- 连续切换 `zh-CN`/`en-US` 至少 10 次，导航文案、状态和结果不得混用 culture。
- 在 Alice、Bob、Administrator 间切换至少两轮，确认在线/离线受限状态与选择一致。
- 反复切换 Realtime、使用鼠标和方向键调整 Priority、选择结果并滚动历史。
- 将主窗口调整到最小尺寸、最大化、恢复，再移动到每台显示器；检查文字、按钮、列表和滚动区域无重叠或裁切。
- 在 100%、150%、200% 系统缩放可用环境分别执行一次；若当前机器不能安全改变系统缩放，记录为未执行而不是通过。

## 20-30 分钟：持续操作与关闭

- 混合执行不少于 50 次导航、搜索、取消、语言、账号和 State 控件操作，快速重复点击不得导致顺序倒置或 UI 冻结。
- 最小化主窗口 30 秒后恢复，确认实时状态和历史区域仍可操作。
- 分别记录用户点击关闭按钮、应用发起关闭和测试 Host stop 的结果；单次人工会话只执行其中一种，三种来源可分三轮完成。
- 关闭后确认四个窗口消失、进程退出、没有 crash dialog，并检查 `run-report.json` 的 `exitCode=0`、`resourcesReleased=true`、`failure=null`。

人工结果必须记录为 Pass、Fail 或 Not Applicable；没有实际执行的项目不能记为 Pass。
