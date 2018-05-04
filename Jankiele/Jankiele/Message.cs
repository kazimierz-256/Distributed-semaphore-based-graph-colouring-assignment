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
        private object personalObject;

        public Message(int senderID, MessageType messageType, object personalObject)
        {
            this.senderID = senderID;
            this.messageType = messageType;
            this.personalObject = personalObject;
        }
        public int GetSenderID() => senderID;
        public MessageType GetMessageType() => messageType;
        public object GetPersonalObject() => personalObject;
    }
}
