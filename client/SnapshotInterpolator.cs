using Godot;
using System.Collections.Generic;
using ImGuiNET;

/*
    Receives and presents the Player the snapshots emmited by the server.
*/
public partial class SnapshotInterpolator : Node
{
    [Export] private int _minBufferTime = 3;

    private readonly List<NetMessage.GameSnapshot> _snapshotBuffer = new();
    private const int RecentPast = 0, NextFuture = 1;
    private double _interpolationFactor = 0;
    private int _bufferTime = 0;
    private double _currentTick = 0;

    public override void _Ready()
    {
        _bufferTime = 6; //TODO: magic number
    }

    public override void _Process(double delta)
    {
        /*TODO: While _currentTick isn't updated, increase the _currentTick by delta in ticks in order to
        smooth out the interpolation, I still believe that it is not as smooth as it was when using
        Time.GetTicksMsec() so this is still pending, but is good enough for now.*/
        _currentTick += delta / PhysicsUtils.FrameTime;
        DisplayDebugInformation();
    }

    public void ProcessTick(int currentTick)
    {
        _currentTick = currentTick;
    }

    public void InterpolateStates(Node playersArray)
    {
        // Point in time to render (in the past)
        double renderTick = _currentTick - _bufferTime;

        if (_snapshotBuffer.Count > 1)
        {
            // Clear any unwanted (past) states
            while (_snapshotBuffer.Count > 2 && renderTick > _snapshotBuffer[1].Tick)
            {
                _snapshotBuffer.RemoveAt(0);
            }

            var nextSnapshot = _snapshotBuffer[NextFuture];
            var prevSnapshot = _snapshotBuffer[RecentPast];

            int timeDiffBetweenStates = nextSnapshot.Tick - prevSnapshot.Tick;
            double renderDiff = renderTick - prevSnapshot.Tick;

            _interpolationFactor = renderDiff / timeDiffBetweenStates;

            var futureStates = nextSnapshot.States;

            for (int i = 0; i < futureStates.Length; i++)
            {
                //TODO: check if the Entity is available in both states
                NetMessage.EntityState futureState = nextSnapshot.States[i];
                NetMessage.EntityState pastState = prevSnapshot.States[i];

                var dummy = playersArray.GetNode<Node3D>(futureState.Id.ToString()); //FIXME: remove GetNode for the love of god

                if (dummy != null && dummy.IsMultiplayerAuthority() == false)
                {
                    dummy.Position = pastState.Position.Lerp(futureState.Position, (float)_interpolationFactor);
                }
            }
        }
    }

    public void PushState(NetMessage.GameSnapshot snapshot)
    {
        if (_snapshotBuffer.Count <= 0 || snapshot.Tick > _snapshotBuffer[^1].Tick)
        {
            _snapshotBuffer.Add(snapshot);
        }
    }

    public void SetBufferTime(int bufferTime)
    {
        _bufferTime = bufferTime + _minBufferTime;
    }

    private void DisplayDebugInformation()
    {
        ImGui.Begin("Snapshot Interpolator Information");

        if (_interpolationFactor > 1) ImGui.PushStyleColor(ImGuiCol.Text, 0xFF0000FF);
        ImGui.Text($"Interp. Factor {_interpolationFactor:0.00}");
        ImGui.PopStyleColor();

        ImGui.Text($"Buffer Size {_snapshotBuffer.Count} snapshots");
        ImGui.Text($"Buffer Time {_bufferTime} ticks");
        ImGui.End();
    }

}