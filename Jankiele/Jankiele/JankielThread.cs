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
        private const int playingPeriodDuration = 5000;
        private Tuple<double, double> coords;
        private JankielPerson[] neighbours;
        private Dictionary<int, JankielPerson> idToJankiel;
        private Dictionary<int, JankielState> idToNeighbourState;
        private Dictionary<int, int> idToIndex;
        private Dictionary<int, int> indexToID;
        private ConcurrentQueue<Message> messages = new ConcurrentQueue<Message>();
        private ISet<Message> archivedMessages = new HashSet<Message>();
        private Semaphore messageArrived = new Semaphore(0, int.MaxValue);
        private int myID;
        private Random random;

        private void SendToID(int id, Message message) => neighbours[idToIndex[id]].SendMessage(message);

        private object messageQueuecreation = new object();

        public JankielPerson(Tuple<double, double> coords, int myID, int seed)
        {
            this.coords = coords;
            this.myID = myID;
            random = new Random(seed);
        }
        private string DescribeOneself() => $"[{coords.Item1}, {coords.Item2}]";

        public void AddNeighbours(IEnumerable<JankielPerson> neighbours)
        {
            var n = neighbours.Count();
            this.neighbours = neighbours.ToArray();
            idToJankiel = neighbours.ToDictionary(neighbour => neighbour.GetID());
            idToNeighbourState = new Dictionary<int, JankielState>();
            foreach (var nid in neighbours.Select(neighbour => neighbour.GetID()))
            {
                idToNeighbourState[nid] = JankielState.receivingIDs;
            }
            idToIndex = new Dictionary<int, int>(n);
            indexToID = new Dictionary<int, int>(n);
        }

        private int GetID() => myID;
        internal Tuple<double, double> GetCoordinates() => coords;
        public void SendMessage(Message message)
        {
            messages.Enqueue(message);
            messageArrived.Release(1);
        }

        public void Launch()
        {
            // this solution has been designed to be as self-explanatory as possible
            // especially if variables are named verbosily and LINQ technology is used
            var myState = JankielState.receivingIDs;
            var defaultTimeout = TimeSpan.FromMilliseconds(500);
            var neighboursThatArentDoneYet = neighbours.Length;
            var myStage = 0;
            var myColor = 0;
            void displayStatus(string comment = "")
            {
                Console.WriteLine($"Color {myColor}, stage {myStage}, {DescribeOneself()} state=[{myState.ToString()}]" + comment);
            }
            void receiveMessageWithin(
                TimeSpan timeout,
                Func<Message, InboxAction> consumerIsFinished)
            {
                var toDelete = new List<Message>();
                var stop = false;
                foreach (var archivedMessage in archivedMessages)
                {
                    switch (consumerIsFinished(archivedMessage))
                    {
                        case InboxAction.continueNext:
                            break;
                        case InboxAction.deleteAndContinueNext:
                            toDelete.Add(archivedMessage);
                            break;
                        case InboxAction.deleteAndStop:
                            toDelete.Add(archivedMessage);
                            stop = true;
                            break;
                        case InboxAction.justStop:
                            stop = true;
                            break;
                    }
                    if (stop)
                        break;
                }
                while (!stop)
                {
                    var success = timeout == TimeSpan.MaxValue
                        ? messageArrived.WaitOne()
                        : messageArrived.WaitOne(timeout);
                    if (!success)
                    {
                        Console.WriteLine($"Time has exipred for {DescribeOneself()}");
                        return;
                    }
                    success &= messages.TryDequeue(out Message message);
                    if (!success)
                        throw new Exception("Signaled a message, yet none received!");
                    archivedMessages.Add(message);
                    switch (consumerIsFinished(message))
                    {
                        case InboxAction.continueNext:
                            break;
                        case InboxAction.deleteAndContinueNext:
                            toDelete.Add(message);
                            break;
                        case InboxAction.deleteAndStop:
                            toDelete.Add(message);
                            stop = true;
                            break;
                        case InboxAction.justStop:
                            stop = true;
                            break;
                    }
                }
                foreach (var messageToDelete in toDelete)
                {
                    archivedMessages.Remove(messageToDelete);
                }
            }

            for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
            {
                neighbours[neighbourIndex].SendMessage(
                    new Message(myID, MessageType.IDBroadcast, neighbourIndex.ToString(), myStage, myColor)
                    );
            }
            var stillCompetingNeighbours = new HashSet<int>();
            var neighboursThatReturnedMessage = new HashSet<int>();
            receiveMessageWithin(defaultTimeout, message =>
            {
                if (message.Color == myColor && message.Stage == myStage)
                    switch (message.MessageType)
                    {
                        case MessageType.IDBroadcast:
                            stillCompetingNeighbours.Add(message.SenderID);
                            foreach (var neighbour in neighbours.Where(neighbour => neighbour.GetID() == message.SenderID))
                            {
                                neighbour.SendMessage(
                                    new Message(myID, MessageType.IDReturn, message.Contents, myStage, myColor)
                                    );
                            }
                            return InboxAction.deleteAndContinueNext;
                        case MessageType.IDReturn:
                            neighboursThatReturnedMessage.Add(message.SenderID);
                            idToIndex[message.SenderID] = message.ContentsAsInt;
                            indexToID[message.ContentsAsInt] = message.SenderID;
                            return InboxAction.deleteAndContinueNext;
                    }
                return InboxAction.continueNext;
            });

            if (stillCompetingNeighbours.SetEquals(neighboursThatReturnedMessage))
            {
                myState = JankielState.doneExchangingInts;
                displayStatus($" exchanged IDs fine {stillCompetingNeighbours.Count}");
            }
            else
            {
                displayStatus(" made a mistake during ID exchange");
                return;
            }
            // received everyone's ID
            myState = JankielState.doneIDsReadyToExchangeInts;
            var superiorNeighbours = new HashSet<int>();
            var allStagesElected = new HashSet<int>();

            for (myColor = 0; ; myColor++)
            {
                displayStatus(" processes next color");
                if (myState == JankielState.loser)
                {
                    int participantsLeft = stillCompetingNeighbours.Count;
                    displayStatus($" waits for {participantsLeft} messages");

                    receiveMessageWithin(TimeSpan.MaxValue, message =>
                    {
                        if (message.Stage == -1 && message.Color == myColor)
                            switch (message.MessageType)
                            {
                                case MessageType.ParticipatingInThisColor:
                                    participantsLeft--;
                                    displayStatus($" waits for {participantsLeft} messages received participant {message.SenderID}");

                                    if (participantsLeft == 0)
                                        return InboxAction.deleteAndStop;
                                    else
                                        return InboxAction.deleteAndContinueNext;
                                case MessageType.FinishedPlayingCymbals:
                                    stillCompetingNeighbours.Remove(message.SenderID);
                                    participantsLeft--;
                                    displayStatus($" waits for {participantsLeft} messages received finished playing by {message.SenderID}");

                                    if (participantsLeft == 0)
                                        return InboxAction.deleteAndStop;
                                    else
                                        return InboxAction.deleteAndContinueNext;
                            }
                        if (message.Color < myColor)
                            return InboxAction.deleteAndContinueNext;
                        else
                            return InboxAction.continueNext;
                    });

                }

                var quitStage = false;
                var previouslyParticipatingThisStage = new HashSet<int>(stillCompetingNeighbours);
                var electedDuringThisStage = new HashSet<int>();
                var electedDuringLastStage = new HashSet<int>();
                for (myStage = 0; !quitStage; myStage++)
                {
                    var participatingThisStage = new HashSet<int>(previouslyParticipatingThisStage.Except(electedDuringLastStage));
                    previouslyParticipatingThisStage = participatingThisStage;
                    var notParticipating = new HashSet<int>();
                    superiorNeighbours = new HashSet<int>();

                    if (participatingThisStage.Count > 0)
                    {
                        var myInt = random.Next();
                        displayStatus($" is preparing for Int exchange... chose {myInt}");
                        foreach (var id in participatingThisStage)
                        {
                            SendToID(
                                id,
                                new Message(myID, MessageType.MyIntBroadcast, myInt.ToString(), myStage, myColor)
                                );
                        }

                        int receivedInts = 0;
                        receiveMessageWithin(TimeSpan.MaxValue, message =>
                        {
                            if (message.Stage == myStage && message.Color == myColor)
                                switch (message.MessageType)
                                {
                                    case MessageType.Dunno:
                                        displayStatus($" received dunno from {message.SenderID}");
                                        return InboxAction.deleteAndContinueNext;
                                    case MessageType.NotParticipatingInThisStage:
                                        displayStatus($" received not participating from {message.SenderID}");
                                        notParticipating.Add(message.SenderID);
                                        participatingThisStage.Remove(message.SenderID);
                                        if (participatingThisStage.Count == 0)
                                            return InboxAction.deleteAndStop;
                                        else
                                            return InboxAction.deleteAndContinueNext;
                                    case MessageType.MyIntBroadcast:
                                        if (message.ContentsAsInt > myInt)
                                        {
                                            superiorNeighbours.Add(message.SenderID);
                                        }
                                        ++receivedInts;
                                        if (receivedInts == participatingThisStage.Count)
                                            return InboxAction.deleteAndStop;
                                        else
                                            return InboxAction.deleteAndContinueNext;
                                }
                            return InboxAction.continueNext;
                        });
                    }

                    //LubyMIS algorithm
                    if (superiorNeighbours.Count == 0)
                    {
                        myState = JankielState.elected;
                        foreach (var id in participatingThisStage)
                        {
                            SendToID(
                                id,
                                new Message(myID, MessageType.NowElected, string.Empty, myStage, myColor)
                                );
                            SendToID(
                                id,
                                new Message(myID, MessageType.NotParticipatingInThisStage, string.Empty, myStage + 1, myColor)
                                );
                        }

                        displayStatus(" is now crashing cymbals... actually beeping");
                        Thread.Sleep(playingPeriodDuration);
                        Console.Beep();
                        myState = JankielState.donePlaying;
                        foreach (var id in stillCompetingNeighbours.Except(allStagesElected))
                        {
                            SendToID(
                                id,
                                new Message(myID, MessageType.FinishedPlayingCymbals, string.Empty, -1, myColor + 1)
                                );
                        }
                        displayStatus(" has done his job well");
                        return;
                    }
                    else
                    {
                        // someone else is superior, but is he the chosen one?
                        receiveMessageWithin(defaultTimeout, message =>
                        {
                            if (message.Stage == myStage && message.Color == myColor)
                                switch (message.MessageType)
                                {
                                    case MessageType.NowElected:
                                        electedDuringThisStage.Add(message.SenderID);
                                        return InboxAction.deleteAndStop;
                                }
                            return InboxAction.continueNext;
                        });

                        if (electedDuringThisStage.Count > 0)
                        {
                            // I'm a loser, superiorneighbours > 0, at least one of them elected
                            myState = JankielState.loser;
                            displayStatus();
                            foreach (var id in participatingThisStage.Except(electedDuringThisStage))
                            {
                                SendToID(id, new Message(myID, MessageType.NotParticipatingInThisStage, string.Empty, myStage + 1, myColor));
                                SendToID(id, new Message(myID, MessageType.ParticipatingInThisColor, string.Empty, -1, myColor + 1));
                            }
                            // waiting for next color
                            quitStage = true;
                        }
                        else
                        {
                            // superiorneighbours > 0, none elected
                            myState = JankielState.dunno;
                            displayStatus();

                            foreach (var id in participatingThisStage)
                            {
                                SendToID(id, new Message(myID, MessageType.Dunno, string.Empty, myStage + 1, myColor));
                            }
                        }
                    }

                }
                electedDuringLastStage = electedDuringThisStage;
                allStagesElected.UnionWith(electedDuringThisStage);
            }
        }
    }
}
