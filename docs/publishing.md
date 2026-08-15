# 发布到 GitHub 指南

本文说明如何把本仓库发布到 GitHub。以下步骤均在你的本地机器上执行，**不包含代替你发布**。

---

## 0. 准备工作

| 项目 | 说明 |
|---|---|
| Git | `git --version` 可查看版本；没有则安装 [Git for Windows](https://git-scm.com/) |
| GitHub 账号 | [github.com](https://github.com) 注册 |
| .NET 8 SDK | 用于本地验证与发布包制作 |
| 可选：GitHub CLI | `winget install --id GitHub.cli`，可简化创建仓库与 Release |

建议在 Git 中设置身份（仅首次需要）：

```powershell
git config --global user.name "你的名字"
git config --global user.email "你的邮箱"
```

---

## 1. 确认仓库内容干净

本仓库已整理为源码 + 文档 + 配置，构建产物已移出到 `D:\SoundGuard-artifacts`（不在仓库内）。
确认 `.gitignore` 已忽略 `bin/`、`obj/`、`publish/` 等目录：

```powershell
git status --ignored
```

若需要重新构建（会重新生成 `bin/obj`，但不会被提交）：

```powershell
dotnet build -c Release
dotnet test
```

---

## 2. 初始化 Git 仓库并提交

在项目根目录 `D:\SoundGuard` 执行：

```powershell
cd D:\SoundGuard

# 初始化仓库（默认分支设为 main）
git init -b main

# 添加所有应提交的文件
git add .

# 查看将要提交的内容
git status

# 创建第一个提交
git commit -m "Initial commit: SoundGuard real-time loudness protector"
```

---

## 3. 在 GitHub 上创建远程仓库

### 方式 A：网页创建

1. 打开 [github.com/new](https://github.com/new)。
2. `Repository name` 填 `SoundGuard`。
3. 选择 **Public**（开源）或 **Private**。
4. **不要**勾选 “Add a README / .gitignore / license”，因为仓库里已经有这些文件。
5. 点击 **Create repository**。

### 方式 B：GitHub CLI 创建

```powershell
gh auth login
gh repo create SoundGuard --public --source . --remote origin --push
```

执行后会自动创建仓库、添加 `origin` 并推送，可跳到第 5 步。

---

## 4. 关联远程并推送

```powershell
git remote add origin https://github.com/<你的用户名>/SoundGuard.git
git push -u origin main
```

> 若使用 SSH：`git remote add origin git@github.com:<你的用户名>/SoundGuard.git`

---

## 5. 推送后的检查

- 打开 `https://github.com/<你的用户名>/SoundGuard`。
- 确认 README、LICENSE、docs、src 均已上传。
- GitHub 会自动识别根目录的 `LICENSE` 为 MIT 许可证。
- CI（`.github/workflows/build.yml`）会自动开始构建并运行测试，可在 **Actions** 页查看结果。

---

## 6. 创建 Release（发布二进制）

### 6.1 本地发布自包含版本（可选）

```powershell
# 自包含单文件（无需目标机器安装 .NET 运行时）
dotnet publish src\SoundGuard.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o D:\SoundGuard-artifacts\release
```

### 6.2 打标签并推送

```powershell
git tag v1.0.0
git push origin v1.0.0
```

### 6.3 用 GitHub CLI 创建 Release 并附加二进制

```powershell
gh release create v1.0.0 `
  --title "SoundGuard v1.0.0" `
  --notes "首个 MVP 版本：双阶段保护引擎、LUFS/True Peak 表头、托盘与自动恢复。" `
  D:\SoundGuard-artifacts\release\SoundGuard.exe
```

也可以在上传前压缩：

```powershell
Compress-Archive -Path D:\SoundGuard-artifacts\release\SoundGuard.exe -DestinationPath D:\SoundGuard-artifacts\SoundGuard-v1.0.0-win-x64.zip
gh release create v1.0.0 D:\SoundGuard-artifacts\SoundGuard-v1.0.0-win-x64.zip --title "SoundGuard v1.0.0"
```

### 6.4 网页方式创建 Release

1. 在仓库页点击右侧 **Releases → Draft a new release**。
2. `Choose a tag` 输入 `v1.0.0` 并创建。
3. 填写标题与说明。
4. 将本地发布包拖入 “Attach binaries” 区域。
5. 点击 **Publish release**。

---

## 7. 可选：完善仓库信息

在仓库页 **Settings** 中：

- **Description**：填写一句话描述，例如 `Windows 系统级实时响度保护器（LUFS/True Peak 监测 + 自动音量保护）`。
- **Topics**：添加 `audio`、`loudness`、`hearing-protection`、`wasapi`、`csharp`、`wpf` 等标签。
- **Social preview**：可上传一张应用截图作为社交预览图。

---

## 8. 常见问题

### Q1：`git push` 提示认证失败
GitHub 不再支持密码推送。请使用 **Personal Access Token (PAT)** 或 **SSH**：
- HTTPS + PAT：在 [github.com/settings/tokens](https://github.com/settings/tokens) 生成 token，作为密码使用。
- SSH：`ssh-keygen` 生成密钥后，在 [github.com/settings/keys](https://github.com/settings/keys) 添加公钥。

### Q2：CI 构建失败
- 查看 **Actions** 页日志。
- 本机复现：`dotnet restore && dotnet build -c Release && dotnet test`。

### Q3：想把构建产物也放进仓库
不建议。`.gitignore` 已忽略 `bin/obj/publish`。二进制请用 **Release** 附件发布；产物留在
`D:\SoundGuard-artifacts` 即可。

### Q4：许可证如何生效
根目录的 `LICENSE` 采用 MIT。GitHub 会在仓库首页自动显示 “MIT license”。如果修改了作者/年份，
记得同步更新 `LICENSE` 中的版权行。
