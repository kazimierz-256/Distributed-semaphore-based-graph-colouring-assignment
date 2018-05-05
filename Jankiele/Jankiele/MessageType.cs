using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jankiele
{
    public enum MessageType
    {
        IDBroadcast,
        FinishedPlaying,
        NowElected,
        NullMessage,
        MyIntBroadcast,
        LoserMessage,
        IDReturn,
        ProceedToNextStage
    }
    public enum JankielState
    {
        receivingIDs,
        elected,
        donePlaying,
        loser,
        doneIDsReadyToExchangeInts,
        doneExchangingInts
    }
}
