using Godot;
using System.Collections.Generic;
using ImGuiNET;
using System;
using NetMessage;
using System.Linq;
using ImGuiGodot.Internal;

public partial class ServerPlayer : CharacterBody3D
{
	public int MultiplayerID { get; set; } = 0;
	public int InstantLatency { get; set; } = 0;

	private Dictionary<int, NetMessage.UserInput> _pendingInputs = new();
	private int _skippedTicks = 0;
	private int _inputQueueSize = 0;

	public override void _Process(double delta)
	{
		DisplayDebugInformation();
	}

	public void ProcessPendingCommands(int currentTick)
	{
		if (_pendingInputs.TryGetValue(currentTick, out UserInput input))
		{
			Move(input);

			_pendingInputs = _pendingInputs.Where(pair => pair.Value.Tick > currentTick)
			.ToDictionary(pair => pair.Key, pair => pair.Value);
			/*
				Note: Using dictionaries for this is probably the worst and most unefficient
				way of queueing non-duplicated inputs, this must be changed in the future.
			*/

			_inputQueueSize = _pendingInputs.Count;
		}
		else
		{
			_skippedTicks++;
		}
	}

	public void PushCommand(NetMessage.UserCommand command)
	{
		foreach (NetMessage.UserInput userInput in command.Commands)
		{
			if (!_pendingInputs.ContainsKey(userInput.Tick))
			{
				_pendingInputs.Add(userInput.Tick, userInput);
			}
		}
	}

	private void Move(NetMessage.UserInput userInput)
	{
		this.Velocity = PlayerMovement.ComputeMotion(
			this.GetRid(),
			this.GlobalTransform,
			this.Velocity,
			PlayerMovement.InputToDirection(userInput.Keys));

		Position += this.Velocity * PlayerMovement.FrameDelta;
	}


	public NetMessage.UserState GetCurrentState()
	{
		return new NetMessage.UserState
		{
			Id = MultiplayerID,
			PosArray = new float[3] { this.Position.X, this.Position.Y, this.Position.Z },
			VelArray = new float[3] { this.Velocity.X, this.Velocity.Y, this.Velocity.Z }
		};
	}

	private void DisplayDebugInformation()
	{
		ImGui.Begin($"Server Player {MultiplayerID}");
		ImGui.Text($"Instant Latency {InstantLatency}");
		ImGui.Text($"Input Queue Count {_inputQueueSize}");
		ImGui.Text($"Missed Frames {_skippedTicks}");
		ImGui.End();
	}
}
