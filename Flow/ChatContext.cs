namespace WarehouseBot.Flow;

public class ChatContext
{
    public string? ClientName { get; set; }
    public List<(string Item, int Qty)> Basket { get; } = new();
    public string? Supervisor { get; set; }
    public string? Dept { get; set; }
    public string? Item { get; set; }
    public int? Qty { get; set; }
}
