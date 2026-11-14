using System.Text.Json;
using Sdv.Connector.Configuration;

namespace Sdv.Connector.Contracts;

internal static class OrderContract
{
    internal static void ValidateMoney(JsonElement e)
    {
        StrictJson.Shape(e, ["value", "currency"]);
        if (!Patterns.MoneyValue().IsMatch(StrictJson.Text(e.GetProperty("value"))) || StrictJson.Text(e.GetProperty("currency")) != "UAH") throw new InvalidDataException();
    }

    internal static JsonElement ValidateOrder(JsonElement order, string number)
    {
        StrictJson.Shape(order, ["orderNumber", "orderStateCode", "receiptState", "amount"], "receiptNumber");
        if (StrictJson.Text(order.GetProperty("orderNumber")) != number || StrictJson.Text(order.GetProperty("orderStateCode")) is not ("Open" or "Closed")) throw new InvalidDataException();
        var state = StrictJson.Text(order.GetProperty("receiptState"));
        if (state is not ("NotFiscalized" or "Fiscalized" or "Unknown") || order.TryGetProperty("receiptNumber", out var receipt) != (state == "Fiscalized")) throw new InvalidDataException();
        if (state == "Fiscalized" && string.IsNullOrWhiteSpace(StrictJson.Text(receipt))) throw new InvalidDataException();
        ValidateMoney(order.GetProperty("amount"));
        return order;
    }
}
