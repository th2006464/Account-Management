# 系统仪表盘 — GARCHINA 账户管理系统

仪表盘实现说明文档，以 [Pages/Admin/Dashboard.cshtml](Pages/Admin/Dashboard.cshtml) 为参考。**不使用任何第三方可视化框架（Chart.js、ECharts 等）**，全部基于 Bootstrap 3 原生组件。

---

## 可视化方案

**零外部依赖。** 所有图表通过以下 Bootstrap 3 原生组件组合实现：

| 组件 | 用途 | Bootstrap 类 |
|------|------|-------------|
| Panel 面板 | 卡片容器 | `panel panel-{color}` |
| Progress Bar 进度条 | 柱状图、占比图 | `progress` + `progress-bar` |
| Table 表格 | 数据列表、运营数据 | `<table>` |
| Flexbox 布局 | 网格行、卡片排列 | `display:flex; gap:8px; flex-wrap:wrap` |
| Glyphicon 图标 | 装饰前缀 | `glyphicon glyphicon-*` |
| 数字 + 颜色 | KPI 大数展示 | `font-size:22px; color:#xxx` |

---

## 页面布局总览

```
┌─────────────────────────────────────────────────────────┐
│  总览卡片行：用户总数 | 启用用户 | 禁用用户 | 锁定用户 | 待处理 │
├──────────────────────────┬──────────────────────────────┤
│  今日操作（密码重置/离职  │  密码到期预警（7天/30天/60天） │
│  /创建用户）             │                              │
├──────────────────────────┬──────────────────────────────┤
│  OU 用户分布（进度条）    │  7日密码更新人数（进度条）     │
├──────────────────────────┬──────────────────────────────┤
│  运营数据（表格）         │  最近30日入职人数（进度条）    │
└──────────────────────────┴──────────────────────────────┘
```

每行 `display:flex; gap:8px; flex-wrap:wrap`，移动端自动换行。

---

## 1. 总览卡片（KPI 大数卡片）

5 个彩色面板横向排列，点击跳转到对应功能页：

```html
<div style="display:flex;gap:8px;margin-top:8px;flex-wrap:wrap;">
    <a asp-page="/Admin/Report" style="text-decoration:none;">
        <div class="panel panel-primary" style="flex:1;min-width:120px;text-align:center;margin-bottom:0;">
            <div class="panel-body" style="padding:10px 8px;">
                <div style="font-size:22px;margin:0;color:#337ab7;">@Model.TotalUsers</div>
                <small style="color:#666;">用户总数</small>
            </div>
        </div>
    </a>
    <!-- 重复结构：panel-success / panel-danger / panel-warning / panel-info -->
</div>
```

**规范：**
- 每个卡片 `flex:1; min-width:120px; text-align:center`
- 大数字 `font-size:22px`，颜色与 panel 类型呼应
- 标签用 `<small style="color:#666;">`
- 卡片可点击时用 `<a>` 包裹，`text-decoration:none` 去掉下划线

**卡片颜色映射：**

| 指标 | panel 类型 | 数字颜色 | 链接目标 |
|------|-----------|---------|---------|
| 用户总数 | `panel-primary` | `#337ab7` | Report |
| 启用用户 | `panel-success` | `#5cb85c` | Report |
| 禁用用户 | `panel-danger` | `#d9534f` | Report |
| 锁定用户 | `panel-warning` | `#f0ad4e` | UserAdmin |
| 待处理 | `panel-info` | `#5bc0de` | Request |

---

## 2. 今日操作 + 密码到期（双栏面板）

两个 `panel-default` 并排，内部用 flexbox + `border-right` 分隔：

```html
<div style="display:flex;gap:8px;margin-top:8px;flex-wrap:wrap;">
    <div class="panel panel-default" style="flex:1;min-width:240px;">
        <div class="panel-heading" style="padding:6px 12px;"><strong>今日操作</strong></div>
        <div class="panel-body" style="padding:8px;">
            <div style="display:flex;gap:8px;">
                <div style="flex:1;text-align:center;border-right:1px solid #eee;">
                    <strong style="font-size:18px;color:#5cb85c;">3</strong><br/>
                    <small style="color:#999;">密码重置</small>
                </div>
                <div style="flex:1;text-align:center;border-right:1px solid #eee;">
                    <strong style="font-size:18px;color:#d9534f;">0</strong><br/>
                    <small style="color:#999;">离职处理</small>
                </div>
                <div style="flex:1;text-align:center;">
                    <strong style="font-size:18px;color:#337ab7;">1</strong><br/>
                    <small style="color:#999;">创建用户</small>
                </div>
            </div>
        </div>
    </div>
    <!-- 密码到期面板同样结构 -->
</div>
```

**规范：**
- 数字 `font-size:18px`，颜色区分操作类型
- 列间用 `border-right: 1px solid #eee` 分隔
- 最后一列不加 `border-right`
- 面板 `min-width:240px` 保证移动端换行后不挤压

---

## 3. OU 用户分布（进度条柱状图）

用 Bootstrap `progress` + `progress-bar` 实现横向柱状图：

```html
<div class="panel panel-default" style="flex:1;min-width:280px;">
    <div class="panel-heading" style="padding:6px 12px;"><strong>OU 用户分布</strong></div>
    <div class="panel-body" style="padding:8px 12px;">
        @foreach (var os in Model.OuStats)
        {
            <div style="margin-bottom:6px;">
                <div style="display:flex;justify-content:space-between;margin-bottom:1px;font-size:12px;">
                    <strong>@os.Label</strong>
                    <span>@os.Total 人 (@os.Percentage.ToString("F1")%)</span>
                </div>
                <div class="progress" style="margin-bottom:0;height:16px;">
                    <div class="progress-bar" style="width:@os.Percentage.ToString("F0")%;min-width:30px;line-height:16px;font-size:11px;">
                        @os.Total 人
                    </div>
                </div>
            </div>
        }
    </div>
</div>
```

**规范：**
- `progress` 高度 16px，`margin-bottom:0`
- `progress-bar` 默认蓝色，`min-width:30px` 保证小值可见
- 标签行 `font-size:12px`，flex space-between 两端对齐
- 值用 `Percentage.ToString("F0")` 整数百分比

---

## 4. 7 日趋势（进度条时序图）

表格 + 进度条组合，按最大值等比缩放：

```html
@if (Model.WeeklyTrends.Count > 0)
{
    int maxVal = Model.WeeklyTrends.Max(t => t.Resets);
    if (maxVal == 0) { maxVal = 1; }
    <table style="width:100%;font-size:11px;">
        @foreach (var t in Model.WeeklyTrends)
        {
            <tr>
                <td style="width:38px;padding:1px 0;">@t.Date</td>
                <td style="padding:1px 3px;">
                    <div class="progress" style="margin:0;height:14px;background:#eee;">
                        <div class="progress-bar progress-bar-success" 
                             style="width:@(100.0 * t.Resets / maxVal)%;min-width:18px;line-height:14px;font-size:10px;">
                            @t.Resets 人
                        </div>
                    </div>
                </td>
            </tr>
        }
    </table>
}
```

**关键算法：**
- 取 7 天中最大值 `maxVal`（至少为 1）
- 每天条宽 = `100.0 * 当日值 / maxVal` — **等比缩放**，最大天撑满
- 绿色进度条 `progress-bar-success`
- `min-width:18px` 保证 0 值天也有微量可见

---

## 5. 运营数据表（纯表格）

简单 `<table>` 键值对展示：

```html
<table style="width:100%;font-size:12px;">
    <tr>
        <td style="padding:2px 0;color:#666;">30天密码重置（管理员）</td>
        <td style="text-align:right;"><strong>@Model.Recent30Resets</strong> 次</td>
    </tr>
    <tr>
        <td style="padding:2px 0;color:#666;">禁用用户占比</td>
        <td style="text-align:right;">
            <strong>@(Model.TotalUsers > 0 ? (100.0 * Model.OuDisabledUsers / Model.TotalUsers).ToString("F1") : "0")%</strong>
        </td>
    </tr>
</table>
```

**规范：**
- 左列灰色描述，右列加粗数值右对齐
- `font-size:12px`，行高通过 `padding:2px 0` 控制
- 比例类数据在模板中计算、格式化

---

## 6. 最近 30 日入职（大数 + 进度条）

顶部醒目数字，下方按 OU 分进度条：

```html
<strong style="font-size:22px;color:#337ab7;">15 <small style="font-size:13px;color:#999;">人</small></strong>
@foreach (var os in Model.NewUserByOu)
{
    <div style="margin-bottom:4px;margin-top:4px;">
        <div style="display:flex;justify-content:space-between;font-size:12px;">
            <span>@os.Label</span><span>@os.Total 人</span>
        </div>
        <div class="progress" style="margin:1px 0 0;height:14px;">
            <div class="progress-bar progress-bar-info" style="width:@os.Percentage.ToString("F0")%;min-width:18px;line-height:14px;font-size:10px;">
                @os.Total
            </div>
        </div>
    </div>
}
```

---

## 7. 后端数据来源

| 面板 | 数据来源 | 查询方式 |
|------|---------|---------|
| 总览卡片（总数/启用/禁用/锁定） | AD 域控 | `DirectorySearcher` 按 OU 遍历 `userAccountControl` |
| 密码到期 | AD `pwdLastSet` 属性 | `DateTime.FromFileTimeUtc()` 计算 90 天剩余 |
| OU 分布 | AD 按 OU 分组统计 | 3 个 OU 分别 `FindAll()` 计数 |
| 今日操作 | 审计日志 `audit.dat` | 按日期前缀过滤 + 关键字匹配 |
| 7 日趋势 | 审计日志 `audit.dat` | 7 天循环 + 反向遍历行 |
| 待处理/入职总数 | `onboard_requests.dat` | `Status == null` 计数 |
| 30 日入职 | AD `whenCreated` 属性 | `>= cutoffDate` 过滤 |
| 管理员数量 | `admins.dat` | `LoadAdminList().Count` |

**核心模式：每个面板数据在 `LoadDashboardData()` 中独立 try/catch，单个面板失败不影响其他。**

```csharp
// 每个数据源独立保护
try { /* 查询 OU 统计 */ } catch { }
try { /* 读取入职请求 */ } catch { }
try { /* 解析审计日志 */ } catch { }
```

---

## 8. 无需引入的可视化库（已评估后排除）

| 库 | 排除原因 |
|----|---------|
| Chart.js | 需额外引入 JS，增加页面体积 |
| ECharts | 几百 KB，对仪表盘场景过重 |
| D3.js | 学习成本高，Bootstrap 进度条已够用 |
| Google Charts | 依赖外部网络，离线环境不可用 |

**用 Bootstrap 进度条替代柱状图/条形图的优势：**
1. 零额外请求 — 复用已加载的 Bootstrap CSS
2. 响应式 — 自动适配面板宽度
3. 服务端渲染 — 无 JS 异步加载，首屏即完成
4. 维护简单 — 只是 `<div>` + 百分比，不涉及 Canvas/SVG

---

## 9. 布局规范总结

```
display: flex; gap: 8px; flex-wrap: wrap;    → 所有行容器
flex: 1; min-width: XXXpx;                    → 所有列面板
margin-top: 8px;                              → 行间距
padding: 6px 12px;                            → panel-heading
padding: 6px 12px / 8px 12px;                 → panel-body
font-size: 12px; color: #999;                 → 辅助文字
font-size: 22px; color: #xxx;                 → KPI 大数字
font-size: 18px; color: #xxx;                 → 二级数字
```

## 10. 性能注意事项

- Dashboard 页面直接查询 AD（3 个 OU × `FindAll()`），数据量大时可能较慢
- `PageSize = 1000` 限制单次查询结果数
- 使用 `PropertiesToLoad` 只加载需要的属性（`userAccountControl`、`pwdLastSet`），减少 AD 传输
- 无缓存 — 每次访问实时查询，保证数据新鲜度
- 审计日志反向遍历（`lines.Reverse()`）以优先匹配最新记录
