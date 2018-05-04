using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jankiele
{
    public class JankielPerson
    {
        private Tuple<double, double> coords;
        private JankielPerson[] neighbours;
        private Dictionary<int, JankielPerson> idToJankiel;
        private ConcurrentQueue<Message> messages = new ConcurrentQueue<Message>();
        private Semaphore messageArrived = new Semaphore(0, int.MaxValue);
        private int id;

        public JankielPerson(Tuple<double, double> coords, int id)
        {
            this.coords = coords;
            this.id = id;
        }

        public void AddNeighbours(IEnumerable<JankielPerson> neighbours)
        {
            this.neighbours = neighbours.ToArray();
            idToJankiel = neighbours.ToDictionary(neighbour => neighbour.GetID());
        }

        public int GetID() => id;
        internal Tuple<double, double> GetCoordinates() => coords;

        public void SendMessage(Message message)
        {
            messages.Enqueue(message);
            messageArrived.Release(1);
        }

        internal void Launch()
        {
            // check out rounds
            // send message
            for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
            {
                Console.WriteLine($"{id} sends a message to unknown {neighbourIndex}");
                neighbours[neighbourIndex].SendMessage(new Message(id, MessageType.IDBroadcast, neighbourIndex));
            }

            for (int expectedMessages = neighbours.Length; expectedMessages > 0; expectedMessages--)
            {
                Console.WriteLine($"{id} waits for incoming message...");
                messageArrived.WaitOne();
                var success = messages.TryDequeue(out Message message);
                if (!success)
                    throw new Exception("oops... failure during dequeuing");
                Console.WriteLine($"{id} unboxing message from {message.GetSenderID()}");
                // read message

                //if (message.GetMessageType() == MessageType.IDBroadcast)
                //{
                //    neighbourIDs[(int)message.GetPersonalObject()] = message.GetSenderID();
                //}
            }

            // if won, then sing for 2 seconds
            // then notify neighbours

        }
    }
}
