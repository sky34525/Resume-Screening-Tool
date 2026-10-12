# ResumeScreening — AI 简历筛选助手

基于 AI 的桌面端简历筛选工具，支持批量导入简历、自定义权重评分、可视化分析报表。采用 WinUI 3 桌面客户端 + ASP.NET Core 本地 API 架构，数据完全本地化处理。

## 功能概览

| 模块 | 功能 |
|------|------|
| **简历库** | 批量导入 PDF 简历，AI 自动解析为结构化档案，支持浏览、搜索、删除 |
| **AI 初筛** | 创建招聘职位，添加候选人简历，AI 按学历/工作经验/专业匹配度/性别/年龄多维度加权评分 |
| **分析报表** | Token 用量趋势图、请求次数统计、按日期聚合的可视化报表 |
| **设置** | DeepSeek API Key 配置（DPAPI 加密缓存）、初筛权重自定义、明暗主题切换 |

## 项目结构

```
ResumeScreening.sln
├── src/
│   ├── ResumeScreening.Api/         # ASP.NET Core Web API (.NET 8)
│   │   ├── Services/                # AI 分析、职位筛选、凭证加密存储
│   │   ├── Program.cs               # 启动入口 + 路由注册
│   │   └── appsettings.json         # API 与 AI 配置
│   ├── ResumeScreening.Contracts/   # 共享 DTO 与数据模型 (.NET 8)
│   └── ResumeScreening.Desktop/     # WinUI 3 桌面客户端 (.NET 10)
│       ├── Models/                  # 客户端数据模型
│       ├── Services/                # API 客户端、工作区缓存
│       ├── ViewModels/              # MVVM 视图模型
│       └── MainPage.*.cs            # 页面分部类（筛选/设置/分析/持久化）
├── publish/                         # 发布输出目录
├── launch.ps1                       # 一键启动脚本
└── .github/workflows/release.yml    # GitHub Actions 自动构建发布
```

## 技术栈

| 层 | 技术 |
|----|------|
| 桌面客户端 | WinUI 3 · Windows App SDK 2.5 · .NET 10 · CommunityToolkit.Mvvm |
| API 后端 | ASP.NET Core 8 · Minimal APIs · HttpClientFactory |
| AI 引擎 | OpenAI 兼容 API（DeepSeek V4）· Structured Outputs (JSON Schema) |
| 安全 | Windows DPAPI 加密 · 仅本地回环访问 · Data Protection API |
| PDF 解析 | PdfPig · PDFtoImage · PyPDF2 |
| 数据存储 | 本地 JSON 文件 · 工作区自动保存/恢复 |

## 快速开始

### 环境要求

- Windows 10 1809+ 或 Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Windows App SDK Runtime 2.5+](https://aka.ms/windowsappsdk/1.6/2.5.1/windowsappruntimeinstall-x64.exe)（仅运行发布包时需要）
- Python 3.12+（用于 PDF 解析脚本）

### 开发模式运行

```powershell
# 1. 启动 API
cd src\ResumeScreening.Api
dotnet run

# 2. 启动桌面应用（另一个终端）
cd src\ResumeScreening.Desktop
dotnet run
```

### 发布与运行

```powershell
# 一键发布
dotnet publish src\ResumeScreening.Api\ResumeScreening.Api.csproj -c Release -o publish\api
dotnet publish src\ResumeScreening.Desktop\ResumeScreening.Desktop.csproj -c Release -p:Platform=x64 -o publish\desktop

# 一键启动
.\launch.ps1
```

## 配置说明

编辑 `src\ResumeScreening.Api\appsettings.json`：

```json
{
  "Urls": "http://localhost:52081",
  "AI": {
    "Enabled": true,
    "BaseUrl": "http://your-ai-endpoint:9000/",
    "ApiPath": "v1/chat/completions",
    "Model": "deepseek-v4-pro",
    "RequestTimeoutSeconds": 180,
    "MaxOutputTokens": 16384
  }
}
```

API Key 通过桌面应用「设置」页面配置，使用 Windows DPAPI 加密存储在 `%LocalAppData%\ResumeScreening\`。

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/health` | 健康检查 |
| `POST` | `/api/resume-analysis` | AI 简历解析 |
| `POST` | `/api/job-screening` | 职位筛选 |
| `GET` | `/api/job-screening/{jobId}` | 获取筛选结果 |
| `GET` | `/api/ai-settings` | 查询 API Key 配置状态 |
| `POST` | `/api/ai-settings` | 保存 API Key |
| `GET` | `/api/analytics/report` | 获取用量分析报表 |

## 安全设计

- API 仅监听 `localhost:52081`，所有 AI 设置端点校验请求来源必须为本地回环地址
- API Key 使用 Windows DPAPI 加密存储，重启后自动解密加载
- AI 评级始终标注为「待人工审核」，系统不会自动拒绝候选人
- 年龄和性别维度默认权重为 0%，需手动启用

## 许可

内部工具，未设定开源许可证。
dotnet run --project src/ResumeScreening.Api
```

服务默认监听 `http://localhost:5080`。Swagger 位于 `/swagger`（Development 环境）。

### WinUI 桌面端

桌面端位于 `src/ResumeScreening.Desktop`，提供候选人简历库的初步界面：可从本机批量选择 PDF 或常见图片，在可滚动的多列卡片中查看首页预览，并预留 AI 初筛、职位管理和分析报表导航入口。

```powershell
winapp run .\src\ResumeScreening.Desktop\ResumeScreening.Desktop.csproj --arch x64
```

本地文件会完整读取到内存：PDF 由 PDFium 生成首页预览，并由 PdfPig 提取可供 AI 初筛使用的文本。若普通 .NET 文件流读不到标准 `%PDF-` 文件头，桌面端会调用本机 Python 3 重新读取字节，并只在系统临时目录生成随即删除的中转文件；整个过程不上传网络，也不会修改原简历。当前导入列表仅保留在本次运行中，且单文件不能超过 25 MB；密码保护的 PDF 需要先解锁。

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
