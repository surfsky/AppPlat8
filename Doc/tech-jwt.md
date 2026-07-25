# JWT 实施策略（标准版）

本文档给出标准 JWT 实施策略，重点覆盖：签名密钥轮换、`aud/iss` 校验、`refresh token` 机制与上线顺序。

## 1. 总体策略

1. `Access Token` 使用 JWT，短期有效。
2. `Refresh Token` 使用随机高熵字符串，长期有效，必须入库。
3. 服务端采用双令牌体系：
   - `Access Token` 负责接口鉴权。
   - `Refresh Token` 负责续签。
4. 令牌绑定业务维度（用户、租户、客户端；可选设备指纹）。

## 2. JWT 字段规范

标准字段：

- `iss`：固定签发方（如服务域名或服务标识）。
- `aud`：客户端受众（如 `web`、`mobile`、`openapi`），严格校验。
- `sub`：用户唯一标识。
- `jti`：令牌唯一 ID，用于审计与吊销。
- `iat`、`nbf`、`exp`：签发、生效、过期时间。

自定义字段建议：

- `role`、`scope`、`orgId`、`dataPermission`。
- 尽量精简，避免把敏感信息直接写入 token。

## 3. 签名算法与密钥管理

1. 算法优先级：
   - 首选非对称：`RS256` 或 `ES256`（验签方不持私钥）。
   - 次选对称：`HS256`（实现简单，但密钥泄漏风险更高）。
2. 密钥托管：
   - 私钥存放在 KMS/HSM/Vault，不写死配置文件。
   - 公钥通过 `JWKS` 对外发布。
3. 令牌头必须携带 `kid`（Key ID）以支持轮换。

## 4. 签名密钥轮换策略（核心）

### 4.1 密钥状态

- `Active`：仅用于签发新 token。
- `Verify-only`：停止签发，仅用于验签旧 token。
- `Retired`：不签发、不验签。

### 4.2 无感轮换流程

1. 生成新密钥 `K2`，发布其公钥到 `JWKS`。
2. 将 `K2` 设为 `Active`，旧密钥 `K1` 设为 `Verify-only`。
3. 新签发 token 全部使用 `K2`；旧 token 继续用 `K1` 验签。
4. 等待旧 token 自然过期后，将 `K1` 标记为 `Retired`。

### 4.3 时间窗口建议

- `Access Token`：15~30 分钟。
- `Verify-only` 保留期：至少 `Access Token 最大寿命 + 时钟偏移缓冲`。
- 常规轮换周期：30/60/90 天；发生安全事件时立即轮换。

### 4.4 应急处置

- 疑似密钥泄露：立即停止该 `kid` 的签发。
- 对高风险场景启用 `jti` 黑名单快速吊销。

## 5. Refresh Token 机制

1. `Refresh Token` 必须随机不可预测（至少 256 bit 熵）。
2. 数据库存储仅保存哈希，不保存明文。
3. 启用 `Refresh Token Rotation`：每次刷新都签发新 refresh token，旧 token 立即失效。
4. 启用复用检测（重放防护）：
   - 若已失效 refresh token 再次使用，视为泄露。
   - 立即撤销该会话链路全部 refresh token，并触发告警。
5. 可绑定 `clientId`、`deviceId`（`IP` 绑定可选，避免误伤移动网络用户）。

## 6. 服务端校验策略

1. 严格校验：签名、`iss`、`aud`、`exp`、`nbf`。
2. 设置时钟偏移容忍（建议 60~120 秒）。
3. 校验通过后继续业务态校验：
   - 用户是否禁用/删除。
   - 角色权限是否发生变化（可配合权限版本号）。
4. 高危接口可加二次鉴权或强校验。

## 7. 吊销与登出策略

1. `Access Token` 默认短效，不做强依赖即时吊销。
2. `Refresh Token` 必须支持即时撤销（数据库状态位）。
3. 强制下线：撤销用户全部 refresh token。
4. 可选：在短窗口内维护 `jti` 黑名单。

## 8. 观测与审计

建议记录字段：`sub`、`jti`、`kid`、`aud`、`clientId`、`ip`、`ua`。

核心监控指标：

- 验签失败率。
- refresh 失败率。
- refresh 复用命中率。
- `kid` 分布异常。

建议告警场景：

- 同一 refresh token 异地/并发复用。
- 短时间验签失败激增。
- 非预期 `aud` 访问。

## 9. 推荐落地顺序

1. 先引入 JWT Access Token（与现有 Cookie 并行）。
2. 新增 Refresh Token 表与刷新接口。
3. 引入 `kid + JWKS + 密钥轮换`。
4. 增加吊销中心与风险策略。

## 10. 面向当前项目的实施建议

1. 保留现有 Cookie 登录，新增 Bearer 作为并行通道。
2. 统一认证入口：优先 Cookie，失败后回退 Bearer。
3. 先上线最小能力：`iss/aud/exp` 校验 + refresh 轮换。
4. 二期再上：`JWKS`、自动轮换、黑名单与风控。
