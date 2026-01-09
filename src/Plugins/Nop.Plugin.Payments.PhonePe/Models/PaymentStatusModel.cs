using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PhonePe.Models;

public record PaymentStatusModel : BaseNopModel
{
    public int OrderId { get; set; }
    public string CustomOrderNumber { get; set; }
    public decimal OrderTotal { get; set; }
    public string PaymentStatus { get; set; }
    public string OrderStatus { get; set; }
    public bool IsProcessing { get; set; }
    public string CheckStatusUrl { get; set; }
    public string Message { get; set; }
    public bool ShowOrderHistory { get; set; }
    public string OrderHistoryUrl { get; set; }
}