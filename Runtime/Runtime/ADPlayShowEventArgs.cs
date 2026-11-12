using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADPlayShowEventArgs : GameEventArgs
    {
        public int SerialID { get; private set; }
        public string PlacementID { get; private set; }
        public string PodID { get; private set; }
        public ADFormat AdFormat { get; private set; }
        public object UserData { get; private set; }
        public string Tag { get; private set; }

        public static ADPlayShowEventArgs Create(GameFrameX.Ad.ADPlayShowEventArgs e)
        {
            ADPlayShowEventArgs adPlayShowEventArgs = ReferencePool.Acquire<ADPlayShowEventArgs>();
            adPlayShowEventArgs.SerialID = e.SerialID;
            adPlayShowEventArgs.PlacementID = e.PlacementID;
            adPlayShowEventArgs.PodID = e.PosID;
            adPlayShowEventArgs.AdFormat = e.ADFormat;
            adPlayShowEventArgs.Tag = e.Tag;
            adPlayShowEventArgs.UserData = e.UserData;
            return adPlayShowEventArgs;
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

        public const string EventId = "GameFrameX.Ad.ADPlayShowEventArgs";
    }
}
