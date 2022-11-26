﻿using BeatLeader_Server.Controllers;
using BeatLeader_Server.Models;
using BeatLeader_Server.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeatLeader_Server.Services
{
    public class DailyRefresh : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IConfiguration _configuration;

        public DailyRefresh(IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _configuration = configuration;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            do {
                int hoursUntil21 = (21 - (int)DateTime.Now.Hour + 24) % 24;

                if (hoursUntil21 == 0)
                {
                    await RefreshSteamPlayers();
                    await RefreshStats();

                    hoursUntil21 = 24;
                }

                await Task.Delay(TimeSpan.FromHours(hoursUntil21), stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }

        public async Task RefreshSteamPlayers()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _context = scope.ServiceProvider.GetRequiredService<AppContext>();
                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                int playerCount = await _context.Players.CountAsync();

                for (int i = 0; i < playerCount; i += 2000)
                {
                    var players = await _context.Players.OrderByDescending(p => p.Id).Select(p => new { 
                            Id = p.Id, 
                            Avatar = p.Avatar, 
                            Country = p.Country,
                            ExternalProfileUrl = p.ExternalProfileUrl 
                        }).Skip(i).Take(2000).ToListAsync();
                    foreach (var p in players)
                    {
                        if (Int64.Parse(p.Id) <= 70000000000000000) { continue; }
                        Player? update = await PlayerUtils.GetPlayerFromSteam(p.Id, _configuration.GetValue<string>("SteamKey"));

                        if (update != null)
                        {
                            var player = new Player() { Id = p.Id };
                            bool urlChanged = false, avatarChanged = false, countryChanged = false;

                            if (update.ExternalProfileUrl != p.ExternalProfileUrl) {
                                player.ExternalProfileUrl = update.ExternalProfileUrl;
                                urlChanged = true;
                            }

                            if (p.Avatar.Contains("steamcdn") && p.Avatar != update.Avatar)
                            {
                                player.Avatar = update.Avatar;
                                avatarChanged = true;
                            }

                            if (p.Country == "not set" && update.Country != "not set")
                            {
                                player.Country = update.Country;
                                countryChanged = true;
                            }

                            if (urlChanged || avatarChanged || countryChanged) {
                                _context.Players.Attach(player);
                            }
                            if (urlChanged) _context.Entry(player).Property(x => x.ExternalProfileUrl).IsModified = true;
                            if (avatarChanged) _context.Entry(player).Property(x => x.Avatar).IsModified = true;
                            if (countryChanged) _context.Entry(player).Property(x => x.Country).IsModified = true;
                        }
                    }

                    await _context.BulkSaveChangesAsync();
                }
            }
        }

        public async Task RefreshStats()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _context = scope.ServiceProvider.GetRequiredService<AppContext>();

                var _playerController = scope.ServiceProvider.GetRequiredService<PlayerRefreshController>();
                await _playerController.RefreshPlayersStats();
            }
        }
    }
}