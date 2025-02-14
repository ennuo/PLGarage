﻿using GameServer.Implementation.Common;
using GameServer.Models.Config;
using GameServer.Models.PlayerData.PlayerCreations;
using GameServer.Models.PlayerData;
using GameServer.Utils;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GameServer.Controllers.Common
{
    public class ApiController : Controller
    {
        private readonly Database database;

        public ApiController(Database database)
        {
            this.database = database;
        }

        [HttpGet]
        [Route("api/GetInstanceName")]
        public IActionResult GetInstanceName()
        {
            return Content(ServerConfig.Instance.InstanceName);
        }

        [Route("api/ServerCommunication")]
        public async Task StartServerCommunication()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                Guid ServerID = Guid.Empty;
                if (Request.Headers.ContainsKey("server_id"))
                    ServerID = Guid.Parse(Request.Headers["server_id"]);
                await ServerCommunication.HandleConnection(database, webSocket, ServerID);
            }
        }

        [HttpGet]
        [Route("api/CheckSession/{SessionID}")]
        public IActionResult CheckSession(Guid SessionID)
        {
            Guid ServerID = Guid.Empty;
            if (Request.Headers.ContainsKey("server_id"))
                ServerID = Guid.Parse(Request.Headers["server_id"]);

            if (ServerCommunication.GetServer(ServerID) == null)
                return Forbid();

            return Content(Session.GetSession(SessionID).Authenticated.ToString().ToLower());
        }

        [HttpGet]
        [Route("api/TrackVotes")]
        public IActionResult GetVotingOptions()
        {
            Guid ServerID = Guid.Empty;
            if (Request.Headers.ContainsKey("server_id"))
                ServerID = Guid.Parse(Request.Headers["server_id"]);

            if (ServerCommunication.GetServer(ServerID) == null)
                return Forbid();

            List<int> TrackIDs = new List<int>();

            Random random = new Random();
            var creations = database.PlayerCreations.Where(match => match.Type == PlayerCreationType.TRACK && !match.IsMNR && match.Platform == Platform.PS3).ToList();

            if (creations.Count <= 3)
            {
                foreach (var creation in creations)
                {
                    TrackIDs.Add(creation.PlayerCreationId);
                }
            }
            else
            {
                while (TrackIDs.Count < 3)
                {
                    var id = creations[random.Next(0, creations.Count - 1)].PlayerCreationId;
                    if (!TrackIDs.Contains(id))
                        TrackIDs.Add(id);
                }
            }

            return Content(JsonConvert.SerializeObject(TrackIDs));
        }
    }
}
