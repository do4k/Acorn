using System.Reflection;
using Acorn.Data;
using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Net.PacketHandlers;
using Acorn.Shared.Caching;
using Acorn.World.Services.Admin;
using Acorn.World.Services.Bans;
using Acorn.World.Services.Map;
using Acorn.World.Services.Arena;
using Acorn.World.Services.Guild;
using Acorn.World.Services.Marriage;
using Acorn.World.Services.Quest;
using Acorn.World.Services.Npc;
using Acorn.World.Services.Party;
using Acorn.World.Services.Player;
using Acorn.World.Services.Spell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acorn.Extensions;

public delegate DateTime UtcNowDelegate();