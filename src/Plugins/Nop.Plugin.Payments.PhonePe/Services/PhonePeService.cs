using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PhonePe.Models;
using Nop.Services.Logging;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PhonePe.Services;

/// <summary>
/// Represents PhonePe service
/// </summary>
public class PhonePeService : IPhonePeService
{
    #region Fields

    private readonly PhonePePaymentSettings _settings;
    private readonly ILogger _logger;
    private readonly IWebHelper _webHelper;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string PRODUCTION_Auth_URL = "https://api.phonepe.com/apis/identity-manager";
    private const string PRODUCTION_URL = "https://api.phonepe.com/apis/pg";
    private const string SANDBOX_URL = "https://api-preprod.phonepe.com/apis/pg-sandbox";

    #endregion

    #region Ctor

    public PhonePeService(
        PhonePePaymentSettings settings,
        ILogger logger,
        IWebHelper webHelper,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings;
        _logger = logger;
        _webHelper = webHelper;
        _httpClientFactory = httpClientFactory;
    }

    #endregion

    #region Methods
    private async Task<string> GetAuthTokenAsync(Order order)
    {
        try
        {
            using (var httpClient = _httpClientFactory.CreateClient())
            {
                //var parameters = new Dictionary<string, string>
                //{
                //    ["client_id"] = "SU2512071910157338464061",
                //    ["client_version"] = "1",
                //    ["client_secret"] = "26d59e17-e0e2-4da1-94e4-7b8a8d83b04d",
                //    ["grant_type"] = "client_credentials"
                //};

                var parameters = new Dictionary<string, string>
                {
                    ["client_id"] = "M23CJ5JI2B2F2_2512301009",
                    ["client_version"] = "1",
                    ["client_secret"] = "YjgwMmJiZTQtZDI5MS00MDBmLWE0YTgtMzQyMDJhNjQ1ZDhj",
                    ["grant_type"] = "client_credentials"
                };

                using (var content = new FormUrlEncodedContent(parameters))
                {
                    var apiUrl = _settings.UseSandbox ? SANDBOX_URL : PRODUCTION_Auth_URL;

                    var response = await httpClient.PostAsync($"{apiUrl}/v1/oauth/token", content);

                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                        if (result.TryGetProperty("access_token", out var accessToken))
                        {
                            return accessToken.GetString() ?? string.Empty;
                        }
                    }
                    await _logger.ErrorAsync($"PhonePe payment initiation of Auth Token failed: {responseContent}");
                    return string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe payment initiation of Auth Token error: {ex.Message}", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Initiate payment with PhonePe
    /// </summary>
    /// <param name="order">Order</param>
    /// <returns>Redirect URL</returns>
    public async Task<string> InitiatePaymentAsync(Order order)
    {
        try
        {
            var merchantTransactionId = $"MT{order.Id}_{DateTime.UtcNow.Ticks}";

            var paymentRequest = new
            {
                merchantOrderId = merchantTransactionId,
                amount = Convert.ToInt64(order.OrderTotal * 100), // Amount in paise
                expireAfter = 1200,
                paymentFlow = new
                {
                    type = "PG_CHECKOUT",
                    message = "Create payment request URL",
                    merchantUrls = new
                    {
                        redirectUrl = $"{_webHelper.GetStoreLocation()}Plugins/PhonePePayment/PaymentCallback"
                    }
                },
                metaInfo = new
                {
                    //orderId = order.Id,
                    //merchantId = _settings.MerchantId,
                    //merchantOrderId = merchantTransactionId,
                    //amount = Convert.ToInt64(order.OrderTotal * 100),
                    //merchantUserId = $"USER{order.CustomerId}",
                    //redirectUrl = $"{_webHelper.GetStoreLocation()}Plugins/PhonePePayment/PaymentCallback",
                    //redirectMode = "POST",
                    //callbackUrl = $"{_webHelper.GetStoreLocation()}Plugins/PhonePeWebhook/Webhook",
                }
            };

            var jsonPayload = JsonSerializer.Serialize(paymentRequest);
            var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPayload));

            var checksum = GenerateChecksum(base64Payload);

            var apiUrl = _settings.UseSandbox ? SANDBOX_URL : PRODUCTION_URL;
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("X-VERIFY", checksum);
          //  client.DefaultRequestHeaders.Add("X-MERCHANT-ID", _settings.MerchantId);

            string headerToken = "O-Bearer " + await GetAuthTokenAsync(order);
            client.DefaultRequestHeaders.Add("Authorization", headerToken);



            var response = await client.PostAsJsonAsync($"{apiUrl}/checkout/v2/pay", paymentRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PhonePeCreatePaymentUrlResponse>(responseContent);
                return result.RedirectUrl;
            }

            await _logger.ErrorAsync($"PhonePe payment initiation failed: {responseContent}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe payment initiation error: {ex.Message}", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Verify payment status
    /// </summary>
    /// <param name="merchantTransactionId">Merchant Transaction ID</param>
    /// <returns>Payment verification result</returns>
    public async Task<(bool success, string message)> VerifyPaymentAsync(string merchantTransactionId)
    {
        try
        {
            await _logger.InformationAsync($"PhonePe: Verifying payment for transaction: {merchantTransactionId}");

            var apiUrl = _settings.UseSandbox ? SANDBOX_URL : PRODUCTION_URL;
            
            // Create checksum for status check
            var checksumPayload = $"/pg/v1/status/{_settings.MerchantId}/{merchantTransactionId}{_settings.SaltKey}";
            var checksum = GenerateChecksum(checksumPayload);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("X-VERIFY", checksum);
            client.DefaultRequestHeaders.Add("X-MERCHANT-ID", _settings.MerchantId);
            client.DefaultRequestHeaders.Add("accept", "application/json");

            var statusUrl = $"{apiUrl}/pg/v1/status/{_settings.MerchantId}/{merchantTransactionId}";
            
            await _logger.InformationAsync($"PhonePe: Status check URL: {statusUrl}");

            var response = await client.GetAsync(statusUrl);
            var responseContent = await response.Content.ReadAsStringAsync();

            await _logger.InformationAsync($"PhonePe: Status response: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonDocument>(responseContent);
                return (false, "Unable to verify payment status. Please contact support.");
                ////// Check if payment was successful
                ////var code = result?.RootElement.GetProperty("code").GetString();
                ////var success = result?.RootElement.GetProperty("success").GetBoolean() ?? false;

                ////if (success && code == "PAYMENT_SUCCESS")
                ////{
                ////    var message = result?.RootElement.GetProperty("message")?.GetString() ?? "Payment completed successfully";

                ////    // Extract payment details
                ////    var data = result?.RootElement.GetProperty("data");
                ////    var transactionId = data?.GetProperty("transactionId").GetString();
                ////    var amount = data?.GetProperty("amount").GetInt64();

                ////    await _logger.InformationAsync($"PhonePe: Payment SUCCESS - TransactionId: {transactionId}, Amount: {amount}");

                ////    return (true, message);
                ////}
                ////else if (code == "PAYMENT_PENDING")
                ////{
                ////    await _logger.WarningAsync($"PhonePe: Payment PENDING for {merchantTransactionId}");
                ////    return (false, "Payment is pending. Please try again later.");
                ////}
                ////else if (code == "PAYMENT_DECLINED" || code == "PAYMENT_ERROR")
                ////{
                ////    var message = result?.RootElement.GetProperty("message")?.GetString() ?? "Payment failed";
                ////    await _logger.WarningAsync($"PhonePe: Payment FAILED - {message}");
                ////    return (false, message);
                ////}
                ////else
                ////{
                ////    await _logger.WarningAsync($"PhonePe: Unknown payment status code: {code}");
                ////    return (false, $"Payment status: {code}");
                ////}
            }
            else
            {
                await _logger.ErrorAsync($"PhonePe: Status check failed with status code: {response.StatusCode}");
                return (false, "Unable to verify payment status. Please contact support.");
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe payment verification error: {ex.Message}", ex);
            return (false, "Payment verification failed. Please contact support.");
        }
    }

    /// <summary>
    /// Refund payment
    /// </summary>
    /// <param name="refundPaymentRequest">Refund payment request</param>
    /// <returns>Refund result</returns>
    public async Task<RefundPaymentResult> RefundPaymentAsync(RefundPaymentRequest refundPaymentRequest)
    {
        try
        {
            var merchantTransactionId = $"RF{refundPaymentRequest.Order.Id}_{DateTime.UtcNow.Ticks}";

            var refundRequest = new
            {
                merchantId = _settings.MerchantId,
                merchantTransactionId = merchantTransactionId,
                originalTransactionId = refundPaymentRequest.Order.AuthorizationTransactionId,
                amount = Convert.ToInt64(refundPaymentRequest.AmountToRefund * 100),
                callbackUrl = $"{_webHelper.GetStoreLocation()}Plugins/PhonePeWebhook/RefundWebhook"
            };

            var jsonPayload = JsonSerializer.Serialize(refundRequest);
            var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPayload));
            var checksum = GenerateChecksum(base64Payload);

            var apiUrl = _settings.UseSandbox ? SANDBOX_URL : PRODUCTION_URL;
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("X-VERIFY", checksum);
            client.DefaultRequestHeaders.Add("X-MERCHANT-ID", _settings.MerchantId);

            var response = await client.PostAsJsonAsync($"{apiUrl}/pg/v1/refund", new { request = base64Payload });
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return new RefundPaymentResult
                {
                    NewPaymentStatus = Core.Domain.Payments.PaymentStatus.Refunded
                };
            }

            return new RefundPaymentResult
            {
                Errors = new[] { $"Refund failed: {responseContent}" }
            };
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe refund error: {ex.Message}", ex);
            return new RefundPaymentResult
            {
                Errors = new[] { ex.Message }
            };
        }
    }

    /// <summary>
    /// Verify webhook signature
    /// </summary>
    /// <param name="payload">Payload</param>
    /// <param name="signature">Signature</param>
    /// <returns>True if valid</returns>
    public bool VerifyWebhookSignature(string payload, string signature)
    {
        var expectedSignature = GenerateChecksum(payload);
        return expectedSignature.Equals(signature, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Generate checksum for PhonePe API (updated for status check)
    /// </summary>
    /// <param name="payload">Payload</param>
    /// <returns>Checksum</returns>
    private string GenerateChecksum(string payload)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var checksum = BitConverter.ToString(hash).Replace("-", "").ToLower();
        return $"{checksum}###{_settings.SaltIndex}";
    }

    #endregion
}