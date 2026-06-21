# Microi.net License 授权系统文档

> 文档版本：2026-06-22  
> 适用范围：`Microi.Server/Microi.License/`（核心库）+ `Microi.Server/Microi.net.Api/Handler/License/`（桥接层）

---

## 架构概览

### 分层设计（v5.7.6 重构）

```
Microi.License/                          ← 【核心库】独立 License 项目（netstandard2.1）
├── LicenseService.cs      完整逻辑：验证/签发/宽限期/心跳/AES加密/离线申请
├── LicenseModel.cs        数据模型：LicensePayload, LicenseVerifyResult, LicenseProductType
└── HardwareHelper.cs      跨平台 HID 采集

Microi.net.Api/Handler/License/
├── LicenseServiceBridge.cs  【桥接层】委托给 Microi.License.LicenseService
│                           (extern alias MicroiLicense 消除与 Microi.net NuGet 的冲突)
└── LicenseBackgroundService.cs  后台心跳服务

Microi.net.Api/
├── Program.cs             启动 License 验证（含开源版跳过逻辑）
└── Controllers/LicenseController.cs  REST API
```

### 版本模式

| 模式 | 条件 | License 校验 | 功能 |
|------|------|:---:|------|
| **开源版** | `DefaultPublicKeyBase64` 未替换 | ❌ 跳过 | 全部基础功能，仅在线AI受限 |
| **个人版** | 公钥已配置 + license.json 有效 (Personal) | ✅ | 高级报表、自定义域名 |
| **企业版** | 公钥已配置 + license.json 有效 (Enterprise) | ✅ | AI插件、多租户、全功能 |

### 三层防御机制（仅非开源版生效）

```
┌─────────────────────────────────────────────────────────┐
│  第一层：RSA 离线验证（本地 SHA256 签名校验）           │
│   • 公钥内嵌于 DLL，无网络依赖                         │
│   • HID 硬件绑定，换机器自动失效                        │
│   • 签名不可伪造（需私钥）                              │
├─────────────────────────────────────────────────────────┤
│  第二层：在线心跳验证（每 12 小时，后台服务）           │
│   • 向 api.itdos.com 发送心跳检测吊销状态              │
│   • 服务端吊销后最多 12 小时内生效                      │
│   • 离线环境允许继续运行（30 天内无告警）               │
├─────────────────────────────────────────────────────────┤
│  第三层：宽限期机制（文件 + DB 双重防篡改）             │
│   • License 缺失时提供 7 天宽限                         │
│   • 首次部署自动授予 7 天初始宽限期（引导生成密钥对）   │
│   • 删除 .lic_grace 文件无效（DB 保留最早时间）         │
│   • 还原旧数据库无效（ValidProof HMAC 含 HID 绑定）     │
└─────────────────────────────────────────────────────────┘
```

### 涉及文件清单

| 文件 | 位置 | 用途 |
|------|------|------|
| `LicenseService.cs` | `Microi.License/` | **核心授权逻辑**（静态类） |
| `LicenseModel.cs` | `Microi.License/` | 数据模型（Payload、Result、Status 常量） |
| `HardwareHelper.cs` | `Microi.License/` | 跨平台 HID 采集（Linux/Win/macOS/Docker） |
| `LicenseServiceBridge.cs` | `Handler/License/` | 桥接层，委托给 Microi.License |
| `LicenseBackgroundService.cs` | `Handler/License/` | 后台心跳服务（每 12 小时） |
| `LicenseController.cs` | `Controllers/` | REST API 端点 |
| `license.vue` | `Microi.Client/src/views/system/` | 前端授权管理页面 |
| `license.json` | `AppBaseDir/`（运行时生成）| 授权文件（gitignore） |
| `.lic_grace` | `AppBaseDir/`（运行时生成）| 宽限期起始时间（gitignore） |
| `.lic_hb` | `AppBaseDir/`（运行时生成）| 心跳状态缓存（gitignore） |

---

## 二、初始化配置（一次性操作）

### 步骤 1：生成 RSA 密钥对

调用管理接口（仅 License 服务器执行一次）：

```
GET /api/License/GenerateKeyPair
```

输出示例：
```json
{
  "PublicKeyBase64": "MIIBIjANBgkqhkiG9w0B...",
  "PrivateKeyBase64": "MIIEvQIBADANBgkqhkiG9w0B...",
  "PublicKeyPem": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
  "PrivateKeyPem": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----"
}
```

### 步骤 2：配置公钥（所有服务器必须）

在 `Microi.License/LicenseService.cs` 中替换常量：

```csharp
private const string DefaultPublicKeyBase64 = "MIIBIjANBgkqhkiG9w0B..."; // 替换为生成的值
```

> **注意**：文件路径为 `Microi.Server/Microi.License/LicenseService.cs`，不是 `Handler/License/`。

或通过环境变量（优先级更高）：

```bash
MICROI_LICENSE_PUBLIC_KEY=MIIBIjANBgkqhkiG9w0B...
```

### 步骤 3：配置私钥（仅 License 服务器）

**不要将私钥写入代码。** 通过环境变量配置：

```bash
# Linux/Docker
export MICROI_LICENSE_PRIVATE_KEY="MIIEvQIBADANBgkqhkiG9w0B..."

# Windows（PowerShell）
$env:MICROI_LICENSE_PRIVATE_KEY = "MIIEvQIBADANBgkqhkiG9w0B..."

# Docker Compose
environment:
  - MICROI_LICENSE_PRIVATE_KEY=MIIEvQIBADANBgkqhkiG9w0B...
```

---

## 三、技术实现细节（v5.7.6）

### 桥接模式 + extern alias

`Microi.net` NuGet 包内含 `LicenseService` 类型，与 `Microi.License` 项目中的同名类型冲突。解决方案：

**1. csproj 中为 `Microi.License` 添加程序集别名：**

```xml
<!-- Microi.net.Api.csproj -->
<ProjectReference Include="../Microi.License/Microi.License.csproj">
  <Aliases>MicroiLicense</Aliases>
</ProjectReference>
```

**2. `LicenseServiceBridge.cs` 通过 extern alias 引用：**

```csharp
extern alias MicroiLicense;
using LicSvc = MicroiLicense::Microi.License.LicenseService;
```

**3. API 项目代码无需修改**，`LicenseService` 通过桥接类透明解析。

### 开源版模式 (IsOpenSourceMode)

开源版判断条件：`DefaultPublicKeyBase64` 未被替换（仍为 `REPLACE_WITH_YOUR_RSA2048_PUBLIC_KEY_BASE64`）。

开源版行为：
- ✅ 跳过 License 校验，系统正常启动
- ✅ 基础 CRUD、表单引擎、工作流等全部可用
- ❌ 在线 AI 相关功能受限（本地 AI 不受影响）

### 首次部署引导宽限期

`CheckGracePeriod()` 检测到无 License 历史且无宽限期文件时，自动授予 7 天初始宽限期，确保开发者能正常启动系统并完成密钥配置。

---

## 四、License 文件格式

### `license.json` 结构

```json
{
  "HID": "A1B2C3D4...",
  "Company": "示例公司",
  "Name": "张三",
  "Phone": "138xxxxxxxx",
  "IP": "192.168.1.100",
  "ProductType": "Enterprise",
  "IssuedAt": "2026-06-01T00:00:00Z",
  "ExpirationDate": "2027-06-01T00:00:00Z",
  "UpdateExpirationDate": "2027-06-01T00:00:00Z",
  "Signature": "Base64EncodedRSASignature..."
}
```

### 产品类型

| ProductType | AI 插件 | 多租户 | 高级报表 | 自定义域名 |
|---|:---:|:---:|:---:|:---:|
| `Personal` | ❌ | ❌ | ✅ | ✅ |
| `Enterprise` | ✅ | ✅ | ✅ | ✅ |

---

## 五、硬件指纹（HID）

### 生成规则

```
优先级：
  1. 环境变量 MICROI_MACHINE_ID（Docker/K8s 固定 HID）
  2. Linux：/etc/machine-id 或 /var/lib/dbus/machine-id
  3. Windows：注册表 HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid
  4. 拼接首个有效网卡 MAC 地址

最终 HID = SHA256(machineId + ":" + mac) → 64位大写十六进制
```

### Docker 部署（固定 HID）

```yaml
environment:
  - MICROI_MACHINE_ID=your-fixed-machine-id-here
```

---

## 六、授权流程

### 在线申请流程（可访问 api.itdos.com）

```
1. 前端页面 → 「授权管理」→「提交授权申请」
2. 填写公司/联系人/电话/验证码 → 点击「在线提交申请」
3. 等待官方审核（Status: Pending → Issued）
4. 切换到「检查并部署License」→「自动部署到服务器」
5. 系统重启后生效
```

### 离线申请流程（纯内网/无公网）

```
1. 前端页面 → 「授权管理」→「提交授权申请」
2. 填写公司/联系人/电话 → 点击「离线申请：生成注册文件」
3. 下载 microi-registration.json（含 HID + 防篡改哈希）
4. 将文件发送至 license@microi.net
5. 收到官方签发的 license.json 后：
   「授权管理」→「手动导入授权文件」→ 上传或粘贴内容
6. 系统重启后生效
```

### 证书续期

1. 到期前联系官方续期，或通过前端「检查并部署」重新部署
2. 重新导入新 `license.json` 后调用 `/api/License/Verify` 确认

---

## 七、API 端点参考

### 客户服务器端点（匿名或登录）

| 方法 | 端点 | 说明 |
|------|------|------|
| GET | `/api/License/GetHardwareId` | 获取当前服务器 HID |
| GET | `/api/License/Verify` | 验证本地 License |
| POST | `/api/License/WriteLicenseFile` | 写入 License 文件 |
| POST | `/api/License/GenerateRegistrationFile` | 生成离线注册申请包 |
| GET | `/api/License/Diagnostics` | 获取诊断信息 |

### License 服务器端点（超级管理员）

| 方法 | 端点 | 说明 |
|------|------|------|
| POST | `/api/License/Apply` | 提交申请 |
| POST | `/api/License/Issue` | 签发 License |
| POST | `/api/License/Approve` | 审批通过并签发 |
| POST | `/api/License/Reject` | 驳回申请 |
| POST | `/api/License/Revoke` | 吊销/恢复 License |
| GET | `/api/License/QueryApplication` | 查询申请状态 |
| GET | `/api/License/GenerateKeyPair` | 生成密钥对 |
| GET | `/api/License/List` | 🆕 License 列表（支持 ?status=Pending 筛选） |
| GET | `/api/License/Logs` | 🆕 操作日志（支持 ?hid=xxx 按 HID 筛选） |

### 操作日志动作类型

| Action | 触发场景 |
|--------|---------|
| `Apply` | 提交授权申请 |
| `Issue` | 直接签发 License |
| `Approve` | 审核通过并签发 |
| `Reject` | 驳回申请 |
| `Revoke` | 作废 License |
| `Restore` | 恢复已作废的 License |
| `Deploy` | 部署 License 到本地服务器 |
| `ImportReg` | 导入离线注册文件 |

### 管理员前端页面

超级管理员登录后，`授权管理` 页面自动显示额外的管理 Tab：

| Tab | 功能 |
|-----|------|
| **License 管理** | 列表查看、状态筛选、审核/驳回、直接签发、作废/恢复 |
| **操作日志** | 按 HID 筛选查看操作记录（操作人、IP、详情、时间） |

---

## 八、宽限期机制详解

### 触发条件

`license.json` 缺失或验证失败时触发：

| 场景 | 行为 |
|------|------|
| **首次部署**（无 License 历史，无 .lic_grace 文件）| 🆕 自动授予 7 天初始宽限期，提示生成密钥对自签发 |
| **曾有 License**（DB 中有 ValidProof 记录，到期 ≤7 天）| 进入 7 天宽限期 |
| **曾有 License**（ValidProof 到期 >7 天）| 无宽限期，拒绝启动 |
| **宽限期内** 删除 .lic_grace | DB 保留最早时间，宽限期不重置 |

### 首次部署引导（Bootstrap Grace）

当 `CheckGracePeriod()` 检测到：
- DB 已就绪
- 无 `ValidProof` 记录（从未有过 License）
- 无 `.lic_grace` 文件（从未进入过宽限期）

则自动创建宽限期标记，授予 7 天初始宽限期，并在日志中打印：
```
Microi：【🆕License引导】首次部署，自动授予 7 天初始宽限期。请尽快生成密钥对并自签发 License！
```

此机制确保开发者首次部署时能正常启动系统，有足够时间完成密钥生成和 License 自签发。

### 防篡改设计

```
ValidProof 存储格式（diy_license 表）：
  HID  = "__LICENSE_VALID_PROOF__"
  LicenseContent = "<expStr>|<HMAC>"

HMAC 计算：
  key  = SHA256(HID + "microi-valid-proof-2026")
  data = expStr + HID
  sig  = HMAC-SHA256(key, data)
```

### 各攻击场景

| 攻击方式 | 结果 |
|---|---|
| 删除 license.json | 宽限期 7 天（需有历史 License） |
| 删除 license.json + .lic_grace | 宽限期不重置（DB 保留最早时间） |
| 还原旧数据库 | HMAC 含 HID，跨机无效；同机旧 DB 有记录则到期计算正确 |
| 首次使用（无 License 历史）| 🆕 自动 7 天初始宽限期，可正常启动 |
| License 过期超 7 天 | 无宽限期，拒绝启动 |

---

## 九、启动行为说明

```
应用启动
  │
  ├─ LicenseService.IsOpenSourceMode()
  │   └─ [true：公钥未配置]
  │       └─ 开源版模式，跳过所有 License 校验，正常启动 ✅
  │
  └─ [false：公钥已配置，进入商业版 License 流程]
      ├─ LoadHeartbeatStatus()     从 .lic_hb 还原上次吊销状态
      ├─ LicenseService.Verify()   RSA 本地验证
      │
      ├─ [Valid=true]
      │   ├─ IsRevokedByServer?    → 是：Exit(1) 拒绝启动
      │   ├─ CheckOfflineDays()    → 超 30 天：打印告警（不阻断）
      │   └─ 正常启动 ✅
      │
      └─ [Valid=false]
          ├─ CheckGracePeriod()
          │   ├─ 首次部署，无 ValidProof，无 .lic_grace → 🆕 自动授予 7 天初始宽限期
          │   ├─ DB 可用且 ValidProof 超期            → 无宽限期，Exit(1)
          │   ├─ 宽限期内（DaysLeft > 0）             → ⚠️ 降级运行
          │   └─ 宽限期已过（DaysLeft = 0）           → ❌ Exit(1) 拒绝启动
          └─ [宽限期模式下功能受限]

应用运行中（每 12 小时）
  └─ LicenseBackgroundService → HeartbeatAsync()
      ├─ 成功且 Status=Revoked → 写 .lic_hb + 内存标记，下次启动触发 Exit(1)
      ├─ 成功且 Status=Issued  → 更新 .lic_hb 心跳时间
      └─ 失败（离线）          → 记录日志，不影响运行

应用后台初始化完成后
  └─ License 状态持久化
      ├─ Valid=true  → WriteValidProof(expirationDate) 更新 HMAC 证明
      └─ Valid=false → PersistGracePeriodToDb() 同步宽限期起始时间到 DB
```

---

## 十、功能门控（Feature Gate）

在业务代码中按功能控制访问：

```csharp
// 检查 AI 插件权限（Enterprise 才有）
if (!LicenseService.IsFeatureAllowed(LicenseService.Features.AiPlugin))
    return Unauthorized("此功能需要 Enterprise 授权");

// 检查多租户权限
if (!LicenseService.IsFeatureAllowed(LicenseService.Features.MultiTenant))
    return Unauthorized("多租户功能需要 Enterprise 授权");
```

| Feature 常量 | 要求 |
|---|---|
| `AiPlugin` | Enterprise |
| `MultiTenant` | Enterprise |
| `AdvancedReport` | 任意有效 License |
| `CustomDomain` | 任意有效 License |
| `LicenseAdmin` | 任意有效 License |

---

## 十一、数据库表结构

### diy_license（License 授权记录）

```sql
CREATE TABLE diy_license (
  Id                  VARCHAR(32)   NOT NULL PRIMARY KEY,
  HID                 VARCHAR(64)   NOT NULL,
  Company             VARCHAR(200)  DEFAULT '',
  Name                VARCHAR(100)  DEFAULT '',
  Phone               VARCHAR(20)   DEFAULT '',
  IP                  VARCHAR(50)   DEFAULT '',
  ProductType         VARCHAR(20)   DEFAULT 'Personal',
  Status              VARCHAR(20)   NOT NULL,   -- Pending/Issued/Revoked/Rejected/Grace/ValidProof
  LicenseContent      TEXT,
  IssuedAt            DATETIME,
  ExpirationDate      DATETIME,
  UpdateExpirationDate DATETIME,
  RejectReason        VARCHAR(500),
  Remark              VARCHAR(500)  DEFAULT '',
  CreateTime          DATETIME      NOT NULL,
  UpdateTime          DATETIME
);
CREATE INDEX idx_diy_license_hid ON diy_license (HID);
```

### diy_license_log（操作日志）

```sql
CREATE TABLE diy_license_log (
  Id          VARCHAR(32)  NOT NULL PRIMARY KEY,
  HID         VARCHAR(64)  NOT NULL,
  Action      VARCHAR(20)  NOT NULL,   -- Apply/Issue/Approve/Reject/Revoke/Restore/Deploy/ImportReg
  Operator    VARCHAR(100) DEFAULT '',
  OperatorIP  VARCHAR(50)  DEFAULT '',
  Detail      VARCHAR(1000) DEFAULT '',
  CreateTime  DATETIME     NOT NULL,
  INDEX idx_log_hid (HID),
  INDEX idx_log_time (CreateTime)
);
```

### 独立数据库配置（可选）

默认 License 数据与主平台共用数据库。如需独立存储，配置以下任一项：

```bash
# 环境变量
MICROI_LICENSE_DB_CONN=Server=127.0.0.1;port=3306;database=microi_license;user=root;password=***

# 或 appsettings.json
{
  "AppSettings": {
    "LicenseDbConn": "Server=127.0.0.1;port=3306;database=microi_license;user=root;password=***"
  }
}
```

> License 数据库类型自动与主库一致（MySQL/SqlServer/Oracle）。

**特殊 HID 保留值（系统内部使用）：**

| HID 值 | 用途 |
|---|---|
| `__LICENSE_GRACE_START__` | 宽限期起始时间 |
| `__LICENSE_VALID_PROOF__` | HMAC 签名的历史有效证明 |

---

## 十二、加密体系说明

### 加密格式

所有加密内容均以 `MILIC_ENC:` 为前缀，后跟 Base64 编码的 `IV(16字节) + CipherText`：

```
MILIC_ENC:<Base64(AES-IV[16] + AES-CipherText)>
```

`AesDecrypt` 自动识别前缀，**无前缀的旧版明文数据原样返回**，保证向后兼容。

### 密钥层次

| 密钥用途 | 派生来源 | 算法 | 保护内容 |
|---|---|---|---|
| 文件密钥 | `PBKDF2(HID + "microi-lic-enc-2026", salt, 100000, SHA256)` | AES-256-CBC | 注册文件体 + License 文件 + DB LicenseContent |
| DB 密钥 | `PBKDF2(MICROI_LICENSE_ENCRYPT_KEY \|\| 默认值, salt, 100000, SHA256)` | AES-256-CBC | ValidProof 内容 + GracePeriod 内容 |

### 数据流

```
注册文件生成（客户端）
  packageJson → AesEncrypt(DeriveFileKey(HID)) → microi-registration.milic
  官方收到后用相同 HID 派生密钥解密，签发时再加密 licenseContent

License 签发（License 服务器）
  licenseJson → RSASign → AesEncrypt(DeriveFileKey(HID)) → DB LicenseContent

License 下载部署（客户端）
  DB LicenseContent(加密) → WriteLicenseFile → AesDecrypt(DeriveFileKey(本机HID))
    → RSA验签 → 写磁盘(保持加密格式)

license.json 本地验证
  读文件 → AesDecrypt(DeriveFileKey(本机HID)) → RSA验签 → 返回验证结果
```

> **注意**：文件密钥与 HID 绑定。将 `license.json` 拷贝到不同机器 → 解密失败 → 验证失败。
> 这在 RSA HID 签名之外又增加了一层机器绑定保护。

---

## 十三、环境变量汇总

### 环境变量

| 变量名 | 必须 | 说明 |
|--------|:----:|------|
| `MICROI_LICENSE_PRIVATE_KEY` | License 服务器 | PKCS#8 DER Base64 私钥 |
| `MICROI_LICENSE_PUBLIC_KEY` | 可选 | 替代内嵌公钥常量 |
| `MICROI_MACHINE_ID` | Docker 推荐 | 固定容器 HID，防止重建后变化 |
| `MICROI_LICENSE_ENCRYPT_KEY` | 生产环境推荐 | DB 内容加密主密钥（未设置使用内置默认值，安全性较低）|

### appsettings.json 配置项

以下字段均在 `AppSettings` 节中配置，**可在不重新部署的情况下修改**（`ReloadOnChange=true`）：

```json
{
  "AppSettings": {
    "LicenseHeartbeatUrl": "https://api.itdos.com/api/License/Heartbeat",
    "LicenseContactEmail": "license@microi.net"
  }
}
```

| 配置键 | 默认值 | 说明 |
|--------|--------|------|
| `LicenseHeartbeatUrl` | `https://api.itdos.com/api/License/Heartbeat` | 心跳验证服务器地址，私有化部署可指向内部服务器 |
| `LicenseContactEmail` | `license@microi.net` | 离线注册文件接收邮箱，前端页面动态展示 |

> 配置优先级：`appsettings.{env}.json` > `appsettings.json` > 代码内置默认值

---

## 十四、常见问题

**Q：License 文件被意外删除怎么办？**  
A：7 天宽限期内通过「手动导入授权文件」重新导入即可，不影响运行。超过 7 天需联系官方。

**Q：Docker 每次重建容器 HID 变了？**  
A：设置环境变量 `MICROI_MACHINE_ID` 为固定值，或将 `license.json` 挂载为持久卷。

**Q：纯内网部署如何申请授权？**  
A：使用「生成注册文件」功能，下载 `microi-registration.json` 发送至 `license@microi.net`，收到授权后通过「手动导入」写入。

**Q：如何判断当前是否处于宽限期？**  
A：调用 `GET /api/License/Verify`，检查返回值中的 `IsGracePeriod` 字段。

**Q：License 吊销后多久生效？**  
A：下次心跳时（最多 12 小时）生效，下次重启时从 `.lic_hb` 文件立即生效。
