# AI 简历筛选系统

一个以 **Moka ATS** 为输入源的 .NET 8 Web API 服务。它通过 Moka Webhook 或按申请 ID 拉取候选人信息及解析简历，并以可解释的多策略结果辅助招聘人员完成初筛。

## 已实现的主链路

```text
Moka Webhook / applicationId
          │
          ▼
  MokaCandidateClient（Basic Auth）
          │  候选人信息 + 简历解析文本
          ▼
  ResumeNormalizer → 规则过滤 / 技能匹配 / AI 结构化评估
          │
          ▼
  可解释筛选报告（持久化） → 人工审核
```

- **Moka 接入**：接收 `pushCandidate` 格式 Webhook、WebHook 幂等去重；也可调用接口按 `applicationId` 主动筛选。
- **多方式筛选**：学历/工作年限/必需技能硬条件、技能关键词覆盖率、可选 OpenAI 兼容模型的结构化评估、风险提示。
- **人机边界**：AI 只产生建议；每份结果均标为 `NeedsHumanReview`，系统不会自动拒绝、不会以性别、年龄、民族、国籍等受保护信息参与评分。
- **可追溯性**：报告包含每条规则的命中情况、权重、证据、模型版本和原始 Moka 申请 ID。结果保存在本地 JSON 文件，适合原型/单实例部署；生产环境应替换为加密数据库存储。

## 启动

机器需要安装 .NET 8 SDK：

```bash
dotnet restore
dotnet run --project src/ResumeScreening.Api
```

服务默认监听 `http://localhost:5080`。Swagger 位于 `/swagger`（Development 环境）。

### WinUI 桌面端

桌面端位于 `src/ResumeScreening.Desktop`，提供候选人简历库的初步界面：可批量导入 PDF 或常见图片，在可滚动的多列卡片中查看首页预览，并预留 AI 初筛、职位管理和分析报表导航入口。

```powershell
winapp run .\src\ResumeScreening.Desktop\ResumeScreening.Desktop.csproj --arch x64
```

当前导入列表仅保留在本次运行中；关闭应用不会修改或删除原始简历文件。

复制 `src/ResumeScreening.Api/appsettings.json` 的相应字段到本机环境变量或 `appsettings.Local.json`（该文件被 Git 忽略）：

```bash
export Moka__ApiKey='来自 Moka CSM 的 API Key'
export Moka__WebhookSecret='为本服务设置的随机共享密钥'
export AI__ApiKey='你的模型服务密钥'
```

`Moka:WebhookSecret` 为空时仅建议在本地调试；生产环境必须配置，并通过反向代理/TLS 限制来源。Moka API 的具体权限与可用字段因租户开通能力不同，配置后先用一个测试申请验证。

## 最小调用示例

1. 创建职位筛选规则：

```bash
curl -X PUT http://localhost:5080/api/jobs/backend-dotnet \
  -H 'Content-Type: application/json' \
  -d '{"title":".NET 后端工程师","minimumYearsOfExperience":3,"minimumEducation":"本科","mustHaveSkills":["C#","ASP.NET Core"],"preferredSkills":["SQL","Docker"],"minimumScoreForRecommendation":70}'
```

2. 根据 Moka 申请 ID 筛选：

```bash
curl -X POST http://localhost:5080/api/screenings/moka/411348665?jobId=backend-dotnet
```

3. 配置 Moka Webhook 到：

```text
POST https://your-domain.example/api/webhooks/moka?jobId=backend-dotnet
Header: X-Moka-Webhook-Secret: <Moka__WebhookSecret>
```

系统只会处理带 `applicationId` 的事件。若收到的事件只包含候选人摘要，服务会按申请 ID 再向 Moka 拉取完整信息与简历文本。

## API

| 方法 | 地址 | 用途 |
| --- | --- | --- |
| `PUT` | `/api/jobs/{jobId}` | 创建或更新职位筛选画像 |
| `GET` | `/api/jobs` | 查看职位画像 |
| `POST` | `/api/screenings/moka/{applicationId}?jobId=...` | 主动拉取并筛选 Moka 候选人 |
| `POST` | `/api/screenings` | 用测试/其他来源的标准化简历筛选 |
| `POST` | `/api/webhooks/moka?jobId=...` | 接收 Moka Webhook |
| `GET` | `/api/screenings/{screeningId}` | 查询一份筛选报告 |
| `GET` | `/api/screenings?jobId=...` | 查看职位的筛选报告 |

## 生产前必做

- 给管理和查询 API 加企业 SSO/RBAC；目前未内置身份认证，不能裸露在公网。
- 将 JSON 存储替换为数据库，并对简历、联系方式、模型日志加密、设置保留期和删除流程。
- 将 Webhook 放在 HTTPS 反向代理之后，校验来源/签名（以租户实际 Moka 文档为准）。
- 对每个职位的评分阈值、规则和模型提示词进行招聘负责人审批与偏差评估。
- 不把模型结论自动回写为淘汰；由有权限的招聘人员审核后，再通过独立的 Moka 回写适配器执行。

## Moka 文档依据

- [候选人 API：按申请 ID 获取解析简历文本](https://www.mokahr.com/docs/api/)
- [候选人 API：获取候选人信息 v2](https://www.mokahr.com/docs/api/view/v2.html)
- [Moka Webhook 事件格式](https://www.mokahr.com/docs/api/index.html)
