Bruce965.NodeDevServer
======================

Automatically start a Node development server alongside with an ASP.NET Core application.


## Requirements

The [.NET SDK](https://dotnet.microsoft.com/download/dotnet) must be installed.

[Node.js](https://nodejs.org/) also needs to be installed in order to start a
Node development server.


## Usage

If you don't have one already, create a new ASP.NET Core application.

```bash
dotnet new web --output MyAspNetCoreApp
```

Add the [Bruce965.NodeDevServer](https://www.nuget.org/packages/Bruce965.NodeDevServer)
NuGet package to your ASP.NET Core application.

```bash
dotnet add MyAspNetCoreApp package Bruce965.NodeDevServer
```

If you don't have one already, create a new Node.js application.
You may use [Vite](https://vitejs.dev/) or any other framework at your preference.

```bash
npm create -y vite -- my-frontend --template vanilla
```

Configure the Node development server in your _Program.cs_ file.

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure the local Node development server.
builder.Services.AddNodeDevServer(options =>
{
    // You may need to tweak these options if you don't use Vite.
    options.HostUri = "http://localhost:5173";
    options.Path = "../my-frontend";
    options.LaunchScript = "dev";
    options.PackageManagers = ["yarn", "npm"];
});

WebApplication app = builder.Build();

// Some Node.js frameworks require this in order to support hot-reload.
app.UseWebSockets();

app.UseRouting();
app.UseEndpoints(_ => {});

if (app.Environment.IsDevelopment())
{
    // In development, forward all requests to Node.js.
    // The first request will automatically launch it.
    app.UseNodeDevServer();
}
else
{
    // In production, use the pre-built files.
    app.UseStaticFiles();
}

app.Run();
```


## License

This project is licensed under the [MIT license](LICENSE).

Some components may be available elsewhere under different license terms,
please refer to the individual source files.
