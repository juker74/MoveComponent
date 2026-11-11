using System;

namespace Game.VideoFlow
{
    public sealed partial class VideoFlowDriver
    {
        void ChangeState(FlowState next)
        {
            var previous = Phase;
            state?.Leave();
            state = next;
            if (previous != next.Phase) Notify(FlowEventKind.PhaseChanged);
            if (CanRun || next is InactiveState) next.Enter();
        }

        void PrepareVideo(Action open) { ChangeState(new PreparingVideoState(this, open)); }
        void SwitchVideo(Action close, Action open) { ChangeState(new SwitchingVideoState(this, close, open)); }

        abstract class FlowState
        {
            protected readonly VideoFlowDriver Driver;
            public FlowPhase Phase { get; }
            protected FlowState(VideoFlowDriver driver, FlowPhase phase) { Driver = driver; Phase = phase; }
            public virtual void Enter() { }
            public virtual void Tick(float delta, float real) { }
            public virtual void Leave() { }
        }

        sealed class InactiveState : FlowState
        {
            public InactiveState(VideoFlowDriver driver, FlowPhase phase) : base(driver, phase) { }
        }

        sealed class PreparingVideoState : FlowState
        {
            readonly Action open;
            public PreparingVideoState(VideoFlowDriver driver, Action open) : base(driver, FlowPhase.PreparingVideo) { this.open = open; }
            public override void Enter()
            {
                open();
                PromoteReadyVideo();
            }
            bool PromoteReadyVideo()
            {
                if (!Driver.CanRun || Driver.currentVideo == null) return false;
                var session = Driver.currentVideo.Session;
                if (session != null && session.Status != VideoPlaybackStatus.Playing && session.Status != VideoPlaybackStatus.Completed) return false;
                Driver.ChangeState(new PlayingVideoState(Driver));
                return Driver.CanRun;
            }
            public override void Tick(float delta, float real)
            {
                if (PromoteReadyVideo()) Driver.state.Tick(delta, real);
                else if (Driver.currentVideo?.Session?.Status != VideoPlaybackStatus.Preparing)
                    Driver.UpdateActivation(Driver.currentVideo?.Video, delta, real);
            }
        }

        sealed class PlayingVideoState : FlowState
        {
            public PlayingVideoState(VideoFlowDriver driver) : base(driver, FlowPhase.PlayingVideo) { }
            public override void Tick(float delta, float real)
            {
                Driver.UpdateVideo(delta, real);
                Driver.UpdateInteractions(delta, real);
            }
        }

        sealed class SwitchingVideoState : FlowState
        {
            readonly Action close, open;
            public SwitchingVideoState(VideoFlowDriver driver, Action close, Action open) : base(driver, FlowPhase.SwitchingVideo)
            { this.close = close; this.open = open; }
            public override void Enter()
            {
                close();
                if (Driver.CanRun) Driver.PrepareVideo(open);
            }
        }

        sealed class InteractionState : FlowState
        {
            readonly Action enter;
            public InteractionState(VideoFlowDriver driver, Action enter = null) : base(driver, FlowPhase.Interacting) { this.enter = enter; }
            public override void Enter() { enter?.Invoke(); }
            public override void Tick(float delta, float real) { Driver.UpdateInteractions(delta, real); }
        }
    }
}
