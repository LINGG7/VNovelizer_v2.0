# Command 列指令说明

本文档依据当前项目源码整理，覆盖框架内置命令、项目自定义命令，以及 `parallel(...)` 组合语法。

- 统计日期：2026-07-27
- 普通命令：42 个
- 组合语法：1 个（`parallel`）
- 当前 CSV 剧本实际使用：19 个普通命令

## 1. 基本语法

### 1.1 单条、串行与并行

```text
commandName(arg1,arg2)
commandA(...)&commandB(...)
parallel(commandA(...);commandB(...))
```

| 写法 | 含义 |
|---|---|
| `&` | 严格串行；上一条命令结束后再执行下一条 |
| `parallel(...;...)` | 同时启动组内命令，等待全部成员结束 |
| `;` | 只用于分隔 `parallel` 组内的命令 |
| `,` | 分隔普通命令参数 |
| `\|` | `choice` 和 `panoramaChoice` 的字段分隔符 |

示例：

```text
charfadein(L,1)&charmove(L,100,-100,1)
parallel(charfadein(L,1);bgfade(room,1);playsfx(door,1))&wait(0.5)
parallel(wait(0.5);parallel(charmove(L,-300,0,1);charmove(R,300,0,1)))
```

命令名不区分大小写。直接编辑 CSV 时，包含逗号的 Command 单元格必须遵循 CSV 引号规则；通过 Excel/XLSX 编辑时由导出工具处理。

### 1.2 每行的执行时机

一行开始时，系统先更新背景、BGM、立绘和文本，然后执行 Command：

- 大部分命令会在文字打字期间立即开始。
- 顶层 `jump(...)` 和 `loadscript(...)` 在本行有文本时，会延迟到文字确认后执行。
- `choice(...)` 会等待文字打完后再显示选项。
- 不要把 `jump`、`loadscript`、`unlockending` 放入 `parallel`；嵌套在并行组内时不会应用顶层流程延迟规则。
- `fadeblackout`、`panorama`、`panoramaChoice` 会请求命令结束后自动推进剧情，应当放在该行流程的末尾。

### 1.3 执行类型

| 类型 | 说明 |
|---|---|
| 立即 | 当帧完成，然后执行下一条 |
| 等待 | 等待动画、资源、音频或计时结束 |
| 启动后返回 | 启动效果但不等待效果播放完 |
| 交互等待 | 等待玩家操作 |

## 2. 指令总览

“已使用”表示在 `Assets/Resources/VNovelizerRes/VNScripts/*.csv` 的 Command 列中检测到。

| 命令 | 用途 | 类型 | 已使用 |
|---|---|---:|:---:|
| `parallel` | 并行执行一组命令 | 等待 | — |
| `wait` | 等待指定秒数 | 等待 | — |
| `jump` | 跳转到当前剧本的行 ID | 立即/流程 | ✓ |
| `loadscript` | 加载另一剧本 | 等待/流程 | ✓ |
| `choice` | 添加剧情选项 | 交互流程 | ✓ |
| `exit` | 自动保存并返回主菜单 | 立即/流程 | — |
| `hide` | 隐藏或恢复游戏界面 | 立即 | — |
| `panorama` | 打开可观察全景图 | 交互等待 | ✓ |
| `panoramaChoice` | 打开带热点的全景选择 | 交互等待 | — |
| `unlockending` | 解锁结局并显示结局选择 | 立即/流程 | ✓ |
| `bgfade` | 淡入切换背景 | 等待 | ✓ |
| `fadeblackin` | 黑幕淡入 | 启动后返回 | — |
| `fadeblackout` | 黑幕淡出 | 等待/推进 | — |
| `shake` | 震动屏幕、对话框或立绘 | 启动后返回 | ✓ |
| `charfadein` | 立绘淡入 | 等待 | ✓ |
| `charfadeout` | 立绘淡出并隐藏 | 等待 | ✓ |
| `charflip` | 翻转立绘 | 立即 | ✓ |
| `charjump` | 立绘跳跃 | 等待 | — |
| `charmove` | 移动立绘 | 等待 | ✓ |
| `charscale` | 缩放立绘 | 等待 | ✓ |
| `setchartrans` | 立即设置立绘位置和缩放 | 立即 | ✓ |
| `playanim` | 播放 Animator 特效 | 等待或常驻 | — |
| `stopanim` | 停止常驻 Animator 特效 | 立即 | — |
| `playparticle` | 播放并登记粒子特效 | 短暂等待 | ✓ |
| `stopparticle` | 停止并注销粒子特效 | 立即 | ✓ |
| `playfilter` | 开启全屏滤镜 | 立即 | — |
| `stopfilter` | 关闭全屏滤镜 | 立即 | — |
| `memoryon` | 开启项目的回忆滤镜 | 立即 | ✓ |
| `memoryoff` | 关闭项目的回忆滤镜 | 立即 | ✓ |
| `playsfx` | 播放一次或多次音效 | 等待 | — |
| `playvideo` | 播放视频 | 等待 | — |
| `showprompt` | 显示短提示 | 启动后返回 | — |
| `t_color` | 设置当前行文本颜色 | 立即 | — |
| `t_size` | 设置当前行文本字号 | 立即 | — |
| `settextspeed` | 设置打字速度 | 立即 | — |
| `setautospeed` | 设置自动播放速度 | 当前 Command 列不生效 | — |
| `config` | 修改语音、文字或自动播放配置 | 立即 | — |
| `setboolflag` | 设置布尔变量 | 立即 | — |
| `setintflag` | 设置整数变量 | 立即 | — |
| `setstringflag` | 设置字符串变量 | 立即 | — |
| `unlockcg` | 解锁 CG | 立即 | — |
| `unlockmusic` | 解锁音乐 | 立即 | ✓ |
| `unlockscene` | 解锁回想场景 | 立即 | ✓ |

## 3. 流程控制

### `parallel`

```text
parallel(command1;command2;command3)
```

同时启动所有成员，并在全部成员结束后继续外层串行流程。支持嵌套，并为每个成员创建独立命令实例。

```text
parallel(charmove(L,-300,0,1);charmove(R,300,0,1);bgfade(room,1))
```

建议只并行互不争用同一 UI 状态的演出命令。两个命令同时修改同一背景、同一立绘或同一滤镜时，最终结果取决于各自结束时间。

### `wait`

```text
wait(seconds)
```

- `seconds`：等待秒数。
- 只能用于异步剧本流程；同步 `ExecuteCommand` 调用会失败。

```text
wait(0.5)
```

### `jump`

```text
jump(lineID)
```

跳转到当前剧本的指定行 ID。ID 不存在时记录错误并保持当前流程。

```text
jump(45)
```

有文本的行中，顶层 `jump` 会等玩家确认文字后再执行。建议作为最后一条命令。

### `loadscript`

```text
loadscript(scriptName)
loadscript(scriptName,startID)
```

异步加载剧本，并从开头或指定行 ID 开始。指定 ID 不存在时从索引 0 开始。

```text
loadscript(sntzm_main2,1)
```

有文本的行中，顶层 `loadscript` 会等玩家确认文字后再执行。建议作为最后一条命令。

### `choice`

```text
choice(displayText|command)
choice(@loc:FULL_KEY|command)
```

等待本行文字打完，向选择面板添加一个选项。多个选项用顶层 `&` 连接：

```text
choice(继续探索|jump(2))&choice(先行撤离|jump(3))
```

- `command` 可为空。
- `@loc:FULL_KEY` 使用当前剧本的本地化表；缺少翻译时显示 key 最后一段。
- 点击选项后，由 `VNManager.ExecuteChoiceCommand` 执行对应命令。

### `exit`

```text
exit()
```

自动保存，必要时退出暂停状态，清理 Tween、效果和对象池，然后加载 `VNMainMenu` 场景。参数必须为空。

### `hide`

```text
hide()
```

调用游戏面板的隐藏操作，效果与玩家触发隐藏界面相同。参数必须为空。

### `panorama`

```text
panorama(backgroundName)
panorama(backgroundName,startYaw,startPitch)
```

加载背景资源目录中的全景贴图并进入观察模式，玩家关闭观察层后自动恢复 Gameplay 状态并推进剧情。

```text
panorama(TestBG,0,0)
```

### `panoramaChoice`

```text
panoramaChoice(backgroundName,startYaw,startPitch,label|yaw|pitch|command;label|yaw|pitch|command)
```

打开不可直接关闭的全景选择界面，并显示最多 3 个热点。选择热点后执行对应命令。

```text
panoramaChoice(Room360,0,0,门口|20|0|jump(10);窗户|-45|5|jump(20))
```

热点命令目前只读取第四个 `|` 字段，因此不适合直接嵌入自身含 `|` 的 `choice(...)`。

### `unlockending`

```text
unlockending(endingID)
```

解锁结局并打开结局选择界面，同时停止本行余下命令。结局数据不存在时使用 ID 作为显示文本。

```text
unlockending(Ending_1)
```

## 4. 背景、转场与震动

### `bgfade`

```text
bgfade(backgroundName)
bgfade(backgroundName,duration)
```

- 默认时长：`1` 秒。
- 从 `VNProjectConfig.BackgroundResPath` 异步加载 Sprite；失败时再尝试 Texture2D。
- 更新当前背景数据，等待淡变完成。
- 玩家中断时立即完成到目标背景。

```text
bgfade(sntzm_main1_10,1)
```

### `fadeblackin`

```text
fadeblackin(duration)
```

调用 `TransitionManager.PlayDarkFadeInOnly` 启动黑幕淡入，但命令不会等待动画结束。

### `fadeblackout`

```text
fadeblackout(duration)
```

等待黑幕淡出完成，并登记“本行命令全部完成后自动前进”。建议放在该行末尾。

### `shake`

```text
shake(target)
shake(target,duration,intensity)
shake(target,duration,intensity,direction)
shake(target,duration,intensity,interval,count)
shake(target,duration,intensity,frequency,direction,interval,count)
```

| 参数 | 说明 | 默认值 |
|---|---|---:|
| `target` | `screen`、`dialogue`、`L/M/R`，也支持完整英文位置名 | 必填 |
| `duration` | 每次震动时长 | `0.5` |
| `intensity` | 位移强度 | `10` |
| `frequency` | 每秒采样次数 | `60` |
| `direction` | `x`、`y` 或 `xy`；也接受 horizontal/vertical 等别名 | `xy` |
| `interval` | 多次震动间隔 | `0` |
| `count` | 震动次数，最少 1 | `1` |

```text
shake(screen,0.5,30,y)
shake(screen,0.2,5,0.2,3)
shake(L,0.5,5,x)
```

该命令启动震动协程后立即返回；需要延迟后续命令时显式追加 `wait(...)`。

## 5. 立绘控制

位置参数通常使用 `L`、`M`、`R`。执行立绘命令时，该槽位必须已有有效立绘。

### `charfadein`

```text
charfadein(position)
charfadein(position,duration)
```

默认 `0.5` 秒，从透明淡入并激活立绘；中断时立即显示到最终状态。

### `charfadeout`

```text
charfadeout(position)
charfadeout(position,duration)
```

默认 `0.5` 秒，淡出后隐藏立绘并恢复 Alpha；中断时立即隐藏。

### `charflip`

```text
charflip(position)
charflip(position,1)
charflip(position,-1)
charflip(position,left)
charflip(position,right)
```

- 不提供方向时切换当前朝向。
- 正数或 `right` 保持正 X，负数或 `left` 使用负 X。
- 保留当前缩放绝对值，并同步内部朝向状态。

### `charjump`

```text
charjump(position)
charjump(position,durationPerJump,times,height)
```

默认每跳 `0.4` 秒、1 次、高度 30。等待所有跳跃完成；中断时停止并恢复原始位置。

### `charmove`

```text
charmove(position,targetX,targetY)
charmove(position,targetX,targetY,duration)
```

默认 `0.5` 秒，使用 OutQuad 缓动移动到目标 anchoredPosition。该变换不继承到下一行，下一行开始时恢复保存的默认变换。

### `charscale`

```text
charscale(scale,duration)
charscale(position,scale,duration)
```

- 两参数形式缩放当前可见的 `L/M/R` 立绘。
- 三参数形式只缩放指定位置。
- 负缩放值转为绝对值，同时保留立绘原有左右翻转符号。
- 该变换不继承到下一行。

### `setchartrans`

```text
setchartrans(position,x,y,scale)
```

立即设置 anchoredPosition 和缩放，保留当前 X 翻转符号。该变换不继承到下一行。

## 6. 动画、粒子与滤镜

### `playanim`

```text
playanim(animationName)
playanim(animationName,position)
playanim(animationName,position,loop)
```

`position` 支持：

```text
M
M(0,300)
(100,200)
```

- 默认位置为 `M`。
- 非循环动画等待 Animator 当前状态长度后回收。
- `loop` 动画创建后登记为常驻效果并返回，使用 `stopanim` 关闭。

### `stopanim`

```text
stopanim(animationName)
```

注销并回收名为 `VNAnim_<animationName>` 的常驻动画。

### `playparticle`

```text
playparticle(effectName)
```

从粒子资源目录加载并播放效果，登记到可保存的活动效果列表。命令最多等待资源回调 1 秒；效果本身持续存在，直到 `stopparticle`。

### `stopparticle`

```text
stopparticle(effectName)
```

注销效果，停止 ParticleSystem 和 UIParticle，并启动 5 秒后的延迟回收。命令本身不等待这 5 秒。

### `playfilter`

```text
playfilter(filterPrefabName)
```

从 `VNovelizerRes/VFX/PostProcess` 加载全屏滤镜预制体，挂到效果层并登记为可恢复效果。重复开启同名滤镜不会重复实例化。

### `stopfilter`

```text
stopfilter(filterPrefabName)
```

注销并销毁指定滤镜对象。

### `memoryon` / `memoryoff`

```text
memoryon()
memoryoff()
```

项目自定义快捷命令：

- `memoryon`：关闭旧版回忆滤镜和灰尘效果，开启 `MemoryOldFilmOverlay`。
- `memoryoff`：关闭 `MemoryOldFilmOverlay`，同时清理兼容用旧滤镜和灰尘效果。

## 7. 音频、视频、提示与文字

### `playsfx`

```text
playsfx(sfxName)
playsfx(sfxName,times)
```

默认播放 1 次。每次等待音效播放完成，多次播放之间间隔 0.05 秒；资源加载最多等待 2 秒。

### `playvideo`

```text
playvideo(videoName)
playvideo(videoName,nextCommand)
```

等待视频播放结束，然后可通过同步命令 API 执行一条后续命令：

```text
playvideo(ending.mp4,loadscript(Chapter2))
```

后续命令使用同步 `ExecuteCommand`，因此不能在此位置使用 `wait` 或 `parallel`。需要复杂流程时，把后续命令写在外层 Command 串行链中。

### `showprompt`

```text
showprompt(text)
showprompt(text,duration)
```

显示提示，默认停留 2 秒。命令启动提示后立即返回。提示文本不适合直接包含逗号。

### `t_color`

```text
t_color(r,g,b)
```

RGB 使用 `0–255`，超出范围会被钳制。只影响当前行，下一行恢复默认文本属性。

### `t_size`

```text
t_size(fontSize)
```

字号限制为 `10–200`。只影响当前行，下一行恢复默认文本属性。

### `settextspeed`

```text
settextspeed(secondsPerCharacter)
```

设置打字速度，单位为秒/字。

### `setautospeed`

```text
setautospeed(seconds)
```

代码意图是设置自动播放速度，但当前类的 `ExecuteAsync` 没有调用 `Execute`。Command 列统一通过异步入口运行，因此该命令目前在 Command 列中不会修改速度；不应在修复前用于正式剧本。

### `config`

```text
config(voice:true)
config(voice:false)
config(textspeed:0.05)
config(autospeed:1.0)
```

配置项使用冒号分隔：

- `voice`：将 VoiceVolume 设置为 1 或 0。
- `textspeed`：更新文本速度。
- `autospeed`：更新自动播放速度。

未知配置项只记录警告。

## 8. 变量与解锁

### `setboolflag`

```text
setboolflag(flagName)
setboolflag(flagName,true)
setboolflag(flagName,false)
```

省略值时默认为 `true`。值必须是可由 `bool.Parse` 解析的 `true` 或 `false`，否则会抛出格式异常。

### `setintflag`

```text
setintflag(flagName,value)
```

值必须是整数。该命令在快速预演/状态模拟时也会应用。

### `setstringflag`

```text
setstringflag(flagName,value)
setstringflag(message,"Hello, World")
setstringflag(message,'Hello, World')
```

第一个逗号之前是变量名，其余内容作为值；首尾成对的单引号或双引号会被移除。该命令在状态模拟时也会应用。

### `unlockcg`

```text
unlockcg(cgName)
```

将指定 CG 标记为已解锁。

### `unlockmusic`

```text
unlockmusic(musicName)
```

将指定音乐标记为已解锁。

### `unlockscene`

```text
unlockscene(sceneName)
```

将指定回想场景标记为已解锁。

## 9. 推荐组合

背景和立绘同时淡入：

```text
parallel(bgfade(room_night,1);charfadein(L,1))
```

左右角色同时移动，全部完成后播放下一段：

```text
parallel(charmove(L,-300,0,1);charmove(R,300,0,1))&loadscript(next_scene,1)
```

震动立即启动，并人为等待演出：

```text
shake(screen,0.5,20,y)&wait(0.5)&bgfade(damaged_room,1)
```

两个普通剧情选项：

```text
choice(打开门|jump(20))&choice(转身离开|jump(30))
```

## 10. 已知限制

- `setautospeed` 当前在 Command 列异步执行路径中不生效。
- `shake`、`fadeblackin`、`showprompt` 启动后立即返回，需要时自行追加 `wait`。
- `jump` 和 `loadscript` 只有位于顶层时才会在有文本的行中延迟到文字确认后；不要放入 `parallel`。
- `playvideo` 的内嵌后续命令走同步 API，不支持异步命令和 `parallel`。
- `fadeblackout`、`panorama`、`panoramaChoice` 会请求自动推进，不要在其后安排依赖玩家停留的命令。
- `exit`、`hide`、`fadeblackin`、`fadeblackout` 在源码中各有两份同名等价实现，由反射注册其中一份；文档按它们的共同实际行为描述。
- `setboolflag` 没有状态模拟实现，而 `setintflag`、`setstringflag` 有；依赖快速跳转/存档状态重建时应验证布尔变量是否符合预期。
- 并行修改同一对象没有自动冲突仲裁，应由剧本作者避免。
