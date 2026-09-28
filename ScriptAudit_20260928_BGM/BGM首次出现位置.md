# BGM 首次出现位置（按剧情分支）

范围：43 个 Excel 剧本，仅检查 BGM 列，不使用 CSV。由 sntzm_intro 开场出发，遵循 jump、loadScript、choice、Note 标签与隐藏分支层级；结局文本为终点。空 BGM 延续已有音乐，stop 不算音乐，也不会重置“曾经听过”的记录。

“首次”指在某条可达路线中，此前未出现过该音乐。跳过早期音乐节点的分支，可能在后面的章节首次听到同一音乐，所以同一首音乐可以有多个首次位置。没有将不同分支强行按文件名或 ID 排出先后。

结果：19 首音乐、51 个首次出现位置；模拟 225462 个剧情状态，未遇到断点。所有 51 个位置均重新回放示例选择路径核对，确认在该路径上没有更早出现相同 BGM。全部非空音乐节点均可达。

## 汇总

| 音乐（BGM 原值） | 可作为首次出现的剧本与 ID |
|---|---|
| 1 enhanced alex-morgan-dark-suspense-thriller | sntzm_david313-315(1).xlsx → ID 1；sntzm_gy(1).xlsx → ID 83 |
| 3 enhanced leberch-horror-night | sntzm_david313-315(4).xlsx → ID 53；sntzm_ending5(1).xlsx → ID 53 |
| 4 enhanced leberch-spooky-piano | sntzm_david313-315(1).xlsx → ID 34；sntzm_final(5).xlsx → ID 20；sntzm_gy(ns1).xlsx → ID 44 |
| 5 enhanced leberch-suspense | sntzm_clark3F(3).xlsx → ID 62；sntzm_clark4F(3).xlsx → ID 69；sntzm_david313-315(1).xlsx → ID 79；sntzm_david4F(4).xlsx → ID 13；sntzm_final(3).xlsx → ID 115 |
| 6 enhanced oceanframemusic-horror-background-music | sntzm_clark4F(4).xlsx → ID 49；sntzm_ending5(3).xlsx → ID 1 |
| 7 enhanced litesaturation-horror | sntzm_clark3F(1).xlsx → ID 4；sntzm_clark4F(3).xlsx → ID 25；sntzm_clark4F(4).xlsx → ID 100；sntzm_david313-315(2).xlsx → ID 1；sntzm_final(1).xlsx → ID 75 |
| 8 enhanced suspence-horror | sntzm_clark4F(2).xlsx → ID 1；sntzm_david313-315(2).xlsx → ID 63 |
| 9 enhanced suspense tensions | sntzm_clark4F(1).xlsx → ID 1；sntzm_final(3).xlsx → ID 93；sntzm_gy(ns1).xlsx → ID 4 |
| 10 enhanced suspense | sntzm_clark3F(2).xlsx → ID 79；sntzm_ending4(1).xlsx → ID 35；sntzm_ending5(1).xlsx → ID 18；sntzm_final(4).xlsx → ID 77 |
| 11 enhanced the_mountain-hope-background | sntzm_clark3F(3).xlsx → ID 21；sntzm_david313-315(3).xlsx → ID 53；sntzm_gy(ns2).xlsx → ID 17 |
| 13 enhanced_audioatlant-total-war-epic-action-cinematic-trailer-main | sntzm_ending5(1).xlsx → ID 69 |
| 14 enhanced_horror-free | sntzm_clark3F(1).xlsx → ID 3；sntzm_david3F(1).xlsx → ID 1 |
| 16 enhanced_suspence | sntzm_clark3F(3).xlsx → ID 4；sntzm_clark4F(3).xlsx → ID 32；sntzm_david313-315(4).xlsx → ID 5；sntzm_david313-315(4).xlsx → ID 66 |
| 17 enhanced_suspense-tense-background-music | sntzm_david313-315(4).xlsx → ID 23；sntzm_final(3).xlsx → ID 70；sntzm_final(4).xlsx → ID 7；sntzm_final(6).xlsx → ID 4 |
| 18 enhanced_APPLE | sntzm_intro.xlsx → ID 1 |
| 19 enhanced_suspense-tension-background-music | sntzm_final(2).xlsx → ID 98；sntzm_final(5).xlsx → ID 21；sntzm_gy(s2).xlsx → ID 88 |
| 20 leberch-suspense | sntzm_david3F(2).xlsx → ID 2 |
| 22 the_mountain-horror | sntzm_clark3F(4).xlsx → ID 1；sntzm_ending3.xlsx → ID 57；sntzm_ending5(2).xlsx → ID 45 |
| leberch-horror-dark-375194 | sntzm_gy(ns2).xlsx → ID 4 |

## 分支与可复现路径

下面的“共同标签”只是路线提示，不一定构成完整的充要条件。每个首次位置所列的示例选择路径已经逐步回放验证。剧本与单元格可直接定位原始 BGM；同一位置可能由其他选择组合到达。

### 1 enhanced alex-morgan-dark-suspense-thriller

- 位置：[sntzm_david313-315(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(1).xlsx>)，ID **1**，工作表“圣诺汀之梦_大卫线313-315（上）”，**I2**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」。

- 位置：[sntzm_gy(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_gy(1).xlsx>)，ID **83**，工作表“圣诺汀之梦_盖亚之心（上）”，**I87**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」。


### 3 enhanced leberch-horror-night

- 位置：[sntzm_david313-315(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(4).xlsx>)，ID **53**，工作表“圣诺汀之梦_大卫线313-315（下）”，**I53**。
  路线共同标签：大卫线、深入探究。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「深入探究」。

- 位置：[sntzm_ending5(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_ending5(1).xlsx>)，ID **53**，工作表“圣诺汀之梦_结局5（上）”，**I50**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」 → sntzm_gy(ns1) / ID 85「不相信盖曼」。


### 4 enhanced leberch-spooky-piano

- 位置：[sntzm_david313-315(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(1).xlsx>)，ID **34**，工作表“圣诺汀之梦_大卫线313-315（上）”，**I31**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」。

- 位置：[sntzm_final(5).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(5).xlsx>)，ID **20**，工作表“圣诺汀之梦_终局（终2）”，**I5**。
  路线共同标签：克拉克线、夜莺线、无视飞蛾（克拉克）、选择亚波伦。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择亚波伦」。

- 位置：[sntzm_gy(ns1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_gy(ns1).xlsx>)，ID **44**，工作表“圣诺汀之梦_盖亚之心（无索菲亚线上）”，**I34**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」。


### 5 enhanced leberch-suspense

- 位置：[sntzm_clark3F(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark3F(3).xlsx>)，ID **62**，工作表“圣诺汀之梦_克拉克线3层（中下）”，**I63**。
  路线共同标签：克拉克线、端详飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」。

- 位置：[sntzm_clark4F(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark4F(3).xlsx>)，ID **69**，工作表“圣诺汀之梦_克拉克线4层（下）”，**I49**。
  路线共同标签：克拉克线、夜莺线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」。

- 位置：[sntzm_david313-315(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(1).xlsx>)，ID **79**，工作表“圣诺汀之梦_大卫线313-315（上）”，**I75**。
  路线共同标签：大卫线、忽视。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」。

- 位置：[sntzm_david4F(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david4F(4).xlsx>)，ID **13**，工作表“圣诺汀之梦_大卫线4层（终）”，**I14**。
  路线共同标签：大卫线、追问。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「继续追问」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」 → sntzm_david313-315(5) / ID 31「你就是亚波伦，对吧？」 → sntzm_david3F(1) / ID 12「装作无事离开」。

- 位置：[sntzm_final(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(3).xlsx>)，ID **115**，工作表“圣诺汀之梦_终局（下）”，**I64**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）、飞蛾线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「追上飞蛾」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_clark4F(4) / ID 6「询问纳丁达克公司的问题」。


### 6 enhanced oceanframemusic-horror-background-music

- 位置：[sntzm_clark4F(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark4F(4).xlsx>)，ID **49**，工作表“圣诺汀之梦_克拉克线4层（终）”，**I86**。
  路线共同标签：克拉克线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」 → sntzm_clark3F(3) / ID 65「优先应对外部威胁，协助队友」 → sntzm_clark3F(4) / ID 36「先发制人，果断逃跑」 → sntzm_clark4F(1) / ID 19「暂且作罢，专注眼前」 → sntzm_clark4F(3) / ID 73「相信亚波伦」。

- 位置：[sntzm_ending5(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_ending5(3).xlsx>)，ID **1**，工作表“圣诺汀之梦_结局5（下）”，**I2**。
  路线共同标签：克拉克线、夜莺线、无视飞蛾（克拉克）、选择大卫。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」 → sntzm_gy(ns1) / ID 85「不相信盖曼」。


### 7 enhanced litesaturation-horror

- 位置：[sntzm_clark3F(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark3F(1).xlsx>)，ID **4**，工作表“圣诺汀之梦_克拉克线3层（上）”，**I4**。
  路线共同标签：不开枪、克拉克线、问艾米丽。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」。

- 位置：[sntzm_clark4F(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark4F(3).xlsx>)，ID **25**，工作表“圣诺汀之梦_克拉克线4层（下）”，**I65**。
  路线共同标签：克拉克线、端详飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪击中他的肩膀」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」 → sntzm_clark3F(3) / ID 65「优先应对外部威胁，协助队友」 → sntzm_clark3F(4) / ID 36「先发制人，果断逃跑」 → sntzm_clark4F(1) / ID 19「暂且作罢，专注眼前」。

- 位置：[sntzm_clark4F(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark4F(4).xlsx>)，ID **100**，工作表“圣诺汀之梦_克拉克线4层（终）”，**I61**。
  路线共同标签：克拉克线、夜莺线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪击中他的肩膀」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」。

- 位置：[sntzm_david313-315(2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(2).xlsx>)，ID **1**，工作表“圣诺汀之梦_大卫线313-315（中）”，**I2**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」。

- 位置：[sntzm_final(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(1).xlsx>)，ID **75**，工作表“圣诺汀之梦_终局（上）”，**I75**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）、飞蛾线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「追上飞蛾」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪击中他的肩膀」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_clark4F(4) / ID 6「询问纳丁达克公司的问题」。


### 8 enhanced suspence-horror

- 位置：[sntzm_clark4F(2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark4F(2).xlsx>)，ID **1**，工作表“圣诺汀之梦_克拉克线4层（中）”，**I2**。
  路线共同标签：克拉克线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」。

- 位置：[sntzm_david313-315(2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(2).xlsx>)，ID **63**，工作表“圣诺汀之梦_大卫线313-315（中）”，**I64**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」。


### 9 enhanced suspense tensions

- 位置：[sntzm_clark4F(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark4F(1).xlsx>)，ID **1**，工作表“圣诺汀之梦_克拉克线4层（上）”，**I2**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」。

- 位置：[sntzm_final(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(3).xlsx>)，ID **93**，工作表“圣诺汀之梦_终局（下）”，**I45**。
  路线共同标签：克拉克线、端详飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」 → sntzm_clark3F(3) / ID 65「优先应对外部威胁，协助队友」 → sntzm_clark3F(4) / ID 36「先发制人，果断逃跑」 → sntzm_clark4F(1) / ID 19「暂且作罢，专注眼前」 → sntzm_clark4F(3) / ID 73「相信索菲亚」 → sntzm_final(4) / ID 52「选择亚波伦」。

- 位置：[sntzm_gy(ns1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_gy(ns1).xlsx>)，ID **4**，工作表“圣诺汀之梦_盖亚之心（无索菲亚线上）”，**I5**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」 → sntzm_david313-315(5) / ID 31「你就是亚波伦，对吧？」 → sntzm_david3F(1) / ID 12「装作无事离开」 → sntzm_final(4) / ID 4「选择克拉克」。


### 10 enhanced suspense

- 位置：[sntzm_clark3F(2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark3F(2).xlsx>)，ID **79**，工作表“圣诺汀之梦_克拉克线3层（中）”，**I74**。
  路线共同标签：克拉克线、端详飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」。

- 位置：[sntzm_ending4(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_ending4(1).xlsx>)，ID **35**，工作表“圣诺汀之梦_结局4（上）”，**I36**。
  路线共同标签：克拉克线、夜莺线、无视飞蛾（克拉克）、选择亚波伦。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择亚波伦」。

- 位置：[sntzm_ending5(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_ending5(1).xlsx>)，ID **18**，工作表“圣诺汀之梦_结局5（上）”，**I19**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」 → sntzm_gy(ns1) / ID 85「不相信盖曼」。

- 位置：[sntzm_final(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(4).xlsx>)，ID **77**，工作表“圣诺汀之梦_终局（终） (1)”，**I71**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」 → sntzm_david313-315(5) / ID 31「你就是亚波伦，对吧？」 → sntzm_david3F(1) / ID 12「装作无事离开」 → sntzm_final(4) / ID 4「选择亚波伦」。


### 11 enhanced the_mountain-hope-background

- 位置：[sntzm_clark3F(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark3F(3).xlsx>)，ID **21**，工作表“圣诺汀之梦_克拉克线3层（中下）”，**I22**。
  路线共同标签：克拉克线、端详飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」。

- 位置：[sntzm_david313-315(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(3).xlsx>)，ID **53**，工作表“圣诺汀之梦_大卫线313-315（中下）”，**I52**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」。

- 位置：[sntzm_gy(ns2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_gy(ns2).xlsx>)，ID **17**，工作表“圣诺汀之梦_盖亚之心（无索菲亚线下）”，**I16**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」 → sntzm_gy(ns1) / ID 85「相信盖曼」。


### 13 enhanced_audioatlant-total-war-epic-action-cinematic-trailer-main

- 位置：[sntzm_ending5(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_ending5(1).xlsx>)，ID **69**，工作表“圣诺汀之梦_结局5（上）”，**I66**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」 → sntzm_gy(ns1) / ID 85「不相信盖曼」。


### 14 enhanced_horror-free

- 位置：[sntzm_clark3F(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark3F(1).xlsx>)，ID **3**，工作表“圣诺汀之梦_克拉克线3层（上）”，**I3**。
  路线共同标签：克拉克线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」。

- 位置：[sntzm_david3F(1).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david3F(1).xlsx>)，ID **1**，工作表“圣诺汀之梦_大卫线3层（上）”，**I2**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」 → sntzm_david313-315(5) / ID 31「你就是亚波伦，对吧？」。


### 16 enhanced_suspence

- 位置：[sntzm_clark3F(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark3F(3).xlsx>)，ID **4**，工作表“圣诺汀之梦_克拉克线3层（中下）”，**I5**。
  路线共同标签：克拉克线、端详飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」。

- 位置：[sntzm_clark4F(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark4F(3).xlsx>)，ID **32**，工作表“圣诺汀之梦_克拉克线4层（下）”，**I23**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」。

- 位置：[sntzm_david313-315(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(4).xlsx>)，ID **5**，工作表“圣诺汀之梦_大卫线313-315（下）”，**I5**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「深入探究」。

- 位置：[sntzm_david313-315(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(4).xlsx>)，ID **66**，工作表“圣诺汀之梦_大卫线313-315（下）”，**I66**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」。


### 17 enhanced_suspense-tense-background-music

- 位置：[sntzm_david313-315(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david313-315(4).xlsx>)，ID **23**，工作表“圣诺汀之梦_大卫线313-315（下）”，**I23**。
  路线共同标签：大卫线、深入探究。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「深入探究」。

- 位置：[sntzm_final(3).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(3).xlsx>)，ID **70**，工作表“圣诺汀之梦_终局（下）”，**I22**。
  路线共同标签：克拉克线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」 → sntzm_clark3F(3) / ID 65「优先应对外部威胁，协助队友」 → sntzm_clark3F(4) / ID 36「先发制人，果断逃跑」 → sntzm_clark4F(1) / ID 19「暂且作罢，专注眼前」 → sntzm_clark4F(3) / ID 73「相信亚波伦」。

- 位置：[sntzm_final(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(4).xlsx>)，ID **7**，工作表“圣诺汀之梦_终局（终） (1)”，**I6**。
  路线共同标签：大卫线、视为陷阱。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」 → sntzm_david313-315(5) / ID 31「你就是亚波伦，对吧？」 → sntzm_david3F(1) / ID 12「装作无事离开」 → sntzm_final(4) / ID 4「选择克拉克」。

- 位置：[sntzm_final(6).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(6).xlsx>)，ID **4**，工作表“圣诺汀之梦_终局3”，**I14**。
  路线共同标签：克拉克线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」。


### 18 enhanced_APPLE

- 位置：[sntzm_intro.xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_intro.xlsx>)，ID **1**，工作表“圣诺汀之梦_前情提要”，**I2**。
  路线共同标签：无，开场即出现。
  一条已验证路径：从 sntzm_intro / ID 1 开始。


### 19 enhanced_suspense-tension-background-music

- 位置：[sntzm_final(2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(2).xlsx>)，ID **98**，工作表“圣诺汀之梦_终局（中）”，**I94**。
  路线共同标签：克拉克线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」 → sntzm_clark3F(3) / ID 65「优先应对外部威胁，协助队友」 → sntzm_clark3F(4) / ID 36「先发制人，果断逃跑」 → sntzm_clark4F(1) / ID 19「暂且作罢，专注眼前」 → sntzm_clark4F(3) / ID 73「相信亚波伦」。

- 位置：[sntzm_final(5).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_final(5).xlsx>)，ID **21**，工作表“圣诺汀之梦_终局（终2）”，**I6**。
  路线共同标签：克拉克线、相信索菲亚、端详飞蛾（克拉克）、选择亚波伦。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」 → sntzm_clark3F(3) / ID 65「优先应对外部威胁，协助队友」 → sntzm_clark3F(4) / ID 36「先发制人，果断逃跑」 → sntzm_clark4F(1) / ID 19「暂且作罢，专注眼前」 → sntzm_clark4F(3) / ID 73「相信索菲亚」 → sntzm_final(4) / ID 52「选择亚波伦」。

- 位置：[sntzm_gy(s2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_gy(s2).xlsx>)，ID **88**，工作表“圣诺汀之梦_盖亚之心（索菲亚线下） (1)”，**I87**。
  路线共同标签：盖亚之心（无索菲亚线下）_12。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」 → sntzm_david313-315(5) / ID 31「你就是亚波伦，对吧？」 → sntzm_david3F(1) / ID 12「仔细端详飞蛾」 → sntzm_david3F(2) / ID 6「再多看上一眼」 → sntzm_final(4) / ID 4「选择克拉克」。


### 20 leberch-suspense

- 位置：[sntzm_david3F(2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_david3F(2).xlsx>)，ID **2**，工作表“圣诺汀之梦_大卫线3层（中）”，**I3**。
  路线共同标签：大卫线、端详飞蛾（大卫）、视为陷阱。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」 → sntzm_david313-315(5) / ID 31「你就是亚波伦，对吧？」 → sntzm_david3F(1) / ID 12「仔细端详飞蛾」。


### 22 the_mountain-horror

- 位置：[sntzm_clark3F(4).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_clark3F(4).xlsx>)，ID **1**，工作表“圣诺汀之梦_克拉克线3层（下）”，**I2**。
  路线共同标签：克拉克线、端详飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「仔细端详飞蛾」 → sntzm_clark3F(3) / ID 65「优先应对外部威胁，协助队友」。

- 位置：[sntzm_ending3.xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_ending3.xlsx>)，ID **57**，工作表“圣诺汀之梦_结局3”，**I58**。
  路线共同标签：大卫线。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「与大卫随行」 → sntzm_david313-315(1) / ID 42「就此作罢」 → sntzm_david313-315(3) / ID 2「沉默不语」 → sntzm_david313-315(4) / ID 2「视为陷阱」 → sntzm_david313-315(5) / ID 31「你就是亚波伦，对吧？」 → sntzm_david3F(1) / ID 12「装作无事离开」 → sntzm_final(4) / ID 4「选择亚波伦」。

- 位置：[sntzm_ending5(2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_ending5(2).xlsx>)，ID **45**，工作表“圣诺汀之梦_结局5（中）”，**I46**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」 → sntzm_gy(ns1) / ID 85「不相信盖曼」。


### leberch-horror-dark-375194

- 位置：[sntzm_gy(ns2).xlsx](<E:/Unity Projects/VNovelizerTest_v1.0/Assets/Resources/VNovelizerRes/ExcelVNScripts/sntzm_gy(ns2).xlsx>)，ID **4**，工作表“圣诺汀之梦_盖亚之心（无索菲亚线下）”，**I92**。
  路线共同标签：克拉克线、无视飞蛾（克拉克）。
  一条已验证路径：sntzm_intro / ID 6「不是」 → sntzm_intro / ID 11「接听无线电」 → sntzm_intro / ID 18「跟上克拉克」 → sntzm_intro / ID 33「询问了关于艾米丽的问题」 → sntzm_intro / ID 41「开枪故意射偏」 → sntzm_clark3F(2) / ID 15「这一定有什么古怪」 → sntzm_final(4) / ID 52「选择大卫」 → sntzm_gy(ns1) / ID 85「不相信盖曼」。

## 边界

这是表格中的音乐触发位置分析，不验证音频资源是否存在或能成功播放，也不包括主菜单、存档恢复、代码或 Command 列另行触发的音乐。