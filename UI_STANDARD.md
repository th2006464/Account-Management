# 页面 UI 规范 — GARCHINA 账户管理系统

面向开发人员的页面 UI 设计标准，以 [Pages/Admin/UserAdmin.cshtml](Pages/Admin/UserAdmin.cshtml) 为基准参考页面。新建 admin 页面时照此结构复制修改。

---

## 整体布局

```
Admin 页面 = _AdminLayout（侧边栏）+ 消息区 + 查询面板 + 操作面板 + 结果面板(N个) + 日志面板 + 确认弹窗
公开页面 = _Layout（无侧边栏）+ 顶部信息栏 + 消息区 + 操作面板 + 结果面板 + 页脚
```

**CSS 框架：Bootstrap 3.3.4**（CDN `apps.bdimg.com`）

**背景色：** `#f5f5f5`

**面板间距：** 统一 `margin-top: 8px`

---

## 远端框架（CDN 引入）

所有 CSS/JS 框架通过百度 CDN（`apps.bdimg.com`）远程加载，无需 npm 安装，无需本地托管。

### 依赖清单

| 资源 | 版本 | CDN 地址 |
|------|------|---------|
| Bootstrap CSS | 3.3.4 | `https://apps.bdimg.com/libs/bootstrap/3.3.4/css/bootstrap.min.css` |
| jQuery | 2.1.4 | `https://apps.bdimg.com/libs/jquery/2.1.4/jquery.min.js` |
| Bootstrap JS | 3.3.4 | `https://apps.bdimg.com/libs/bootstrap/3.3.4/js/bootstrap.min.js` |

### 加载位置

所有远端资源在 Layout 文件中统一引入，**子页面无需重复引用**：

```
_Layout.cshtml        → 公开页面布局，引入全部 3 个 CDN 资源
_AdminLayout.cshtml   → Admin 管理布局，引入全部 3 个 CDN 资源 + 自定义侧边栏样式
```

```html
<!-- _AdminLayout.cshtml / _Layout.cshtml 的 <head> -->
<link rel="stylesheet" href="https://apps.bdimg.com/libs/bootstrap/3.3.4/css/bootstrap.min.css" />
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />

<!-- </body> 之前 -->
<script src="https://apps.bdimg.com/libs/jquery/2.1.4/jquery.min.js"></script>
<script src="https://apps.bdimg.com/libs/bootstrap/3.3.4/js/bootstrap.min.js"></script>
```

### 使用说明

- **Bootstrap 3.x 组件可直接使用**：Grid 栅格、Panel 面板、Alert 警告框、Modal 弹窗、Badge 徽标、Glyphicon 图标
- **Glyphicon 图标**：Bootstrap 3 内置，用法 `<span class="glyphicon glyphicon-search"></span>`
- **jQuery**：Bootstrap JS 插件依赖，所有自定义脚本在 jQuery 之后加载
- **不引入额外 CSS/JS 库**，保持最小依赖

---

## 1. Admin 布局骨架（_AdminLayout）

所有 admin 页面自动使用侧边栏布局，无需每个页面重复声明：

```
┌──────────┐  ┌──────────────────────────────────────┐
│  Logo    │  │                                      │
│  ──────  │  │  消息区（alert-danger / alert-success）│
│  用户信息 │  │                                      │
│  ──────  │  │  查询面板（panel panel-default）       │
│  导航菜单 │  │                                      │
│          │  │  操作面板（panel panel-warning）       │
│  ──────  │  │                                      │
│  齿轮/退出│  │  结果面板 × N（panel panel-success 等）│
│          │  │                                      │
│  220px   │  │  日志面板（panel panel-info）          │
└──────────┘  └──────────────────────────────────────┘
```

**侧边栏 220px 固定，主内容区 `margin-left: 220px`，移动端 (<768px) 侧边栏变全宽。**

---

## 2. 消息区

页面顶部，表单之外，最先渲染：

```html
@if (!Model.IsAuthenticated)
{
    <div class="alert alert-danger">请先<a asp-page="/Admin/Login">登录</a>。</div>
    return;
}

@if (!string.IsNullOrEmpty(Model.ErrorMessage))
{
    <div class="alert alert-danger">@Model.ErrorMessage</div>
}
@if (!string.IsNullOrEmpty(Model.ResultMessage))
{
    <div class="alert alert-success" style="white-space: pre-line;">@Model.ResultMessage</div>
}
```

**规范：**
- 错误 `alert alert-danger`，成功 `alert alert-success`
- 成功消息加 `white-space: pre-line` 支持换行
- 未登录检查放在最前面，直接 `return` 阻断后续渲染

---

## 3. 查询/搜索面板

灰色边框面板，紧凑布局，搜索框 + 按钮同行：

```html
<form method="post" id="mainForm">
    <div class="panel panel-default">
        <div class="panel-heading" style="padding:6px 12px;"><strong>搜索用户</strong></div>
        <div class="panel-body" style="padding:8px 12px;">
            <div style="display:flex;gap:8px;">
                <input class="form-control" id="SearchEmployeeId" name="SearchEmployeeId"
                       value="@Model.SearchEmployeeId" style="flex:1;"
                       placeholder="输入员工号或姓名关键字搜索" autocomplete="off" />
                <button type="submit" name="action" value="search" class="btn btn-primary">搜索</button>
            </div>
        </div>
    </div>
```

**规范：**
- `panel-heading` padding: `6px 12px`
- `panel-body` padding: `8px 12px`
- 表单内容用 `display:flex; gap:8px` 同行排列
- 输入框 `flex:1` 占满剩余空间
- `autocomplete="off"` 关闭自动填充
- 按钮通过 `name="action" value="search"` 区分不同提交操作

---

## 4. 操作/控制面板

黄色面板（`panel-warning`），按钮分组排列：

```html
    <div class="panel panel-warning" style="margin-top:8px;">
        <div class="panel-heading" style="padding:6px 12px;"><strong>控制面板</strong></div>
        <div class="panel-body" style="padding:8px 12px;">
            <div style="display:flex;gap:12px;flex-wrap:wrap;">
                <button type="submit" name="action" value="reset" class="btn btn-success btn-sm">重置密码</button>
                <button type="button" class="btn btn-danger btn-sm" onclick="confirmAction('offboard','离职')">一键离职</button>
                <button type="submit" name="action" value="unlock" class="btn btn-warning btn-sm" style="margin-left:auto;">解锁所有账号</button>
            </div>
            <div style="display:flex;gap:12px;margin-top:8px;">
                <button type="button" class="btn btn-default btn-sm" onclick="confirmAction('clearHistory','清空记录')">清空记录</button>
                <button type="button" id="btnAddGroup" class="btn btn-default btn-sm">加用户组</button>
            </div>
        </div>
    </div>

    <input type="hidden" name="GroupName" id="GroupName" value="" />
</form>
```

**规范：**
- 使用 `panel panel-warning` 黄色面板标识操作区
- 按钮统一 `btn-sm` 小尺寸
- 按钮行 `display:flex; gap:12px; flex-wrap:wrap`
- 多行按钮之间 `margin-top:8px`
- `margin-left:auto` 可将按钮推到行尾
- 附加参数用 `<input type="hidden">` 放在 `</form>` 之前

---

## 5. 按钮颜色规范

| 操作类型 | 按钮样式 | UserAdmin 中的示例 |
|---------|---------|-------------------|
| 创建/保存/提交 | `btn btn-success`（绿色） | 重置密码 |
| 查询/搜索 | `btn btn-primary`（蓝色） | 搜索用户 |
| 危险操作 | `btn btn-danger`（红色） | 一键离职 |
| 警告操作 | `btn btn-warning`（黄色） | 解锁所有账号 |
| 普通/工具操作 | `btn btn-default`（灰色） | 清空记录、加用户组 |

**所有 admin 页面按钮使用 `btn-sm` 大小。**

**危险操作（离职、批量重置）必须使用 Bootstrap Modal 二次确认，禁止用浏览器 `confirm()`。**

---

## 6. 结果面板

操作反馈区域，按结果类型使用不同面板颜色，垂直堆叠：

```html
@if (!string.IsNullOrEmpty(Model.UserDetail))
{
    <div class="panel panel-success" style="margin-top:8px;">
        <div class="panel-heading" style="padding:6px 12px;">用户详细信息</div>
        <div class="panel-body" style="padding:8px 12px;">
            <pre style="margin:0;font-size:12px;">@Model.UserDetail</pre>
        </div>
    </div>
}

@if (Model.ResetResults != null && Model.ResetResults.Count > 0)
{
    <div class="panel panel-success" style="margin-top:8px;">
        <div class="panel-heading" style="padding:6px 12px;">密码重置结果</div>
        <div class="panel-body" style="padding:6px 12px;">
            <pre style="margin:0;font-size:12px;">@string.Join(Environment.NewLine, Model.ResetResults)</pre>
        </div>
    </div>
}

@if (Model.OffboardResults != null && Model.OffboardResults.Count > 0)
{
    <div class="panel panel-danger" style="margin-top:8px;">
        <div class="panel-heading" style="padding:6px 12px;">离职处理结果</div>
        <div class="panel-body" style="padding:6px 12px;">
            <pre style="margin:0;font-size:12px;">@string.Join(Environment.NewLine, Model.OffboardResults)</pre>
        </div>
    </div>
}
```

**规范：**
- 每个结果区独立一个 panel，`margin-top:8px` 间距
- `panel-heading` 统一 `padding:6px 12px`
- `panel-body` 内容区 `padding:6px 12px`，表单区 `padding:8px 12px`
- `<pre>` 标签 `margin:0; font-size:12px` 紧凑展示
- 列表数据用 `string.Join(Environment.NewLine, ...)` 拼接

**面板颜色映射：**

| 面板类型 | 用途 |
|---------|------|
| `panel panel-default` | 查询搜索、邮件记录 |
| `panel panel-warning` | 操作控制面板 |
| `panel panel-success` | 查询结果、重置结果、解锁结果 |
| `panel panel-danger` | 离职/危险操作结果 |
| `panel panel-info` | 操作日志/历史记录 |

---

## 7. 日志/历史面板

操作记录和邮件发送记录，空态用灰色文字提示：

```html
<div class="panel panel-info" style="margin-top:8px;">
    <div class="panel-heading" style="padding:6px 12px;"><strong>操作记录</strong></div>
    <div class="panel-body" style="padding:6px 12px;">
        @if (Model.OperationHistory != null && Model.OperationHistory.Count > 0)
        {
            <pre style="max-height:200px;overflow-y:auto;font-size:12px;margin:0;">@string.Join(Environment.NewLine, Model.OperationHistory)</pre>
        }
        else
        {
            <p style="color:#999;margin:0;">暂无操作记录。</p>
        }
    </div>
</div>
```

**规范：**
- 使用 `panel panel-info` 浅蓝面板
- 内容多时 `max-height:200px; overflow-y:auto` 限制高度
- **空态不要空白**，显示 `<p style="color:#999;margin:0;">暂无xxx。</p>`
- 邮件记录 `max-height:150px`

---

## 8. 二次确认弹窗（Bootstrap Modal）

所有危险操作必须使用 Modal 弹窗确认：

```html
<div class="modal fade" id="confirmModal" tabindex="-1" role="dialog">
    <div class="modal-dialog modal-sm" role="document">
        <div class="modal-content">
            <div class="modal-header" style="padding:10px 15px;">
                <button type="button" class="close" data-dismiss="modal">&times;</button>
                <h5 class="modal-title">操作确认</h5>
            </div>
            <div class="modal-body" style="padding:15px;" id="confirmMsg"></div>
            <div class="modal-footer" style="padding:8px 15px;">
                <button type="button" class="btn btn-default btn-sm" data-dismiss="modal">取消</button>
                <button type="button" class="btn btn-danger btn-sm" id="btnConfirmYes">确认</button>
            </div>
        </div>
    </div>
</div>
```

配套 JavaScript：

```javascript
var pendingAction = '';
function confirmAction(action, name) {
    pendingAction = action;
    var msg = action === 'offboard'
        ? '<strong>确认对该用户执行离职操作？</strong><br/><small>此操作将禁用账号并修改邮箱和UPN。</small>'
        : '<strong>确认清空所有操作记录？</strong><br/><small>此操作不可恢复。</small>';
    document.getElementById('confirmMsg').innerHTML = msg;
    $('#confirmModal').modal('show');
}
document.getElementById('btnConfirmYes').addEventListener('click', function() {
    $('#confirmModal').modal('hide');
    var input = document.createElement('input');
    input.type = 'hidden'; input.name = 'action'; input.value = pendingAction;
    document.getElementById('mainForm').appendChild(input);
    document.getElementById('mainForm').submit();
});
```

**规范：**
- `modal-sm` 小尺寸弹窗
- 确认按钮放右侧，取消放左侧
- 通过动态创建 `<input type="hidden">` 传递操作类型，而非直接提交

---

## 9. 附加操作弹窗（如加用户组）

需要额外输入的操作，单独弹窗收集参数：

```html
<div class="modal fade" id="addGroupModal" tabindex="-1" role="dialog">
    <div class="modal-dialog" role="document">
        <div class="modal-content">
            <div class="modal-header">
                <button type="button" class="close" data-dismiss="modal">&times;</button>
                <h4 class="modal-title">添加到用户组</h4>
            </div>
            <div class="modal-body">
                <div class="form-group">
                    <label>输入用户组名称</label>
                    <input class="form-control" id="modalGroupName" placeholder="如 GARCHINA_OA_Users" />
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-default" data-dismiss="modal">取消</button>
                <button type="button" id="btnConfirmAddGroup" class="btn btn-primary">确认添加</button>
            </div>
        </div>
    </div>
</div>
```

**规范：**
- 默认尺寸 `modal-dialog`（不加 `modal-sm`）
- 取消按钮 `btn btn-default`，确认按钮 `btn btn-primary`
- 弹窗内表单验证用 `alert()` 提示（简单场景）

---

## 10. JavaScript 规范

### 10.1 防止重复提交

```javascript
var submitting = false;
form.addEventListener('submit', function(e) {
    if (submitting) {
        e.preventDefault();
        return false;
    }
    submitting = true;
    var btns = form.querySelectorAll('button[type="submit"]');
    setTimeout(function() {
        btns.forEach(function(b) { b.disabled = true; });
    }, 0);
    setTimeout(function() {
        submitting = false;
        btns.forEach(function(b) { b.disabled = false; });
    }, 5000);
});
```

### 10.2 Enter 键触发搜索

```javascript
form.addEventListener('keydown', function(e) {
    if (e.key === 'Enter' && e.target.tagName !== 'BUTTON') {
        e.preventDefault();
        form.querySelector('button[value="search"]').click();
    }
});
```

### 10.3 所有 JS 用 IIFE 包裹

```javascript
(function() {
    var form = document.getElementById('mainForm');
    // ... 事件绑定 ...
})();
```

**避免全局变量污染。**

---

## 11. 公开页面布局（_Layout，无侧边栏）

公开页面（如 Index、Onboard）使用 `_Layout`，顶部有绿色信息栏：

```html
<div class="alert alert-success" style="text-align: center; margin-top: 20px;">
    <h2 style="margin-top: 0; margin-bottom: 8px;">页面主标题</h2>
    说明文字<br />
    CN IT Support &lt;<a href="mailto:CN_IT_Support@sinarmas-agri.com">CN_IT_Support@sinarmas-agri.com</a>&gt;
</div>
```

**规范：**
- `alert alert-success` 绿色背景，居中
- 标题 H2，上边距 0、下边距 8px
- 与页面顶部 20px 留白
- 联系方式用 `<a mailto:>` 链接

---

## 12. 注意事项面板（仅公开页面）

黄色警告框，展示使用说明：

```html
<div class="panel panel-warning">
    <div class="panel-heading"><strong>注意事项：务必仔细阅读</strong></div>
    <div class="panel-body" style="line-height: 1.8;">
        <p>&#128226; 说明内容行1</p>
        <p>&#128161; 说明内容行2</p>
    </div>
</div>
```

行高 1.8，每条 `<p>` 标签，emoji 图标前缀（📢📌⚠️✅❌🚧）。

---

## 13. 页脚（仅公开页面）

```html
<footer class="border-top footer text-muted">
    <div class="container" style="text-align: center;">
        GARCHINA账号管理 - <a asp-page="/Admin/UserAdmin">账户管理系统</a>
    </div>
</footer>
```

---

## 14. 新建页面快速参考

| 页面类型 | 参考模板 | 布局 |
|---------|---------|------|
| Admin 管理页面 | [Pages/Admin/UserAdmin.cshtml](Pages/Admin/UserAdmin.cshtml) | _AdminLayout（侧边栏） |
| 公开页面 | Pages/Index.cshtml | _Layout（无侧边栏） |
| 仪表盘 | Pages/Admin/Dashboard.cshtml | _AdminLayout |
| 弹窗确认 | UserAdmin.cshtml 中的 confirmModal | Bootstrap Modal |

**新建 admin 页面步骤：**
1. 在 `Pages/Admin/` 下创建 `.cshtml` + `.cshtml.cs`
2. CSHTML 照搬 UserAdmin.cshtml 的结构：消息区 → 查询面板 → 操作面板 → 结果面板(N) → 日志面板 → Modal
3. 无需声明 Layout，`_ViewStart.cshtml` 中按路径自动匹配 `_AdminLayout`
4. 后端使用 PRG 模式（Post-Redirect-Get）
