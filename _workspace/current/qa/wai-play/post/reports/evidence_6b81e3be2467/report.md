# Cinder Court 测评报告

## 1. 基础信息

- 游戏类型：`survivor_like`
- 游戏地址：http://127.0.0.1:8766/?mode=arena&intro=off
- 测试会话：`evidence_6b81e3be2467`
- 导出时间：2026-08-09T15:28:51.127060+09:00

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

- 证据质量分：**65 / 100**
- 证据质量等级：**medium**
- 问题证据可信度：**61 / 100**
- 可信度等级：**medium**
- 可信度阈值：60
- 证据是否充分：是
- 低可信问题数量：0 个
- 完整性状态：complete
- 测试录像：`/Users/jangyoung/orca/workspaces/HongT/main/_workspace/current/qa/wai-play/post/evidence/game_test_20260809_152738_ff6f6d98/gameplay.webm`
- 已生成问题片段：0 个

## 6. 主要问题与证据

### 1. 按下操作后，游戏响应得太慢

- 问题 ID：`quality_technical_quality_input_latency`
- 严重程度：**major**
- 优先级：**P1**
- 问题说明：该项当前评分为 0.9/5.0。大多数操作中的较慢响应约为 2818 毫秒。这说明生存肉鸽在“按下操作后，游戏响应得太慢”方面存在可复现的不足。
- 问题频次：出现于 1/1 次尝试
- 最佳证据尝试：第 1 次（baseline）
- 证据结论：**证据充分**
- 证据 ID：`attempt_01_baseline_step_8_27790fc5`
- 对应步骤：1, 2, 3, 4, 7, 8
- 结构化状态：`{"player.hp":0,"player.max_hp":100,"player.level":1,"player.exp":0,"player.position":{"x":804.334,"y":601.528},"enemy_count":5,"combat.kills":4,"resources.exp_orbs":0,"upgrade":{"is_selecting_upgrade":false,"options":[]},"boss":{"exists":false,"hp":0,"max_hp":0,"phase":0,"phase_count":3},"world.elapsed":32.219,"status":{"done":true,"success":false,"failed":true,"reason":"overrun"},"_available_field_count":12,"_raw_state_available":true}`
- 状态变化：`[{"field":"player.hp","before":34,"after":0,"change_type":"decreased"},{"field":"player.position","before":{"x":768,"y":601.528},"after":{"x":804.334,"y":601.528},"change_type":"changed"},{"field":"status","before":{"done":false,"success":false,"failed":false,"reason":"running"},"after":{"done":true,"success":false,"failed":true,"reason":"overrun"},"change_type":"changed"},{"field":"world.elapsed","before":28.345,"after":32.219,"change_type":"increased"}]`
- 截图文件：`未生成截图证据`
- 优化建议：建议目标：缩短操作响应时间。具体做法：避免在输入回调里执行大量计算，把画面更新安排到下一帧并及时确认输入已经接收。验收方法：关键操作的较慢响应应低于 150 毫秒。


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
- 严重程度：major
- 出现频次：出现于 1/1 次尝试
- 最佳证据尝试：第 1 次（baseline）
- 对应步骤：1, 2, 3, 4, 7, 8
- 对应录像片段：无可用录像片段
- 证据 ID：attempt_01_baseline_step_8_27790fc5
- 证据结论：证据充分，且仅引用上述一次尝试。
- 问题描述：该项当前评分为 0.9/5.0。大多数操作中的较慢响应约为 2818 毫秒。这说明生存肉鸽在“按下操作后，游戏响应得太慢”方面存在可复现的不足。
- 优化建议：建议目标：缩短操作响应时间。具体做法：避免在输入回调里执行大量计算，把画面更新安排到下一帧并及时确认输入已经接收。验收方法：关键操作的较慢响应应低于 150 毫秒。

## 历史记忆说明

- 历史记忆条数：0
- 历史数据仅用于同类游戏背景对照，未用于新增本次问题或修改固定规则评分。


---

## 8. 证据说明

录像、问题片段、截图、状态快照、证据可信度和 integrity.json 共同组成自动测评证据链。证据不足的问题会降低可信度展示，但不会进入自动化证据校验流程。