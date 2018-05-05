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
        private const int sleepTimeMS = 2000;
        private Tuple<double, double> coords;
        private JankielPerson[] neighbours;
        private Dictionary<int, JankielPerson> idToJankiel;
        private bool[] isActive;
        private Dictionary<int, int> idToIndex;
        private Dictionary<int, int> indexToID;
        private ConcurrentQueue<Message> messages = new ConcurrentQueue<Message>();
        private Semaphore messageArrived = new Semaphore(0, int.MaxValue);
        private int myID;
        private Random random;

        public JankielPerson(Tuple<double, double> coords, int myID, int seed)
        {
            this.coords = coords;
            this.myID = myID;
            random = new Random(seed);
        }
        private string DescribeOneself() => $"id{myID} [{coords.Item1}, {coords.Item2}]";

        public void AddNeighbours(IEnumerable<JankielPerson> neighbours)
        {
            var n = neighbours.Count();
            this.neighbours = neighbours.ToArray();
            idToJankiel = neighbours.ToDictionary(neighbour => neighbour.GetID());
            isActive = new bool[n];
            for (int i = 0; i < isActive.Length; i++)
            {
                isActive[i] = true;
            }
            idToIndex = new Dictionary<int, int>(n);
            indexToID = new Dictionary<int, int>(n);
        }

        public int GetID() => myID;
        internal Tuple<double, double> GetCoordinates() => coords;

        public void SendMessage(Message message)
        {
            messages.Enqueue(message);
            messageArrived.Release(1);
        }

        public void Launch()
        {
            // check out rounds
            // send message
            var state = JankielState.receivingIDs;
            var inIndependentState = false;
            var expectedNeighbours = neighbours.Length;

            void sendSameMessageToAll(MessageType type, string messageContents)
            {
                foreach (var neighbour in neighbours)
                    neighbour.SendMessage(new Message(myID, type, messageContents));
            }
            void receiveEveryonesMessage(Action<Message> action, int expectedNumOfMessages = -1)
            {
                if (expectedNumOfMessages < 0)
                {
                    expectedNumOfMessages = expectedNeighbours;
                }
                for (int expectedMessages = expectedNumOfMessages; expectedMessages > 0; expectedMessages--)
                {
                    //Console.WriteLine($"{DescribeOneself()} waits for incoming message...");
                    messageArrived.WaitOne();
                    var success = messages.TryDequeue(out Message message);
                    if (!success)
                        throw new Exception("oops... failure during dequeuing");
                    //Console.WriteLine($"{DescribeOneself()} unboxing message from {message.GetSenderID()}");
                    // read message
                    action.Invoke(message);
                }
            }

            for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
            {
                //Console.WriteLine($"{DescribeOneself()} sends a messages to unknown index {neighbourIndex}");
                neighbours[neighbourIndex].SendMessage(new Message(myID, MessageType.IDBroadcast, neighbourIndex.ToString()));
            }
            receiveEveryonesMessage(message =>
            {
                if (message.GetMessageType() == MessageType.IDBroadcast)
                {
                    idToIndex[message.GetSenderID()] = int.Parse(message.GetContents());
                    indexToID[int.Parse(message.GetContents())] = message.GetSenderID();
                }
            });

            // received everyone's ID
            var neighbourUpperBoundLog = Math.Log(4 * 7);
            var carefullyChosenConstant = 1;
            var upperBoundOnNumberOfNodes = 6;
            bool generateRandomBoolean(int param)
            {
                return random.NextDouble() * Math.Pow(2, param - neighbourUpperBoundLog) < 1;
            }

            // FIRST STAGE

            var inMaximalSet = InMIS.dunno;
            //for (int i = 0, maxI = (int)Math.Ceiling(neighbourUpperBoundLog); i < maxI; i++)
            //{
            //    for (int j = 0, maxJ = (int)Math.Ceiling(carefullyChosenConstant * Math.Log(upperBoundOnNumberOfNodes)); j < maxJ; j++)
            //    {
            //        if (inMaximalSet == InMIS.yes || inMaximalSet == InMIS.no)
            //        {
            //            sendSameMessageToAll(MessageType.NullMessage, null);
            //            sendSameMessageToAll(MessageType.NullMessage, null);
            //            continue;
            //        }
            //        var selected = false;
            //        var receivedB = false;

            //        var success = generateRandomBoolean(i);
            //        if (success)
            //        {
            //            selected = true;
            //            sendSameMessageToAll(MessageType.BMessageBroadcast, "1");
            //        }
            //        else
            //        {
            //            sendSameMessageToAll(MessageType.NullMessage, null);
            //        }
            //        receiveEveryonesMessage(message =>
            //        {
            //            if (message.GetMessageType() == MessageType.BMessageBroadcast
            //                && message.GetContents() == "1")
            //            {
            //                selected = false;
            //                receivedB = true;
            //            }
            //        },
            //        i == 0 && j == 0 ? expectedNeighbours : 2 * expectedNeighbours);

            //        if (selected)
            //        {
            //            sendSameMessageToAll(MessageType.BMessageBroadcast, "1");
            //            inMaximalSet = InMIS.yes;
            //            break;
            //        }
            //        else if (receivedB)
            //        {
            //            sendSameMessageToAll(MessageType.NullMessage, null);
            //            inMaximalSet = InMIS.no;
            //            break;
            //        }
            //    }
            //}

            Console.WriteLine($"    {DescribeOneself()} is selected? " + (inMaximalSet.ToString()));
            //inIndependentState = idToJankiel.Keys.All(id => id < myID);

            //if (inIndependentState)
            //{
            //    state = JankielState.elected;
            //    // if won, notify neighbours and then sing for 2 seconds
            //    for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
            //    {
            //        if (!isActive[neighbourIndex])
            //            continue;

            //        Console.WriteLine($"{myID} sends a message to neighbour of id {indexToID[neighbourIndex]}");
            //        neighbours[neighbourIndex].SendMessage(new Message(myID, MessageType.NowElected, neighbourIndex.ToString()));
            //    }
            //    Thread.Sleep(sleepTimeMS);
            //}
            //else
            //{
            //    state = JankielState.dunno;
            //    // notify neighbour about the unknown
            //    sendSameMessageToAll(MessageType.NullMessage, string.Empty);
            //    //receive messages
            //}

            //// then notify neighbours

        }
    }
}
