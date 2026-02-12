using GenZStore.Data;
using GenZStore.DTOs;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Text;

namespace GenZStore.Services
{
    public class BakongService
    {
        private readonly AppDbContext _context;

        public BakongService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentResponseDto?> GenerateKhqrAsync(Guid orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return null;

            // ---------------------------------------------------------
            // 1. SETUP YOUR PERSONAL INFO
            // ---------------------------------------------------------
            var abaAccountId = "2819314";  // 👈 YOUR ID HERE
            var merchantName = "GenZStore"; // Your Name or Shop Name
            var city = "Phnom Penh";
            var currency = "840"; // 840 = USD, 116 = KHR
            var amount = order.TotalAmount.ToString("F2"); // Format: 10.50

            // ---------------------------------------------------------
            // 2. BUILD THE RAW STRING (EMVCo Standard)
            // ---------------------------------------------------------
            // This is the structure Bakong/ABA expects.
            // We use StringBuilder to tag pieces together safely.

            var sb = new StringBuilder();

            // 00: Payload Format (01)
            sb.Append("000201");

            // 01: Point of Initiation (12 = Dynamic/One-time)
            sb.Append("010212");

            // 29: Merchant Account Info (Bakong System)
            // We construct the sub-content first to get its length
            var merchantInfo = $"0006bakong01{abaAccountId.Length:D2}{abaAccountId}";
            sb.Append($"29{merchantInfo.Length:D2}{merchantInfo}");

            // 52: Merchant Category Code (General)
            sb.Append("52045999");

            // 53: Currency (USD)
            sb.Append($"5303{currency}");

            // 54: Amount
            sb.Append($"54{amount.Length:D2}{amount}");

            // 58: Country
            sb.Append("5802KH");

            // 59: Merchant Name
            sb.Append($"59{merchantName.Length:D2}{merchantName}");

            // 60: City
            sb.Append($"60{city.Length:D2}{city}");

            // 62: Additional Data (Order ID)
            // Truncate Order ID to fit cleanly if needed
            var refId = orderId.ToString("N").Substring(0, 20);
            var additionalData = $"01{refId.Length:D2}{refId}";
            sb.Append($"62{additionalData.Length:D2}{additionalData}");

            // 63: CRC (Checksum) Placeholder
            sb.Append("6304");

            // ---------------------------------------------------------
            // 3. CALCULATE CHECKSUM (The Magic Math)
            // ---------------------------------------------------------
            // The bank app checks this to prove the QR is valid.
            var crc = CalculateCrc16(sb.ToString());
            var fullKhqr = sb.ToString() + crc;

            // ---------------------------------------------------------
            // 4. GENERATE IMAGE
            // ---------------------------------------------------------
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(fullKhqr, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrBytes = qrCode.GetGraphic(20);
            var base64Image = Convert.ToBase64String(qrBytes);

            return new PaymentResponseDto
            {
                OrderId = order.Id,
                KhqrString = fullKhqr,
                QrImageBase64 = $"data:image/png;base64,{base64Image}",
                Amount = order.TotalAmount,
                Currency = "USD",
                Md5Hash = Guid.NewGuid()
            };
        }

        // 🧠 STANDARD CRC16-CCITT ALGORITHM (Do not change this!)
        private static string CalculateCrc16(string data)
        {
            // Initial value for CRC-CCITT (0xFFFF)
            ushort crc = 0xFFFF;
            // Polynomial (0x1021)
            ushort polynomial = 0x1021;

            byte[] bytes = Encoding.UTF8.GetBytes(data);

            foreach (byte b in bytes)
            {
                for (int i = 0; i < 8; i++)
                {
                    bool bit = ((b >> (7 - i) & 1) == 1);
                    bool c15 = ((crc >> 15 & 1) == 1);
                    crc <<= 1;
                    if (c15 ^ bit) crc ^= polynomial;
                }
            }

            // Return as 4-character Hex String (e.g., "A1B2")
            return (crc & 0xFFFF).ToString("X4");
        }
    }
}