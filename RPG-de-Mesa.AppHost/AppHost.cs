var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.RPG_de_Mesa>("rpg-de-mesa");

builder.Build().Run();
