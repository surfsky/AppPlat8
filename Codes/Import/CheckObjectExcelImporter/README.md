# CheckObject Excel Importer

导入 `Doc/Data/260514-对象数据.xlsx` 到 `CheckObjects`，并将 `所属网格` 按树结构补齐到 `Orgs`。

## 功能

- 支持将 Excel 的 `主键ID` 列导入到 `CheckObject.Code` 字段。
- 按 `社会统一信用代码` 优先匹配已有 `CheckObject`。
- 当无社会信用代码时，按 Excel 的 `ID` 匹配已有 `CheckObject`。
- 若仍未命中，则按 `名称 + 所属网格叶子节点` 兜底匹配。
- 不存在则新增，存在则更新。
- `所属网格` 自动拆分为树路径，缺失节点自动插入 `Orgs`。
- 自动尝试补充 `CheckObjects.LatestCheckDt` 列（可用 `--no-schema-update` 关闭）。
- 自动尝试补充 `CheckObjects.Code` 列（可用 `--no-schema-update` 关闭）。
- 生成导入日志，默认输出到 `Doc/Data/260514-对象数据.import.log.txt`。

## 用法

在仓库根目录执行：

```bash
dotnet run --project Codes/Import/CheckObjectExcelImporter/CheckObjectExcelImporter.csproj
```

可选参数：

- `--excel <path>`: Excel 文件路径
- `--db <path>`: SQLite 文件路径
- `--report <path>`: 导入报告输出路径
- `--dry-run`: 仅解析和映射，不写数据库
- `--no-schema-update`: 不执行 `LatestCheckDt` 列补充

示例：

```bash
dotnet run --project Codes/Import/CheckObjectExcelImporter/CheckObjectExcelImporter.csproj --dry-run
```
