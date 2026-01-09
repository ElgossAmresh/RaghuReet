using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PhonePe.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PhonePe.Components;

/// <summary>
/// PhonePe payment view component
/// </summary>
public class PhonePePaymentViewComponent : NopViewComponent
{
    private readonly ILocalizationService _localizationService;

    public PhonePePaymentViewComponent(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new PaymentInfoModel
        {
            DescriptionText = await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.PaymentMethodDescription")
        };

        return View("~/Plugins/Payments.PhonePe/Views/PaymentInfo.cshtml", model);
    }
}