
#--------------------------------------------
# Aspnetcore mvc 演变
#--------------------------------------------
最简示例，开启网站，显示 hello world
    Host.CreateDefaultBuilder()
        .ConfigureWebHost(webHostBuilder => webHostBuilder
            .UseKestrel()
            .UseUrls("http://0.0.0.0:3721;https://0.0.0.0:9527")
            .Configure(app => app.Run(context => context.Response.WriteAsync("Hello World."))))
        .Build()
        .Run()
        ;

配置服务、中间件、MVC
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(webHostBuilder => webHostBuilder
            .ConfigureServices(servicecs => servicecs
                .AddRouting()
                .AddControllersWithViews())
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapControllers())))
        .Build()
        .Run()
        ;
    public class HelloController
    {
        [HttpGet("/hello")]
        public string SayHello() => "Hello World";
    }

将配置写到单独的类
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(webHostBuilder => webHostBuilder.UseStartup<Startup>())
        .Build()
        .Run()
        ;
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services) => services
            .AddRouting()
            .AddControllersWithViews()
        ;
        public void Configure(IApplicationBuilder app) => app
            .UseRouting()
            .UseEndpoints(endpoints => endpoints.MapControllers())
        ;
    }


拆分控制器和视图
    ``` 
    public class HelloController : Controller
    {
        [HttpGet("/hello/{name}")]
        public IActionResult SayHello(string name)
        {
            ViewBag.Name = name;
            return View();  // 会去找 /views/hello/SayHello.cshtml
        }
    }
    /views/hello/SayHello.cshtml
    <html>
    <head>
        <title>Hello World</title>
    </head>
    <body>
        <p>Hello, @ViewBag.Name</p>
    </body>
    </html>
    ```