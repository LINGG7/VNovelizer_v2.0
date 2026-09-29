# bgfade 背景资源核对

范围：43 个 Excel 剧本的 Command 列（包含 parallel 中的 bgfade），对照 Backgrounds 目录及子目录的 256 个图片文件；忽略扩展名和大小写，不将 .meta 当图片。只读检查，未修改文件。

## 缺失图片：5 个资源名、6 处引用

| 背景资源名 | 剧本 | ID | 单元格 |
|---|---|---|---|
| sntzm_clark4F(1)_9 | sntzm_clark4F(1).xlsx | 9 | K11 |
| sntzm_clark4F(1)_41 | sntzm_clark4F(1).xlsx | 41 | K39 |
| sntzm_clark4F(1)_41 | sntzm_david3F(2).xlsx | 60 | K60 |
| sntzm_clark4F(1)_73 | sntzm_clark4F(1).xlsx | 73 | K52 |
| sntzm_clark4F(1)_81 | sntzm_clark4F(1).xlsx | 81 | K61 |
| sntzm_david3_81 | sntzm_david313-315(2).xlsx | 43 | K44 |

## 命令误写：1 处

sntzm_gy(ns1).xlsx，ID 72，K58：

```text
parallel(memoryoff();bgfade(bgfade(sntzm_gy(2)_66,1),1))
```

这里将 bgfade 嵌套写了两次。sntzm_gy(2)_66 的图片实际存在，建议命令改为：

```text
parallel(memoryoff();bgfade(sntzm_gy(2)_66,1))
```

未检出其他 bgfade 图片缺失。本次只核对文件存在性，不验证 Unity 图片导入设置或实际加载结果。