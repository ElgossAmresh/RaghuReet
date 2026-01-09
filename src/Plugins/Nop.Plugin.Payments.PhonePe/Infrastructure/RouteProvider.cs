using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PhonePe.Infrastructure;

/// <summary>
/// Represents plugin route provider
/// </summary>
public class RouteProvider : IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        // Admin configuration
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Payments.PhonePe.Configure",
            pattern: "Admin/PhonePePayment/Configure",
            defaults: new { controller = "PhonePePayment", action = "Configure", area = "Admin" });

        // Payment callback (main endpoint)
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Payments.PhonePe.PaymentCallback",
            pattern: "Plugins/PhonePePayment/PaymentCallback",
            defaults: new { controller = "PhonePePayment", action = "PaymentCallback" });

        // Payment return (alternative endpoint)
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Payments.PhonePe.Return",
            pattern: "Plugins/PhonePePayment/Return",
            defaults: new { controller = "PhonePePayment", action = "Return" });

        // Webhook endpoint
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Payments.PhonePe.Webhook",
            pattern: "Plugins/PhonePePayment/Webhook",
            defaults: new { controller = "PhonePeWebhook", action = "Webhook" });

    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}