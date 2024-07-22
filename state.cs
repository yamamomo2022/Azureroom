using System;
using System.Collections.Generic;

namespace EchoBot
{
    public class DialogueRecord
    {
        public string Timestamp { get; set; }  // Use DateTime for accurate timestamps
        public string ChannelId { get; set; }
        public List<genAIMessage> Messages { get; set; }  // Consistent casing for clarity
    }

    public class genAIMessage  // Consistent casing for clarity
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class UserProfile
    {
        public string Name { get; set; }
    }
}
