﻿using BeatLeader_Server.Extensions;
using BeatLeader_Server.Migrations.ReadApp;
using BeatLeader_Server.Models;
using Lib.AspNetCore.ServerTiming;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static BeatLeader_Server.Controllers.RankController;
using static BeatLeader_Server.Utils.ResponseUtils;

namespace BeatLeader_Server.Controllers
{
    public class StatsController : Controller
    {
        private readonly AppContext _context;
        private readonly ReadAppContext _readContext;

        private readonly IServerTiming _serverTiming;
        private readonly IConfiguration _configuration;

        public StatsController(
            AppContext context,
            ReadAppContext readContext,
            IWebHostEnvironment env,
            IServerTiming serverTiming,
            IConfiguration configuration)
        {
            _context = context;
            _readContext = readContext;

            _serverTiming = serverTiming;
            _configuration = configuration;
        }

        [HttpGet("~/player/{id}/scoresstats")]
        public async Task<ActionResult<ResponseWithMetadata<PlayerLeaderboardStats>>> GetScoresStats(
            string id,
            [FromQuery] string sortBy = "date",
            [FromQuery] string order = "desc",
            [FromQuery] int page = 1,
            [FromQuery] int count = 8,
            [FromQuery] string? search = null,
            [FromQuery] string? diff = null,
            [FromQuery] string? type = null,
            [FromQuery] float? stars_from = null,
            [FromQuery] float? stars_to = null,
            [FromQuery] int? eventId = null)
        {
            IQueryable<PlayerLeaderboardStats> sequence;

            using (_serverTiming.TimeAction("sequence"))
            {
                sequence = _readContext.PlayerLeaderboardStats.Include(pl => pl.Score).Where(t => t.PlayerId == id);
                switch (sortBy)
                {
                    case "date":
                        sequence = sequence.Order(order, t => t.Timeset);
                        break;
                    //case "pp":
                    //    sequence = sequence.Order(order, t => t.Pp);
                    //    break;
                    //case "acc":
                    //    sequence = sequence.Order(order, t => t.Accuracy);
                    //    break;
                    //case "pauses":
                    //    sequence = sequence.Order(order, t => t.Pauses);
                    //    break;
                    //case "rank":
                    //    sequence = sequence.Order(order, t => t.Rank);
                    //    break;
                    //case "stars":
                    //    sequence = sequence
                    //                .Include(lb => lb.Leaderboard)
                    //                .ThenInclude(lb => lb.Difficulty)
                    //                .Order(order, s => s.Leaderboard.Difficulty.Stars)
                    //                .Where(s => s.Leaderboard.Difficulty.Status == DifficultyStatus.ranked);
                        break;
                    default:
                        break;
                }
                //if (search != null)
                //{
                //    string lowSearch = search.ToLower();
                //    sequence = sequence
                //        .Include(lb => lb.Leaderboard)
                //        .ThenInclude(lb => lb.Song)
                //        .Where(p => p.Leaderboard.Song.Author.ToLower().Contains(lowSearch) ||
                //                    p.Leaderboard.Song.Mapper.ToLower().Contains(lowSearch) ||
                //                    p.Leaderboard.Song.Name.ToLower().Contains(lowSearch));
                //}
                //if (eventId != null)
                //{
                //    var leaderboardIds = _context.EventRankings.Where(e => e.Id == eventId).Include(e => e.Leaderboards).Select(e => e.Leaderboards.Select(lb => lb.Id)).FirstOrDefault();
                //    if (leaderboardIds?.Count() != 0)
                //    {
                //        sequence = sequence.Where(s => leaderboardIds.Contains(s.LeaderboardId));
                //    }
                //}
                //if (diff != null)
                //{
                //    sequence = sequence.Include(lb => lb.Leaderboard).ThenInclude(lb => lb.Difficulty).Where(p => p.Leaderboard.Difficulty.DifficultyName.ToLower().Contains(diff.ToLower()));
                //}
                //if (type != null)
                //{
                //    sequence = sequence.Include(lb => lb.Leaderboard).ThenInclude(lb => lb.Difficulty).Where(p => type == "ranked" ? p.Leaderboard.Difficulty.Status == DifficultyStatus.ranked : p.Leaderboard.Difficulty.Status != DifficultyStatus.ranked);
                //}
                //if (stars_from != null)
                //{
                //    sequence = sequence.Include(lb => lb.Leaderboard).ThenInclude(lb => lb.Difficulty).Where(p => p.Leaderboard.Difficulty.Stars >= stars_from);
                //}
                //if (stars_to != null)
                //{
                //    sequence = sequence.Include(lb => lb.Leaderboard).ThenInclude(lb => lb.Difficulty).Where(p => p.Leaderboard.Difficulty.Stars <= stars_to);
                //}
            }

            ResponseWithMetadata<PlayerLeaderboardStats> result;
            using (_serverTiming.TimeAction("db"))
            {
                result = new ResponseWithMetadata<PlayerLeaderboardStats>()
                {
                    Metadata = new Metadata()
                    {
                        Page = page,
                        ItemsPerPage = count,
                        Total = sequence.Count()
                    },
                    Data = sequence
                            .Skip((page - 1) * count)
                            .Take(count)
                            //.Include(lb => lb.Leaderboard)
                            //    .ThenInclude(lb => lb.Song)
                            //    .ThenInclude(lb => lb.Difficulties)
                            //.Include(lb => lb.Leaderboard)
                            //    .ThenInclude(lb => lb.Difficulty)
                            //    .ThenInclude(d => d.ModifierValues)
                            //.Include(sc => sc.ScoreImprovement)
                            //.Select(ScoreWithMyScore)
                            .ToList()
                };
            }

            //string? currentID = HttpContext.CurrentUserID(_readContext);
            //if (currentID != null && currentID != id)
            //{
            //    var leaderboards = result.Data.Select(s => s.LeaderboardId).ToList();

            //    var myScores = _readContext.Scores.Where(s => s.PlayerId == currentID && leaderboards.Contains(s.LeaderboardId)).Select(RemoveLeaderboard).ToList();
            //    foreach (var score in result.Data)
            //    {
            //        score.MyScore = myScores.FirstOrDefault(s => s.LeaderboardId == score.LeaderboardId);
            //    }
            //}

            return result;
        }

        [HttpGet("~/watched/{scoreId}/")]
        public async Task<ActionResult<VoteStatus>> Played(
            int scoreId)
        {
            var ip = HttpContext.Request.HttpContext.Connection.RemoteIpAddress;

            if (ip == null) return BadRequest();

            int ipHash = ip.GetHashCode();

            if ((await _context.WatchingSessions.FirstOrDefaultAsync(ws => ws.ScoreId == scoreId && ws.IPHash == ipHash)) != null) return Ok(); 

            Score? score = await _context.Scores.FindAsync(scoreId);
            if (score == null) return NotFound();

            score.ReplayWatched++;

            string? currentID = HttpContext.CurrentUserID(_context);
            if (currentID != null)
            {
                var player = await _context.Players.Where(p => p.Id == currentID).Include(p => p.ScoreStats).FirstOrDefaultAsync();
                if (player != null) {
                    player.ScoreStats.WatchedReplays++;
                }
            }

            _context.WatchingSessions.Add(new ReplayWatchingSession {
                ScoreId = scoreId,
                IPHash = ipHash
            });
            _context.SaveChanges();

            return Ok();
        }
    }
}