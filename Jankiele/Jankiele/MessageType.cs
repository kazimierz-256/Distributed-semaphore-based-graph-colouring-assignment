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
        BMessageBroadcast
    }
    enum JankielState
    {
        receivingIDs,
        elected,
        dunno
    }
    enum InMIS
    {
        yes,
        no,
        dunno
    }
}
