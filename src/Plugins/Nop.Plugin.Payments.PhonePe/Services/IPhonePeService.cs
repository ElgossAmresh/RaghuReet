using Nop.Core.Domain.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PhonePe.Services;

/// <summary>
/// Represents PhonePe service interface
/// </summary>
public interface IPhonePeService
{
    /// <summary>
    /// Initiate payment with PhonePe
    /// </summary>
    /// <param name="order">Order</param>
    /// <returns>Redirect URL</returns>
    Task<string> InitiatePaymentAsync(Order order);

    /// <summary>
    /// Verify payment status
    /// </summary>
    /// <param name="transactionId">Transaction ID</param>
    /// <returns>Payment verification result</returns>
    Task<(bool success, string message)> VerifyPaymentAsync(string transactionId);

    /// <summary>
    /// Refund payment
    /// </summary>
    /// <param name="refundPaymentRequest">Refund payment request</param>
    /// <returns>Refund result</returns>
    Task<RefundPaymentResult> RefundPaymentAsync(RefundPaymentRequest refundPaymentRequest);

    /// <summary>
    /// Verify webhook signature
    /// </summary>
    /// <param name="payload">Payload</param>
    /// <param name="signature">Signature</param>
    /// <returns>True if valid</returns>
    bool VerifyWebhookSignature(string payload, string signature);

    /// <summary>
    /// Validates the PhonePe webhook Authorization header.
    /// </summary>
    /// <param name="receivedHeader">The value of the 'Authorization' header from the request.</param>
    /// <param name="username">The username you configured in the PhonePe Dashboard.</param>
    /// <param name="password">The password you configured in the PhonePe Dashboard.</param>
    /// <returns>True if the signature is valid; otherwise, false.</returns>
    bool ValidateCallback(string receivedHeader, string username, string password);
}