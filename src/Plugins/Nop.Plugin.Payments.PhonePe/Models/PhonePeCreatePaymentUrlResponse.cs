using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.PhonePe.Models;
public class PhonePeCreatePaymentUrlResponse
{
    [JsonPropertyName("orderId")]
    public string OrderID { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; }

    [JsonPropertyName("expireAt")]
    public long ExpireAt { get; set; }

    [JsonPropertyName("redirectUrl")]
    public string RedirectUrl { get; set; }
}
