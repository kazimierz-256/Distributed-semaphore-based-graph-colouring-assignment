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
        MyIntBroadcast,
        NotParticipatingInThisStage,
        IDReturn,
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
    public enum InboxAction
    {
        continueNext,
        deleteAndContinueNext,
        deleteAndStop,
        justStop
    }
}
