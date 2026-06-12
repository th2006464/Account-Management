# 页面 UI 规范 — GARCHINA 账户管理系统

面向开发人员的页面 UI 设计规范，确保所有页面风格一致。

---

## 整体布局

```html
页面结构 = 顶部信息栏 + 通知面板（可选）+ 操作面板 + 结果面板 + 页脚
```

**CSS 框架：Bootstrap 3.3.4**（CDN 引入）

**全局容器宽度：** 主页面 80%，onboard 页面 50%

**背景色：** `#f5f5f5`

---

## 1. 顶部信息栏

绿底居中，H2 标题 + 说明文字 + 联系方式：

```html
<div class="alert alert-success" style="text-align: center; margin-top: 20px;">
    <h2 style="margin-top: 0; margin-bottom: 8px;">页面主标题</h2>
    说明文字<br />
    CN IT Support &lt;<a href="mailto:CN_IT_Support@sinarmas-agri.com">CN_IT_Support@sinarmas-agri.com</a>&gt;
</div>
```

**规范：**
- 使用 `alert alert-success` 绿色背景
- 标题使用 `<h2>`，上下间距 0 + 8px
- 与页面顶部保留 20px 留白
- 联系方式用 `<a mailto:>` 可点击链接

---

## 2. 注意事项面板（可选）

黄色警告框，用于展示使用说明：

```html
<div class="panel panel-warning">
    <div class="panel-heading"><strong>注意事项：务必仔细阅读</strong></div>
    <div class="panel-body" style="line-height: 1.8;">
        <p>&#128226; 说明内容行1</p>
        <p>&#128161; 说明内容行2</p>
    </div>
</div>
```

**规范：**
- 使用 `panel panel-warning` 黄色面板
- 行高 1.8，每条用 `<p>` 标签
- emoji 图标作为每条前缀（📢📌⚠️✅❌🚧）

---

## 3. 操作面板

灰色边框面板，包含表单输入和提交按钮：

```html
<form method="post" id="mainForm" autocomplete="off">
    <div class="panel panel-default">
        <div class="panel-heading">面板标题</div>
        <div class="panel-body">
            <div class="form-group">
                <label for="Field1">字段名 *</label>
                <input class="form-control" id="Field1" name="Field1" placeholder="输入提示" autocomplete="off" />
            </div>
            <!-- 按钮区 -->
            <div class="action-buttons">
                <button type="submit" class="btn btn-success">主操作按钮</button>
                <button type="submit" class="btn btn-primary" style="order: 2;">次要按钮</button>
            </div>
        </div>
    </div>
</form>
```

**规范：**
- 使用 `panel panel-default` 灰色边框面板
- 必填字段加 `*` 标记
- 表单元素 `autocomplete="off"`，密码字段 `autocomplete="new-password"`
- 按钮之间间隙 20px，使用 flex 布局：`display: flex; gap: 20px;`
- 主导按钮绿色 `btn btn-success`，次要蓝色 `btn btn-primary`，危险红色 `btn btn-danger`

**密码可见性切换按钮：** 所有密码框旁放置"显示密码"按钮，统一控制。按下时显示明文，松开恢复。使用 `btn btn-sm btn-default`。

---

## 4. 按钮颜色规范

| 操作类型 | 按钮样式 | 示例 |
|---------|---------|------|
| 创建/保存/提交 | `btn btn-success`（绿色） | 提交申请、创建用户 |
| 查询/搜索 | `btn btn-primary`（蓝色） | 查询状态、搜索用户 |
| 危险操作 | `btn btn-danger`（红色） | 一键离职、批量重置 |
| 警告操作 | `btn btn-warning`（黄色） | 解锁账号 |
| 普通操作 | `btn btn-default`（灰色） | 清空记录、重置表单 |
| 信息操作 | `btn btn-info`（浅蓝） | 创建用户链接 |

**危险操作（离职、批量重置）必须加二次确认弹窗（Bootstrap Modal），不能用浏览器 `confirm()`。**

---

## 5. 结果/信息展示面板

操作完成后的反馈区域：

```html
@@if (!string.IsNullOrEmpty(Model.ErrorMessage))
{
    <div class="alert alert-danger">@Model.ErrorMessage</div>
}
@@if (!string.IsNullOrEmpty(Model.ResultMessage))
{
    <div class="alert alert-success" style="white-space: pre-line;">@Model.ResultMessage</div>
}

@@if (!string.IsNullOrEmpty(Model.UserDetail))
{
    <div class="panel panel-success" style="margin-top: 10px;">
        <div class="panel-heading">查询结果</div>
        <div class="panel-body">
            <pre>@Model.UserDetail</pre>
        </div>
    </div>
}
```

**规范：**
- 错误信息：`alert alert-danger` 红色
- 成功信息：`alert alert-success` 绿色，加 `white-space: pre-line` 支持换行
- 详细信息用 `panel panel-success` 面板，`<pre>` 标签保留格式
- 各面板间距 8-10px

---

## 6. 密码强度实时检测

新密码输入框下方显示动态检测列表：

```html
<div id="pwdChecks" style="display:none; margin-top: 8px;">
    <span>&#9679; 至少9个字符</span><br />
    <span>&#9679; 包含大写字母</span><br />
    <span>&#9679; 包含小写字母</span><br />
    <span>&#9679; 包含数字</span><br />
    <span>&#9679; 包含符号</span><br />
    <span>&#9679; 无连续字符(如abcd、1234)</span>
</div>
```

每项初始显示灰色圆点 `&#9679;`，满足条件变为绿色勾号 `&#10004;`。通过 JavaScript 实时监听 `input` 事件判断。

**密码规则：9位+、含大小写+数字+符号、无 4 位连续字符（abcd/1234/qwer）**

---

## 7. 页脚

所有公共页面底部统一显示：

```html
<footer class="border-top footer text-muted">
    <div class="container" style="text-align: center;">
        GARCHINA账号管理 - <a asp-page="/Admin/UserAdmin">账户管理系统</a>
    </div>
</footer>
```

居中显示，链接到管理后台入口。

---

## 8. 响应式布局

- 桌面端：主页面宽度 80%，侧边栏固定 220px
- 移动端（<768px）：侧边栏变全宽，主内容 margin-left 归零

---

## 9. JavaScript 规范

**防止重复提交：**
```javascript
var submitting = false;
form.addEventListener('submit', function(e) {
    if (submitting) { e.preventDefault(); return; }
    submitting = true;
    setTimeout(function() {
        form.querySelectorAll('button[type="submit"]').forEach(function(b) { b.disabled = true; });
    }, 0);
    setTimeout(function() { submitting = false; }, 5000);
});
```

**Enter 键触发默认按钮：** 在表单 `keydown` 事件中拦截 Enter 键，触发对应按钮 `click()`。

---

## 10. 复制可用模板

新建页面时，复制已有页面结构：
- 主页面（公开）：参考 `Pages/Index.cshtml`
- 管理页面（需登录）：参考 `Pages/Admin/UserAdmin.cshtml`
- 仪表盘：参考 `Pages/Admin/Dashboard.cshtml`

所有 admin 页面自动使用 `_AdminLayout`（侧边栏布局），无需额外设置。
