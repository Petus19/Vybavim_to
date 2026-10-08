var builder = Host.CreateApplicationBuilder(args);

// Sem sa budú zapájať Common, moduly a spracovanie fronty.

var host = builder.Build();
host.Run();
