using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PhonePe.Components;
using Nop.Plugin.Payments.PhonePe.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.PhonePe;

/// <summary>
/// PhonePe payment processor
/// </summary>
public class PhonePePaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly IOrderTotalCalculationService _orderTotalCalculationService;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;
    private readonly IPhonePeService _phonePeService;
    private readonly PhonePePaymentSettings _phonePePaymentSettings;

    #endregion

    #region Ctor

    public PhonePePaymentProcessor(
        ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        IPhonePeService phonePeService,
        PhonePePaymentSettings phonePePaymentSettings)
    {
        _localizationService = localizationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _phonePeService = phonePeService;
        _phonePePaymentSettings = phonePePaymentSettings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Process a payment
    /// </summary>
    /// <param name="processPaymentRequest">Payment info required for an order processing</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the process payment result
    /// </returns>
    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult
        {
            NewPaymentStatus = PaymentStatus.Pending
        });
    }

    /// <summary>
    /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
    /// </summary>
    /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        // Redirect to PhonePe payment gateway
        var redirectUrl = await _phonePeService.InitiatePaymentAsync(postProcessPaymentRequest.Order);

        if (!string.IsNullOrEmpty(redirectUrl))
        {
            //_webHelper.IsRequestBeingRedirected = true;
            _webHelper.RedirectToUrl(redirectUrl);
        }
    }

    /// <summary>
    /// Returns a value indicating whether payment method should be hidden during checkout
    /// </summary>
    /// <param name="cart">Shopping cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true - hide; false - display.
    /// </returns>
    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets additional handling fee
    /// </summary>
    /// <param name="cart">Shopping cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the additional handling fee
    /// </returns>
    public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
            _phonePePaymentSettings.AdditionalFee, _phonePePaymentSettings.AdditionalFeePercentage);
    }

    /// <summary>
    /// Captures payment
    /// </summary>
    /// <param name="capturePaymentRequest">Capture payment request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the capture payment result
    /// </returns>
    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
    }

    /// <summary>
    /// Refunds a payment
    /// </summary>
    /// <param name="refundPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        return await _phonePeService.RefundPaymentAsync(refundPaymentRequest);
    }

    /// <summary>
    /// Voids a payment
    /// </summary>
    /// <param name="voidPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
    }

    /// <summary>
    /// Process recurring payment
    /// </summary>
    /// <param name="processPaymentRequest">Payment info required for an order processing</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the process payment result
    /// </returns>
    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult
        {
            Errors = new[] { "Recurring payment not supported" }
        });
    }

    /// <summary>
    /// Cancels a recurring payment
    /// </summary>
    /// <param name="cancelPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult
        {
            Errors = new[] { "Recurring payment not supported" }
        });
    }

    /// <summary>
    /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
    /// </summary>
    /// <param name="order">Order</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        // PhonePe supports re-posting for pending orders
        if (order.PaymentStatus == PaymentStatus.Pending)
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Validate payment form
    /// </summary>
    /// <param name="form">The parsed form values</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of validating errors
    /// </returns>
    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        var warnings = new List<string>();
        return Task.FromResult<IList<string>>(warnings);
    }

    /// <summary>
    /// Get payment information
    /// </summary>
    /// <param name="form">The parsed form values</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the payment info holder
    /// </returns>
    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        return Task.FromResult(new ProcessPaymentRequest());
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/PhonePePayment/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(PhonePePaymentViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        // Settings
        var settings = new PhonePePaymentSettings
        {
            UseSandbox = true,
            AdditionalFee = 0,
            AdditionalFeePercentage = false
        };
        await _settingService.SaveSettingAsync(settings);

        // Locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.PhonePe.Instructions"] = "PhonePe payment gateway for secure online transactions.",
            ["Plugins.Payments.PhonePe.Fields.MerchantId"] = "Merchant ID",
            ["Plugins.Payments.PhonePe.Fields.MerchantId.Hint"] = "Enter your PhonePe Merchant ID.",
            ["Plugins.Payments.PhonePe.Fields.SaltKey"] = "Salt Key",
            ["Plugins.Payments.PhonePe.Fields.SaltKey.Hint"] = "Enter your PhonePe Salt Key.",
            ["Plugins.Payments.PhonePe.Fields.SaltIndex"] = "Salt Index",
            ["Plugins.Payments.PhonePe.Fields.SaltIndex.Hint"] = "Enter your PhonePe Salt Index.",
            ["Plugins.Payments.PhonePe.Fields.UseSandbox"] = "Use Sandbox",
            ["Plugins.Payments.PhonePe.Fields.UseSandbox.Hint"] = "Check to enable sandbox (test) environment.",
            ["Plugins.Payments.PhonePe.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.PhonePe.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.PhonePe.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.PhonePe.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.PhonePe.PaymentMethodDescription"] = "Pay securely using PhonePe",
            ["Plugins.Payments.PhonePe.PaymentStatus"] = "Payment Status",
            ["Plugins.Payments.PhonePe.ProcessingPayment"] = "Processing Your Payment",
            ["Plugins.Payments.PhonePe.PleaseWait"] = "Please wait while we verify your payment...",
            ["Plugins.Payments.PhonePe.PaymentSuccess"] = "Payment completed successfully!",
            ["Plugins.Payments.PhonePe.PaymentFailed"] = "Payment failed",
            ["Plugins.Payments.PhonePe.PaymentPending"] = "Payment is being processed",
            ["Plugins.Payments.PhonePe.PaymentProcessing"] = "Your payment is being processed",
            ["Plugins.Payments.PhonePe.InvalidTransaction"] = "Invalid transaction details",
            ["Plugins.Payments.PhonePe.OrderNotFound"] = "Order not found",
            ["Plugins.Payments.PhonePe.PaymentError"] = "An error occurred processing your payment",
            ["Plugins.Payments.PhonePe.StatusCheckTimeout"] = "We're still processing your payment",
            ["Plugins.Payments.PhonePe.CheckOrderHistory"] = "Please check your order history for status updates",
            ["Plugins.Payments.PhonePe.ViewOrders"] = "View My Orders",
            ["Plugins.Payments.PhonePe.CheckOrderHistoryMessage"] = "You can check your order status in your order history",
            ["Plugins.Payments.PhonePe.PaymentMethodDescription"] = "Pay securely using PhonePe",
            ["Plugins.Payments.PhonePe.PaymentInfo.Info"] = "You will be redirected to PhonePe's secure payment gateway to complete your transaction."

        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UninstallAsync()
    {
        // Settings
        await _settingService.DeleteSettingAsync<PhonePePaymentSettings>();

        // Locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.PhonePe");

        await base.UninstallAsync();
    }

    /// <summary>
    /// Gets a payment method description that will be displayed on checkout pages in the public store
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync("Plugins.Payments.PhonePe.PaymentMethodDescription");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether capture is supported
    /// </summary>
    public bool SupportCapture => false;

    /// <summary>
    /// Gets a value indicating whether partial refund is supported
    /// </summary>
    public bool SupportPartiallyRefund => true;

    /// <summary>
    /// Gets a value indicating whether refund is supported
    /// </summary>
    public bool SupportRefund => true;

    /// <summary>
    /// Gets a value indicating whether void is supported
    /// </summary>
    public bool SupportVoid => false;

    /// <summary>
    /// Gets a recurring payment type of payment method
    /// </summary>
    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    /// <summary>
    /// Gets a payment method type
    /// </summary>
    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

    /// <summary>
    /// Gets a value indicating whether we should display a payment information page for this plugin
    /// </summary>
    public bool SkipPaymentInfo => false;

    #endregion
}