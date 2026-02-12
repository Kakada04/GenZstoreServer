namespace GenZStore.Models
{
    public class Transaction
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order Order { get; set; }

        public string PaymentMethod { get; set; } // Bakong, ABA, AC
        public decimal Amount { get; set; }
        public string Status { get; set; } // Success, Failed
        public DateTime TransactionDate { get; set; }
        public string ReferenceCode { get; set; }
    }
}
