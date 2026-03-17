# Razor page 生命周期


--------------------------------------------------------
RazorPage
https://learn.microsoft.com/zh-cn/aspnet/core/razor-pages/?view=aspnetcore-7.0&tabs=visual-studio
--------------------------------------------------------
几个易错点先记下（供回顾）
    model中的属性只在第一次绑定时有效，Post回来后再次对属性赋值是不会影响到客户端的。
    post 处理要想影响到客户端，可以使用以下方法将脚本传递到客户端去执行：
        FineUICore.PageContext.RegistStartupScript();
        UIHelper.Grid("xxx").SomeAction();
        其实方法二就是对方法一的封装，FineUI对控件的Ajax支持并不全面，可考虑用方法一或自行封装Helper。
    尽量别用ViewData，而是用模型的属性。
        ViewData 是老的 mvc 方式传递参数的方法，是弱类型的，编译不报错。
        AppBoxCore RazorPage 引入 ViewData 只是为了兼容老的 mvc 代码，可以考虑删除。
    如何将客户端参数带到服务器端
        根本上都是构造请求字符串
        FineUI的话，设置 fields=“grid1”就可以了，会将参数带到服务器端


MVC、RazorPage、WebForm的区别
    https://www.cnblogs.com/tdfblog/p/asp-net-razor-pages-vs-mvc.html
    Razor页面与ASP.NET MVC开发使用的视图组件非常相似，它们具有所有相同的语法和功能。
    RazorPage 最关键的区别是模型和控制器代码也包含在Razor页面中。它是一个MVVM（Model-View-ViewModel）框架，它支持双向数据绑定，更简单的开发体验。
    RazorPage 可以看作是 WebForm 的演变，把前后端要交互的数据交给开发者，而不是一股脑子把整个页面的数据都放到ViewState中进行传递。


    
--------------------------------------------------------
初始化
--------------------------------------------------------
Startup.cs
    builder.Services.AddRazorPages();
    builder.Services.AddRazorPages(options =>
    {
        options.RootDirectory = "/MyPages";
        options.Conventions.AuthorizeFolder("/MyPages/Admin");
    });




配置读写
    appsettings.json
        {
            "Urls": "http://localhost:8080"
        }
    IConfiguration Configuration;  // IOC 注入
    string urls = Configuration["Urls"];
    
--------------------------------------------------------
RazorPage
--------------------------------------------------------
示例1
    @page
    @model IndexModel
    @using Microsoft.AspNetCore.Mvc.RazorPages
    @functions {
        public class IndexModel : PageModel
        {
            public string Message { get; private set; } = "In page model: ";
            public void OnGet()
            {
                Message += $" Server seconds  { DateTime.Now.Second.ToString() }";
            }
        }
    }
    <p>@Model.Message</p>


示例2
    @page
    @model ContosoUniversity.Pages.Students.DeleteModel
    <h1>Delete</h1>
    <p class="text-danger">@Model.ErrorMessage</p>
    <h3>Are you sure you want to delete this?</h3>
    <div>
        <h4>Student</h4>
        <dl class="row">
            <dt class="col-sm-2">@Html.DisplayNameFor(model => model.Student.LastName)</dt>
            <dd class="col-sm-10">@Html.DisplayFor(model => model.Student.LastName)</dd>
            <dt class="col-sm-2">@Html.DisplayNameFor(model => model.Student.FirstMidName)</dt>
            <dd class="col-sm-10">@Html.DisplayFor(model => model.Student.FirstMidName)</dd>
            <dt class="col-sm-2">@Html.DisplayNameFor(model => model.Student.EnrollmentDate)</dt>
            <dd class="col-sm-10">@Html.DisplayFor(model => model.Student.EnrollmentDate)</dd>
        </dl>
        <form method="post">
            <input type="hidden" asp-for="Student.Id" />
            <input type="submit" value="Delete" class="btn btn-danger" /> |
            <input type="submit" value="Save" asp-page-handler="First" class="btn btn-primary btn-xs" />
            <a asp-page="./Index">Back to List</a>
        </form>
    </div>


RazorPage 中用到的特殊代码
    采用 @Html.DisaplayFor(), @Html.DisplayNameFor() 等方法显示模型数据
    采用 @Url.Handler() 方法生成回调链接
    采用 asp-for 显示绑定数据
    采用 asp-page-handler, asp-page 定义razorpage 及处理方法
    采用 asp-action, asp-controller 定义mvc处理的控制器和方法



页面指令
    @page
    @namespace RazorPagesIntro.Pages.Customers
    @model NameSpaceModel
    @namespace RazorPagesContacts.Pages
    @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
    @tagHelperPrefix taghelper:
        <taghelper:form asp-action="Process" asp-controller="Home" />


Helper
    @Url.Handler("SignOutClick")
        http://localhost:52221/Test/Buttons?handler=SignOutClick
    @Url.Action("SignOut")
        http://localhost:52221/Test/Buttons?action=SignOut
    @Url.Content
        IconUrl="@Url.Content("~/res/images/my_face_80.jpg")"
    @Html.DisplayNameFor
        @Html.DisplayNameFor(model => model.Student.LastName)


Page dbcontext 数据库上下文
    AppDbContext是用依赖注入的方式赋值的，在 Startup.cs 中进行注册
    public class EditModel : PageModel
    {
        private readonly AppDbContext _db;
        public EditModel(AppDbContext db)
        {
            _db = db;
        }
    }


OnGet/OnGetAsync
    Get方法在首次展示页面时进行调用，此时可以对模型进行赋值，并传递给 RazorPage 进行处理
    public IActionResult OnGet()
    {
        ViewBag.btnClientClick2Script = Alert.GetShowInTopReference("通过ViewBag传递的客户端事件");
        var action = Asp.GetQueryString("action");
        if (action == "SignOut")
            return RedirectToPage("/Login");
        return UIHelper.Result();
    }
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
            return NotFound();
        Student = await _context.Students.FindAsync(id);
        if (Student == null)
            return NotFound();
        return Page();
    }

OnPost/OnPostAsync
    Post 相关方法在页面回发时调用，此时可以对数据进行校验、数据库、页面跳转等操作。此时若对模型进行赋值并不会传递给 RazorPage 进行处理
    默认
        // Student/Edit/98
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();
            if (Student != null) 
                _context.Students.Add(Student);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    删除
        // Students?id=1&handler=delete
        public async Task<IActionResult> OnPostDeleteAsync(int id)
    其它结构
        // 处理方法名结构如：OnPost[handler]Async
        <input type="submit" asp-page-handler="JoinList" value="Join" />
        <input type="submit" asp-page-handler="JoinListUC" value="JOIN UC" />
        public async Task<IActionResult> OnPostJoinListAsync()
        public async Task<IActionResult> OnPostJoinListUCAsync()
    参数
        <form method="post" asp-page-handler="search" asp-route-query="Core">    
            <button>Search "Core"</button>
        </form>
        <form method="post" asp-page-handler="delete" asp-route-id="1">
            <button>Delete Id 1</button>
        </form>
        第一个是以前看到的search处理方法，它发送“Core”作为查询参数。
        第二个是针对delete处理方法，并发送id为1，这表示它会删除第一条数据。
        public async Task OnPostSearchAsync(string query)
        public async Task<IActionResult> OnPostDeleteAsync(int id)

## 页面数据提交相应示例

``` c#
<form method="post">
    @Html.AntiForgeryToken() <!-- 必须加，否则400错误 -->
    <button type="submit" name="handler" value="Save">保存</button>
    <button type="submit" name="handler" value="Reset">重置</button>    
    <!-- 删除按钮：传递 handler=Delete + 额外参数 id -->
    <button type="submit" name="handler" value="Delete" formaction="/SiteConfig?id=1">删除</button>
</form>
public class SiteConfigModel : PageModel
{
    public IActionResult OnPostSave([FromBody] SiteConfig req)
    {
        return BuildResult(0, "保存成功", req);
    }
    public IActionResult OnPostReset()
    {
        SiteConfig.Instance = new SiteConfig();
        return BuildResult(0, "重置成功");
    }
    public IActionResult OnPostDelete(int id)
    {
        return BuildResult(0, "删除成功");
    }
}
```


Web资源隔离
    将 CSS 样式隔离到各个页面、视图和组件以减少或避免：发布应用后，框架会自动将资源移动到 wwwroot 目录
    https://learn.microsoft.com/zh-cn/aspnet/core/razor-pages/?view=aspnetcore-6.0&tabs=visual-studio
    Pages/Index.cshtml.css
    Pages/Index.cshtml.js
    Index.razor.js
    @section Scripts {
      <script src="~/Pages/Index.cshtml.js"></script>
    }

CSS 预处理
    AspNetCore.SassCompiler 可以在生成过程开始时编译 SASS/SCSS 文件

Layout
    Shared/_Layout.cshtml
        @RenderSection("head", false)
        @RenderSection("body", true)
        @RenderSection("script", false)
    Page
        @{Layout = "_Layout";}
        @{Layout = null;}
        @section head {...}
        @section body {...}
        @section script {...}


ViewData
    ViewData 是一个PageModal的成员，是一个对象数组，非强类型不建议使用
    对应的有 RazorPageBase.ViewBag
    [ViewData] public string Title { get; } = "About";
    <title>@ViewData["Title"]</title>
    <h1>@Model.Title</h1>


RazorPageFilter
    Razor 页面筛选器提供的以下方法可在全局或页面级应用。类似以前的HttpModule。详见微软官方帮助网页。
    同步方法：
        OnPageHandlerSelected：在选择处理程序方法后，但在模型绑定发生之前调用。
        OnPageHandlerExecuting：在模型绑定完成后，执行处理程序方法之前调用。
        OnPageHandlerExecuted：在执行处理器方法后，生成操作结果前调用。
    异步方法：
        OnPageHandlerSelectionAsync：在选择处理程序方法后，但在模型绑定发生前，进行异步调用。
        OnPageHandlerExecutionAsync：在调用处理程序方法前，但在模型绑定结束后，进行异步调用。
    配置
        services.AddRazorPages(options =>
        {
            options.Conventions.AddFolderApplicationModelConvention(
                "/Movies",
                model => model.Filters.Add(new SampleAsyncPageFilter(Configuration)));
        });


PageModel.Result 返回
    success
        ContentResult               : 静态文本
        PageResult                  : 页面
        FileStreamResult            : 文件流
        FileContentResult           : 
        PartialViewResult           : 
        PhysicalFileResult          : 
    fail                            
        BadRequestObjectResult      : 
        BadRequestResult            : 
        ForbidResult                : 禁止访问
        ChallengeResult             : 
        VirtualFileResult           : 
        NotFoundResult              : 文件未找到
        NotFoundObjectResult        : 
        UnauthorizedResult          : 非授权
    route                           
        RedirectResult              : 跳转
        RedirectToActionResult      : 
        RedirectToPageResult        : 
        RedirectToRouteResult       : 
        LocalRedirectResult         : 
    auth                            
        SignInResult                : 登录
        SignOutResult               : 注销
    other                           
        StatusCodeResult            : 
        ObjectResult                : 
        ViewComponentResult         : 
    页面跳转                        
        RedirectToPage              : 


XSS 防注入能力
    安全输出：@Model.Content
    危险输出：@Html.Raw(Model.Content)
    清理示例（伪码）：
    var sanitizer = new HtmlSanitizer();
    var safe = sanitizer.Sanitize(userInput);


--------------------------------------------------------
Razor page data binding
--------------------------------------------------------
页面数据模型、绑定属性
    https://learn.microsoft.com/zh-cn/aspnet/core/data/ef-rp/crud?view=aspnetcore-6.0#overposting
    [BindProperty]
    public Student? Student { get; set; }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();
        if (Student != null) 
            _context.Students.Add(Student);
        await _context.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
    <input asp-for="Customer!.Name" />
    使用绑定属性，可以在前台和后台自动传递类对象，简化字段的收集组装工作
    对于一些简单类型的字段，用绑定属性可以大大简化数据采集验证代码

绑定属性过度发布的处理
    https://learn.microsoft.com/zh-cn/aspnet/core/data/ef-rp/crud?view=aspnetcore-6.0#overposting
    若页面端不需要填写所有字段，使用 BindProperty 会有两个潜在的问题
        （1）占用带宽。对简单类问题不大，对于复杂类型需要考证。
        （2）黑客会Post非需要字段，导致数据被篡改。
        public class Student
        {
            public int Id { get; set; }
            public string LastName { get; set; }
            public string FirstMidName { get; set; }
            public DateTime EnrollmentDate { get; set; }
            public string Secret { get; set; }            // 隐私字段
        }
    解决方案
        （1）把需要修改的字段剥离出来，作为PageModel的属性。
        （2）创建PageModel，只包含需要修改的字段。如：
        public class StudentVM
        {
            public int Id { get; set; }
            public string LastName { get; set; }
            public string FirstMidName { get; set; }
            public DateTime EnrollmentDate { get; set; }
        }

数据获取和更新
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
            return NotFound();
        Student = await _context.Students.FindAsync(id);
        if (Student == null)
            return NotFound();
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int id)
    {
        var studentToUpdate = await _context.Students.FindAsync(id);
        if (studentToUpdate == null)
            return NotFound();

        if (await TryUpdateModelAsync<Student>(
            studentToUpdate,
            "student",
            s => s.FirstMidName, s => s.LastName, s => s.EnrollmentDate))
        {
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }

        return Page();
    }

--------------------------------------------------------
数据验证
--------------------------------------------------------
    实体对象
        using System.ComponentModel.DataAnnotations;
        public class Movie
        {
            public int Id { get; set; }

            [StringLength(60, MinimumLength = 3)]
            [Required]
            public string Title { get; set; }

            [Display(Name = "Release Date")]
            [DataType(DataType.Date)]
            public DateTime ReleaseDate { get; set; }

            [Range(1, 100)]
            [DataType(DataType.Currency)]
            [Column(TypeName = "decimal(18, 2)")]
            public decimal Price { get; set; }

            [RegularExpression(@"^[A-Z]+[a-zA-Z\s]*$")]
            [Required]
            [StringLength(30)]
            public string Genre { get; set; }

            [RegularExpression(@"^[A-Z]+[a-zA-Z0-9""'\s-]*$")]
            [StringLength(5)]
            [Required]
            public string Rating { get; set; }
        }
    客户端校验
        @page
        @model CreateFATHModel
        <html>
        <body>
            <p>Enter your name.</p>
            <div asp-validation-summary="All"></div>
            <form method="POST">
                <div>Name: <input asp-for="Customer.Name" /></div>
                <input type="submit" asp-page-handler="JoinList" value="Join" />
                <input type="submit" asp-page-handler="JoinListUC" value="JOIN UC" />
            </form>
        </body>
        </html>
    服务器端校验
        if (!ModelState.IsValid)
            return Page();
 

#--------------------------------------------
# Page Filter
#--------------------------------------------
Razor 页面筛选器：
    类似 HttpModule 的东东
    https://learn.microsoft.com/zh-cn/aspnet/core/razor-pages/filter?view=aspnetcore-7.0
    - 在选择处理程序方法后但在模型绑定发生前运行代码。
    - 在模型绑定完成后，执行处理程序方法前运行代码。
    - 在执行处理程序方法后运行代码。
    - 可在页面或全局范围内实现。
    - 可以用依赖项注入 (DI) 填充构造函数依赖项。 有关详细信息，请参阅 ServiceFilterAttribute 和 TypeFilterAttribute。
同步方法：
    OnPageHandlerSelected：在选择处理程序方法后，但在模型绑定发生之前调用。
    OnPageHandlerExecuting：在模型绑定完成后，执行处理程序方法之前调用。
    OnPageHandlerExecuted：在执行处理器方法后，生成操作结果前调用。
异步方法：
    OnPageHandlerSelectionAsync：在选择处理程序方法后，但在模型绑定发生前，进行异步调用。
    OnPageHandlerExecutionAsync：在调用处理程序方法前，但在模型绑定结束后，进行异步调用。


ResultFilterAttribute
    简化Filter，用Attribute提供服务
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Filters;
    namespace PageFilter.Filters
    {
        public class AddHeaderAttribute  : ResultFilterAttribute
        {
            private readonly string _name;
            private readonly string _value;

            public AddHeaderAttribute (string name, string value)
            {
                _name = name;
                _value = value;
            }

            public override void OnResultExecuting(ResultExecutingContext context)
            {
                context.HttpContext.Response.Headers.Add(_name, new string[] { _value });
            }
        }
    }
    [AddHeader("Author", "Rick")]
    public class TestModel : PageModel
    [Authorize]
    public class ModelWithAuthFilterModel : PageModel




--------------------------------------------
## TagHelper
https://blog.csdn.net/sD7O95O/article/details/132484920
https://github.com/bingbing-gui/Asp.Net-Core-Skill/tree/master/Fundamentals/AspNetCore.TagHelpers/AspNetCore.BuiltInTagHelpers
--------------------------------------------
表单相关Tag Helpers：
    Form
        asp-controller 根据应用程序的路由指定目标Controller，如果省略，使用当前视图文件所在的控制器
        asp-action  根据应用程序的路由指定目标Action方法，如果省略，使用视图文件当前Action方法
        asp-route 通过指定路由的名称生成action属性
        asp-route-*  指定url额外的段，例如 asp-route-id 使用提供id段的值
        asp-area  指定目标区域的名称
        asp-antiforgery 生成一个隐藏的请求验证令牌用来防止跨站点请求攻击，经常和[ValidateAntiForgeryToken]特性一起使用，[ValidateAntiForgeryToken]使用在HTTP Post方法上
    FormAction
    Input
    Label
    Option
    Select
    TextArea
    ValidationMessage
    ValidationSummary

缓存：
    Cache
        <cache expires-after="@TimeSpan.FromSeconds(20)">
            Date Time: @DateTime.Now.ToString("HH:mm:ss")
        </cache>
    Distributed

其他：
    Image
    Anchor
    Script
        asp-src-include   指定在视图中包含的JavaScript文件
        asp-src-exclude   指定在视图中排除的JavaScript文件
        asp-append-version  指定是否把文件版本附加到连接地址，使用 Cache Busting
        asp-fallback-src-include  如果使用的CDN出现一些问题时，指定备用的JavaScript文件
        asp-fallback-src-exclude  如果使用CDN出现一些问题，指定排除的JavaScript文件
        asp-fallback-test  JavaScript代码段，用来测试能否正确加载CDN
    Link(css)
        asp-href-include                  指定视图中包含的CSS文件
        asp-href-exclude                  指定视图中排除的CSS文件
        asp-append-version                指定css文件查询的版本号，用作Cache Busting
        asp-fallback-href-include         指定包含的CSS文件，在CDN出现问题时
        asp-fallback-href-exclude         指定排除的CSS文件，在CDN出现问题时
        asp-fallback-href-test-class      指定一个CSS文件类测试CDN是否可用
        asp-fallback-href-test-property   使用CSS类的属性测试CDN
        asp-fallback-href-test-value      指定一个测试值，用来确定CDN是否可用
    Environment




## PageModel 响应生命周期

graph TD
    A[接收HTTP请求] --> B[路由匹配（找到对应Page）]
    B --> C[初始化PageModel实例]
    C --> D[模型绑定（绑定路由/表单/Body参数）]
    D --> E[执行过滤器（如授权/验证）]
    E --> F{判断请求方法}
    F -->|GET| G[执行OnGet/OnGetXXX方法]
    F -->|POST| H[执行OnPost/OnPostXXX方法]
    G --> I[页面渲染（返回HTML）]
    H --> J[返回结果（HTML/JSON/Redirect等）]
    I --> K[响应客户端]
    J --> K

OnGet()	
    GET 请求到当前 Page，且无指定 ActionName（默认触发）	
    访问页面（如 /SiteConfig）时自动执行，初始化 Item 属性
    浏览器直接访问 https://你的域名/SiteConfig（GET 请求），自动执行 OnGet()，初始化 Item 属性，然后渲染页面（页面中可通过 @Model.Item 访问）。

OnGetData()	
    GET 请求 + URL 携带 handler=Data（如 /SiteConfig?handler=Data）	
    前端 AJAX GET 请求该 URL 时触发，返回 SiteConfig 数据
    $.get("/SiteConfig?handler=Data", function(res) {
        console.log(res); // 接收 BuildResult 返回的 JSON
    });

OnPostSave()	
    POST 请求 + URL 携带 handler=Save（或表单 / Body 携带 __RequestVerificationToken + handler=Save）	
    前端 AJAX POST 请求（带 Body）触发，处理保存逻辑
    // axios 示例
    axios.post("/SiteConfig?handler=Save", { /* SiteConfig 数据 */ }, {
        headers: {
            "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() // 防跨站伪造令牌
        }
    }).then(res => {
        console.log(res);
    });

OnPost()	
    POST 请求到当前 Page，无指定 handler（默认 POST 方法）	
    无后缀的默认 POST 方法





## 页面数据提交相应示例

``` c#
<button onclick="saveConfig()">保存</button>
<button onclick="resetConfig()">重置</button>
<button onclick="deleteConfig(1)">删除</button>

<script>
// 获取防跨站令牌
const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

// 保存
function saveConfig() {
    const data = { /* 你的配置数据 */ };
    fetch("/SiteConfig?handler=Save", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": token
        },
        body: JSON.stringify(data)
    }).then(res => res.json()).then(console.log);
}

// 重置
function resetConfig() {
    fetch("/SiteConfig?handler=Reset", {
        method: "POST",
        headers: { "RequestVerificationToken": token }
    }).then(res => res.json()).then(console.log);
}

// 删除
function deleteConfig(id) {
    fetch(`/SiteConfig?handler=Delete&id=${id}`, {
        method: "POST",
        headers: { "RequestVerificationToken": token }
    }).then(res => res.json()).then(console.log);
}
</script>
```





-----------------------------------------------------
局部视图 Partical view
-----------------------------------------------------
Views/Shared/Partials/EleFormHeader.cshtml

``` html
<div class="ele-form-header py-2 px-3 bg-primary text-white mb-3">
    <h5 class="mb-0">@Model</h5>
</div>
```

a.cshtml
``` html
@page
@model YourProjectName.EleUI.Samples.aModel

<!-- 引用局部视图，传递模型参数（标题） -->
<partial name="~/Views/Shared/Partials/EleFormHeader.cshtml" model="基础信息表单" />

<!-- 原有表单内容 -->
<EleForm Model="Model.Form" LabelWidth="140px">
    <EleInput For="Form.Name" Label="名称" Required="true"></EleInput>
</EleForm>
```



-----------------------------------------------------
## ViewComponent 视图组件（可带逻辑）
-----------------------------------------------------
Views/Shared/Components/EleUserInfo/Default.cshtml

``` html
@model YourProjectName.Models.User
<div class="card mb-3" style="width: 18rem;">
    <div class="card-header">
        <h6 class="mb-0">用户信息</h6>
    </div>
    <div class="card-body">
        <p class="card-text">用户名：@Model.Username</p>
        <p class="card-text">邮箱：@Model.Email</p>
        <p class="card-text">角色：@Model.RoleName</p>
    </div>
</div>
```


ViewComponents/EleUserInfoViewComponent.cs

``` csharp
using Microsoft.AspNetCore.Mvc;
namespace YourProjectName.ViewComponents
{
    // 命名规则：[组件名] + ViewComponent（可通过 [ViewComponent(Name = "EleUserInfo")] 自定义名称）
    public class EleUserInfoViewComponent : ViewComponent
    {
        // 可选：依赖注入服务（如用户服务）
        private readonly IUserService _userService;

        // 构造函数注入
        public EleUserInfoViewComponent(IUserService userService)
        {
            _userService = userService;
        }

        // 核心方法：Invoke（同步）或 InvokeAsync（异步），返回视图
        public async Task<IViewComponentResult> InvokeAsync(int userId)
        {
            // 后台逻辑：查询用户信息（模拟）
            var user = await _userService.GetUserByIdAsync(userId);
            
            // 返回视图，并传递模型
            return View(user);
        }
    }
}
```

a.cshtml

``` html
@page
@model YourProjectName.EleUI.Samples.bModel
<!-- 引用视图组件，传递参数（userId） -->
<vc:ele-user-info user-id="1"></vc:ele-user-info>
```