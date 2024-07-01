using Godot;
using System.Collections.Generic;
using NetMessage;
using System;
using ImGuiNET;
using MemoryPack;
using System.Linq;

/*
    Main player script, send movement packets to the server, does CSP, and reconciliation. 
*/
public partial class ClientPlayer : CharacterBody3D
{
    private bool _simulating = false;
    private readonly List<LocalInput> _userInputs = new ();
    private int _networkId = -1;
    private int _lastStampReceived = 0;
    private int _misspredictionCounter = 0;

    private byte _automoveInput = 0b0000_1000;
    private bool _autoMoveEnabled = false;

    public override void _Ready()
    {
        _networkId = Multiplayer.GetUniqueId();
    }

    public override void _Process(double delta)
    {
        DisplayDebugInformation();
    }

    public void ProcessTick(int currentTick)
    {
        var userInput = GenerateUserInput(currentTick);
        if (_autoMoveEnabled)
        {
            SolveAutoMove();
            userInput.Input = _automoveInput;
        }
        if(!_simulating)
        {
            PushLocalState(userInput);
            SendInputs(currentTick);
            AdvancePhysics(userInput.Input);
        }
    }

    private void PushLocalState(LocalInput input)
    {
        input.State = GetCurrentState();
        _userInputs.Add(input); 
    }

    // Applies inputs ahead of the server (Prediction)
    private void AdvancePhysics(byte input)
    {
        this.Velocity = PlayerMovement.ComputeMotion(
            this.GlobalTransform,
            this.Velocity,
            PlayerMovement.InputToDirection(input));

        MoveAndSlide();
    }

    // Called when a UserState is received from the server
    // Here we validate that our prediction was correct
    public void ReceiveState(NetMessage.EntityState incomingState, int forTick)
    {
        if (_simulating)
            return;

        // Ignore any stamp that should have been received in the past
        if (forTick > _lastStampReceived)
            _lastStampReceived = forTick;
        else return;

        // TODO: Figure out why this only works when adding +1 to forTick
        _userInputs.RemoveAll(input => input.Tick < forTick + 1); // Delete all stored inputs up to that point, we don't need them anymore
        LocalInput stateForTick = _userInputs.Where(x => x.Tick == forTick + 1).FirstOrDefault();

        if (stateForTick.State == null)
            return;

        // IncomingState is where we should have been at the tick, stateForTick is our current position
		var deviation = incomingState.Position - stateForTick.State.Position;

		// Reconciliation with authoritative state if the deviation is too high
		if (deviation.LengthSquared() > 0.0001f)
		{   
            _simulating = true;
            // Re-apply all inputs that haven't been processed by the server starting from the last acked state (the one just received)
            Transform3D expectedTransform = this.GlobalTransform;
            expectedTransform.Origin = incomingState.Position;

            Vector3 expectedVelocity = incomingState.Velocity;

            GD.PrintErr($"Client {this._networkId} prediction mismatch ({deviation.Length()}) (Stamp {forTick})!\nExpected Pos:{expectedTransform.Origin} Vel:{expectedVelocity}\nCalculated Pos:{stateForTick.State.Position} Vel:{stateForTick.State.Velocity}\n");

            GlobalTransform = expectedTransform;
            Velocity = expectedVelocity;

            foreach (var userInput in _userInputs) // Re-apply all inputs
            {
                expectedVelocity = PlayerMovement.ComputeMotion(
                    expectedTransform,
                    expectedVelocity,
                    PlayerMovement.InputToDirection(userInput.Input));

                Velocity = expectedVelocity;
                Velocity *= (float)GetPhysicsProcessDeltaTime() / (float)GetProcessDeltaTime();
                MoveAndSlide();
                Velocity /= (float)GetPhysicsProcessDeltaTime() / (float)GetProcessDeltaTime();
            }

            _misspredictionCounter++;
            _simulating = false;
        }
    }

    // For Debug Only
    private void SolveAutoMove()
    {
        if (this.Position.X > 5 && _automoveInput == 0b0000_1000)
        {
            _automoveInput = 0b0000_0100;
        }
        else if (this.Position.X < -5 && _automoveInput == 0b0000_0100)
        {
            _automoveInput = 0b0000_1000;
        }
    }

    // Sends all non-processed inputs to the server
    private void SendInputs(int currentTick)
    {
        var userCmd = new NetMessage.UserCommand
        {
            Tick = currentTick,
            Inputs = _userInputs.Select(i => i.Input).ToArray()
        };

        if (this.IsMultiplayerAuthority() && Multiplayer.GetUniqueId() != 1)
        {
            byte[] data = MemoryPackSerializer.Serialize<NetMessage.ICommand>(userCmd);

            (Multiplayer as SceneMultiplayer).SendBytes(data, 1,
                MultiplayerPeer.TransferModeEnum.Unreliable, 0);
        }
    }

    public NetMessage.EntityState GetCurrentState()
	{
		return new NetMessage.EntityState
		{
			Id = _networkId,
			PosArray = new float[3] { this.Position.X, this.Position.Y, this.Position.Z },
			VelArray = new float[3] { this.Velocity.X, this.Velocity.Y, this.Velocity.Z }
		};
	}

    private static LocalInput GenerateUserInput(int tick)
    {
        byte keys = 0;

        if (Input.IsActionPressed("right")) keys |= (byte)InputFlags.Right;
        if (Input.IsActionPressed("left")) keys |= (byte)InputFlags.Left;
        if (Input.IsActionPressed("forward")) keys |= (byte)InputFlags.Forward;
        if (Input.IsActionPressed("backward")) keys |= (byte)InputFlags.Backward;
        if (Input.IsActionPressed("space")) keys |= (byte)InputFlags.Space;
        if (Input.IsActionPressed("shift")) keys |= (byte)InputFlags.Shift;

        var userInput = new LocalInput
        {
            Tick = tick,
            Input = keys
        };

        return userInput;
    }

    private void DisplayDebugInformation()
    {
        ImGui.Begin("Player Network Information");
        ImGui.Text($"Network Id {_networkId}");
        ImGui.Text($"Position {Position.Snapped(Vector3.One * 0.01f)}");
        ImGui.Text($"Redundant Inputs {_userInputs.Count}");
        ImGui.Text($"Last Stamp Rec. {_lastStampReceived}");
        ImGui.Text($"Misspredictions {_misspredictionCounter}");
        ImGui.Checkbox("Automove?", ref _autoMoveEnabled);
        ImGui.End();
    }

    private struct LocalInput
    {
        public int Tick;
        public byte Input;
        public EntityState State;
    }
}
