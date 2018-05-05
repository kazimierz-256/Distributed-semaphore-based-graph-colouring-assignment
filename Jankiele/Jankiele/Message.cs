using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jankiele
{
    public class Message
    {
        private int senderID;
        private MessageType messageType;
        private string messageContents;

        public Message(int senderID, MessageType messageType, string messageContents)
        {
            this.senderID = senderID;
            this.messageType = messageType;
            this.messageContents = messageContents;
        }
        public int GetSenderID() => senderID;
        public MessageType GetMessageType() => messageType;
        public string GetContents() => messageContents;
    }
}
