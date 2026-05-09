using App.EleUI;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

var builder = WebApplication.CreateBuilder(args);

// Ensure static web assets from referenced projects are available even when
// running outside Development (current run mode is often Production).
builder.WebHost.UseStaticWebAssets();

var razorPages = builder.Services
    .AddRazorPages()
    .AddRazorPagesOptions(options =>
    {
        options.RootDirectory = "/";
        options.Conventions.AddFolderRouteModelConvention("/", model =>
        {
            foreach (var selector in model.Selectors.ToList())
            {
                var template = AttributeRouteModel.CombineTemplates("EleUISamples", selector.AttributeRouteModel?.Template);
                model.Selectors.Add(new SelectorModel
                {
                    AttributeRouteModel = new AttributeRouteModel
                    {
                        Template = template
                    }
                });
            }
        });
    })
    .AddApplicationPart(typeof(EleAppTagHelper).Assembly);

// EleUISamples is for live component demos, so always enable Razor runtime
// compilation to make .cshtml changes visible after browser refresh.
razorPages.AddRazorRuntimeCompilation();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "0";
    }
});
app.UseRouting();

app.MapGet("/", () => Results.Redirect("/EleUISamples"));
app.MapGet("/EleUISamples", () => Results.Redirect("/EleUISamples/Index"));
app.MapRazorPages();

app.Run("http://localhost:6070");
