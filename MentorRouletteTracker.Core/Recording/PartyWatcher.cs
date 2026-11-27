using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Recording;

public sealed class PartyWatcher
{
	public static readonly TimeSpan SameWipe = TimeSpan.FromSeconds(30);

	private readonly Dictionary<uint, Member> _members = [];
	private readonly List<uint> _gone = [];
	private int _frame;
	private bool _inFight;
	private DateTime? _partyWipeAt;
	private DateTime? _gameWipeAt;
	private RunNote.PartyChanged? _lastPartyNote;

	public event Action<RunNote>? Noted;

	public PartyFrame Seen(Party party, DateTime at)
	{
		_frame++;
		var died = new Counts();
		var up = new Counts();

		foreach (var seen in party.Members)
		{
			if (!_members.TryGetValue(seen.Id, out var member))
			{
				member = new Member { DiedThisFight = seen.Dead };
				member.See(seen, _frame);
				_members[seen.Id] = member;
				continue;
			}

			if (member.Frame == _frame)
			{
				continue;
			}

			if (!member.Dead && seen.Dead)
			{
				member.DiedThisFight = true;
				died.Add(seen.OtherParty);
			}
			else if (member.Dead && !seen.Dead)
			{
				up.Add(seen.OtherParty);
			}

			member.See(seen, _frame);
		}

		ForgetTheGone();
		NoteSize(party.NotHere);

		var fighting = _members.Values.Any(member => member.InCombat);
		if (!_inFight)
		{
			ForgetDeathsOfTheLiving();
			if (fighting)
			{
				_inFight = true;
				Noted?.Invoke(new RunNote.FightStarted());
			}
		}

		NoteDeaths(died, up, otherParty: false);
		NoteDeaths(died, up, otherParty: true);

		var wipe = died.All > 0 && _members.Values.All(member => member.Dead || (member.DiedThisFight && !member.InCombat))
			? PartyWiped(at)
			: null;

		if (_inFight && !fighting)
		{
			EndFight(FightEnd.NobodyInCombat);
		}

		return wipe is var (source, members) ? new PartyFrame(died.All, source, members) : new PartyFrame(died.All);
	}

	public WipeSource? GameWiped(DateTime at)
	{
		EndFight(FightEnd.GameWipeMessage);

		if (_partyWipeAt is { } partyWipe && at - partyWipe <= SameWipe)
		{
			_partyWipeAt = null;
			Noted?.Invoke(new RunNote.WipeSkipped(WipeSource.GameMessage, at - partyWipe));
			return null;
		}

		_gameWipeAt = at;
		return WipeSource.GameMessage;
	}

	public void Reset()
	{
		_members.Clear();
		_inFight = false;
		_partyWipeAt = null;
		_gameWipeAt = null;
		_lastPartyNote = null;
	}

	private (WipeSource, int)? PartyWiped(DateTime at)
	{
		if (_gameWipeAt is { } gameWipe && at - gameWipe <= SameWipe)
		{
			_gameWipeAt = null;
			Noted?.Invoke(new RunNote.WipeSkipped(WipeSource.Party, at - gameWipe));
			return null;
		}

		_partyWipeAt = at;
		return (WipeSource.Party, _members.Count);
	}

	private void EndFight(FightEnd reason)
	{
		if (_inFight)
		{
			_inFight = false;
			Noted?.Invoke(new RunNote.FightOver(reason));
		}
	}

	private void ForgetDeathsOfTheLiving()
	{
		foreach (var member in _members.Values)
		{
			member.DiedThisFight = member.Dead;
		}
	}

	private void ForgetTheGone()
	{
		foreach (var (id, member) in _members)
		{
			if (member.Frame != _frame)
			{
				_gone.Add(id);
			}
		}

		foreach (var id in _gone)
		{
			_members.Remove(id);
		}

		_gone.Clear();
	}

	private void NoteSize(int notHere)
	{
		var own = _members.Values.Count(member => !member.OtherParty);
		var alliance = _members.Values.Any(member => member.OtherParty) ? _members.Count : 0;
		var size = new RunNote.PartyChanged(own, alliance, notHere);
		if (size != _lastPartyNote)
		{
			_lastPartyNote = size;
			Noted?.Invoke(size);
		}
	}

	private void NoteDeaths(Counts died, Counts up, bool otherParty)
	{
		var diedHere = died.Of(otherParty);
		var upHere = up.Of(otherParty);
		if (diedHere == 0 && upHere == 0)
		{
			return;
		}

		var among = otherParty ? _members.Values.ToList() : _members.Values.Where(member => !member.OtherParty).ToList();
		var dead = among.Count(member => member.Dead);
		int? diedThisFight = _inFight ? among.Count(member => member.DiedThisFight) : null;

		for (var i = 0; i < diedHere; i++)
		{
			Noted?.Invoke(new RunNote.MemberDied(dead, diedThisFight, among.Count, otherParty));
		}

		for (var i = 0; i < upHere; i++)
		{
			Noted?.Invoke(new RunNote.MemberUp(dead, among.Count, otherParty));
		}
	}

	private struct Counts
	{
		public int Own { get; private set; }

		public int Others { get; private set; }

		public readonly int All => Own + Others;

		public void Add(bool otherParty)
		{
			if (otherParty)
			{
				Others++;
			}
			else
			{
				Own++;
			}
		}

		public readonly int Of(bool otherParty) => otherParty ? Others : Own;
	}

	private sealed class Member
	{
		public int Frame { get; private set; }

		public bool Dead { get; private set; }

		public bool DiedThisFight { get; set; }

		public bool InCombat { get; private set; }

		public bool OtherParty { get; private set; }

		public void See(PartyMember seen, int frame)
		{
			Frame = frame;
			Dead = seen.Dead;
			InCombat = !seen.Dead && seen.InCombat;
			OtherParty = seen.OtherParty;
		}
	}
}
