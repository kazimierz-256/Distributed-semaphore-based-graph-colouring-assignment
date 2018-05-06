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
            // check out rounds
            // send message
            var myState = JankielState.receivingIDs;
            var defaultTimeout = TimeSpan.FromMilliseconds(3000);
            var neighboursThatArentDoneYet = neighbours.Length;
            var expectedStages = (int)(2 * Math.Log(6));
            var myStage = 0;
            var myColor = 0;
            void displayStatus(string comment = "")
            {
                Console.WriteLine($"Color {myColor}, stage {myStage}, status of {DescribeOneself()} is {myState.ToString()}" + comment);
            }
            //void sendMessageToNeighboursSuchThat(
            //    Func<JankielState, bool> neighbourPredicate,
            //    Message message)
            //{
            //    for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
            //    {
            //        if (neighbourPredicate == null
            //            || neighbourPredicate(idToNeighbourState[indexToID[neighbourIndex]]))
            //            neighbours[neighbourIndex].SendMessage(message);
            //    }
            //}
            void receiveMessageWithin(
                TimeSpan timeout,
                Func<Message, ReadAction> consumerIsFinished)
            {
                var toDelete = new List<Message>();
                var stop = false;
                foreach (var archivedMessage in archivedMessages)
                {
                    switch (consumerIsFinished(archivedMessage))
                    {
                        case ReadAction.continueNext:
                            break;
                        case ReadAction.deleteAndContinueNext:
                            toDelete.Add(archivedMessage);
                            break;
                        case ReadAction.deleteAndStop:
                            toDelete.Add(archivedMessage);
                            stop = true;
                            break;
                        case ReadAction.justStop:
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
                        case ReadAction.continueNext:
                            break;
                        case ReadAction.deleteAndContinueNext:
                            toDelete.Add(message);
                            break;
                        case ReadAction.deleteAndStop:
                            toDelete.Add(message);
                            stop = true;
                            break;
                        case ReadAction.justStop:
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
                //Console.WriteLine($"{DescribeOneself()} sends a messages to unknown index {neighbourIndex}");
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
                            return ReadAction.deleteAndContinueNext;
                        case MessageType.IDReturn:
                            neighboursThatReturnedMessage.Add(message.SenderID);
                            idToIndex[message.SenderID] = message.ContentsAsInt;
                            indexToID[message.ContentsAsInt] = message.SenderID;
                            return ReadAction.deleteAndContinueNext;
                    }
                return ReadAction.continueNext;
            });

            if (stillCompetingNeighbours.SetEquals(neighboursThatReturnedMessage))
            {
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
                                        return ReadAction.deleteAndStop;
                                    else
                                        return ReadAction.deleteAndContinueNext;
                                case MessageType.FinishedPlayingCymbals:
                                    stillCompetingNeighbours.Remove(message.SenderID);
                                    participantsLeft--;
                                    displayStatus($" waits for {participantsLeft} messages received finished playing by {message.SenderID}");

                                    if (participantsLeft == 0)
                                        return ReadAction.deleteAndStop;
                                    else
                                        return ReadAction.deleteAndContinueNext;
                            }
                        if (message.Color < myColor)
                            return ReadAction.deleteAndContinueNext;
                        else
                            return ReadAction.continueNext;
                    });

                }

                //foreach (var id in activeNeighboursID.Except(elected))
                //{
                //    SendToID(id, new Message(myID, MessageType.AreYouReadyForThisColor, string.Empty, -1, myColor));
                //}

                //receiveMessageWithin(TimeSpan.MaxValue, message =>
                //{
                //    if (message.Color == myColor && message.Stage == -1)
                //        switch (message.MessageType)
                //        {
                //            case MessageType.AreYouReadyForThisColor:

                //                break;
                //        }
                //    return ReadAction.continueNext;
                //});

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
                    //elected = new HashSet<int>();

                    var myInt = random.Next();
                    displayStatus($" is preparing for Int exchange... chose {myInt}");
                    if (participatingThisStage.Count > 0)
                    {
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
                                        return ReadAction.deleteAndContinueNext;
                                    case MessageType.NotParticipatingInThisStage:
                                        displayStatus($" received not participating from {message.SenderID}");
                                        notParticipating.Add(message.SenderID);
                                        participatingThisStage.Remove(message.SenderID);
                                        if (participatingThisStage.Count == 0)
                                            return ReadAction.deleteAndStop;
                                        else
                                            return ReadAction.deleteAndContinueNext;
                                    case MessageType.MyIntBroadcast:
                                        if (message.ContentsAsInt > myInt)
                                        {
                                            superiorNeighbours.Add(message.SenderID);
                                        }
                                        ++receivedInts;
                                        if (receivedInts == participatingThisStage.Count)
                                            return ReadAction.deleteAndStop;
                                        else
                                            return ReadAction.deleteAndContinueNext;
                                }
                            return ReadAction.continueNext;
                        });
                    }

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

                        displayStatus(" is now crashing cymbals...");
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
                        //int receivedSuperiorElections = 0;
                        // someone else is superior, but is he the chosen one?
                        receiveMessageWithin(defaultTimeout, message =>
                        {
                            if (message.Stage == myStage && message.Color == myColor)
                                switch (message.MessageType)
                                {
                                    case MessageType.NowElected:
                                        electedDuringThisStage.Add(message.SenderID);
                                        //receivedSuperiorElections++;
                                        //if (receivedSuperiorElections == superiorNeighbours.Count)
                                        return ReadAction.deleteAndStop;
                                        //else
                                        //    return ReadAction.deleteAndContinueNext;
                                }
                            return ReadAction.continueNext;
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
            //var setOfAllSuperiorIDs = new HashSet<int>();
            //var waitingToFinishIDs = new HashSet<int>();

            //receiveMessageWithin(TimeSpan.MaxValue, neighboursThatArentDoneYet, message =>
            //     {
            //         if (message.MessageType == MessageType.MyIntBroadcast)
            //         {
            //             if (message.ContentsAsInt >= chosenInt)
            //             {
            //                 // found a superior neighbour :(
            //                 setOfAllSuperiorIDs.Add(message.SenderID);
            //             }
            //             return true;
            //         }
            //         return false;
            //     });

            //proceedToNextStage();
            //myState = JankielState.doneExchangingInts;
            //displayStatus(" just finished exchanging ints");
            //var finishedPlayingOffset = 2;
            //var loserOffset = 1;
            //if (setOfAllSuperiorIDs.Count() == 0)
            //{
            //    myState = JankielState.elected;
            //    // I am superior over my meighbours, buahahaha
            //    sendMessageToNeighboursSuchThat(
            //        MessageType.NowElected,
            //        string.Empty,
            //        state => state != JankielState.donePlaying);

            //    // i'm elected, play the music & notify neighbours
            //    displayStatus();
            //    // play the song!
            //    Thread.Sleep(playingPeriodDuration);
            //    myState = JankielState.donePlaying;
            //    displayStatus();


            //    sendMessageToNeighboursSuchThat(
            //        MessageType.FinishedPlaying,
            //        string.Empty,
            //        state => state != JankielState.donePlaying,
            //        myStage + finishedPlayingOffset);
            //}
            //else
            //{
            //    // someone is superior over me, yet dunno whether it is elected :/
            //    var isLoser = false;
            //    receiveMessageWithin(defaultTimeout, setOfAllSuperiorIDs.Count, message =>
            //                {
            //                    if (message.MessageType == MessageType.NowElected)
            //                    {
            //                        waitingToFinishIDs.Add(message.SenderID);
            //                        // indeed, a larger neighbour is really elected :(
            //                        idToNeighbourState[message.SenderID] = JankielState.elected;
            //                        isLoser = true;
            //                        return true;
            //                    }
            //                    return false;
            //                });

            //    if (isLoser)
            //    {
            //        myState = JankielState.loser;
            //        sendMessageToNeighboursSuchThat(
            //            MessageType.LoserMessage,
            //            string.Empty,
            //            state => state != JankielState.elected,
            //            myStage + loserOffset);
            //        // i'm done here for this round, listening for winners to finish
            //        displayStatus(" is waiting for players to finish playing");
            //        receiveMessageWithin(TimeSpan.MaxValue, waitingToFinishIDs.Count(), message =>
            //                        {
            //                            if (message.MessageType == MessageType.FinishedPlaying)
            //                            {
            //                                neighboursThatArentDoneYet--;
            //                                waitingToFinishIDs.Remove(message.SenderID);
            //                                idToNeighbourState[message.SenderID] = JankielState.donePlaying;
            //                                return true;
            //                            }
            //                            return false;
            //                        },
            //                        myStage + finishedPlayingOffset);
            //        // now.. actively competing for next MIS!
            //        myState = JankielState.doneIDsReadyToExchangeInts;
            //        myColor++;
            //        myStage = 1;
            //        displayStatus(" is prepared for next round of MIS!");
            //        // send out ids
            //        sendMessageToNeighboursSuchThat(MessageType.IDBroadcast, myID.ToString(), _ => true);
            //    }
            //    else
            //    {
            //        // not superior, but superior neighbours weren't elected
            //        // neither a winner neither a looser (neighbouring with larger vertices that have larger neighbours)
            //        // expecting messages from losers
            //        receiveMessageWithin(defaultTimeout, neighboursThatArentDoneYet, message =>
            //        {
            //            if (message.MessageType == MessageType.LoserMessage)
            //            {
            //                idToNeighbourState[message.SenderID] = JankielState.loser;
            //                if (message.ContentsAsInt >= chosenInt)
            //                {
            //                    // found a superior neighbour :(
            //                    setOfAllSuperiorIDs.Add(message.SenderID);
            //                }
            //                return true;
            //            }
            //            return false;
            //        },
            //        myStage + loserOffset);
            //        // now, check if i'm superior and so on...
            //        if (setOfAllSuperiorIDs.Count > 0)
            //        {
            //            // wait for next round
            //        }
            //        else
            //        {
            //            // become elected
            //        }
            //        displayStatus(" dunno");

            //    }
            //}

            //proceedToNextStage(finishedPlayingOffset + 1);
            //displayStatus();


            //Console.WriteLine($"    {DescribeOneself()} is selected? " + (validCandidate.ToString()));
            //inIndependentState = idToJankiel.Keys.All(id => id < myID);

            //if (inIndependentState)
            //{
            //    state = JankielState.elected;
            //    // if won, notify neighbours and then sing for 2 seconds
            //    for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
            //    {
            //        if (!isActivelyApplying[neighbourIndex])
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
            //    sendSameMessageToAllActive(MessageType.NullMessage, string.Empty);
            //    //receive messages
            //}

            //// then notify neighbours

        }
    }
}
