﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
	class StoppingMode : Mode
	{
        private static readonly ILogger Logger = LogManager.GetLogger();
        WaitHandleGroup handle = new WaitHandleGroup();
        private Stopwatch _shutdownTime;

		public override TorrentState State
		{
			get { return TorrentState.Stopping; }
		}

		public StoppingMode(TorrentManager manager)
			: base(manager)
		{
            _shutdownTime = Stopwatch.StartNew();

			CanAcceptConnections = false;
			ClientEngine engine = manager.Engine;
			if (manager.Mode is HashingMode)
				handle.AddHandle(((HashingMode)manager.Mode).hashingWaitHandle, "Hashing");

			if (manager.TrackerManager.CurrentTracker != null && manager.TrackerManager.CurrentTracker.Status == TrackerState.Ok)
				handle.AddHandle(manager.TrackerManager.Announce(TorrentEvent.Stopped), "Announcing");

			foreach (PeerId id in manager.Peers.ConnectedPeers)
				if (id.Connection != null)
					id.Connection.Dispose();

			manager.Peers.ClearAll();

			handle.AddHandle(engine.DiskManager.CloseFileStreams(manager), "DiskManager");

			manager.Monitor.Reset();
			manager.PieceManager.Reset();
			engine.ConnectionManager.CancelPendingConnects(manager);
			engine.Stop();
		}

		public override void HandlePeerConnected(PeerId id, MonoTorrent.Common.Direction direction)
		{
			id.CloseConnection();
		}

		public override void Tick(int counter)
		{
			if (handle.WaitOne(0, true))
			{
				handle.Close();
				Manager.Mode = new StoppedMode(Manager);
                _shutdownTime.Stop();

                if (_shutdownTime.Elapsed.TotalSeconds > 3)
                    Logger.Warn("Stopping takes {0} s ", _shutdownTime.Elapsed.TotalSeconds);
			}
		}
	}
}
