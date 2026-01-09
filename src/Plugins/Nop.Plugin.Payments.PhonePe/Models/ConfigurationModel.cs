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
    /// Gets or sets merchant ID
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.PhonePe.Fields.MerchantId")]
    [Required]
    public string MerchantId { get; set; }
    public bool MerchantId_OverrideForStore { get; set; }

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
}