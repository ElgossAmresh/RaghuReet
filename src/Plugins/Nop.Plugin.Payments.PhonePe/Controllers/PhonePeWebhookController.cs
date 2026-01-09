using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PhonePe.Services;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.PhonePe.Controllers;

/// <summary>
/// PhonePe webhook controller
/// </summary>
public class PhonePeWebhookController : Controller
{
    #region Fields

    private readonly IPhonePeService _phonePeService;
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly ILogger _logger;

    #endregion

    #region Ctor

    public PhonePeWebhookController(
        IPhonePeService phonePeService,
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        ILogger logger)
    {
        _phonePeService = phonePeService;
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _logger = logger;
    }

    #endregion

    #region Methods

    [HttpPost]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync();

            // Get signature from header (authorization header contains the checksum)
            var signature = Request.Headers["authorization"].ToString();

            await _logger.InformationAsync($"PhonePe webhook received. Payload: {payload}");
            await _logger.InformationAsync($"PhonePe webhook signature: {signature}");

            //TODO - Need to fix it later
            ////// Verify webhook signature
            ////if (!_phonePeService.VerifyWebhookSignature(payload, signature))
            ////{
            ////    await _logger.ErrorAsync("PhonePe webhook signature verification failed");
            ////    return BadRequest("Invalid signature");
            ////}

            // Configure JsonSerializerOptions for case-insensitive deserialization
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

                // Parse the webhook payload
                var webhookData = JsonSerializer.Deserialize<PhonePeWebhookPayload>(payload, options);

            if (webhookData?.Payload == null)
            {
                await _logger.ErrorAsync("PhonePe webhook: Invalid payload structure");
                return BadRequest("Invalid payload");
            }

            // Extract order ID from merchantOrderId (format: MT{OrderId}_{Timestamp})
            var orderId = ExtractOrderIdFromMerchantOrderId(webhookData.Payload.MerchantOrderId);
            if (orderId <= 0)
            {
                await _logger.ErrorAsync($"PhonePe webhook: Invalid merchantOrderId format: {webhookData.Payload.MerchantOrderId}");
                return BadRequest("Invalid merchant order ID");
            }

            // Get the order
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
            {
                await _logger.ErrorAsync($"PhonePe webhook: Order not found. OrderId: {orderId}");
                return NotFound("Order not found");
            }

            // Process based on event type and state
            if (webhookData.Event == "pg.order.completed" && webhookData.Payload.State == "COMPLETED")
            {
                await ProcessCompletedPayment(order, webhookData.Payload);
            }
            else if (webhookData.Payload.State == "FAILED")
            {
                await ProcessFailedPayment(order, webhookData.Payload);
            }
            else if (webhookData.Payload.State == "PENDING")
            {
                await ProcessPendingPayment(order, webhookData.Payload);
            }
            else
            {
                await _logger.WarningAsync($"PhonePe webhook: Unhandled state '{webhookData.Payload.State}' for order {orderId}");
            }

            return Ok();
        }
        catch (JsonException jsonEx)
        {
            await _logger.ErrorAsync($"PhonePe webhook JSON parsing error: {jsonEx.Message}", jsonEx);
            return BadRequest("Invalid JSON payload");
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe webhook error: {ex.Message}", ex);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> RefundWebhook()
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync();

            var signature = Request.Headers["authorization"].ToString();

            if (!_phonePeService.VerifyWebhookSignature(payload, signature))
            {
                await _logger.ErrorAsync("PhonePe refund webhook signature verification failed");
                return BadRequest("Invalid signature");
            }

            await _logger.InformationAsync($"PhonePe refund webhook received: {payload}");

            return Ok();
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe refund webhook error: {ex.Message}", ex);
            return StatusCode(500);
        }
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Process completed payment
    /// </summary>
    private async Task ProcessCompletedPayment(Order order, WebhookPayload payload)
    {
        try
        {
            // Check if payment details exist
            if (payload.PaymentDetails == null || !payload.PaymentDetails.Any())
            {
                await _logger.WarningAsync($"PhonePe webhook: No payment details for order {order.Id}");
                return;
            }

            var paymentDetail = payload.PaymentDetails.FirstOrDefault(pd => pd.State == "COMPLETED");
            if (paymentDetail == null)
            {
                await _logger.WarningAsync($"PhonePe webhook: No completed payment detail for order {order.Id}");
                return;
            }

            await _logger.InformationAsync($"PhonePe: Processing completed payment for Order #{order.Id}");

            // Update order with transaction details
            order.AuthorizationTransactionId = paymentDetail.TransactionId;
            order.AuthorizationTransactionCode = "PAYMENT_SUCCESS";
            order.AuthorizationTransactionResult = $"Payment completed via {paymentDetail.PaymentMode}";
            
            await _orderService.UpdateOrderAsync(order);

            // Add order note with payment details
            var splitInstrument = paymentDetail.SplitInstruments?.FirstOrDefault();
            var instrumentInfo = splitInstrument?.Instrument != null
                ? $" - {splitInstrument.Instrument.Type} ({splitInstrument.Instrument.MaskedCardNumber})"
                : "";

            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = order.Id,
                Note = $"PhonePe payment completed. Transaction ID: {paymentDetail.TransactionId}, Mode: {paymentDetail.PaymentMode}{instrumentInfo}",
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            // Mark order as paid if still pending
            if (order.PaymentStatus == PaymentStatus.Pending)
            {
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
                await _logger.InformationAsync($"Order #{order.Id} marked as paid via webhook");
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe webhook: Error processing completed payment for order {order.Id}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Process failed payment
    /// </summary>
    private async Task ProcessFailedPayment(Order order, WebhookPayload payload)
    {
        try
        {
            await _logger.WarningAsync($"PhonePe: Payment failed for Order #{order.Id}");

            // Update order with failure details
            order.AuthorizationTransactionCode = "PAYMENT_FAILED";
            order.AuthorizationTransactionResult = "Payment failed";
            await _orderService.UpdateOrderAsync(order);

            // Add order note
            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = order.Id,
                Note = $"PhonePe payment failed. Order ID: {payload.OrderId}, State: {payload.State}",
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            // Optionally cancel the order
            if (order.OrderStatus == OrderStatus.Pending)
            {
                await _orderProcessingService.CancelOrderAsync(order, true);
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe webhook: Error processing failed payment for order {order.Id}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Process pending payment
    /// </summary>
    private async Task ProcessPendingPayment(Order order, WebhookPayload payload)
    {
        try
        {
            await _logger.InformationAsync($"PhonePe: Payment pending for Order #{order.Id}");

            // Add order note
            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = order.Id,
                Note = $"PhonePe payment pending. Order ID: {payload.OrderId}, State: {payload.State}",
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"PhonePe webhook: Error processing pending payment for order {order.Id}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extract order ID from merchant order ID
    /// </summary>
    private int ExtractOrderIdFromMerchantOrderId(string merchantOrderId)
    {
        try
        {
            // Format: MT{OrderId}_{Timestamp}
            if (string.IsNullOrEmpty(merchantOrderId) || !merchantOrderId.StartsWith("MT"))
                return 0;

            var parts = merchantOrderId.Substring(2).Split('_');
            if (parts.Length < 2)
                return 0;

            return int.TryParse(parts[0], out var orderId) ? orderId : 0;
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region Models

    private class PhonePeWebhookPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("event")]
        public string Event { get; set; }

        [JsonPropertyName("payload")]
        public WebhookPayload Payload { get; set; }
    }

    private class WebhookPayload
    {
        [JsonPropertyName("merchantId")]
        public string MerchantId { get; set; }

        [JsonPropertyName("merchantOrderId")]
        public string MerchantOrderId { get; set; }

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("expireAt")]
        public long ExpireAt { get; set; }

        [JsonPropertyName("metaInfo")]
        public Dictionary<string, string> MetaInfo { get; set; }

        [JsonPropertyName("paymentDetails")]
        public List<PaymentDetail> PaymentDetails { get; set; }
    }

    private class PaymentDetail
    {
        [JsonPropertyName("paymentMode")]
        public string PaymentMode { get; set; }

        [JsonPropertyName("transactionId")]
        public string TransactionId { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("splitInstruments")]
        public List<SplitInstrument> SplitInstruments { get; set; }
    }

    private class SplitInstrument
    {
        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("rail")]
        public Rail Rail { get; set; }

        [JsonPropertyName("instrument")]
        public Instrument Instrument { get; set; }
    }

    private class Rail
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("transactionId")]
        public string TransactionId { get; set; }

        [JsonPropertyName("authorizationCode")]
        public string AuthorizationCode { get; set; }

        [JsonPropertyName("serviceTransactionId")]
        public string ServiceTransactionId { get; set; }
    }

    private class Instrument
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("bankTransactionId")]
        public string BankTransactionId { get; set; }

        [JsonPropertyName("bankId")]
        public string BankId { get; set; }

        [JsonPropertyName("arn")]
        public string Arn { get; set; }

        [JsonPropertyName("brn")]
        public string Brn { get; set; }

        [JsonPropertyName("geoScope")]
        public string GeoScope { get; set; }

        [JsonPropertyName("cardNetwork")]
        public string CardNetwork { get; set; }

        [JsonPropertyName("maskedCardNumber")]
        public string MaskedCardNumber { get; set; }
    }

    #endregion
}