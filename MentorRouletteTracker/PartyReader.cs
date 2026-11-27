using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using GameGroupManager = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager;
using GamePartyMember = FFXIVClientStructs.FFXIV.Client.Game.Group.PartyMember;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Recording;

namespace MentorRouletteTracker;

internal sealed unsafe class PartyReader
{
	private const byte AllianceSlotInUse = 0x01;

	private readonly IClientState _clientState;
	private readonly ICondition _condition;
	private readonly IObjectTable _objectTable;
	private readonly List<PartySlot> _slots = [];

	public PartyReader(IClientState clientState, ICondition condition, IObjectTable objectTable)
	{
		_clientState = clientState;
		_condition = condition;
		_objectTable = objectTable;
	}

	public Party Read()
	{
		_slots.Clear();
		var player = _objectTable.LocalPlayer;
		var playerId = player?.EntityId ?? 0;
		var group = GameGroupManager.Instance();
		if (group != null)
		{
			ref var main = ref group->MainGroup;
			var count = Math.Min((int)main.MemberCount, main.PartyMembers.Length);
			for (var i = 0; i < count; i++)
			{
				_slots.Add(Slot(in main.PartyMembers[i], otherParty: false, playerId));
			}

			if (main.IsAlliance)
			{
				foreach (ref readonly var member in main.AllianceMembers)
				{
					if ((member.Flags & AllianceSlotInUse) != 0)
					{
						_slots.Add(Slot(in member, otherParty: true, playerId));
					}
				}
			}
		}

		var self = new PartySlot(
			playerId,
			player?.CurrentHp ?? 0,
			player?.MaxHp ?? 0,
			_clientState.TerritoryType,
			_condition[ConditionFlag.InCombat]);
		return PartyReading.Read(_slots, self);
	}

	private PartySlot Slot(in GamePartyMember member, bool otherParty, uint playerId)
	{
		var inCombat = member.CurrentHP > 0 && (member.EntityId == playerId
			? _condition[ConditionFlag.InCombat]
			: _objectTable.SearchByEntityId(member.EntityId) is ICharacter character && character.StatusFlags.HasFlag(StatusFlags.InCombat));
		return new PartySlot(member.EntityId, member.CurrentHP, member.MaxHP, member.TerritoryType, inCombat, otherParty);
	}
}
