# CodeGraph 使用指南

CodeGraph 是一个为 AI 助手设计的本地优先的代码情报和知识图谱工具。它通过扫描代码库，构建符号关系图谱（Nodes & Edges），从而为 AI 提供深度的上下文理解能力。

## 1. 安装方法

CodeGraph 基于 Node.js 开发，建议进行全局安装：

```bash
# 全局安装
npm install -g @colbymchenry/codegraph
```

## 2. 项目初始化

在项目根目录下执行初始化命令，这会创建 `.codegraph/` 目录并构建初始索引：

```bash
# 初始化当前目录
codegraph init .
```

项目已配置 `codegraph.config.json`，自动排除了 `bin/`, `obj/`, `node_modules/` 等冗余目录。

## 3. 常用命令

| 命令 | 说明 |
| :--- | :--- |
| `codegraph status` | 查看当前索引统计信息（文件数、节点数、数据库大小等） |
| `codegraph index . --force` | 强制重新扫描所有文件并重建索引 |
| `codegraph sync` | 增量同步自上次索引以来的代码变更 |
| `codegraph query "关键词"` | 在图谱中快速检索类、方法、变量等符号 |
| `codegraph callers <symbol>` | 查找指定符号的所有调用者 |
| `codegraph context "任务描述"` | 针对特定任务生成相关的代码上下文（输出为 Markdown） |
| `codegraph unlock` | 如果索引过程中意外中断导致锁定，使用此命令解锁 |

## 4. MCP 服务集成

CodeGraph 支持 **Model Context Protocol (MCP)**，可以让 Claude, Cursor, Trae 等 AI 助手直接调用其分析能力。

### 启动服务
```bash
codegraph serve --mcp
```

### 配置到 AI 助手 (Claude Desktop 示例)
编辑配置文件 `~/Library/Application Support/Claude/claude_desktop_config.json`：

```json
{
  "mcpServers": {
    "codegraph": {
      "command": "codegraph",
      "args": ["serve", "--mcp"]
    }
  }
}
```

## 5. 项目配置

项目根目录下的 `codegraph.config.json` 控制着扫描行为：
- `exclude`: 忽略不需要索引的路径。
- `include`: 指定需要索引的文件类型。
- `watcher`: 控制自动监听和同步行为。

---
*生成的索引数据库存储在 `.codegraph/` 目录中，建议将其加入 `.gitignore`。*
