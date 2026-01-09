using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Payments.PhonePe.Models;
using Nop.Plugin.Payments.PhonePe.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PhonePe.Controllers;

/// <summary>
/// PhonePe payment controller
/// </summary>
[AutoValidateAntiforgeryToken]
public class PhonePePaymentController : BasePluginController
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly ILogger _logger;
    private readonly INotificationService _notificationService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IOrderService _orderService;
    private readonly IPermissionService _permissionService;
    private readonly IPhonePeService _phonePeService;
    private readonly ISettingService _settingService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly PhonePePaymentSettings _phonePePaymentSettings;

    #endregion

    #region Ctor

    public PhonePePaymentController(
        ILocalizationService localizationService,
        ILogger logger,
        INotificationService notificationService,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        IPermissionService permissionService,
        IPhonePeService phonePeService,
        ISettingService settingService,
        IStoreContext storeContext,
        IWorkContext workContext,
        PhonePePaymentSettings phonePePaymentSettings)
    {
        _localizationService = localizationService;
        _logger = logger;
        _notificationService = notificationService;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _permissionService = permissionService;
        _phonePeService = phonePeService;
        _settingService = settingService;
        _storeContext = storeContext;
        _workContext = workContext;
        _phonePePaymentSettings = phonePePaymentSettings;
    }

    #endregion

    #region Methods - Admin Configuration

    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    public async Task<IActionResult> Configure()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS))
            return AccessDeniedView();

        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<PhonePePaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            ActiveStoreScopeConfiguration = storeScope,
            MerchantId = settings.MerchantId,
            SaltKey = settings.SaltKey,
            SaltIndex = settings.SaltIndex,
            UseSandbox = settings.UseSandbox,
            AdditionalFee = settings.AdditionalFee,
            AdditionalFeePercentage = settings.AdditionalFeePercentage
        };

        if (storeScope > 0)
        {
            model.MerchantId_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.MerchantId, storeScope);
            model.SaltKey_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.SaltKey, storeScope);
            model.SaltIndex_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.SaltIndex, storeScope);
            model.UseSandbox_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.UseSandbox, storeScope);
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AdditionalFeePercentage, storeScope);
        }

        return View("~/Plugins/Payments.PhonePe/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS))
            return AccessDeniedView();

        if (!ModelState.IsValid)
            return await Configure();

        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<PhonePePaymentSettings>(storeScope);

        settings.MerchantId = model.MerchantId;
        settings.SaltKey = model.SaltKey;
        settings.SaltIndex = model.SaltIndex;
        settings.UseSandbox = model.UseSandbox;
        settings.AdditionalFee = model.AdditionalFee;
        settings.AdditionalFeePercentage = model.AdditionalFeePercentage;

        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.MerchantId, model.MerchantId_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.SaltKey, model.SaltKey_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.SaltIndex, model.SaltIndex_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);

        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #endregion

    #region Methods - Payment Callback

    /// <summary>
    /// Handle payment callback from PhonePe
    /// </summary>
    [HttpPost]
    [HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PaymentCallback()
    {
        try
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();

            // Log all received parameters for debugging
            var allParams = string.Join(", ", Request.Query.Select(q => $"{q.Key}={q.Value}"));
            await _logger.InformationAsync($"PhonePe callback received. Query params: {allParams}");

            // Extract merchant transaction ID (contains order ID)
            var merchantTransactionId = Request.Query["merchantOrderId"].ToString();
            //if (string.IsNullOrEmpty(merchantTransactionId))
            //{
            //    merchantTransactionId = Request.Form["merchantOrderId"].ToString();
            //}

            // Get transaction ID from query string or form
            var transactionId = Request.Query["transactionId"].ToString();
            //if (string.IsNullOrEmpty(transactionId))
            //{
            //    transactionId = Request.Form["transactionId"].ToString();
            //}

            // FALLBACK STRATEGY: If merchantOrderId is missing
            if (string.IsNullOrEmpty(merchantTransactionId))
            {
                await _logger.WarningAsync("PhonePe callback: merchantOrderId is missing. Attempting fallback strategy.");

                // Strategy 1: Try to get the most recent pending order for this customer
                var recentOrder = await GetRecentPendingOrderAsync(customer, store);

                if (recentOrder != null)
                {
                    await _logger.InformationAsync($"PhonePe: Found recent pending order #{recentOrder.Id} for customer");

                    // Redirect to payment status check page
                    return RedirectToAction("CheckPaymentStatus", new { orderId = recentOrder.Id });
                }

                // Strategy 2: No recent order found - show generic message
                await _logger.ErrorAsync($"PhonePe callback: No merchantOrderId and no recent pending orders for customer {customer.Id}");

                // Redirect to a custom payment status page with message
                TempData["PaymentStatusMessage"] = await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.PaymentProcessing");
                return RedirectToAction("PaymentStatus");
            }

            // Normal flow when merchantOrderId exists
            var orderId = ExtractOrderIdFromMerchantTransactionId(merchantTransactionId);
            if (orderId <= 0)
            {
                await _logger.ErrorAsync($"PhonePe callback: Invalid merchant transaction ID format: {merchantTransactionId}");
                TempData["PaymentStatusMessage"] = await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.InvalidTransaction");
                return RedirectToAction("PaymentStatus");
            }

            // Get the order
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
            {
                await _logger.ErrorAsync($"PhonePe callback: Order not found. OrderId: {orderId}");
                TempData["PaymentStatusMessage"] = await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.OrderNotFound");
                return RedirectToAction("PaymentStatus");
            }

            // Verify the order belongs to current customer (security check)
            if (order.CustomerId != customer.Id)
            {
                await _logger.WarningAsync($"PhonePe callback: Order {orderId} does not belong to customer {customer.Id}");
                return RedirectToRoute("Homepage");
            }

            // Since PhonePe webhook handles the actual status update, 
            // just redirect to a status checking page
            return RedirectToAction("CheckPaymentStatus", new { orderId = order.Id });
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe payment callback error: {ex.Message}", ex);
            TempData["PaymentStatusMessage"] = await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.PaymentError");
            return RedirectToAction("PaymentStatus");
        }
    }

    /// <summary>
    /// Handle return from PhonePe (alternative callback endpoint)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Return()
    {
        // Redirect to main callback handler
        return await PaymentCallback();
    }

    /// <summary>
    /// Check payment status page (polling for webhook update)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckPaymentStatus(int orderId)
    {
        try
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var order = await _orderService.GetOrderByIdAsync(orderId);

            if (order == null || order.CustomerId != customer.Id)
            {
                await _logger.WarningAsync($"PhonePe: Invalid order access attempt. OrderId: {orderId}, CustomerId: {customer.Id}");
                return RedirectToRoute("Homepage");
            }

            // Check current order status
            if (order.PaymentStatus == Nop.Core.Domain.Payments.PaymentStatus.Paid)
            {
                // Payment already processed by webhook
                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }

            var model = new PaymentStatusModel
            {
                OrderId = order.Id,
                CustomOrderNumber = order.CustomOrderNumber,
                OrderTotal = order.OrderTotal,
                PaymentStatus = order.PaymentStatus.ToString(),
                OrderStatus = order.OrderStatus.ToString(),
                IsProcessing = order.PaymentStatus == Nop.Core.Domain.Payments.PaymentStatus.Pending,
                CheckStatusUrl = Url.Action("GetPaymentStatus", new { orderId = order.Id })
            };

            // Use the correct view path for NopCommerce plugins
            return View("~/Plugins/Payments.PhonePe/Views/PaymentStatus.cshtml", model);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe payment status check error: {ex.Message}", ex);
            return RedirectToRoute("Homepage");
        }
    }

    /// <summary>
    /// API endpoint to get current payment status (for AJAX polling)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPaymentStatus(int orderId)
    {
        try
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var order = await _orderService.GetOrderByIdAsync(orderId);

            if (order == null || order.CustomerId != customer.Id)
            {
                return Json(new { success = false, message = "Order not found" });
            }

            // With the following, ensuring you reference the enum, not the method:
            if (order.PaymentStatus == Nop.Core.Domain.Payments.PaymentStatus.Paid)
            {
                // Payment already processed by webhook
                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
            // Check if webhook has updated the order
            if (order.PaymentStatus == Nop.Core.Domain.Payments.PaymentStatus.Paid)
            {
                return Json(new
                {
                    success = true,
                    status = "paid",
                    message = await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.PaymentSuccess"),
                    redirectUrl = Url.RouteUrl("CheckoutCompleted", new { orderId = order.Id })
                });
            }
            else if (order.PaymentStatus == Nop.Core.Domain.Payments.PaymentStatus.Voided ||
                     order.OrderStatus == OrderStatus.Cancelled)
            {
                return Json(new
                {
                    success = false,
                    status = "failed",
                    message = await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.PaymentFailed"),
                    redirectUrl = Url.RouteUrl("ShoppingCart")
                });
            }
            else
            {
                return Json(new
                {
                    success = true,
                    status = "pending",
                    message = await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.PaymentPending")
                });
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe get payment status error: {ex.Message}", ex);
            return Json(new { success = false, message = "Error checking status" });
        }
    }

    /// <summary>
    /// Generic payment status page (when no order information available)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PaymentStatus()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
      
        var recentCompletedOrder = await GetRecentCompletedOrderAsync(customer, store);
    
        var model = new PaymentStatusModel
        {
            Message = TempData["PaymentStatusMessage"]?.ToString() ??
                 await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.PaymentProcessing"),
            ShowOrderHistory = true,
            OrderHistoryUrl = Url.RouteUrl("CustomerOrders")
        };

        if (recentCompletedOrder != null)
        {
            await _logger.InformationAsync($"PhonePe: Found recent completed order #{recentCompletedOrder.Id} for customer");
          //  model.PaymentStatus = recentCompletedOrder.PaymentStatus;
            // Redirect to payment status check page
            return RedirectToAction("CheckPaymentStatus", new { orderId = recentCompletedOrder.Id });
        }

        // Use the correct view path for NopCommerce plugins
        return View("~/Plugins/Payments.PhonePe/Views/PaymentStatus.cshtml", model);
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Extract order ID from merchant transaction ID
    /// </summary>
    /// <param name="merchantTransactionId">Merchant transaction ID (format: MT{OrderId}_{Timestamp})</param>
    /// <returns>Order ID</returns>
    private int ExtractOrderIdFromMerchantTransactionId(string merchantTransactionId)
    {
        try
        {
            // Format: MT{OrderId}_{Timestamp}
            // Example: MT123_637891234567890000
            if (string.IsNullOrEmpty(merchantTransactionId) || !merchantTransactionId.StartsWith("MT"))
                return 0;

            var parts = merchantTransactionId.Substring(2).Split('_');
            if (parts.Length < 2)
                return 0;

            return int.TryParse(parts[0], out var orderId) ? orderId : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get recent pending order for customer (fallback strategy)
    /// </summary>
    private async Task<Order> GetRecentPendingOrderAsync(Customer customer, Store store)
    {
        try
        {
            // Get orders from last 15 minutes with pending payment status
            var recentOrders = await _orderService.SearchOrdersAsync(
                storeId: store.Id,
                customerId: customer.Id,
                psIds: new List<int> { (int)Nop.Core.Domain.Payments.PaymentStatus.Pending },
                createdFromUtc: DateTime.UtcNow.AddMinutes(-15),
                pageSize: 1
            );

            return recentOrders.FirstOrDefault();
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Error getting recent pending order: {ex.Message}", ex);
            return null;
        }
    }

    private async Task<Order> GetRecentCompletedOrderAsync(Customer customer, Store store)
    {
        try
        {
            // Get orders from last 15 minutes with pending payment status
            var recentOrders = await _orderService.SearchOrdersAsync(
                storeId: store.Id,
                customerId: customer.Id,
                psIds: new List<int> { (int)Nop.Core.Domain.Payments.PaymentStatus.Paid },
                createdFromUtc: DateTime.UtcNow.AddMinutes(-15),
                pageSize: 1
            );

            return recentOrders.FirstOrDefault();
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Error getting recent paid order: {ex.Message}", ex);
            return null;
        }
    }
    #endregion
}
