using GenZStore.Data;
using GenZStore.DTOs;
using RestSharp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GenZStore.Services
{
    public class PayWayService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public PayWayService(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<PaymentResponseDto?> CreateKhqrTransactionAsync(Guid orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return null;

            // 1. CONFIG
            var merchantId = _config["PayWay:MerchantId"]?.Trim();
            var apiKey = _config["PayWay:ApiKey"]?.Trim();
            var apiUrl = _config["PayWay:BaseUrl"]?.Trim();

            // 2. DATA
            var reqTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            var tranId = orderId.ToString("N").Substring(0, 20);
            var amount = order.TotalAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var firstName = "GenZ";
            var lastName = "Customer";
            var email = "customer@example.com";
            var phone = "099999999";

            // ✅ return_url MUST be Base64
            var returnUrlRaw = "https://google.com";
            var returnUrl = Convert.ToBase64String(Encoding.UTF8.GetBytes(returnUrlRaw));

            var type = "purchase";
            // ✅ Using the option that gives us the KHQR string and Deeplink
            var paymentOption = "abapay_khqr_deeplink";
            var currency = "USD";
            var shipping = "0.00";

            // Optional Params (Must be defined for Hash)
            var cancelUrl = "";
            var skipSuccessPage = "";
            var continueSuccessUrl = "";
            var returnDeeplink = "";
            var customFields = "";
            var returnParams = "";
            var viewType = "";
            var paymentGate = "";
            var payout = "";
            var additionalParams = "";
            var lifetime = "";
            var googlePayToken = "";

            // 3. ITEMS (Base64 JSON)
            var itemsList = new[] { new { name = "Order " + tranId, quantity = "1", price = amount } };
            var itemsJson = JsonSerializer.Serialize(itemsList);
            var itemsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(itemsJson));

            // 4. HASH (Strict Order)
            var stringToSign =
                reqTime + merchantId + tranId + amount + itemsBase64 + shipping +
                firstName + lastName + email + phone +
                type + paymentOption + returnUrl + cancelUrl + continueSuccessUrl + returnDeeplink +
                currency + customFields + returnParams + payout + lifetime + additionalParams +
                googlePayToken + skipSuccessPage;

            var hash = CreateHmacHash(stringToSign, apiKey);

            // 5. REQUEST
            var client = new RestClient(apiUrl);
            var request = new RestRequest("", Method.Post);
            request.AlwaysMultipartFormData = true;
            request.AddHeader("Accept", "application/json");

            request.AddParameter("req_time", reqTime);
            request.AddParameter("merchant_id", merchantId);
            request.AddParameter("tran_id", tranId);
            request.AddParameter("firstname", firstName);
            request.AddParameter("lastname", lastName);
            request.AddParameter("email", email);
            request.AddParameter("phone", phone);
            request.AddParameter("type", type);
            request.AddParameter("payment_option", paymentOption);
            request.AddParameter("items", itemsBase64);
            request.AddParameter("shipping", shipping);
            request.AddParameter("amount", amount);
            request.AddParameter("currency", currency);
            request.AddParameter("return_url", returnUrl);
            request.AddParameter("cancel_url", cancelUrl);
            request.AddParameter("skip_success_page", skipSuccessPage);
            request.AddParameter("continue_success_url", continueSuccessUrl);
            request.AddParameter("return_deeplink", returnDeeplink);
            request.AddParameter("custom_fields", customFields);
            request.AddParameter("return_params", returnParams);
            request.AddParameter("payout", payout);
            request.AddParameter("additional_params", additionalParams);
            request.AddParameter("lifetime", lifetime);
            request.AddParameter("google_pay_token", googlePayToken);
            request.AddParameter("hash", hash);

            try
            {
                var response = await client.ExecuteAsync(request);

                if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
                {
                    if (response.Content.Trim().StartsWith("<"))
                        throw new Exception($"PayWay returned HTML: {response.Content}");

                    using var doc = JsonDocument.Parse(response.Content);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("status", out var statusObj))
                    {
                        var code = statusObj.GetProperty("code").ToString();

                        if (code == "00")
                        {
                            // 1. Get Values safely
                            string qrString = root.TryGetProperty("qr_string", out var qr) ? qr.GetString() : "";
                            string deeplink = root.TryGetProperty("abapay_deeplink", out var dl) ? dl.GetString() : "";
                            string checkoutUrl = root.TryGetProperty("checkout_qr_url", out var ck) ? ck.GetString() : "";

                            // ✅ FIX: If qrString is empty, EXTRACT it from the deeplink
                            if (string.IsNullOrEmpty(qrString) && !string.IsNullOrEmpty(deeplink))
                            {
                                // Regex to find "?qrcode=..." or "&qrcode=..."
                                var match = System.Text.RegularExpressions.Regex.Match(deeplink, "qrcode=([^&]+)");
                                if (match.Success)
                                {
                                    // Decode it (e.g. changes %40 back to @)
                                    qrString = System.Net.WebUtility.UrlDecode(match.Groups[1].Value);
                                }
                            }

                            return new PaymentResponseDto
                            {
                                OrderId = order.Id,
                                KhqrString = qrString, // Now this will have data!
                                AppDeeplink = deeplink,
                                CheckoutUrl = checkoutUrl,
                                Amount = order.TotalAmount,
                                Currency = "USD"
                            };
                        }
                        else
                        {
                            var msg = statusObj.GetProperty("message").GetString();
                            throw new Exception($"ABA Error (Code {code}): {msg}");
                        }
                    }
                }

                throw new Exception($"PayWay Response Error: {response.Content}");
            }
            catch (Exception ex)
            {
                throw new Exception($"PayWay Exception: {ex.Message}");
            }
        }
        // Helper: Check Status (Polling)
        public async Task<bool> CheckTransactionStatusAsync(string rawOrderId)
        {
            // 1. CONFIG
            var merchantId = _config["PayWay:MerchantId"]?.Trim();
            var apiKey = _config["PayWay:ApiKey"]?.Trim();
            // ✅ UDPATE: Use the new v2 URL
            var apiUrl = "https://checkout-sandbox.payway.com.kh/api/payment-gateway/v1/payments/check-transaction-2";

            // 2. PREPARE DATA
            var reqTime = DateTime.UtcNow.ToString("yyyyMMddHHmmss"); // Use UTC or Local based on ABA requirements (usually purely sequential matters)

            // Clean ID logic (same as before)
            string tranId;
            if (Guid.TryParse(rawOrderId, out Guid guidOrder))
            {
                tranId = guidOrder.ToString("N").Substring(0, 20);
            }
            else
            {
                tranId = rawOrderId.Length > 20 ? rawOrderId.Substring(0, 20) : rawOrderId;
            }

            // 3. HASH CALCULATION
            // Documentation says: req_time + merchant_id + tran_id
            var stringToSign = reqTime + merchantId + tranId;
            var hash = CreateHmacHash(stringToSign, apiKey);

            // 4. REQUEST (Use JSON Body)
            var client = new RestClient(apiUrl);
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            // Create the JSON payload object
            var payload = new
            {
                req_time = reqTime,
                merchant_id = merchantId,
                tran_id = tranId,
                hash = hash
            };

            request.AddJsonBody(payload);

            try
            {
                Console.WriteLine($"[ABA Check] Checking ID: {tranId}...");
                var response = await client.ExecuteAsync(request);

                if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
                {
                    using var doc = JsonDocument.Parse(response.Content);
                    var root = doc.RootElement;

                    // 5. CHECK API STATUS ("status": { "code": "00" })
                    if (root.TryGetProperty("status", out var statusObj))
                    {
                        var apiCode = statusObj.GetProperty("code").ToString();

                        if (apiCode == "00")
                        {
                            // 6. CHECK TRANSACTION STATUS ("data": { "payment_status": "APPROVED" })
                            if (root.TryGetProperty("data", out var dataObj))
                            {
                                var paymentStatus = dataObj.GetProperty("payment_status").GetString();
                                Console.WriteLine($"[ABA Check] Status: {paymentStatus}");

                                // "APPROVED" or "PRE-AUTH" counts as paid
                                if (paymentStatus == "APPROVED" || paymentStatus == "PRE-AUTH")
                                {
                                    return true;
                                }
                            }
                        }
                        else
                        {
                            var msg = statusObj.TryGetProperty("message", out var m) ? m.GetString() : "Unknown";
                            Console.WriteLine($"[ABA Check] API Error: {apiCode} - {msg}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[ABA Check] HTTP Error: {response.StatusCode} - {response.Content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ABA Check] EXCEPTION: {ex.Message}");
            }

            return false;
        }

        private string CreateHmacHash(string data, string key)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }
    }
}