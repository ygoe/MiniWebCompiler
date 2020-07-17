using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Unclassified.Util
{
	public class NamedPipeClient : IDisposable
	{
		private readonly string pipeName;
		private readonly byte[] buffer = new byte[4096];
		private readonly StringBuilder messageBuilder = new StringBuilder();
		private readonly SynchronizationContext synchronizationContext = AsyncOperationManager.SynchronizationContext;
		private NamedPipeClientStream clientStream;
		private bool isDisposed;

		public NamedPipeClient(string pipeName, int timeout = Timeout.Infinite)
		{
			this.pipeName = pipeName;
			Connect(timeout);
		}

		public bool IsConnected => clientStream.IsConnected;

		public bool Reconnect { get; set; }

		public event EventHandler Reconnected;

		public event EventHandler<NamedPipeClientMessageEventArgs> Message;

		public event EventHandler Disconnected;

		public void Dispose()
		{
			if (!isDisposed)
			{
				isDisposed = true;
				try
				{
					clientStream.WaitForPipeDrain();
				}
				catch (IOException)
				{
					// Broken pipe, this is expected when the connection was closed remotely.
				}
				catch (InvalidOperationException)
				{
					// Not connected, this is expected when cancelling the connect operation.
				}
				clientStream.Dispose();
			}
		}

		public async Task SendAsync(string message)
		{
			if (!clientStream.IsConnected)
				throw new InvalidOperationException("The client is not connected.");

			byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
			await Task.Factory.FromAsync(clientStream.BeginWrite, clientStream.EndWrite, sendBuffer, 0, sendBuffer.Length, null);
			clientStream.Flush();
		}

		private void Connect(int timeout)
		{
			clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
			try
			{
				clientStream.Connect(timeout);
			}
			catch (TimeoutException)
			{
				return;
			}
			clientStream.ReadMode = PipeTransmissionMode.Message;
			clientStream.BeginRead(buffer, 0, buffer.Length, ReadCompleted, null);
		}

		public async Task<string> SendWithResponseAsync(string message, int timeout = Timeout.Infinite)
		{
			if (!clientStream.IsConnected)
				throw new InvalidOperationException("The client is not connected.");

			var tcs = new TaskCompletionSource<string>();
			void MessageHandler(object sender, NamedPipeClientMessageEventArgs args)
			{
				tcs.SetResult(args.Message);
			}
			Message += MessageHandler;
			try
			{
				byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
				await Task.Factory.FromAsync(clientStream.BeginWrite, clientStream.EndWrite, sendBuffer, 0, sendBuffer.Length, null);
				clientStream.Flush();
				var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
				if (completedTask == tcs.Task)
					return await tcs.Task;
				else
					return null;
			}
			finally
			{
				Message -= MessageHandler;
			}
		}

		private void ReadCompleted(IAsyncResult result)
		{
			int readBytes = clientStream.EndRead(result);
			if (readBytes > 0)
			{
				messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, readBytes));

				if (!clientStream.IsMessageComplete)
				{
					clientStream.BeginRead(buffer, 0, buffer.Length, ReadCompleted, null);
				}
				else
				{
					string message = messageBuilder.ToString().TrimEnd('\0');
					messageBuilder.Clear();
					if (messageBuilder.Capacity > 64 * 1024)
					{
						messageBuilder.Capacity = buffer.Length;
					}
					synchronizationContext.Post(
						a => Message?.Invoke(this, (NamedPipeClientMessageEventArgs)a),
						new NamedPipeClientMessageEventArgs(this) { Message = message });

					// Continue reading for next message
					clientStream.BeginRead(buffer, 0, buffer.Length, ReadCompleted, null);
				}
			}
			else
			{
				if (!isDisposed)
				{
					synchronizationContext.Post(a => Disconnected?.Invoke(this, (EventArgs)a), EventArgs.Empty);
					if (Reconnect)
					{
						Connect(Timeout.Infinite);
						synchronizationContext.Post(a => Reconnected?.Invoke(this, (EventArgs)a), EventArgs.Empty);
					}
					else
					{
						Dispose();
					}
				}
			}
		}
	}

	public class NamedPipeClientMessageEventArgs : EventArgs
	{
		private readonly NamedPipeClient client;

		public NamedPipeClientMessageEventArgs(NamedPipeClient client)
		{
			this.client = client;
		}

		public string Message { get; set; }

		public Task RespondAsync(string message)
		{
			return client.SendAsync(message);
		}
	}
}
