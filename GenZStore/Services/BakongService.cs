using GenZStore.Data;
using GenZStore.DTOs;
using kh.gov.nbc.bakong_khqr;
using kh.gov.nbc.bakong_khqr.model;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Reflection; // Required for the fix

namespace GenZStore.Services
{
    public class BakongService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private const string BAKONG_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJkYXRhIjp7ImlkIjoiNzZmYjhlNjZlZGExNGI4YyJ9LCJpYXQiOjE3NzExMzA1MzIsImV4cCI6MTc3ODkwNjUzMn0.mTSy3SpujW1YI1h7vZhdxJvkt0FYpHvdvXRxGNNAUTI";
        private const string BAKONG_API_URL = "https://api-bakong.nbc.gov.kh/v1";

        public BakongService(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BAKONG_TOKEN);
        }

        public async Task<PaymentResponseDto?> GenerateKhqrAsync(Guid orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) throw new Exception("Order ID not found in database.");

            // 1. Setup Merchant Info
            var merchantInfo = new MerchantInfo
            {
                BakongAccountID = "kakada_ung@bkrt",
                MerchantName = "GenZStore",
                MerchantID = "123456",
                AcquiringBank = "Bakong",
                MerchantCity = "Phnom Penh",
                Currency = KHQRCurrency.USD,
                Amount = (double)order.TotalAmount,
                BillNumber = orderId.ToString("N").Substring(0, 20),
                StoreLabel = "GenZStore",
                TerminalLabel = "Cashier-01",
                ExpirationTimestamp = DateTimeOffset.Now.AddMinutes(15).ToUnixTimeMilliseconds()
            };

            // 2. Generate KHQR String via SDK
            var result = BakongKHQR.GenerateMerchant(merchantInfo);

            if (result.Status.Code != 0)
            {
                throw new Exception($"Bakong SDK Error: {result.Status.Message} (Code: {result.Status.Code})");
            }

            // 3. Extract QR String SAFELY (Fixes CS1061)
            // We use a helper function to find the property because SDK names vary (QR, QrCode, qr, etc.)
            string fullKhqr = GetQrStringFromData(result.Data);

            if (string.IsNullOrEmpty(fullKhqr))
            {
                throw new Exception("Bakong SDK returned success but QR string was empty.");
            }

            // 4. Compute MD5
            string md5Hash = ComputeMd5(fullKhqr);

            // 5. Generate QR Image
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(fullKhqr, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrBytes = qrCode.GetGraphic(20);

            return new PaymentResponseDto
            {
                OrderId = order.Id,
                KhqrString = fullKhqr,
                QrImageBase64 = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}",
                Amount = order.TotalAmount,
                Currency = "USD",
                Md5Hash = md5Hash,
                // AppDeeplink = await GenerateDeepLinkAsync(fullKhqr) // Optional: Uncomment if you need deep links
            };
        }

        public async Task<bool> CheckPaymentStatusAsync(string md5Hash)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BAKONG_API_URL}/check_transaction_by_md5", new { md5 = md5Hash });

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<BakongStatusResponse>();
                return data?.ResponseCode == 0;
            }
            return false;
        }

        private string ComputeMd5(string input)
        {
            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = md5.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // ✅ THE FIX: Helper to safely find the QR property
        private string GetQrStringFromData(object dataObj)
        {
            if (dataObj == null) return null;

            var type = dataObj.GetType();
            // Try all common naming conventions for the QR property
            var prop = type.GetProperty("QrCode")
                    ?? type.GetProperty("QR")
                    ?? type.GetProperty("QRCode")
                    ?? type.GetProperty("qr")
                    ?? type.GetProperty("Khqr");

            return prop?.GetValue(dataObj)?.ToString();
        }
    }

    public class BakongStatusResponse
    {
        public int ResponseCode { get; set; }
        public string ResponseMessage { get; set; }
    }
}