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
        FinishedPlayingCymbals,
        NowElected,
        NullMessage,
        MyIntBroadcast,
        NotParticipatingInThisStage,
        IDReturn,
        ProceedToNextStage,
        AreYouReadyForThisColor,
        ParticipatingInThisColor,
        Dunno
    }
    public enum JankielState
    {
        receivingIDs,
        elected,
        donePlaying,
        loser,
        doneIDsReadyToExchangeInts,
        doneExchangingInts,
        dunno
    }
    public enum InboxType
    {
        nothingSpecial
    }
    public enum ReadAction
    {
        continueNext,
        deleteAndContinueNext,
        deleteAndStop,
        justStop
    }
}
