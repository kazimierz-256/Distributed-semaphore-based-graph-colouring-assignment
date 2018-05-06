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
        private int stage;
        private int color;
        private InboxType inboxType;

        public Message(int senderID, MessageType messageType, string messageContents, int stage, int color, InboxType inboxType = InboxType.nothingSpecial)
        {
            this.senderID = senderID;
            this.messageType = messageType;
            this.messageContents = messageContents;
            this.stage = stage;
            this.color = color;
            this.inboxType = inboxType;
        }
        public int SenderID => senderID;
        public MessageType MessageType => messageType;
        public int ContentsAsInt => int.Parse(messageContents);
        public string Contents => messageContents;
        public int Stage => stage;
        public int Color => color;
        public InboxType InboxType => InboxType;
    }
}
