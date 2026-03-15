using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.PhonePe.Models;

/// <summary>
/// Represents a configuration model
/// </summary>
public record ConfigurationModel : BaseNopModel, ISettingsModel
{
    /// <summary>
    /// Gets or sets the active store scope configuration
    /// </summary>
    public int ActiveStoreScopeConfiguration { get; set; }

    /// <summary>
    /// Gets or sets client ID (Production)
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.ClientId")]
    [Required]
    public string ClientId { get; set; }
    public bool ClientId_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets client secret (Production)
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.ClientSecret")]
    [Required]
    [DataType(DataType.Password)]
    public string ClientSecret { get; set; }
    public bool ClientSecret_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets sandbox client ID
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.SandboxClientId")]
    public string SandboxClientId { get; set; }
    public bool SandboxClientId_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets sandbox client secret
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.SandboxClientSecret")]
    [DataType(DataType.Password)]
    public string SandboxClientSecret { get; set; }
    public bool SandboxClientSecret_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets salt key
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.SaltKey")]
    [Required]
    [DataType(DataType.Password)]
    public string SaltKey { get; set; }
    public bool SaltKey_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets salt index
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.SaltIndex")]
    [Required]
    public int SaltIndex { get; set; }
    public bool SaltIndex_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use sandbox
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.UseSandbox")]
    public bool UseSandbox { get; set; }
    public bool UseSandbox_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets additional fee
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }
    public bool AdditionalFee_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use percentage for additional fee
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }
    public bool AdditionalFeePercentage_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets webhook username
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.WebhookUser")]
    public string WebhookUser { get; set; }
    public bool WebhookUser_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets webhook password
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.WebhookPassword")]
    [DataType(DataType.Password)]
    public string WebhookPassword { get; set; }
    public bool WebhookPassword_OverrideForStore { get; set; }
}