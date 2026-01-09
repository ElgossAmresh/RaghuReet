using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PhonePe;

/// <summary>
/// Represents PhonePe payment settings
/// </summary>
public class PhonePePaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets merchant ID
    /// </summary>
    public string MerchantId { get; set; }

    /// <summary>
    /// Gets or sets salt key
    /// </summary>
    public string SaltKey { get; set; }

    /// <summary>
    /// Gets or sets salt index
    /// </summary>
    public int SaltIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use sandbox (test environment)
    /// </summary>
    public bool UseSandbox { get; set; }

    /// <summary>
    /// Gets or sets additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use percentage for additional fee
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets payment callback URL
    /// </summary>
    public string CallbackUrl { get; set; }

    /// <summary>
    /// Gets or sets redirect URL
    /// </summary>
    public string RedirectUrl { get; set; }
}