using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADPlayClickEventArgs : GameEventArgs
    {
        public int SerialID { get; private set; }
        public string PlacementID { get; private set; }
        public string PodID { get; private set; }
        public ADFormat AdFormat { get; private set; }
        public object UserData { get; private set; }
        public string Tag { get; private set; }

        public static ADPlayClickEventArgs Create(GameFrameX.Ad.ADPlayClickEventArgs e)
        {
            ADPlayClickEventArgs adPlayClickEventArgs = ReferencePool.Acquire<ADPlayClickEventArgs>();
            adPlayClickEventArgs.SerialID = e.SerialID;
            adPlayClickEventArgs.PlacementID = e.PlacementID;
            adPlayClickEventArgs.PodID = e.PosID;
            adPlayClickEventArgs.AdFormat = e.ADFormat;
            adPlayClickEventArgs.Tag = e.Tag;
            adPlayClickEventArgs.UserData = e.UserData;
            return adPlayClickEventArgs;
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

        public const string EventId = "GameFrameX.Ad.ADPlayClickEventArgs";
    }
}
