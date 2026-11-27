using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Recording;

/// <summary>
/// Counts the party's deaths, and says when it wipes, from what each frame shows of it and the game's wipe message. The game only sends its
/// message when it resets the whole party (trials and raids), so in dungeons and guildhests, where each player returns
/// to the start themselves, the party is all there is.
/// <list type="bullet">
/// <item>
/// <b>A fight</b> starts when anyone alive is in combat, and is over once nobody alive is. Someone who returned to the
/// start is alive and out of combat, so the fight carries on while the rest of the party fights.
/// </item>
/// <item>
/// <b>A death</b> is a member already seen going from alive to dead, in any party of the alliance. Someone first seen
/// dead (after the plugin starts, coming back, or joining) isn't one until they're up and die again. Dying, being
/// raised and dying again is two.
/// </item>
/// <item>
/// <b>A wipe</b> is everyone in the party (all of the alliance in an alliance raid) out of the fight: each is dead, or
/// died this fight and is now out of combat. Someone raised and back in combat is still fighting. It's checked only as
/// a death is seen, so it counts as the last one dies: a fight that ends with everyone up again isn't one, and nor is
/// being first seen dead (after the plugin starts, or coming back), which can't count a wipe that already happened.
/// </item>
/// <item>
/// <b>Between fights,</b> a member who's still dead counts as having died in the next fight, and everyone else starts
/// it afresh. So the whole party lying dead at once is a wipe, however many fights it took.
/// </item>
/// <item>
/// <b>The game's wipe message</b> and a wipe seen from the party within <see cref="SameWipe"/> of each other are the
/// same wipe, whichever comes first. The message also ends the fight, since the game raises everyone.
/// </item>
/// </list>
/// Members are told apart by id, not slot, so a replacement in a slot is someone new, and someone who leaves no longer
/// has to die. Not thread safe.
/// </summary>
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

	/// <summary>
	/// What it saw, for the log: the party's size, fights, each death and member up again, and wipes skipped
	/// </summary>
	public event Action<RunNote>? Noted;

	/// <summary>
	/// What one frame shows of the party
	/// </summary>
	/// <returns>Its deaths, and a wipe if it finished one that wasn't already counted</returns>
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

	/// <summary>
	/// The game's wipe message: it's resetting the whole party
	/// </summary>
	/// <returns>The wipe, unless it was already counted from the party</returns>
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

	/// <summary>
	/// Forgets the party and the fight: a run's starting, or the player's back in it after being away. Everyone's seen
	/// for the first time again next frame.
	/// </summary>
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

	/// <summary>
	/// The fight's over, if there was one. Its deaths are forgotten from the next frame, which is between fights.
	/// </summary>
	private void EndFight(FightEnd reason)
	{
		if (_inFight)
		{
			_inFight = false;
			Noted?.Invoke(new RunNote.FightOver(reason));
		}
	}

	/// <summary>
	/// Between fights: only those still dead carry into the next one
	/// </summary>
	private void ForgetDeathsOfTheLiving()
	{
		foreach (var member in _members.Values)
		{
			member.DiedThisFight = member.Dead;
		}
	}

	/// <summary>
	/// Forgets whoever wasn't in this frame: they left, or aren't here
	/// </summary>
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

	/// <summary>
	/// A line for each death and each member alive again, with the counts after this frame: the player's own party's
	/// for their own party, and the whole alliance's for the other parties
	/// </summary>
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

	/// <summary>
	/// Members in one frame, in the player's own party and the alliance's others
	/// </summary>
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
		/// <summary>
		/// The last frame they were seen in
		/// </summary>
		public int Frame { get; private set; }

		public bool Dead { get; private set; }

		/// <summary>
		/// Died this fight, or between fights, dead now
		/// </summary>
		public bool DiedThisFight { get; set; }

		/// <summary>
		/// Alive and in combat
		/// </summary>
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
