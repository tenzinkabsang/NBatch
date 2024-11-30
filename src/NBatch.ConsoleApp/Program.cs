// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;

Console.WriteLine("Hello, World!");

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var connString = config["ConnectionStrings:MyDb"];