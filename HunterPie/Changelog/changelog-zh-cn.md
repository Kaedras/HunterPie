﻿![banner](https://cdn.discordapp.com/attachments/402557384209203200/743894519329587251/update-10396.png)

**插件系统**

HunterPie现已支持插件。插件可以访问HunterPie处理的所有内容，包括玩家数据、怪物数据、游戏事件。如果您有兴趣开发自己的扩展插件，您可以在[这里](https://github.com/Haato3o/HunterPie.Plugins/blob/master/TwitchIntegration/main.cs)找到一个示例。

**怪物部件**


- **文本格式:** 增加了异常状态栏，状态计时器和部位血量文本格式选项。请参阅下面的特殊格式:
    - **{}{Current}** - 当前条形值.
    - **{}{Max}** - 最大条形值.
    - **{}{Percentage}** - 当前值 / 最大值 * 100 (没有结尾的%)

特殊格式是在HunterPie呈现文本时将被替换的文字。它们需要按照上表中所示的方式编写，区分大小写。

**例子:**
- **{}{Current}/{Max} ({Percentage}%)** 显示结果为 500/1000 (50%)
- **{}{Percentage}%** 显示结果为 50%

---

**衣装部件**

- **紧凑模式:** 添加了衣装部件的紧凑模式，图标变得更小，只显示冷却时间和当前计时器。

![img](https://cdn.discordapp.com/attachments/402557384209203200/742950877828087808/design_mantle_compact.png)

---

**计时器**

- 受技能 *“集中 ”* 影响的任务计时器现在会根据您的技能等级自动调整。
- 受技能 *“强化持续 ”* 影响的增益状态计时器现在会根据您的技能等级自动调整。
---

**伤害部件**

- **DPS计算:** 伤害部件现在在玩家第一次击中怪物后才会计算每秒的伤害。
- **尺寸调整:** 您现在可以调整伤害部件的大小，使其水平或垂直对齐。

![wee](https://cdn.discordapp.com/attachments/402557384209203200/743905320107245649/unknown.png)

---

**编辑模式**

- 对编辑模式切换进行了优化，切换编辑模式时，应花费较少的时间来渲染部件。

- 启用编辑模式时，将位置、比例和渲染时间文本添加到部件。

---

**其他变化**

- 增加霜刃冰牙龙破坏临界值。
- 添加样式文件，可以覆盖所有主题，不必从头创建新的主题。该文件在HunterPie.Resources/UI/Overwrite.xaml
- 升级到 .NET 4.8 框架。
- 增加强制DirectX 11全屏显示覆盖层选项。
- 设置选项页面宽度与整体窗口宽度相匹配，不再保持固定宽度。
---

**Bug修复**

- 修复了异常状态栏在编辑模式中调整位置有多余空白的问题。
- 修复了如果用户的计算机意外关闭，将损坏用户的 *“config.json ”* 的错误。
- 修复了HunterPie挂钩到Stracker的控制台窗口以获取游戏版本而不是获取游戏窗口的问题。
---

**简体中文翻译 Ver.1.0.3.96 By 枫雨（FengYuu#9220）**
