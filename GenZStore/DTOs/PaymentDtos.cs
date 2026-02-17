namespace GenZStore.DTOs
{
    public class PaymentRequestDto
    {
        public Guid OrderId { get; set; }
    }

    public class PaymentResponseDto
    {
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }

        // ABA Specifics
        public string KhqrString { get; set; }      // The raw string for generating QR
        public string AppDeeplink { get; set; }     // Button to open ABA App directly
        public string CheckoutUrl { get; set; }     // URL for hosted checkout page
        public string QrImageBase64 { get; set; }   // (Optional) if returned
        public string Md5Hash { get; set; }              // (Optional) if you want to store a hash for verification
    }

    public class PaymentCallbackDto
    {
        // This is what the Bank sends back to your Webhook
        public string TranId { get; set; }
        public string OrderId { get; set; }
        public string Status { get; set; } // SUCCESS, FAILED
        public string Hash { get; set; }
        public decimal Amount { get; set; }
    }
}