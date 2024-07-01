using Godot;
using ImGuiNET;
using MemoryPack;

/*
	Network manager for the client, handles server connection and routes packages.
*/
public partial class ClientManager : Node
{
	[Export] private string _address = "localhost";
	[Export] private int _port = 9999;

	private SceneMultiplayer _multiplayer = new();
	private SnapshotInterpolator _snapshotInterpolator;
	private ClientClock _clock;
	private Node _entityArray;

	public override void _EnterTree()
	{
		// Connects to the server
		ConnectClient();

		_entityArray = GetNode("/root/Main/EntityArray");

		// Stores NetworkClock node instance
		_clock = GetNode<ClientClock>("ClientClock");
		_clock.LatencyCalculated += OnLatencyCalculated;

		// Stores SnapshotInterpolator node instance
		_snapshotInterpolator = GetNode<SnapshotInterpolator>("SnapshotInterpolator");
	}

	public override void _Process(double delta)
	{
		_snapshotInterpolator.InterpolateStates(_entityArray);
		DisplayDebugInformation();
	}

	public override void _PhysicsProcess(double delta)
	{
		_clock.ProcessTick();
		int currentTick = _clock.GetCurrentTick();
		int currentRemoteTick = _clock.GetCurrentRemoteTick();
		if (CustomSpawner.LocalPlayer != null)
			CustomSpawner.LocalPlayer.ProcessTick(currentRemoteTick);
		_snapshotInterpolator.ProcessTick(currentTick);
	}

	private void OnPacketReceived(long id, byte[] data)
	{
		var command = MemoryPackSerializer.Deserialize<NetMessage.ICommand>(data);

		if (command is NetMessage.GameSnapshot snapshot)
		{
			ProcessSnapshot(snapshot);
		}
	}

	private void ProcessSnapshot(NetMessage.GameSnapshot snapshot)
	{
		_snapshotInterpolator.PushState(snapshot);

		foreach (NetMessage.EntityState state in snapshot.States)
		{
			if (state.Id == Multiplayer.GetUniqueId())
			{
				CustomSpawner.LocalPlayer.ReceiveState(state, snapshot.Tick);
			}
		}
	}

	private void OnLatencyCalculated(int latencyAverageTicks, int jitterAverageTicks)
	{
		_snapshotInterpolator.SetBufferTime(latencyAverageTicks + jitterAverageTicks);
	}

	private void ConnectClient()
	{
		_multiplayer.PeerPacket += OnPacketReceived;

		ENetMultiplayerPeer peer = new();
		peer.CreateClient(_address, _port);
		_multiplayer.MultiplayerPeer = peer;
		GetTree().SetMultiplayer(_multiplayer);
		GD.Print("Client connected to ", _address, ":", _port);
	}

	private void DisplayDebugInformation()
	{
		ImGui.Begin("Client Information");
		ImGui.Text($"Framerate {Engine.GetFramesPerSecond()}fps");
		ImGui.Text($"Physics Tick {Engine.PhysicsTicksPerSecond}hz");
		ImGui.End();
	}
}
