using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(c => c.AddJsonFile("appsettings.json", false, true))
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<ITelegramBotClient>(_ =>
            new TelegramBotClient(ctx.Configuration["BotToken"]!));

        services.AddSingleton(provider =>
        {
            var cred = GoogleCredential.FromFile(ctx.Configuration["Google:CredentialsFile"]!)
                                       .CreateScoped(SheetsService.Scope.Spreadsheets);
            return new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = "WarehouseBot"
            });
        });

        services.AddHostedService<BotWorker>();
    })
    .Build()
    .Run();
