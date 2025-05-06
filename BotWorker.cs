using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using WarehouseBot.Flow;

public sealed class BotWorker : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly SheetsService _sheets;
    private readonly string _sheetId;

    private const string ReportSheet = "'Отчёт'";
    private readonly ConcurrentDictionary<long, ChatState> _sessions = new();

    public BotWorker(IConfiguration cfg,
                     ITelegramBotClient bot,
                     SheetsService sheets)
    {
        _bot = bot;
        _sheets = sheets;
        _sheetId = cfg["SpreadsheetId"]
                   ?? throw new ArgumentNullException(nameof(cfg), "SpreadsheetId missing in configuration");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureHeaderAsync(stoppingToken);

        _bot.StartReceiving(
            HandleUpdate,
            HandleError,
            new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
            cancellationToken: stoppingToken);
    }

    private async Task HandleUpdate(ITelegramBotClient _, Update upd, CancellationToken ct)
    {
        if (upd.Type == UpdateType.Message && (upd.Message?.Text?.StartsWith("/start") ?? false))
        {
            var chat = upd.Message.Chat.Id;
            _sessions.TryRemove(chat, out ChatState _);
            await _bot.SendMessage(
                chatId: chat,
                text: "Начинаем…",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        var chatId = upd.Type switch
        {
            UpdateType.Message => upd.Message!.Chat.Id,
            UpdateType.CallbackQuery => upd.CallbackQuery!.Message!.Chat.Id,
            _ => 0L
        };
        if (chatId == 0) return;

        var session = _sessions.GetOrAdd(
            chatId,
            id => new ChatState(_bot, id, _sheets, _sheetId)
        );

        if (upd.Type == UpdateType.Message)
        {
            var txt = upd.Message!.Text ?? string.Empty;
            Console.WriteLine($"[Chat {chatId}] State={session.Fsm.State}, trigger=GotText('{txt}')");
            try
            {
                await session.Fsm.FireAsync(session.GotText, txt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FSM Error: {ex.Message}");
            }
        }
    }

    private async Task EnsureHeaderAsync(CancellationToken ct)
    {
        var range = $"{ReportSheet}!A1:F1";
        var cur = await _sheets.Spreadsheets.Values.Get(_sheetId, range).ExecuteAsync(ct);

        if (cur.Values?.Count > 0) return;

        var header = new List<object?>
        {
            "Супервайзер",
            "Клиент",
            "Отдел",
            "Товар",
            "Кол‑во",
            "Дата/время"
        };

        var req = _sheets.Spreadsheets.Values.Update(
            new ValueRange { Values = new[] { header } },
            _sheetId,
            range);
        req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

        await req.ExecuteAsync(ct);
        Console.WriteLine("⚙️ Заголовок 'Отчёт' создан.");
    }

    private Task HandleError(ITelegramBotClient _, Exception ex, CancellationToken __)
    {
        Console.WriteLine($"Polling Error: {ex}");
        return Task.CompletedTask;
    }
}