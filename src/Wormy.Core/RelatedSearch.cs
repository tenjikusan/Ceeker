namespace Wormy.Core;

public static class RelatedSearch
{
    private static readonly string[][] Groups =
    [
        ["invoice","invoices","bill","bills"], ["receipt","receipts"],
        ["wifi","wi-fi","wireless"], ["password","passwords","passcode","passcodes"],
        ["photo","photos","picture","pictures","photograph","photographs"],
        ["resume","cv"], ["meeting","meetings","minutes"],
        ["presentation","presentations","slides"], ["budget","budgets"],
        ["contract","contracts","agreement","agreements"]
    ];
    public static IReadOnlyList<string> Alternatives(string term) => Groups.FirstOrDefault(group => group.Contains(term,StringComparer.OrdinalIgnoreCase)) ?? [term];
}
