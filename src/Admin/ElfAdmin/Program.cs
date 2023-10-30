using ElfAdmin.Components;
using Microsoft.Fast.Components.FluentUI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddFluentUIComponents(options =>
{
    options.HostingModel = BlazorHostingModel.WebAssembly;
});

builder.Services.AddScoped<ElfAuthorizationMessageHandler>();

// builder.HostEnvironment.BaseAddress
builder.Services.AddHttpClient("ElfAPI", client => client.BaseAddress = new Uri("https://go.edi.wang"))
    .AddHttpMessageHandler<ElfAuthorizationMessageHandler>();

// Supply HttpClient instances that include access tokens when making requests to the server project
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("ElfAPI"));

builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add("api://a439e578-3ff8-4bee-91e5-96141234bc67/access_as_user");
});

await builder.Build().RunAsync();