using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PhonePe.Models;

/// <summary>
/// Represents a payment info model
/// </summary>
public record PaymentInfoModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets description text
    /// </summary>
    public string DescriptionText { get; set; }
}