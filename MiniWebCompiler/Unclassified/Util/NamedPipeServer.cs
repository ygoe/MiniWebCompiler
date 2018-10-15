using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Unclassified.Util
{
	public class NamedPipeServer : IDisposable
	{
		private string pipeName;
		private int nextServerId = 1;
		private Dictionary<int, InternalServer> servers = new Dictionary<int, InternalServer>();
		private readonly SynchronizationContext synchronizationContext = AsyncOperationManager.SynchronizationContext;

		public NamedPipeServer(string pipeName)
		{
			this.pipeName = pipeName;
		}

		public string PipeName => pipeName;

		public event EventHandler<NamedPipeServerConnectedEventArgs> Connected;

		public event EventHandler<NamedPipeServerMessageEventArgs> Message;

		public event EventHandler<NamedPipeServerDisconnectedEventArgs> Disconnected;

		public void Start()
		{
			StartServer();
		}

		public void Dispose()
		{
			if (servers.TryGetValue(nextServerId - 1, out InternalServer listeningServer))
			{
				StopServer(listeningServer);
			}
			foreach (var server in servers.Values)
			{
				StopServer(server);
			}
			servers.Clear();
		}

		public Task SendAsync(int connectionId, string message)
		{
			return servers[connectionId].SendAsync(message);
		}

		private void StartServer()
		{
			var server = new InternalServer(nextServerId, pipeName);
			servers.Add(nextServerId, server);
			server.Connected += OnConnected;
			server.Message += OnMessage;
			server.Disconnected += OnDisconnected;
			nextServerId++;
			server.Start();
		}

		private void StopServer(InternalServer server)
		{
			server.Connected -= OnConnected;
			server.Message -= OnMessage;
			server.Disconnected -= OnDisconnected;
			server.Dispose();
		}

		private void OnConnected(object sender, NamedPipeServerConnectedEventArgs args)
		{
			synchronizationContext.Post(a => Connected?.Invoke(this, (NamedPipeServerConnectedEventArgs)a), args);
			StartServer();
		}

		private void OnMessage(object sender, NamedPipeServerMessageEventArgs args)
		{
			synchronizationContext.Post(a => Message?.Invoke(this, (NamedPipeServerMessageEventArgs)a), args);
		}

		private void OnDisconnected(object sender, NamedPipeServerDisconnectedEventArgs args)
		{
			synchronizationContext.Post(a => Disconnected?.Invoke(this, (NamedPipeServerDisconnectedEventArgs)a), args);
			StopServer(servers[args.ConnectionId]);
			servers.Remove(args.ConnectionId);
		}

		private class InternalServer : IDisposable, INamedPipeResponder
		{
			private int id;
			private string pipeName;
			private NamedPipeServerStream serverStream;
			private byte[] buffer = new byte[4096];
			private StringBuilder messageBuilder = new StringBuilder();
			private bool isDisposed;

			public InternalServer(int id, string pipeName)
			{
				this.id = id;
				this.pipeName = pipeName;
			}

			public event EventHandler<NamedPipeServerConnectedEventArgs> Connected;

			public event EventHandler<NamedPipeServerMessageEventArgs> Message;

			public event EventHandler<NamedPipeServerDisconnectedEventArgs> Disconnected;

			public void Start()
			{
				serverStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
				serverStream.BeginWaitForConnection(ConnectionCompleted, null);
			}

			public void Dispose()
			{
				if (!isDisposed)
				{
					isDisposed = true;
					try
					{
						serverStream.WaitForPipeDrain();
					}
					catch (IOException)
					{
						// Broken pipe, this is expected when the connection was closed remotely.
					}
					catch (InvalidOperationException)
					{
						// Not connected, this is expected when cancelling the connect operation.
					}
					if (serverStream.IsConnected)
					{
						serverStream.Disconnect();
					}
					serverStream.Dispose();
				}
			}

			public async Task SendAsync(string message)
			{
				if (!serverStream.IsConnected)
					throw new InvalidOperationException("The server is not connected.");

				var sendBuffer = Encoding.UTF8.GetBytes(message);
				await Task.Factory.FromAsync(serverStream.BeginWrite, serverStream.EndWrite, sendBuffer, 0, sendBuffer.Length, null);
				serverStream.Flush();
			}

			public Task RespondAsync(string message)
			{
				return SendAsync(message);
			}

			private void ConnectionCompleted(IAsyncResult result)
			{
				if (isDisposed) return;

				serverStream.EndWaitForConnection(result);
				Connected?.Invoke(this, new NamedPipeServerConnectedEventArgs { ConnectionId = id });
				serverStream.BeginRead(buffer, 0, buffer.Length, ReadCompleted, null);
			}

			private void ReadCompleted(IAsyncResult result)
			{
				int readBytes = serverStream.EndRead(result);
				if (readBytes > 0)
				{
					messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, readBytes));

					if (!serverStream.IsMessageComplete)
					{
						serverStream.BeginRead(buffer, 0, buffer.Length, ReadCompleted, null);
					}
					else
					{
						string message = messageBuilder.ToString().TrimEnd('\0');
						messageBuilder.Clear();
						if (messageBuilder.Capacity > 64 * 1024)
						{
							messageBuilder.Capacity = buffer.Length;
						}
						Message?.Invoke(this, new NamedPipeServerMessageEventArgs(this) { ConnectionId = id, Message = message });

						// Continue reading for next message
						serverStream.BeginRead(buffer, 0, buffer.Length, ReadCompleted, null);
					}
				}
				else
				{
					if (!isDisposed)
					{
						Disconnected?.Invoke(this, new NamedPipeServerDisconnectedEventArgs { ConnectionId = id });
						Dispose();
					}
				}
			}
		}

		public interface INamedPipeResponder
		{
			Task RespondAsync(string message);
		}
	}

	public class NamedPipeServerConnectedEventArgs : EventArgs
	{
		public int ConnectionId { get; set; }
	}

	public class NamedPipeServerMessageEventArgs : EventArgs
	{
		private NamedPipeServer.INamedPipeResponder responder;

		public NamedPipeServerMessageEventArgs(NamedPipeServer.INamedPipeResponder responder)
		{
			this.responder = responder;
		}

		public int ConnectionId { get; set; }

		public string Message { get; set; }

		public Task RespondAsync(string message)
		{
			return responder.RespondAsync(message);
		}
	}

	public class NamedPipeServerDisconnectedEventArgs : EventArgs
	{
		public int ConnectionId { get; set; }
	}
}
