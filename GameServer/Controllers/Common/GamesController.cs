using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameServer.Implementation.Common;
using GameServer.Models;
using GameServer.Models.PlayerData;
using GameServer.Models.PlayerData.Games;
using GameServer.Models.Request;
using GameServer.Models.Response;
using GameServer.Utils;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers.Common
{
    public class GamesController : Controller
    {
        private readonly Database database;

        public GamesController(Database database)
        {
            this.database = database;
        }

        [HttpGet]
        [Route("locations.xml")]
        public IActionResult Locations()
        {
            var resp = new Response<EmptyResponse>
            {
                status = new ResponseStatus { id = 0, message = "Successful completion" },
                response = new EmptyResponse { }
            };
            return Content(resp.Serialize(), "application/xml;charset=utf-8");
        }

        [HttpGet]
        [Route("/resources/single_player_game.create_finish_and_post_stats.xml")]
        public IActionResult GetSinglePlayerXML()
        { //Because for whatever reason MNR: Road Trip Refuses to take my xml with LBPK variables in it -_-
            Guid SessionID = Guid.Empty;
            if (Request.Cookies.ContainsKey("session_id"))
                SessionID = Guid.Parse(Request.Cookies["session_id"]);
            var session = Session.GetSession(SessionID);
            string resp;
            if (session.IsMNR && System.IO.File.Exists("GameResources/MNR.single_player_game.create_finish_and_post_stats.xml"))
                resp = System.IO.File.ReadAllText("GameResources/MNR.single_player_game.create_finish_and_post_stats.xml");
            else if (System.IO.File.Exists("GameResources/single_player_game.create_finish_and_post_stats.xml"))
                resp = System.IO.File.ReadAllText("GameResources/single_player_game.create_finish_and_post_stats.xml");
            else
                return NotFound();
            return Content(resp, "application/xml;charset=utf-8");
        }

        [HttpPost]
        [Route("single_player_games/create_finish_and_post_stats.xml")]
        public IActionResult PostSinglePlayerGameStats(Game game, GamePlayer game_player, GamePlayerStats game_player_stats)
        {
            int GameID = database.Games.Count() + 1,
                GamePlayerID = database.GamePlayers.Count() + 1,
                GamePlayerStatsID = database.GamePlayerStats.Count() + 1;
            Guid SessionID = Guid.Empty;
            if (Request.Cookies.ContainsKey("session_id"))
                SessionID = Guid.Parse(Request.Cookies["session_id"]);
            var Track = database.PlayerCreations.FirstOrDefault(match => match.PlayerCreationId == game.track_idx);
            var session = Session.GetSession(SessionID);
            var user = database.Users.FirstOrDefault(match => match.Username == session.Username);
            string FormScore = Request.Form["game_player_stats[score]"];
            string FormFinishTime = Request.Form["game_player_stats[finish_time]"];
            string FormPoints = Request.Form["game_player_stats[points]"];
            string FormVolatility = Request.Form["game_player_stats[volatility]"];
            string FormDeviation = Request.Form["game_player_stats[deviation]"];
            string FormBestLapTime = Request.Form["game_player_stats[best_lap_time]"];
            string FormLongestDrift = Request.Form["game_player_stats[longest_drift]"];
            string FormLongestHangTime = Request.Form["game_player_stats[longest_hang_time]"];
            string FormLatitude = Request.Form["game_player_stats[latitude]"];
            string FormLongitude = Request.Form["game_player_stats[longitude]"];

            if (session.IsMNR)
            {
                Track = database.PlayerCreations.FirstOrDefault(match => match.PlayerCreationId == game_player_stats.track_idx);
                game.host_player_id = database.Users.FirstOrDefault(match => match.Username == session.Username).UserId;
                game.track_idx = game_player_stats.track_idx;
            }
            UserGeneratedContentUtils.AddStoryLevel(database, game.track_idx);

            if (Track == null || user == null)
            {
                var errorResp = new Response<EmptyResponse>
                {
                    status = new ResponseStatus { id = -620, message = "No player creation exists for the given ID" },
                    response = new EmptyResponse { }
                };
                return Content(errorResp.Serialize(), "application/xml;charset=utf-8");
            }

            if (FormScore != null)
                game_player_stats.score = float.Parse(FormScore, CultureInfo.InvariantCulture.NumberFormat);
            if (FormFinishTime != null)
                game_player_stats.finish_time = float.Parse(FormFinishTime, CultureInfo.InvariantCulture.NumberFormat);
            if (FormPoints != null)
                game_player_stats.points = float.Parse(FormPoints, CultureInfo.InvariantCulture.NumberFormat);
            if (FormVolatility != null)
                game_player_stats.volatility = float.Parse(FormVolatility, CultureInfo.InvariantCulture.NumberFormat);
            if (FormDeviation != null)
                game_player_stats.deviation = float.Parse(FormDeviation, CultureInfo.InvariantCulture.NumberFormat);
            if (FormBestLapTime != null)
                game_player_stats.best_lap_time = float.Parse(FormBestLapTime, CultureInfo.InvariantCulture.NumberFormat);
            if (FormLongestDrift != null)
                game_player_stats.longest_drift = float.Parse(FormLongestDrift, CultureInfo.InvariantCulture.NumberFormat);
            if (FormLongestHangTime != null)
                game_player_stats.longest_hang_time = float.Parse(FormLongestHangTime, CultureInfo.InvariantCulture.NumberFormat);
            if (FormLatitude != null)
                game_player_stats.latitude = float.Parse(FormLatitude, CultureInfo.InvariantCulture.NumberFormat);
            if (FormLongitude != null)
                game_player_stats.longitude = float.Parse(FormLongitude, CultureInfo.InvariantCulture.NumberFormat);
            if (session.IsMNR)
                game_player_stats.ghost_car_data = Request.Form.Files.GetFile("game_player_stats[ghost_car_data]");

            Score score;

            if(session.IsMNR && session.Platform == Platform.PS3 && user.CharacterIdx != game_player_stats.character_idx)
                user.CharacterIdx = game_player_stats.character_idx;

            if (session.IsMNR && session.Platform == Platform.PS3 && user.KartIdx != game_player_stats.kart_idx)
                user.KartIdx = game_player_stats.kart_idx;

            if (user.LongestDrift < game_player_stats.longest_drift)
                user.LongestDrift = game_player_stats.longest_drift;

            if (user.LongestHangTime < game_player_stats.longest_hang_time)
                user.LongestHangTime = game_player_stats.longest_hang_time;

            if (!session.IsMNR)
            {
                score = database.Scores.FirstOrDefault(match => match.PlayerId == game.host_player_id
                    && match.SubKeyId == game.track_idx
                    && match.SubGroupId == (int)game.game_type
                    && match.Platform == game.platform
                    && match.PlaygroupSize == game_player_stats.playgroup_size && match.IsMNR == session.IsMNR);
            }
            else
            {
                score = database.Scores.FirstOrDefault(match => match.PlayerId == game.host_player_id
                    && match.SubKeyId == game_player_stats.track_idx
                    && match.SubGroupId == (int)game.game_type - 10
                    && match.Platform == session.Platform && match.IsMNR == session.IsMNR);
            }

            database.Games.Add(new GameData
            {
                Id = GameID,
                GameState = game.game_state,
                GameType = game.game_type,
                HostPlayerId = game.host_player_id,
                IsRanked = game.is_ranked,
                Name = game.name,
                Platform = game.platform,
                //MNR
                TrackIdx = game.track_idx,
                NumberLaps = game.number_laps,
                Privacy = game.privacy,
                SpeedClass = game.speed_class,
                Track = game.track,
                TrackGroup = game.track_group
            });
            database.GamePlayers.Add(new GamePlayerData
            {
                Id = GamePlayerID,
                GameId = GameID,
                GameState = game_player.game_state,
                PlayerId = game_player.player_id,
                TeamId = game_player.team_id
            });
            database.GamePlayerStats.Add(new GamePlayerStatsData
            {
                Id = GamePlayerStatsID,
                GameId = GameID,
                Deviation = game_player_stats.deviation,
                FinishPlace = game_player_stats.finish_place,
                FinishTime = game_player_stats.finish_time,
                IsComplete = game_player_stats.is_complete,
                IsWinner = game_player_stats.is_winner,
                LapsCompleted = game_player_stats.laps_completed,
                NumKills = game_player_stats.num_kills,
                PlaygroupSize = game_player_stats.playgroup_size,
                Points = game_player_stats.points,
                Score = game_player_stats.score,
                Stat1 = game_player_stats.stat_1,
                Stat2 = game_player_stats.stat_2,
                Volatility = game_player_stats.volatility,
                //MNR
                Bank = game_player_stats.bank,
                BestLapTime = game_player_stats.best_lap_time,
                CharacterIdx = game_player_stats.character_idx,
                KartIdx = game_player_stats.kart_idx,
                LongestDrift = game_player_stats.longest_drift,
                LongestHangTime = game_player_stats.longest_hang_time,
                TrackIdx = game_player_stats.track_idx,
                MusicIdx = game_player_stats.music_idx,
                //MNR: Road Trip
                Latitude = game_player_stats.latitude,
                Longitude = game_player_stats.longitude,
                LocationTag = game_player_stats.location_tag,
                TrackPlatform = game_player_stats.track_platform
            });

            var leaderboard = database.Scores.Where(match => match.SubKeyId == game.track_idx && match.SubGroupId == (int)game.game_type &&
                match.PlaygroupSize == game_player_stats.playgroup_size && match.Platform == game.platform).ToList();

            if (Track.ScoreboardMode == 1)
                leaderboard.Sort((curr, prev) => curr.FinishTime.CompareTo(prev.FinishTime));
            else
                leaderboard.Sort((curr, prev) => prev.Points.CompareTo(curr.Points));

            if (leaderboard != null && !session.IsMNR)
            {
                var FastestTime = leaderboard.FirstOrDefault();
                var HighScore = leaderboard.FirstOrDefault();
                if (Track.ScoreboardMode == 1 && FastestTime != null && FastestTime.FinishTime > game_player_stats.finish_time)
                {
                    database.ActivityLog.Add(new ActivityEvent
                    {
                        AuthorId = user.UserId,
                        Type = ActivityType.player_event,
                        List = ActivityList.both,
                        Topic = "player_beat_finish_time",
                        Description = $"{game_player_stats.finish_time}",
                        PlayerId = 0,
                        PlayerCreationId = Track.PlayerCreationId,
                        CreatedAt = DateTime.UtcNow,
                        AllusionId = Track.PlayerCreationId,
                        AllusionType = "PlayerCreation::Track"
                    });
                }
                if (Track.ScoreboardMode == 0 && HighScore != null && HighScore.Points < game_player_stats.score)
                {
                    database.ActivityLog.Add(new ActivityEvent
                    {
                        AuthorId = user.UserId,
                        Type = ActivityType.player_event,
                        List = ActivityList.both,
                        Topic = "player_beat_score",
                        Description = $"{game_player_stats.score}",
                        PlayerId = 0,
                        PlayerCreationId = Track.PlayerCreationId,
                        CreatedAt = DateTime.UtcNow,
                        AllusionId = Track.PlayerCreationId,
                        AllusionType = "PlayerCreation::Track"
                    });
                }
            }
            string GhostDataMD5 = "";
            MemoryStream GhostData = new MemoryStream();
            if (session.IsMNR)
            {
                game_player_stats.ghost_car_data.OpenReadStream().CopyTo(GhostData);
                GhostDataMD5 = UserGeneratedContentUtils.CalculateGhostCarDataMD5(new MemoryStream(GhostData.ToArray()));
            }
            bool SaveGhost = false;

            if (score != null)
            {               
                if (score.FinishTime > game_player_stats.finish_time)
                {
                    score.FinishTime = game_player_stats.finish_time;
                    score.UpdatedAt = DateTime.UtcNow;
                    score.CharacterIdx = game_player_stats.character_idx;
                    score.KartIdx = game_player_stats.kart_idx;
                    SaveGhost = true;
                }
                if (score.Points < game_player_stats.score)
                {
                    score.Points = game_player_stats.score;
                    score.UpdatedAt = DateTime.UtcNow;
                }
                if (score.BestLapTime > game_player_stats.best_lap_time)
                {
                    score.BestLapTime = game_player_stats.best_lap_time;
                    score.UpdatedAt = DateTime.UtcNow;
                    score.CharacterIdx = game_player_stats.character_idx;
                    score.KartIdx = game_player_stats.kart_idx;
                    SaveGhost = true;
                }
                if (SaveGhost)
                    score.GhostCarDataMD5 = GhostDataMD5;
                if (session.Platform == Platform.PSV)
                {
                    score.Latitude = game_player_stats.latitude;
                    score.Longitude = game_player_stats.longitude;
                }
            }
            else
            {
                database.Scores.Add(new Score
                {
                    CreatedAt = DateTime.UtcNow,
                    FinishTime = game_player_stats.finish_time,
                    Platform = session.Platform == Platform.PSV ? game_player_stats.track_platform : session.Platform,
                    PlayerId = game.host_player_id,
                    PlaygroupSize = game_player_stats.playgroup_size,
                    Points = game_player_stats.score,
                    SubGroupId = session.IsMNR ? (int)game.game_type - 10 : (int)game.game_type,
                    SubKeyId = game.track_idx,
                    UpdatedAt = DateTime.UtcNow,
                    Latitude = game_player_stats.latitude,
                    Longitude = game_player_stats.longitude,
                    BestLapTime = game_player_stats.best_lap_time,
                    KartIdx = game_player_stats.kart_idx,
                    CharacterIdx = game_player_stats.character_idx,
                    GhostCarDataMD5 = GhostDataMD5,
                    IsMNR = session.IsMNR
                });
                SaveGhost = true;
            }
            if (session.IsMNR && SaveGhost)
            {
                GhostData.Position = 0;
                UserGeneratedContentUtils.SaveGhostCarData(game.game_type, session.Platform,
                    game_player_stats.track_idx, database.Users.FirstOrDefault(match => match.Username == session.Username).UserId,
                    GhostData);
            }
            database.SaveChanges();

            var resp = new Response<List<GameResponse>>
            {
                status = new ResponseStatus { id = 0, message = "Successful completion" },
                response = new List<GameResponse> { new GameResponse {
                    id = database.Games.Count(),
                    game_player_id = database.GamePlayers.Count(),
                    game_player_stats_id = database.GamePlayerStats.Count()
                } }
            };

            return Content(resp.Serialize(), "application/xml;charset=utf-8");
        }
    }
}