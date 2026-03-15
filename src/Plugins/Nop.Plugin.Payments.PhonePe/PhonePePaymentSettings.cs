using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PhonePe;

/// <summary>
/// Represents PhonePe payment settings
/// </summary>
public class PhonePePaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets client ID
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets client secret
    /// </summary>
    public string ClientSecret { get; set; }

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

    /// <summary>
    /// Gets or sets webhook username for authentication
    /// </summary>
    public string WebhookUser { get; set; }

    /// <summary>
    /// Gets or sets webhook password for authentication
    /// </summary>
    public string WebhookPassword { get; set; }
}