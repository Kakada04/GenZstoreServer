namespace GenZStore.Models
{
    public class ChatLog
    {
        public Guid Id { get; set; }
        public string UserQuestion { get; set; }
        public string AiAnswer { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
