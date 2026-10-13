var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.SecretSanta_ApiService>("apiservice");

builder.AddProject<Projects.SecretSanta_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
