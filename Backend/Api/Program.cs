var builder = WebApplication.CreateBuilder(args);

// Sem sa budú zapájať Common a moduly, napr.:
// builder.Services.AddListingsModule(builder.Configuration);

var app = builder.Build();

app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));

app.Run();
