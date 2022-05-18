namespace Shared;

public class TransactionApplied
{
    public Guid Id { get; set; }
    public Guid User { get; set; }
    public string Reason { get; set; } = "";
    public float Debit { get; set; }
    public float Credit { get; set; }
    public DateTimeOffset Date { get; set; }
}