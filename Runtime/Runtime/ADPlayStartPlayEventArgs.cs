using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADPlayStartPlayEventArgs : GameEventArgs
    {
        public int SerialID { get; private set; }
        public string PlacementID { get; private set; }
        public string PodID { get; private set; }
        public ADFormat AdFormat { get; private set; }
        public object UserData { get; private set; }
        public string Tag { get; private set; }

        public static ADPlayStartPlayEventArgs Create(GameFrameX.Ad.ADPlayStartPlayEventArgs e)
        {
            ADPlayStartPlayEventArgs adPlayStartPlayEventArgs = ReferencePool.Acquire<ADPlayStartPlayEventArgs>();
            adPlayStartPlayEventArgs.SerialID = e.SerialID;
            adPlayStartPlayEventArgs.PlacementID = e.PlacementID;
            adPlayStartPlayEventArgs.PodID = e.PosID;
            adPlayStartPlayEventArgs.AdFormat = e.ADFormat;
            adPlayStartPlayEventArgs.Tag = e.Tag;
            adPlayStartPlayEventArgs.UserData = e.UserData;
            return adPlayStartPlayEventArgs;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            PodID = string.Empty;
            AdFormat = default;
            UserData = null;
            Tag = string.Empty;
        }

        public override string Id => EventId;

        public const string EventId = "GameFrameX.Ad.ADPlayStartPlayEventArgs";
    }
}
