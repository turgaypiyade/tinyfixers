using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent bot replies to life requests. Each message grants one life when accepted.</summary>
public sealed class TeamLifeInbox
{
    private const string SaveKey = "team_life_inbox_v1";
    private const int DonationLimit = 5;

    [Serializable]
    public sealed class Reply
    {
        public string id;
        public string sender;
        public long sentTicks;
        public bool accepted;
    }

    [Serializable]
    public sealed class BotRequest
    {
        public string id;
        public string sender;
        public long sentTicks;
        public bool helped;
    }

    [Serializable]
    private sealed class State
    {
        public string teamId;
        public int pending; // Legacy aggregate inbox, migrated to individual replies.
        public string sender;
        public int requested;
        public long nextGiftTicks;
        public List<Reply> replies;
        public long nextBotRequestTicks;
        public List<BotRequest> botRequests;
    }

    private readonly State state;
    private readonly Action changed;
    private readonly Func<int, int, int> randomRange;
    private Func<string> pickSender;
    private int botCount;

    public IReadOnlyList<Reply> Replies => state.replies;
    public IReadOnlyList<BotRequest> BotRequests => state.botRequests;
    public int Pending => state.replies.FindAll(reply => !reply.accepted).Count;
    public bool IsWaiting => state.requested > 0;
    public bool CanRequest => LivesManager.Current < LivesManager.MaxLives && Pending == 0 && !IsWaiting;

    public TeamLifeInbox(string teamId, Action changed, Func<int, int, int> randomRange = null)
    {
        this.changed = changed;
        this.randomRange = randomRange ?? UnityEngine.Random.Range;
        State saved = null;
        try { saved = JsonUtility.FromJson<State>(PlayerPrefs.GetString(SaveKey, "")); }
        catch (ArgumentException) { }
        state = saved != null && saved.teamId == teamId ? saved : new State { teamId = teamId };
        if (state.replies == null)
        {
            state.replies = new List<Reply>();
            for (int i = 0; i < Mathf.Clamp(state.pending, 0, DonationLimit); i++)
                AddReply(state.sender, DateTime.UtcNow);
        }
        state.pending = 0;
        if (state.botRequests == null) state.botRequests = new List<BotRequest>();
        if (state.nextBotRequestTicks <= 0 || state.nextBotRequestTicks > DateTime.MaxValue.Ticks)
            state.nextBotRequestTicks = DateTime.UtcNow.AddSeconds(this.randomRange(60, 121)).Ticks;
        state.requested = Mathf.Clamp(state.requested, 0, DonationLimit - Pending);
        if (!IsWaiting) state.nextGiftTicks = 0;
        else if (state.nextGiftTicks <= 0 || state.nextGiftTicks > DateTime.MaxValue.Ticks)
            Schedule(DateTime.UtcNow);
        Save();
    }

    public void SetBots(int count, Func<string> sender)
    {
        botCount = Mathf.Max(0, count);
        pickSender = sender;
        Tick(DateTime.UtcNow);
    }

    public bool Request(DateTime now)
    {
        if (!CanRequest) return false;
        state.requested = Mathf.Min(DonationLimit, LivesManager.MaxLives - LivesManager.Current);
        Schedule(now);
        Save();
        changed?.Invoke();
        return true;
    }

    public void Tick(DateTime now)
    {
        if (botCount <= 0) return;
        bool botRequested = TickBotRequest(now);
        bool received = TickReplies(now);
        if (botRequested || received) changed?.Invoke();
    }

    private bool TickReplies(DateTime now)
    {
        if (!IsWaiting || now.Ticks < state.nextGiftTicks) return false;

        // Catch up only on requested replies, never generate unsolicited gifts.
        bool received = false;
        for (int i = 0; i < DonationLimit && IsWaiting && now.Ticks >= state.nextGiftTicks; i++)
        {
            if (Pending >= DonationLimit || LivesManager.Current + Pending >= LivesManager.MaxLives)
            {
                Schedule(now);
                break;
            }

            var due = new DateTime(state.nextGiftTicks, DateTimeKind.Utc);
            AddReply(pickSender?.Invoke(), due);
            state.requested--;
            received = true;
            if (IsWaiting) Schedule(due);
            else state.nextGiftTicks = 0;
        }
        Save();
        return received;
    }

    private bool TickBotRequest(DateTime now)
    {
        if (now.Ticks < state.nextBotRequestTicks) return false;
        // One request on return, rather than flooding the chat with offline requests.
        state.nextBotRequestTicks = now.AddSeconds(randomRange(180, 361)).Ticks;
        string sender = pickSender?.Invoke();
        bool canRequest = !string.IsNullOrEmpty(sender)
            && state.botRequests.FindAll(request => !request.helped).Count < Mathf.Min(3, botCount)
            && !state.botRequests.Exists(request => !request.helped && request.sender == sender);
        if (canRequest)
        {
            while (state.botRequests.Count >= 30)
            {
                int index = state.botRequests.FindIndex(request => request.helped);
                if (index < 0) break;
                state.botRequests.RemoveAt(index);
            }
            state.botRequests.Add(new BotRequest
            {
                id = Guid.NewGuid().ToString("N"), sender = sender, sentTicks = now.Ticks
            });
        }
        Save();
        return canRequest;
    }

    public bool HelpBot(string requestId)
    {
        var request = state.botRequests.Find(item => item.id == requestId);
        if (request == null || request.helped) return false;
        // Team help follows the existing free-help rule; it does not spend a wallet life.
        request.helped = true;
        Save();
        changed?.Invoke();
        return true;
    }

    public int Accept(string replyId)
    {
        var reply = state.replies.Find(item => item.id == replyId);
        if (reply == null || reply.accepted || LivesManager.Current >= LivesManager.MaxLives) return 0;
        // Mark first: a wallet event can refresh/re-enter the UI synchronously.
        reply.accepted = true;
        Save(flush: false);
        // AddLives saves both the updated message and wallet together.
        LivesManager.AddLives(1);
        changed?.Invoke();
        return 1;
    }

    private void AddReply(string sender, DateTime sentAt)
    {
        // Keep recent claimed messages visible, without unbounded save growth.
        while (state.replies.Count >= 50)
        {
            int index = state.replies.FindIndex(reply => reply.accepted);
            if (index < 0) break;
            state.replies.RemoveAt(index);
        }
        state.replies.Add(new Reply
        {
            id = Guid.NewGuid().ToString("N"),
            sender = string.IsNullOrEmpty(sender) ? "Takım arkadaşın" : sender,
            sentTicks = sentAt.Ticks
        });
    }

    private void Schedule(DateTime from)
    {
        state.nextGiftTicks = from.AddSeconds(randomRange(30, 91)).Ticks;
    }

    private void Save(bool flush = true)
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(state));
        if (flush) PlayerPrefs.Save();
    }
}
