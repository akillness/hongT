# Cinder Court 测评报告

## 1. 基础信息

- 游戏类型：`survivor_like`
- 游戏地址：http://127.0.0.1:8766/?mode=arena&intro=off
- 测试会话：`evidence_e5521c88a2ba`
- 导出时间：2026-08-09T15:15:59.119696+09:00

## 2. 测试结论

overrun

## 3. 综合评分

- 综合分数：**-**
- 五星评分：**- / 5.0**
- 评分可信度：未知

## 4. 关键节点与能力覆盖

- 关键节点：关键节点接口没有通过真实功能自检，本轮只进行自然流程或有限黑盒测试。
- 能力覆盖：本次测试覆盖 5 项能力，能力覆盖率为 62%。已覆盖：状态观察、移动与导航、战斗操作、生存压力控制、成功或失败结算；未充分覆盖：资源收集、升级决策、Boss 阶段验证。

## 5. 证据质量与可信度

- 证据质量分：**82 / 100**
- 证据质量等级：**high**
- 问题证据可信度：**71 / 100**
- 可信度等级：**medium**
- 可信度阈值：60
- 证据是否充分：是
- 低可信问题数量：0 个
- 完整性状态：complete
- 测试录像：`/Users/jangyoung/orca/workspaces/HongT/main/_workspace/current/qa/wai-play/baseline/evidence/game_test_20260809_151514_c7d6cfdd/gameplay.webm`
- 已生成问题片段：1 个

## 6. 主要问题与证据

### 1. 按下操作后，游戏响应得太慢

- 问题 ID：`quality_technical_quality_input_latency`
- 严重程度：**minor**
- 优先级：**P2**
- 问题说明：该项当前评分为 1.8/5.0。大多数操作中的较慢响应约为 359 毫秒。这说明生存肉鸽在“按下操作后，游戏响应得太慢”方面存在可复现的不足。
- 问题频次：出现于 1/1 次尝试
- 最佳证据尝试：第 1 次（baseline）
- 证据结论：**证据充分**
- 证据 ID：`attempt_01_baseline_step_15_2673601c`
- 对应步骤：2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
- 结构化状态：`{"player.hp":0,"player.max_hp":100,"player.level":1,"player.exp":0,"player.position":{"x":892.158,"y":792.366},"enemy_count":2,"combat.kills":2,"resources.exp_orbs":0,"upgrade":{"is_selecting_upgrade":false,"options":[]},"boss":{"exists":false,"hp":0,"max_hp":0,"phase":0,"phase_count":3},"world.elapsed":21.371,"status":{"done":true,"success":false,"failed":true,"reason":"overrun"},"_available_field_count":12,"_raw_state_available":true}`
- 状态变化：`[{"field":"player.hp","before":2,"after":0,"change_type":"decreased"},{"field":"player.position","before":{"x":889.453,"y":793.775},"after":{"x":892.158,"y":792.366},"change_type":"changed"},{"field":"status","before":{"done":false,"success":false,"failed":false,"reason":"running"},"after":{"done":true,"success":false,"failed":true,"reason":"overrun"},"change_type":"changed"},{"field":"world.elapsed","before":20.873,"after":21.371,"change_type":"increased"}]`
- 截图文件：`未生成截图证据`
- 优化建议：建议目标：缩短操作响应时间。具体做法：避免在输入回调里执行大量计算，把画面更新安排到下一帧并及时确认输入已经接收。验收方法：关键操作的较慢响应应低于 150 毫秒。

### 2. 按下操作后，画面没有及时反馈

- 问题 ID：`quality_feedback_input_feedback`
- 严重程度：**minor**
- 优先级：**P2**
- 问题说明：该项当前评分为 1.8/5.0。这说明生存肉鸽在“按下操作后，画面没有及时反馈”方面存在可复现的不足。
- 问题频次：出现于 1/1 次尝试
- 最佳证据尝试：第 1 次（baseline）
- 证据结论：**证据充分**
- 证据 ID：`attempt_01_baseline_step_14_37c3b5dd`
- 对应步骤：1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14
- 结构化状态：`{"player.hp":2,"player.max_hp":100,"player.level":1,"player.exp":0,"player.position":{"x":889.453,"y":793.775},"enemy_count":2,"combat.kills":2,"resources.exp_orbs":0,"upgrade":{"is_selecting_upgrade":false,"options":[]},"boss":{"exists":false,"hp":0,"max_hp":0,"phase":0,"phase_count":3},"world.elapsed":20.873,"status":{"done":false,"success":false,"failed":false,"reason":"running"},"_available_field_count":12,"_raw_state_available":true}`
- 状态变化：`[{"field":"player.hp","before":9,"after":2,"change_type":"decreased"},{"field":"player.position","before":{"x":837.151,"y":821.002},"after":{"x":889.453,"y":793.775},"change_type":"changed"},{"field":"world.elapsed","before":19.562,"after":20.873,"change_type":"increased"}]`
- 整场录像：`/Users/jangyoung/orca/workspaces/HongT/main/_workspace/current/qa/wai-play/baseline/evidence/game_test_20260809_151514_c7d6cfdd/gameplay.webm`
- 录像时间段：32341ms - 42770ms
- 独立问题片段：`/Users/jangyoung/orca/workspaces/HongT/main/_workspace/current/qa/wai-play/baseline/reports/evidence_e5521c88a2ba/clips/002_quality_feedback_input_feedback_32341_42770.webm`
- 截图文件：`未生成截图证据`
- 优化建议：建议目标：让玩家知道操作已经生效。具体做法：按下按钮或方向键后立即给出按压、移动、声音或状态提示，不要只修改内部数值。验收方法：抽查 10 次操作，每次都能在 0.15 秒内看到或听到反馈。


---

## 7. AI Reporter 报告

# Cinder Court AI 测评报告

## 测试结论

- 固定规则评分：未知
- 测试结论：overrun
- 通关结果：未达到严格通关条件
- 实际尝试次数：1
- 问题事实来源：聚合后的 problem_cards
- 证据策略：每个问题只展示一次最佳匹配尝试的证据

## 问题与最佳证据

### 1. 按下操作后，游戏响应得太慢

- Problem ID：quality_technical_quality_input_latency
- 严重程度：minor
- 出现频次：出现于 1/1 次尝试
- 最佳证据尝试：第 1 次（baseline）
- 对应步骤：2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
- 对应录像片段：无可用录像片段
- 证据 ID：attempt_01_baseline_step_15_2673601c
- 证据结论：证据充分，且仅引用上述一次尝试。
- 问题描述：该项当前评分为 1.8/5.0。大多数操作中的较慢响应约为 359 毫秒。这说明生存肉鸽在“按下操作后，游戏响应得太慢”方面存在可复现的不足。
- 优化建议：建议目标：缩短操作响应时间。具体做法：避免在输入回调里执行大量计算，把画面更新安排到下一帧并及时确认输入已经接收。验收方法：关键操作的较慢响应应低于 150 毫秒。

### 2. 按下操作后，画面没有及时反馈

- Problem ID：quality_feedback_input_feedback
- 严重程度：minor
- 出现频次：出现于 1/1 次尝试
- 最佳证据尝试：第 1 次（baseline）
- 对应步骤：1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14
- 对应录像片段：/Users/jangyoung/orca/workspaces/HongT/main/_workspace/current/qa/wai-play/baseline/evidence/game_test_20260809_151514_c7d6cfdd/gameplay.webm，32341ms–42770ms
- 证据 ID：attempt_01_baseline_step_14_37c3b5dd
- 证据结论：证据充分，且仅引用上述一次尝试。
- 问题描述：该项当前评分为 1.8/5.0。这说明生存肉鸽在“按下操作后，画面没有及时反馈”方面存在可复现的不足。
- 优化建议：建议目标：让玩家知道操作已经生效。具体做法：按下按钮或方向键后立即给出按压、移动、声音或状态提示，不要只修改内部数值。验收方法：抽查 10 次操作，每次都能在 0.15 秒内看到或听到反馈。

## 历史记忆说明

- 历史记忆条数：0
- 历史数据仅用于同类游戏背景对照，未用于新增本次问题或修改固定规则评分。


---

## 8. 证据说明

录像、问题片段、截图、状态快照、证据可信度和 integrity.json 共同组成自动测评证据链。证据不足的问题会降低可信度展示，但不会进入自动化证据校验流程。