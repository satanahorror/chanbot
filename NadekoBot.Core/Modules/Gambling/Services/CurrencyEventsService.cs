﻿using NadekoBot.Core.Services;
using NadekoBot.Core.Modules.Gambling.Common.Events;
using System.Collections.Concurrent;
using NadekoBot.Modules.Gambling.Common;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;
using System;
using NLog;
using NadekoBot.Core.Services.Database.Models;
using System.Net.Http;
using Newtonsoft.Json;
using System.Linq;
using NadekoBot.Core.Modules.Gambling.Common.CurrencyEvents;

namespace NadekoBot.Modules.Gambling.Services
{
    public class CurrencyEventsService : INService
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly ICurrencyService _cs;
        private readonly IBotConfigProvider _bc;
        private readonly IBotCredentials _creds;
        private readonly HttpClient _http;
        private readonly Logger _log;
        private readonly ConcurrentDictionary<ulong, ICurrencyEvent> _events =
            new ConcurrentDictionary<ulong, ICurrencyEvent>();

        public CurrencyEventsService(DbService db, DiscordSocketClient client,
            IBotCredentials creds, ICurrencyService cs, IBotConfigProvider bc)
        {
            _db = db;
            _client = client;
            _cs = cs;
            _bc = bc;
            _creds = creds;
            _http = new HttpClient();
            _log = LogManager.GetCurrentClassLogger();

#if GLOBAL_NADEKO
            if (_client.ShardId == 0)
            {
                Task t = BotlistUpvoteLoop();
            }
#endif
        }

        private async Task BotlistUpvoteLoop()
        {
            if (string.IsNullOrWhiteSpace(_creds.BotListToken))
                return;
            while (true)
            {
                await Task.Delay(TimeSpan.FromHours(1));
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get,
                        $"https://discordbots.org/api/bots/116275390695079945/votes?onlyids=true&days=1");
                    req.Headers.Add("Authorization", _creds.BotListToken);
                    var res = await _http.SendAsync(req);
                    if (!res.IsSuccessStatusCode)
                    {
                        _log.Warn("Botlist API not reached.");
                        continue;
                    }
                    var resStr = await res.Content.ReadAsStringAsync();
                    var ids = JsonConvert.DeserializeObject<ulong[]>(resStr);
                    await _cs.AddBulkAsync(ids, ids.Select(x => "Voted - <https://discordbots.org/bot/nadeko/vote>"), ids.Select(x => 10L), true);

                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                }
            }
            //await ReplyConfirmLocalized("bot_list_awarded",
            //    Format.Bold(amount.ToString()),
            //    Format.Bold(ids.Length.ToString())).ConfigureAwait(false);
        }

        public async Task<bool> TryCreateEventAsync(ulong guildId, ulong channelId, Event.Type type,
            EventOptions opts, Func<Event.Type, EventOptions, long, EmbedBuilder> embed)
        {
            SocketGuild g = _client.GetGuild(guildId);
            SocketTextChannel ch = g?.GetChannel(channelId) as SocketTextChannel;
            if (ch == null)
                return false;

            ICurrencyEvent ce;

            if (type == Event.Type.Reaction)
            {
                ce = new ReactionEvent(_client, _cs, _bc, g, ch, opts, embed);
            }
            //else if (type == Event.Type.NotRaid)
            //{
            //    ce = new NotRaidEvent(_client, _cs, _bc, g, ch, opts, embed);
            //}
            else
            {
                return false;
            }

            var added = _events.TryAdd(guildId, ce);
            if (added)
            {
                try
                {
                    ce.OnEnded += OnEventEnded;
                    await ce.Start();
                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                    _events.TryRemove(guildId, out ce);
                    return false;
                }
            }
            return added;
        }

        private Task OnEventEnded(ulong gid)
        {
            _events.TryRemove(gid, out _);
            return Task.CompletedTask;
        }
    }
}
