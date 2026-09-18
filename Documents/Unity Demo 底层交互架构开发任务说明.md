# Unity Demo 底层交互架构开发任务说明

## 1. 任务目标

在当前 Unity 测试场景中完成一套轻量、组件化、可组合、可扩展的交互底层，用最小案例验证以下闭环：

```text
PlayerInput → PlayerInteractor → Current Interactable
→ InteractionAction → InteractionResult
→ InteractionHUD / FeedbackHUD
```

当前阶段只关注底层交互架构和可运行验证，不实现完整蛋糕流程或复杂玩法。

## 2. 架构原则与职责边界

| 层级 | 职责 |
|---|---|
| Scene | 摆放环境、玩家、交互物和测试对象 |
| Prefab | 定义对象是什么，例如 Player、交互台、测试 Cube、UI |
| Component | 定义对象能做什么，例如检测、交互、动作和显示 |
| Player | 发现当前交互目标，并发起交互 |
| HUD | 统一显示交互提示和交互结果 |

交互物不重复实现 F 键检测、距离检测、HUD 或反馈 UI。交互行为通过多个 Action 组合完成，而不是为每类交互物编写一套完整系统。

## 3. 实现范围

### 3.1 PlayerInteractor

新增 `PlayerInteractor`，职责包括：

- 检测玩家附近或当前指向的 `IInteractable`；
- 维护 `CurrentTarget`；
- 在目标变化时通知 `InteractionHUD`；
- 玩家按 F 时调用当前目标的交互接口；
- 接收交互结果并交给 `FeedbackHUD`；
- 不包含具体玩法逻辑。

如果项目已有 Input System 或 F 键输入，直接复用，不重新搭建输入框架；不得破坏或重构现有 `PlayerController` 和移动逻辑。

### 3.2 基础交互结构

建立以下轻量结构：

- `IInteractable`：判断当前玩家是否可交互、提供 Prompt、执行交互；
- `Interactable`：通用交互组件，持有并按顺序执行 Action；
- `InteractionContext`：至少包含发起交互的 Player、当前目标，并保留后续扩展空间；
- `InteractionResult`：至少包含 `success` 和 `feedback message`，后续可扩展反馈类型。

不要在当前阶段引入复杂事件总线、DI、Service Locator 或大型状态管理。

### 3.3 Interactable

`Interactable` 负责：

- 配置 Prompt，例如“制作奶油”“放置水果”“测试交互”；
- 判断是否可交互；
- 持有一组 `InteractionAction`；
- 按配置顺序执行 Action；
- 汇总并返回 `InteractionResult`。

### 3.4 InteractionAction

建立通用 `InteractionAction` 基类或接口，使一个交互物可以组合多个动作。至少提供以下最小实现，并保证可编译、可在 Inspector 配置或以项目现有方式使用：

- `PlayAnimationAction`：播放目标 Animator 的指定动画或触发器；
- `ChangeMaterialAction`：切换 Renderer 材质；
- `ToggleObjectAction`：启用/禁用目标 GameObject；
- `AddProgressAction`：增加一个轻量进度值并可返回结果；
- `AttachObjectAction`：将目标物体挂接到指定 Transform。

组合示例：

```text
打奶油：Interactable → PlayAnimationAction → AddProgressAction → ChangeMaterialAction
挂水果：Interactable → AttachObjectAction → AddProgressAction
```

若某个 Action 暂无完整业务数据，先实现最小可验证行为，不提前扩展成完整业务系统。

### 3.5 HUD

全项目只保留一套固定 HUD。

`InteractionHUD`：固定位置显示当前 Prompt，例如：

```text
[F]
测试交互
```

有目标时由 `PlayerInteractor` 调用 `Show(prompt)`，无目标时调用 `Hide()`。交互物只提供 Prompt 数据，不携带独立 Canvas。

`FeedbackHUD`：固定位置显示 `InteractionResult` 的文字反馈，例如“交互成功”“无法执行”“奶油打发完成”。当前阶段只做文字反馈；交互物和 Action 不直接生成 UI。

## 4. 建议文件结构

只整理项目新增代码，不移动或拆散第三方 Asset：

```text
Assets/
└── _Project/
    ├── Scenes/
    ├── Prefabs/
    │   ├── Player/
    │   ├── Interactables/
    │   └── UI/
    └── Scripts/
        ├── Core/
        │   └── Interaction/
        │       ├── IInteractable.cs
        │       ├── Interactable.cs
        │       ├── InteractionContext.cs
        │       └── InteractionResult.cs
        ├── Player/
        │   └── PlayerInteractor.cs
        ├── Interaction/
        │   └── Actions/
        │       ├── PlayAnimationAction.cs
        │       ├── ChangeMaterialAction.cs
        │       ├── ToggleObjectAction.cs
        │       ├── AddProgressAction.cs
        │       └── AttachObjectAction.cs
        └── UI/
            ├── InteractionHUD.cs
            └── FeedbackHUD.cs
```

## 5. Scene 配置要求

当前只有一个测试 Scene 时，不要为了架构强行拆分。未来可拆为 `Environment Scene` 与 `Gameplay / Celebration Scene`，并考虑 Additive 加载；本阶段先在当前 Scene 跑通。

测试 Scene 至少包含：

- 现有 Player 和 PlayerController；
- Player 上的 `PlayerInteractor`；
- 一套 `InteractionHUD` 和 `FeedbackHUD`；
- 一个带 Collider 的测试 Cube；
- Cube 上的 `Interactable` 与 `ToggleObjectAction`；
- 一个可被 Toggle 的目标物体。

## 6. 最小测试案例

测试物结构：

```text
TestInteractable
├── Collider
├── Interactable（Prompt：测试交互）
└── ToggleObjectAction
```

验收流程：

1. 玩家进入交互范围或对准测试物；
2. `InteractionHUD` 显示 `[F] 测试交互`；
3. 玩家按 F；
4. `ToggleObjectAction` 执行，目标物体启用/禁用或发生其他明确变化；
5. `FeedbackHUD` 显示“交互成功”；
6. 玩家离开目标后，`InteractionHUD` 消失；
7. 场景 Console 无新增报错或异常。

## 7. Unity Editor 验证步骤

1. 打开当前测试 Scene，确认 Player、测试物和两套 HUD 均存在；
2. 检查 Player 的 `PlayerInteractor` 引用、检测范围/层级和输入配置；
3. 检查测试物的 Collider、`Interactable`、Prompt 和 Action 配置；
4. 检查 Action 的目标对象、Renderer、Animator 或 Transform 引用没有丢失；
5. Play 模式下验证“无目标隐藏提示、进入范围显示提示、按 F 执行动作、显示反馈、离开范围隐藏提示”；
6. 重复按 F，确认失败或重复执行时不会产生空引用、报错或错误 UI；
7. 停止运行后检查 Inspector 配置和场景保存状态。

## 8. 完成汇报要求

完成后必须汇报：

1. 新增和修改了哪些文件；
2. 当前架构关系和数据流；
3. Scene 中需要挂载哪些组件；
4. 测试物如何配置；
5. 在 Unity Editor 中如何验证；
6. 当前剩余 TODO，以及明确说明没有继续扩展哪些内容。

## 9. 明确不做事项

当前阶段不实现：

- Bot 或 AI 行为树；
- 完整蛋糕制作流程和复杂玩法；
- Inventory 系统；
- 复杂任务系统；
- 多人同步或多人玩法；
- ScriptableObject Event Channel；
- 大型 Service Locator；
- Dependency Injection；
- 复杂状态机；
- 过度抽象或为未来需求提前搭建大型框架。

最终目标是完成一套可直接验证、职责清晰、后续可通过新增 Action 扩展的轻量交互底层。
