using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Stateless;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace WarehouseBot.Flow
{
    public class ChatState
    {
        private enum States
        {
            AskPass,
            AskSupervisor,
            AskClient,
            AskDepartment,
            AskItem,
            AskQuantity,
            AskDateTime,
            Completed
        }

        private enum Triggers
        {
            TextReceived
        }

        private readonly StateMachine<States, Triggers> _machine;
        private readonly StateMachine<States, Triggers>.TriggerWithParameters<string> _textTrigger;
        public StateMachine<States, Triggers> Fsm => _machine;

        private readonly ITelegramBotClient _bot;
        private readonly long _chatId;
        private readonly SheetsService _sheets;
        private readonly string _sheetId;

        private string _password;
        private string _supervisor;
        private string _client;
        private string _department;
        private string _item;
        private int _quantity;
        private DateTime _dateTime;

        public ChatState(ITelegramBotClient bot, long chatId, SheetsService sheets, string sheetId)
        {
            _bot = bot;
            _chatId = chatId;
            _sheets = sheets;
            _sheetId = sheetId;

            _machine = new StateMachine<States, Triggers>(States.AskPass);
            _textTrigger = _machine.SetTriggerParameters<string>(Triggers.TextReceived);
            ConfigureStateMachine();
            _ = _machine.FireAsync(_textTrigger, string.Empty);
        }

        private void ConfigureStateMachine()
        {
            _machine.Configure(States.AskPass)
                .OnEntryAsync(() => SendAsync("Введите пароль:"))
                .PermitDynamic(_textTrigger, txt => ValidatePassword(txt)
                    ? States.AskSupervisor
                    : States.AskPass);

            _machine.Configure(States.AskSupervisor)
                .OnEntryAsync(() => SendAsync("Кто супервизор?"))
                .OnEntryFromAsync(_textTrigger, txt => { _supervisor = txt; return Task.CompletedTask; })
                .Permit(_textTrigger, States.AskClient);

            _machine.Configure(States.AskClient)
                .OnEntryAsync(() => SendAsync("Укажите имя клиента:"))
                .OnEntryFromAsync(_textTrigger, txt => { _client = txt; return Task.CompletedTask; })
                .Permit(_textTrigger, States.AskDepartment);

            _machine.Configure(States.AskDepartment)
                .OnEntryAsync(() => SendAsync("Отдел клиента:"))
                .OnEntryFromAsync(_textTrigger, txt => { _department = txt; return Task.CompletedTask; })
                .Permit(_textTrigger, States.AskItem);

            _machine.Configure(States.AskItem)
                .OnEntryAsync(() => SendAsync("Какой товар?"))
                .OnEntryFromAsync(_textTrigger, txt => { _item = txt; return Task.CompletedTask; })
                .Permit(_textTrigger, States.AskQuantity);

            _machine.Configure(States.AskQuantity)
                .OnEntryAsync(() => SendAsync("Укажите количество:"))
                .OnEntryFromAsync(_textTrigger, txt => { _quantity = int.TryParse(txt, out var q) ? q : 0; return Task.CompletedTask; })
                .Permit(_textTrigger, States.AskDateTime);

            _machine.Configure(States.AskDateTime)
                .OnEntryAsync(() => SendAsync("Введите дату и время (yyyy-MM-dd HH:mm), или оставьте пустым:"))
                .OnEntryFromAsync(_textTrigger, txt => { _dateTime = DateTime.TryParse(txt, out var dt) ? dt : DateTime.UtcNow; return Task.CompletedTask; })
                .Permit(_textTrigger, States.Completed);

            _machine.Configure(States.Completed)
                .OnEntryAsync(async () =>
                {
                    await WriteToSheet();
                    await SendAsync("Запись успешно сохранена в таблице.");
                })
                .Permit(_textTrigger, States.AskSupervisor);
        }

        private bool ValidatePassword(string txt) => txt == "654";

        private Task SendAsync(string text) => _bot.SendTextMessageAsync(_chatId, text);

        private async Task WriteToSheet()
        {
            var row = new List<object>
            {
                _supervisor,
                _client,
                _department,
                _item,
                _quantity,
                _dateTime.ToString("yyyy-MM-dd HH:mm")
            };
            var valueRange = new ValueRange { Values = new List<IList<object>> { row } };
            var request = _sheets.Spreadsheets.Values.Append(valueRange, _sheetId, "'Отчёт.csv'!A:F");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
            await request.ExecuteAsync();
        }
    }
}
