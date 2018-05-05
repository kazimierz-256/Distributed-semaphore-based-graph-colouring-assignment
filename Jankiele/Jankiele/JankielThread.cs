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
        private const int playingPeriodDuration = 2000;
        private Tuple<double, double> coords;
        private JankielPerson[] neighbours;
        private Dictionary<int, JankielPerson> idToJankiel;
        private Dictionary<int, JankielState> idToNeighbourState;
        private Dictionary<int, int> idToIndex;
        private Dictionary<int, int> indexToID;
        private ConcurrentQueue<Message> messages = new ConcurrentQueue<Message>();
        private Semaphore messageArrived = new Semaphore(0, int.MaxValue);
        private int myID;
        private Random random;

        private object messageQueuecreation = new object();

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
            idToNeighbourState = new Dictionary<int, JankielState>();
            foreach (var nid in neighbours.Select(neighbour => neighbour.GetID()))
            {
                idToNeighbourState[nid] = JankielState.receivingIDs;
            }
            idToIndex = new Dictionary<int, int>(n);
            indexToID = new Dictionary<int, int>(n);
        }

        public int GetID() => myID;
        internal Tuple<double, double> GetCoordinates() => coords;
        private void AddStageIfNecessary(int color, int stage)
        {
            lock (messageQueuecreation)
            {
                if (!messages.ContainsKey(color))
                {
                    messages.Add(color, new Dictionary<int, ConcurrentQueue<Message>>());
                    messageArrived.Add(color, new Dictionary<int, Semaphore>());
                }
                if (!messages[color].ContainsKey(stage))
                {

                }
            }
        }
        public void SendMessage(Message message)
        {
            AddStageIfNecessary(message.Stage);
            messages[message.Stage].Enqueue(message);
            messageArrived[message.Stage].Release(1);
        }

        public void Launch()
        {
            // check out rounds
            // send message
            var myState = JankielState.receivingIDs;
            TimeSpan defaultTimeout = TimeSpan.FromMilliseconds(1000);
            int myStage = 1;
            int myRound = 1;
            var neighboursThatArentDoneYet = neighbours.Length;

            void displayStatus(string comment = "")
            {
                Console.WriteLine($"Stage {myStage}, status of {DescribeOneself()} is {myState.ToString()}" + comment);
            }
            void sendMessageToNeighboursSuchThat(MessageType type, string messageContents = "", Func<JankielState, bool> neighbourPredicate = null, int targetStage = -1, int targetRound = -1)
            {
                if (targetStage == -1)
                    targetStage = myStage;
                if (targetRound == -1)
                    targetRound = myRound;
                for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
                {
                    if (neighbourPredicate == null
                        || neighbourPredicate(idToNeighbourState[indexToID[neighbourIndex]]))
                        neighbours[neighbourIndex].SendMessage(new Message(myID, type, messageContents, targetStage, targetRound));
                }
            }
            void receiveMessageWithin(TimeSpan timeout, int expectedNumOfMessagesUpperBound, Func<Message, bool> acceptingPredicate = null, int sourceStage = -1)
            {
                if (sourceStage == -1)
                {
                    sourceStage = myStage;
                }
                AddStageIfNecessary(sourceStage);

                for (int expectedMessages = expectedNumOfMessagesUpperBound; expectedMessages > 0;)
                {
                    //Console.WriteLine($"{DescribeOneself()} waits for incoming message...");
                    var success = timeout == TimeSpan.MaxValue ? messageArrived[sourceStage].WaitOne() : messageArrived[sourceStage].WaitOne(timeout);
                    if (!success)
                    {
                        Console.WriteLine($"Time has exipred for {DescribeOneself()}");
                        return;
                    }
                    success &= messages[sourceStage].TryDequeue(out Message message);
                    if (!success)
                        throw new Exception("Signaled a message, yet none received!");
                    //Console.WriteLine($"{DescribeOneself()} unboxing message from {message.GetSenderID()}");
                    // read message
                    if (acceptingPredicate == null)
                    {
                        expectedMessages--;
                    }
                    else if (acceptingPredicate.Invoke(message))
                    {
                        expectedMessages--;
                    }
                    //else
                    //{
                    //    // this message wasn't expected, so it is enqueued once again
                    //    messages[sourceStage].Enqueue(message);
                    //}
                }
            }

            void proceedToNextStage(int increase = 1)
            {
                myStage += increase;
                sendMessageToNeighboursSuchThat(
                MessageType.ProceedToNextStage,
                string.Empty,
                state => state != JankielState.donePlaying
                );
                receiveMessageWithin(TimeSpan.MaxValue, neighboursThatArentDoneYet, message => message.MessageType == MessageType.ProceedToNextStage);
                ++myStage;
            }

            for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
            {
                //Console.WriteLine($"{DescribeOneself()} sends a messages to unknown index {neighbourIndex}");
                neighbours[neighbourIndex].SendMessage(
                    new Message(myID, MessageType.IDBroadcast, neighbourIndex.ToString(), myStage, myRound)
                    );
            }
            receiveMessageWithin(TimeSpan.MaxValue, neighboursThatArentDoneYet * 2, message =>
               {
                   if (message.MessageType == MessageType.IDBroadcast)
                   {
                       // ask the server to reply
                       foreach (var neighbour in neighbours.Where(neighbour => neighbour.GetID() == message.SenderID))
                       {
                           neighbour.SendMessage(
                               new Message(myID, MessageType.IDReturn, message.Contents, myStage, myRound)
                               );
                       }

                       return true;
                   }
                   else if (message.MessageType == MessageType.IDReturn)
                   {
                       idToIndex[message.SenderID] = message.ContentsAsInt;
                       indexToID[message.ContentsAsInt] = message.SenderID;
                       return true;
                   }
                   return false;
               });

            proceedToNextStage();

            if (indexToID.Count == neighboursThatArentDoneYet)
                myState = JankielState.doneIDsReadyToExchangeInts;
            displayStatus();
            // received everyone's ID

            // FIRST STAGE

            var setOfAllSuperiorIDs = new HashSet<int>();
            var waitingToFinishIDs = new HashSet<int>();
            var chosenInt = random.Next();

            sendMessageToNeighboursSuchThat(
                MessageType.MyIntBroadcast,
                chosenInt.ToString(),
                state => state != JankielState.donePlaying);

            receiveMessageWithin(TimeSpan.MaxValue, neighboursThatArentDoneYet, message =>
             {
                 if (message.MessageType == MessageType.MyIntBroadcast)
                 {
                     if (message.ContentsAsInt >= chosenInt)
                     {
                         // found a superior neighbour :(
                         setOfAllSuperiorIDs.Add(message.SenderID);
                     }
                     return true;
                 }
                 return false;
             });

            proceedToNextStage();
            myState = JankielState.doneExchangingInts;
            displayStatus(" just finished exchanging ints");
            var finishedPlayingOffset = 2;
            var loserOffset = 1;
            if (setOfAllSuperiorIDs.Count() == 0)
            {
                myState = JankielState.elected;
                // I am superior over my meighbours, buahahaha
                sendMessageToNeighboursSuchThat(
                    MessageType.NowElected,
                    string.Empty,
                    state => state != JankielState.donePlaying);

                // i'm elected, play the music & notify neighbours
                displayStatus();
                // play the song!
                Thread.Sleep(playingPeriodDuration);
                myState = JankielState.donePlaying;
                displayStatus();


                sendMessageToNeighboursSuchThat(
                    MessageType.FinishedPlaying,
                    string.Empty,
                    state => state != JankielState.donePlaying,
                    myStage + finishedPlayingOffset);
            }
            else
            {
                // someone is superior over me, yet dunno whether it is elected :/
                var isLoser = false;
                receiveMessageWithin(defaultTimeout, setOfAllSuperiorIDs.Count, message =>
                {
                    if (message.MessageType == MessageType.NowElected)
                    {
                        waitingToFinishIDs.Add(message.SenderID);
                        // indeed, a larger neighbour is really elected :(
                        idToNeighbourState[message.SenderID] = JankielState.elected;
                        isLoser = true;
                        return true;
                    }
                    return false;
                });

                if (isLoser)
                {
                    myState = JankielState.loser;
                    sendMessageToNeighboursSuchThat(
                        MessageType.LoserMessage,
                        string.Empty,
                        state => state != JankielState.elected,
                        myStage + loserOffset);
                    // i'm done here for this round, listening for winners to finish
                    displayStatus(" is waiting for players to finish playing");
                    receiveMessageWithin(TimeSpan.MaxValue, waitingToFinishIDs.Count(), message =>
                    {
                        if (message.MessageType == MessageType.FinishedPlaying)
                        {
                            neighboursThatArentDoneYet--;
                            waitingToFinishIDs.Remove(message.SenderID);
                            idToNeighbourState[message.SenderID] = JankielState.donePlaying;
                            return true;
                        }
                        return false;
                    },
                    myStage + finishedPlayingOffset);
                    // now.. actively competing for next MIS!
                    myState = JankielState.doneIDsReadyToExchangeInts;
                    myRound++;
                    myStage = 1;
                    displayStatus(" is prepared for next round of MIS!");
                    // send out ids
                    sendMessageToNeighboursSuchThat(MessageType.IDBroadcast, myID.ToString(), _ => true);
                }
                else
                {
                    // not superior, but superior neighbours weren't elected
                    // neither a winner neither a looser (neighbouring with larger vertices that have larger neighbours)
                    // expecting messages from losers
                    receiveMessageWithin(defaultTimeout, neighboursThatArentDoneYet, message =>
                    {
                        if (message.MessageType == MessageType.LoserMessage)
                        {
                            idToNeighbourState[message.SenderID] = JankielState.loser;
                            if (message.ContentsAsInt >= chosenInt)
                            {
                                // found a superior neighbour :(
                                setOfAllSuperiorIDs.Add(message.SenderID);
                            }
                            return true;
                        }
                        return false;
                    },
                    myStage + loserOffset);
                    // now, check if i'm superior and so on...
                    if (setOfAllSuperiorIDs.Count > 0)
                    {
                        // wait for next round
                    }
                    else
                    {
                        // become elected
                    }
                    displayStatus(" dunno");

                }
            }

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
